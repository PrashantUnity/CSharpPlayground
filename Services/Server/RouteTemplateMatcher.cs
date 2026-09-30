using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Server;

public enum SegmentKind
{
    Literal,
    ConstraintParameter,
    GenericParameter,
    CatchAll
}

public class ParsedRouteSegment
{
    public SegmentKind Kind { get; }
    public string Raw { get; }
    public string ParameterName { get; }
    public string? Constraint { get; }

    public ParsedRouteSegment(string segment)
    {
        Raw = segment;

        if (segment.StartsWith("{*") && segment.EndsWith("}"))
        {
            Kind = SegmentKind.CatchAll;
            ParameterName = segment[2..^1].Trim();
            Constraint = null;
        }
        else if (segment.StartsWith("{") && segment.EndsWith("}"))
        {
            var inner = segment[1..^1].Trim();
            var colonIndex = inner.IndexOf(':');
            if (colonIndex > 0)
            {
                Kind = SegmentKind.ConstraintParameter;
                ParameterName = inner[..colonIndex].Trim();
                Constraint = inner[(colonIndex + 1)..].Trim().ToLowerInvariant();
            }
            else
            {
                Kind = SegmentKind.GenericParameter;
                ParameterName = inner;
                Constraint = null;
            }
        }
        else
        {
            Kind = SegmentKind.Literal;
            ParameterName = string.Empty;
            Constraint = null;
        }
    }

    public bool Matches(string segment, out string? extractedValue)
    {
        extractedValue = null;
        switch (Kind)
        {
            case SegmentKind.Literal:
                return string.Equals(Raw, segment, StringComparison.OrdinalIgnoreCase);

            case SegmentKind.ConstraintParameter:
                if (CheckConstraint(segment, Constraint))
                {
                    extractedValue = segment;
                    return true;
                }
                return false;

            case SegmentKind.GenericParameter:
                extractedValue = segment;
                return true;

            case SegmentKind.CatchAll:
                extractedValue = segment;
                return true;

            default:
                return false;
        }
    }

    private static bool CheckConstraint(string value, string? constraint) => constraint switch
    {
        "int" or "integer" or "long" => long.TryParse(value, out _),
        "guid" => Guid.TryParse(value, out _),
        "bool" or "boolean" => bool.TryParse(value, out _),
        "alpha" => value.All(char.IsLetter),
        _ => true
    };
}

public class ParsedRoute
{
    public FryServerCellItem Cell { get; }
    public string Method { get; }
    public string RouteTemplate { get; }
    public IReadOnlyList<ParsedRouteSegment> Segments { get; }
    public int SpecificityScore { get; }
    public bool HasCatchAll { get; }

    public ParsedRoute(FryServerCellItem cell)
    {
        Cell = cell;
        Method = (cell.Method ?? "GET").Trim().ToUpperInvariant();
        RouteTemplate = NormalizeRoute(cell.Route ?? "/");

        var rawSegments = RouteTemplate.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var segments = new List<ParsedRouteSegment>(rawSegments.Length);
        var score = 0;
        var hasCatchAll = false;

        foreach (var raw in rawSegments)
        {
            var parsed = new ParsedRouteSegment(raw);
            segments.Add(parsed);

            score += parsed.Kind switch
            {
                SegmentKind.Literal => 100,
                SegmentKind.ConstraintParameter => 50,
                SegmentKind.GenericParameter => 20,
                SegmentKind.CatchAll => 1,
                _ => 0
            };

            if (parsed.Kind == SegmentKind.CatchAll)
            {
                hasCatchAll = true;
            }
        }

        Segments = segments;
        SpecificityScore = score;
        HasCatchAll = hasCatchAll;
    }

    private static string NormalizeRoute(string route)
    {
        var r = route.Trim();
        if (!r.StartsWith('/')) r = "/" + r;
        if (r.Length > 1 && r.EndsWith('/')) r = r.TrimEnd('/');
        return r;
    }
}

