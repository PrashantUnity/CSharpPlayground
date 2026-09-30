using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Server;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.Server;

public class RouteTemplateMatcherTests
{
    [Fact]
    public void Match_SimpleRouteWithParameter_ExtractsParamAndQuery()
    {
        var cell = new FryServerCellItem
        {
            Id = "c1",
            Method = "GET",
            Route = "/users/{id}",
            Type = FryServerCellType.Endpoint
        };

        var matcher = new RouteTemplateMatcher(new[] { cell });

        var result = matcher.Match("GET", "/users/123", "?includeOrders=true");

        Assert.True(result.IsMatched);
        Assert.Equal("c1", result.MatchedCell?.Id);
        Assert.Equal("123", result.PathParameters["id"]);
        Assert.Equal("true", result.QueryParameters["includeOrders"]);
    }

    [Fact]
    public void Match_WithApiPrefix_StripsPrefixAndMatches()
    {
        var cell = new FryServerCellItem
        {
            Id = "c2",
            Method = "GET",
            Route = "/users/{id}",
            Type = FryServerCellType.Endpoint
        };

        var matcher = new RouteTemplateMatcher(new[] { cell });

        var result = matcher.Match("GET", "/api/users/456", "?active=1", apiPrefix: "/api");

        Assert.True(result.IsMatched);
        Assert.Equal("456", result.PathParameters["id"]);
        Assert.Equal("1", result.QueryParameters["active"]);
        Assert.Equal("/users/456", result.NormalizedPath);
    }

    [Fact]
    public void Match_Precedence_LiteralWinsOverParameter()
    {
        var literalCell = new FryServerCellItem
        {
            Id = "literal",
            Method = "GET",
            Route = "/users/me",
            Type = FryServerCellType.Endpoint
        };

        var paramCell = new FryServerCellItem
        {
            Id = "param",
            Method = "GET",
            Route = "/users/{id}",
            Type = FryServerCellType.Endpoint
        };

        var matcher = new RouteTemplateMatcher(new[] { paramCell, literalCell });

        var result = matcher.Match("GET", "/users/me");

        Assert.True(result.IsMatched);
        Assert.Equal("literal", result.MatchedCell?.Id);
    }

    [Fact]
    public void Match_TypeConstraint_IntOnlyMatchesNumeric()
    {
        var intCell = new FryServerCellItem
        {
            Id = "int-cell",
            Method = "GET",
            Route = "/users/{id:int}",
            Type = FryServerCellType.Endpoint
        };

        var genericCell = new FryServerCellItem
        {
            Id = "generic-cell",
            Method = "GET",
            Route = "/users/{slug}",
            Type = FryServerCellType.Endpoint
        };

        var matcher = new RouteTemplateMatcher(new[] { genericCell, intCell });

        var intResult = matcher.Match("GET", "/users/999");
        Assert.True(intResult.IsMatched);
        Assert.Equal("int-cell", intResult.MatchedCell?.Id);
        Assert.Equal("999", intResult.PathParameters["id"]);

        var stringResult = matcher.Match("GET", "/users/alice");
        Assert.True(stringResult.IsMatched);
        Assert.Equal("generic-cell", stringResult.MatchedCell?.Id);
        Assert.Equal("alice", stringResult.PathParameters["slug"]);
    }

    [Fact]
    public void Match_CatchAllWildcard_CapturesRestOfPath()
    {
        var cell = new FryServerCellItem
        {
            Id = "catch-all",
            Method = "GET",
            Route = "/files/{*path}",
            Type = FryServerCellType.Endpoint
        };

        var matcher = new RouteTemplateMatcher(new[] { cell });

        var result = matcher.Match("GET", "/files/docs/2026/report.pdf");

        Assert.True(result.IsMatched);
        Assert.Equal("docs/2026/report.pdf", result.PathParameters["path"]);
    }

    [Fact]
    public void Match_MethodMismatch_ReturnsMethodNotAllowed()
    {
        var cell = new FryServerCellItem
        {
            Id = "get-only",
            Method = "GET",
            Route = "/orders/{id}",
            Type = FryServerCellType.Endpoint
        };

        var matcher = new RouteTemplateMatcher(new[] { cell });

        var result = matcher.Match("DELETE", "/orders/123");

        Assert.False(result.IsMatched);
        Assert.True(result.MethodNotAllowed);
        Assert.Contains("GET", result.AllowedMethods);
    }

    [Fact]
    public void Match_AnyMethod_MatchesAnyVerb()
    {
        var cell = new FryServerCellItem
        {
            Id = "any-method",
            Method = "ANY",
            Route = "/webhook",
            Type = FryServerCellType.Endpoint
        };

        var matcher = new RouteTemplateMatcher(new[] { cell });

        Assert.True(matcher.Match("POST", "/webhook").IsMatched);
        Assert.True(matcher.Match("PUT", "/webhook").IsMatched);
        Assert.True(matcher.Match("GET", "/webhook").IsMatched);
    }

    [Fact]
    public void Match_DisabledCell_IsIgnored()
    {
        var cell = new FryServerCellItem
        {
            Id = "disabled",
            Method = "GET",
            Route = "/health",
            Enabled = false,
            Type = FryServerCellType.Endpoint
        };

        var matcher = new RouteTemplateMatcher(new[] { cell });

        var result = matcher.Match("GET", "/health");

        Assert.False(result.IsMatched);
    }
}
