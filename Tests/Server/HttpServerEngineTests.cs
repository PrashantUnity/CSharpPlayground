using System.Net;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models.Server;
using PdfEditorApp.Plugins.CSharpEditor.Services.Server;
using Xunit;

namespace CSharpEditorPlugin.Tests.Server;

public class HttpServerEngineTests
{
    private static int GetFreePort()
    {
        using var temp = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        temp.Start();
        var port = ((IPEndPoint)temp.LocalEndpoint).Port;
        temp.Stop();
        return port;
    }

    [Fact]
    public async Task ServerEngine_EndToEnd_ServesExternalHttpRequest()
    {
        var port = GetFreePort();
        var doc = new FryServerDocumentItem
        {
            ServerConfig = new FryServerConfiguration
            {
                Host = "localhost",
                Port = port,
                Scheme = "http",
                ApiPrefix = "/api",
                EnableCors = true
            },
            Cells = new List<FryServerCellItem>
            {
                new()
                {
                    Id = "user-endpoint",
                    Method = "GET",
                    Route = "/users/{id}",
                    Type = FryServerCellType.Endpoint,
                    Source = @"
                        var inc = Context.QueryValue<bool>(""includeOrders"");
                        return Ok(new {
                            userId = id,
                            hasOrders = inc,
                            appName = ""FryServer""
                        });
                    "
                }
            }
        };

        var engine = new FryHttpListenerServerEngine();

        try
        {
            await engine.StartAsync(doc);
            Assert.Equal(ServerLifecycleState.Running, engine.State);

            using var client = new HttpClient();
            var response = await client.GetAsync($"http://localhost:{engine.BoundPort}/api/users/123?includeOrders=true");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();

            using var parsed = JsonDocument.Parse(json);
            Assert.Equal("123", parsed.RootElement.GetProperty("userId").GetString());
            Assert.True(parsed.RootElement.GetProperty("hasOrders").GetBoolean());
            Assert.Equal("FryServer", parsed.RootElement.GetProperty("appName").GetString());

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (engine.TrafficLog.Count == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.True(engine.TotalRequestsServed >= 1);
            Assert.NotEmpty(engine.TrafficLog);
        }
        finally
        {
            await engine.StopAsync();
            Assert.Equal(ServerLifecycleState.Stopped, engine.State);
        }
    }

    [Fact]
    public async Task ServerEngine_CorsPreflight_ReturnsPrivateNetworkAndCorsHeaders()
    {
        var port = GetFreePort();
        var doc = new FryServerDocumentItem
        {
            ServerConfig = new FryServerConfiguration
            {
                Host = "localhost",
                Port = port,
                Scheme = "http",
                ApiPrefix = "/api",
                EnableCors = true,
                AllowPrivateNetwork = true
            },
            Cells = new List<FryServerCellItem>
            {
                new()
                {
                    Id = "ping",
                    Method = "GET",
                    Route = "/ping",
                    Type = FryServerCellType.Endpoint,
                    Source = "return Ok(new { message = \"pong\" });"
                }
            }
        };

        var engine = new FryHttpListenerServerEngine();

        try
        {
            await engine.StartAsync(doc);

            using var client = new HttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Options, $"http://localhost:{engine.BoundPort}/api/ping");
            req.Headers.Add("Origin", "http://localhost:3000");
            req.Headers.Add("Access-Control-Request-Private-Network", "true");

            var response = await client.SendAsync(req);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
            Assert.True(response.Headers.Contains("Access-Control-Allow-Private-Network"));
        }
        finally
        {
            await engine.StopAsync();
        }
    }

