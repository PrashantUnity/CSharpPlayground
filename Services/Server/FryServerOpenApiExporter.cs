using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Server;

public static class FryServerOpenApiExporter
{
    private static readonly Regex RouteParamRegex = new(@"\{(\*?)([^}:]+)(?::[^}]+)?\}", RegexOptions.Compiled);

    public static string GenerateOpenApiJson(FryServerDocumentItem document)
    {
        var paths = new Dictionary<string, object>();

        foreach (var cell in document.Cells.Where(c => c.Enabled && c.Type == FryServerCellType.Endpoint))
        {
            var openApiPath = NormalizeOpenApiPath(cell.Route);
            if (!paths.TryGetValue(openApiPath, out var pathItemObj))
            {
                pathItemObj = new Dictionary<string, object>();
                paths[openApiPath] = pathItemObj;
            }

            var pathItem = (Dictionary<string, object>)pathItemObj;
            var method = cell.Method.ToLowerInvariant();
            if (method is "any" or "*") method = "get";

            var parameters = new List<object>();

            // Extract path parameters from route
            var matches = RouteParamRegex.Matches(cell.Route);
            foreach (Match match in matches)
            {
                var paramName = match.Groups[2].Value;
                parameters.Add(new
                {
                    name = paramName,
                    @in = "path",
                    required = true,
                    schema = new { type = "string" }
                });
            }

            // Extract query parameters from test harness
            foreach (var qp in cell.TestHarness.QueryParams)
            {
                parameters.Add(new
                {
                    name = qp.Key,
                    @in = "query",
                    required = false,
                    schema = new { type = "string" }
                });
            }

            var operation = new Dictionary<string, object>
            {
                ["summary"] = cell.Title,
                ["description"] = string.IsNullOrEmpty(cell.Description) ? cell.Title : cell.Description,
                ["parameters"] = parameters,
                ["responses"] = new Dictionary<string, object>
                {
                    [cell.DefaultStatusCode.ToString()] = new
                    {
                        description = "Successful response",
                        content = new Dictionary<string, object>
                        {
                            [cell.ResponseContentType] = new
                            {
                                schema = new { type = "object" }
                            }
                        }
                    }
                }
            };

            if (method is "post" or "put" or "patch" && !string.IsNullOrEmpty(cell.TestHarness.Body))
            {
                operation["requestBody"] = new
                {
                    required = true,
                    content = new Dictionary<string, object>
                    {
                        [cell.TestHarness.BodyContentType] = new
                        {
                            schema = new { type = "object" }
                        }
                    }
                };
            }

            pathItem[method] = operation;
        }

        var openApiDoc = new Dictionary<string, object>
        {
            ["openapi"] = "3.0.1",
            ["info"] = new
            {
                title = document.Title,
                description = document.Description,
                version = "1.0.0"
            },
            ["servers"] = new[]
            {
                new { url = document.ServerConfig.BaseUrl }
            },
            ["paths"] = paths
        };

        return JsonSerializer.Serialize(openApiDoc, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string NormalizeOpenApiPath(string route)
    {
        var path = route.Trim();
        if (!path.StartsWith('/')) path = "/" + path;
        // Convert catch-all {*filepath} to standard OpenAPI {filepath}
        return RouteParamRegex.Replace(path, "{$2}");
    }
}