public record RouteMatchResult
{
    public bool IsMatched { get; init; }
    public FryServerCellItem? MatchedCell { get; init; }
    public bool MethodNotAllowed { get; init; }
    public IReadOnlyList<string> AllowedMethods { get; init; } = Array.Empty<string>();
    public Dictionary<string, string> PathParameters { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> QueryParameters { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string MatchedRouteTemplate { get; init; } = string.Empty;
    public string NormalizedPath { get; init; } = "/";

    public static RouteMatchResult NotFound(string path, Dictionary<string, string>? queryParams = null) => new()
    {
        IsMatched = false,
        NormalizedPath = path,
        QueryParameters = queryParams ?? new(StringComparer.OrdinalIgnoreCase)
    };

    public static RouteMatchResult NotAllowed(string path, IEnumerable<string> allowed, Dictionary<string, string>? queryParams = null) => new()
    {
        IsMatched = false,
        MethodNotAllowed = true,
        NormalizedPath = path,
        AllowedMethods = allowed.Distinct().ToList(),
        QueryParameters = queryParams ?? new(StringComparer.OrdinalIgnoreCase)
    };
}

public class RouteTemplateMatcher
{
    private readonly List<ParsedRoute> _routes = new();

    public RouteTemplateMatcher(IEnumerable<FryServerCellItem>? cells = null)
    {
        if (cells != null)
        {
            RegisterCells(cells);
        }
    }

    public void RegisterCells(IEnumerable<FryServerCellItem> cells)
    {
        _routes.Clear();
        foreach (var cell in cells.Where(c => c.Enabled && c.Type == FryServerCellType.Endpoint))
        {
            _routes.Add(new ParsedRoute(cell));
        }

        // Sort routes by specificity descending (highest priority first)
        _routes.Sort((a, b) => b.SpecificityScore.CompareTo(a.SpecificityScore));
    }

    public RouteMatchResult Match(string httpMethod, string rawPath, string? queryString = null, string? apiPrefix = null)
    {
        var method = (httpMethod ?? "GET").Trim().ToUpperInvariant();
        var path = NormalizePath(rawPath, apiPrefix);
        var queryParams = ParseQueryString(queryString);

        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        var methodMatches = new List<(ParsedRoute Route, Dictionary<string, string> Params)>();
        var pathMatchesAllMethods = new List<string>();

        foreach (var route in _routes)
        {
            if (TryMatchSegments(route, pathSegments, out var extractedParams))
            {
                pathMatchesAllMethods.Add(route.Method);

                var isMethodMatch = route.Method == "ANY" || route.Method == "*" || route.Method == method;
                if (isMethodMatch)
                {
                    methodMatches.Add((route, extractedParams));
                }
            }
        }

        if (methodMatches.Count > 0)
        {
            var best = methodMatches[0];
            return new RouteMatchResult
            {
                IsMatched = true,
                MatchedCell = best.Route.Cell,
                MatchedRouteTemplate = best.Route.RouteTemplate,
                PathParameters = best.Params,
                QueryParameters = queryParams,
                NormalizedPath = path,
                AllowedMethods = pathMatchesAllMethods.Distinct().ToList()
            };
        }

        if (pathMatchesAllMethods.Count > 0)
        {
            return RouteMatchResult.NotAllowed(path, pathMatchesAllMethods, queryParams);
        }

        return RouteMatchResult.NotFound(path, queryParams);
    }

    private static bool TryMatchSegments(ParsedRoute route, string[] requestSegments, out Dictionary<string, string> parameters)
    {
        parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!route.HasCatchAll && route.Segments.Count != requestSegments.Length)
        {
            return false;
        }

        if (route.HasCatchAll && requestSegments.Length < route.Segments.Count - 1)
        {
            return false;
        }

        for (var i = 0; i < route.Segments.Count; i++)
        {
            var segmentDef = route.Segments[i];

            if (segmentDef.Kind == SegmentKind.CatchAll)
            {
                // Capture remaining segments
                var remaining = string.Join('/', requestSegments.Skip(i));
                parameters[segmentDef.ParameterName] = remaining;
                return true;
            }

            if (i >= requestSegments.Length)
            {
                return false;
            }

            var requestSegment = requestSegments[i];
            if (!segmentDef.Matches(requestSegment, out var val))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(segmentDef.ParameterName) && val != null)
            {
                parameters[segmentDef.ParameterName] = WebUtility.UrlDecode(val);
            }
        }

        return true;
    }

    public static string NormalizePath(string rawPath, string? apiPrefix = null)
    {
        var path = rawPath.Trim();
        var qIdx = path.IndexOf('?');
        if (qIdx >= 0)
        {
            path = path[..qIdx];
        }

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        if (!string.IsNullOrWhiteSpace(apiPrefix))
        {
            var prefix = apiPrefix.Trim();
            if (!prefix.StartsWith('/')) prefix = "/" + prefix;
            prefix = prefix.TrimEnd('/');

            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                path = path[prefix.Length..];
                if (!path.StartsWith('/')) path = "/" + path;
            }
        }

        if (path.Length > 1 && path.EndsWith('/'))
        {
            path = path.TrimEnd('/');
        }

        return string.IsNullOrEmpty(path) ? "/" : path;
    }

    public static Dictionary<string, string> ParseQueryString(string? queryString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return result;
        }

        var qs = queryString.TrimStart('?');
        var pairs = qs.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var eq = pair.IndexOf('=');
            if (eq >= 0)
            {
                var key = WebUtility.UrlDecode(pair[..eq]);
                var val = WebUtility.UrlDecode(pair[(eq + 1)..]);
                result[key] = val;
            }
            else
            {
                var key = WebUtility.UrlDecode(pair);
                result[key] = string.Empty;
            }
        }

        return result;
    }
}