    [Fact]
    public async Task ServerEngine_SharedState_PersistsAcrossRequests()
    {
        var port = GetFreePort();
        var doc = new FryServerDocumentItem
        {
            ServerConfig = new FryServerConfiguration
            {
                Host = "localhost",
                Port = port,
                Scheme = "http",
                ApiPrefix = "/api"
            },
            Cells = new List<FryServerCellItem>
            {
                new()
                {
                    Id = "startup-seed",
                    Type = FryServerCellType.Startup,
                    Source = "State[\"counter\"] = 100;"
                },
                new()
                {
                    Id = "counter-endpoint",
                    Method = "GET",
                    Route = "/counter/increment",
                    Type = FryServerCellType.Endpoint,
                    Source = @"
                        var val = (int)State[""counter""] + 1;
                        State[""counter""] = val;
                        return Ok(new { current = val });
                    "
                }
            }
        };

        var engine = new FryHttpListenerServerEngine();

        try
        {
            await engine.StartAsync(doc);

            using var client = new HttpClient();
            var res1 = await client.GetStringAsync($"http://localhost:{engine.BoundPort}/api/counter/increment");
            using var doc1 = JsonDocument.Parse(res1);
            Assert.Equal(101, doc1.RootElement.GetProperty("current").GetInt32());

            var res2 = await client.GetStringAsync($"http://localhost:{engine.BoundPort}/api/counter/increment");
            using var doc2 = JsonDocument.Parse(res2);
            Assert.Equal(102, doc2.RootElement.GetProperty("current").GetInt32());
        }
        finally
        {
            await engine.StopAsync();
        }
    }

    [Fact]
    public async Task ServerEngine_UnmappedRoute_Returns404()
    {
        var port = GetFreePort();
        var doc = new FryServerDocumentItem
        {
            ServerConfig = new FryServerConfiguration
            {
                Host = "localhost",
                Port = port,
                Scheme = "http",
                ApiPrefix = "/api"
            }
        };

        var engine = new FryHttpListenerServerEngine();

        try
        {
            await engine.StartAsync(doc);

            using var client = new HttpClient();
            var response = await client.GetAsync($"http://localhost:{engine.BoundPort}/api/nonexistent");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await engine.StopAsync();
        }
    }

    [Fact]
    public async Task ServerEngine_ExecuteLoopbackTest_RunsDirectlyWithoutSocket()
    {
        var cell = new FryServerCellItem
        {
            Id = "loopback-cell",
            Method = "GET",
            Route = "/greet/{name}",
            Type = FryServerCellType.Endpoint,
            Source = @"
                var name = Context.ParamValue<string>(""name"");
                return Ok(new { greeting = ""Hello, "" + name });
            "
        };

        var engine = new FryHttpListenerServerEngine();
        var harness = new FryServerTestHarnessItem
        {
            PathParams = new() { ["name"] = "World" }
        };

        var result = await engine.ExecuteLoopbackTestAsync(cell, harness);

        Assert.Equal(200, result.StatusCode);
        var jsonRes = Assert.IsType<JsonResult>(result);
        Assert.NotNull(jsonRes.Value);
    }

    [Fact]
    public async Task ServerEngine_WhenCellCodeUpdatedWhileRunning_ReturnsUpdatedLogicImmediately()
    {
        var port = GetFreePort();
        var cell = new FryServerCellItem
        {
            Id = "hot-reload-cell",
            Method = "GET",
            Route = "/version",
            Type = FryServerCellType.Endpoint,
            Source = "return Ok(new { version = 1, message = \"Original\" });"
        };

        var doc = new FryServerDocumentItem
        {
            ServerConfig = new FryServerConfiguration
            {
                Host = "localhost",
                Port = port,
                Scheme = "http",
                ApiPrefix = "/api"
            },
            Cells = new List<FryServerCellItem> { cell }
        };

        var engine = new FryHttpListenerServerEngine();

        try
        {
            await engine.StartAsync(doc);
            using var client = new HttpClient();

            // First request: returns version 1
            var response1 = await client.GetAsync($"http://localhost:{engine.BoundPort}/api/version");
            Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
            var json1 = await response1.Content.ReadAsStringAsync();
            using var doc1 = JsonDocument.Parse(json1);
            Assert.Equal(1, doc1.RootElement.GetProperty("version").GetInt32());
            Assert.Equal("Original", doc1.RootElement.GetProperty("message").GetString());

            // User updates code inside running server
            cell.Source = "return Ok(new { version = 2, message = \"Updated Logic!\" });";
            await engine.InvalidateCellCompilationAsync(cell);

            // Second request: MUST return version 2 with updated logic!
            var response2 = await client.GetAsync($"http://localhost:{engine.BoundPort}/api/version");
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
            var json2 = await response2.Content.ReadAsStringAsync();
            using var doc2 = JsonDocument.Parse(json2);
            Assert.Equal(2, doc2.RootElement.GetProperty("version").GetInt32());
            Assert.Equal("Updated Logic!", doc2.RootElement.GetProperty("message").GetString());
        }
        finally
        {
            await engine.StopAsync();
        }
    }
}
