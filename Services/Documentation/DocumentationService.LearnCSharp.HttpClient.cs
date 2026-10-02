using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocArticle CreateHttpClientArticle()
    {
        return new DocArticle
        {
            Id = "learn_httpclient",
            Title = "HttpClient: GET, POST, PUT, DELETE & Downloads",
            Subtitle = "Make real-world HTTP requests, send JSON, download files, and handle errors correctly.",
            ReadingTime = "7 min read",
            Summary = "HttpClient is the standard .NET way to talk to web APIs. This chapter covers every common verb, streaming downloads and uploads, and the headers, auth, and timeout details that trip people up.",
            Keywords = new List<string> { "http", "httpclient", "get", "post", "put", "delete", "download", "upload", "rest", "api", "json", "headers", "bearer token", "authorization", "timeout", "status code" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Making GET Requests",
                    Content = "HttpClient.GetStringAsync is the simplest way to fetch a URL's body as text. For structured data, use GetAsync so you can inspect the status code, then deserialize the response body with System.Text.Json.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Create one HttpClient and reuse it for the lifetime of your app (a static field, or IHttpClientFactory in ASP.NET Core). Creating and disposing a new instance per request — as each isolated snippet below does for simplicity — can exhaust available sockets under sustained load."
                },
                new()
                {
                    Heading = "Sending POST, PUT & DELETE Requests",
                    Content = "Send a JSON body with StringContent, and always check the response status before trusting the result.",
                    BulletPoints = new List<string>
                    {
                        "POST → create a new resource, usually returns 201 Created.",
                        "PUT → replace/update an existing resource, usually returns 200 OK.",
                        "DELETE → remove a resource, often returns 200 OK or 204 No Content."
                    }
                },
                new()
                {
                    Heading = "Downloading & Uploading Files",
                    Content = "For downloads, stream the response body straight to a file instead of buffering the whole thing in memory. For uploads, MultipartFormDataContent mirrors an HTML form with enctype=\"multipart/form-data\".",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "These examples write to your system temp folder and require internet access to run."
                },
                new()
                {
                    Heading = "Headers, Authentication, Timeouts & Error Handling",
                    Content = "Add custom headers through HttpRequestMessage.Headers. Bearer tokens go in the Authorization header. Always check IsSuccessStatusCode (or call EnsureSuccessStatusCode) rather than assuming success, and use a CancellationTokenSource to bound how long you'll wait.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "Calling EnsureSuccessStatusCode() throws HttpRequestException on any non-2xx response — catch it (or check IsSuccessStatusCode first) instead of letting an API outage crash your script."
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "GetAsync", ReturnType = "Task<HttpResponseMessage>", Parameters = "string requestUri", Description = "Sends a GET request and returns once the response headers are received." },
                new() { MethodName = "PostAsync", ReturnType = "Task<HttpResponseMessage>", Parameters = "string requestUri, HttpContent content", Description = "Sends a POST request with the given body." },
                new() { MethodName = "EnsureSuccessStatusCode", ReturnType = "HttpResponseMessage", Parameters = "", Description = "Throws HttpRequestException if the status code does not indicate success; otherwise returns itself." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_http_get_string",
                    Title = "GET Request — Raw String",
                    Description = "Fetch a URL and read the body as plain text.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Net.Http;
                    using System.Threading.Tasks;

                    using var client = new HttpClient();

                    string body = await client.GetStringAsync("https://jsonplaceholder.typicode.com/posts/1");

                    Console.WriteLine(body);
                    """
                },
                new()
                {
                    Id = "snip_learn_http_get_json",
                    Title = "GET Request — Deserialize JSON",
                    Description = "Fetch JSON and deserialize it into a strongly-typed record.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Net.Http;
                    using System.Text.Json;
                    using System.Threading.Tasks;

                    using var client = new HttpClient();

                    var response = await client.GetAsync("https://jsonplaceholder.typicode.com/posts/1");
                    response.EnsureSuccessStatusCode();

                    string json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var post = JsonSerializer.Deserialize<Post>(json, options);

                    post.Dump("Deserialized Post");

                    record Post(int UserId, int Id, string Title, string Body);
                    """
                },
                new()
                {
                    Id = "snip_learn_http_post_json",
                    Title = "POST Request with a JSON Body",
                    Description = "Create a new resource by sending a JSON payload.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Net.Http;
                    using System.Text;
                    using System.Text.Json;
                    using System.Threading.Tasks;

                    using var client = new HttpClient();

                    var payload = new NewPost("Learning HttpClient", "This post was created from C# Code Studio.", 7);
                    string requestJson = JsonSerializer.Serialize(payload);

                    using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("https://jsonplaceholder.typicode.com/posts", content);

                    Console.WriteLine($"Status: {(int)response.StatusCode} {response.StatusCode}");
                    Console.WriteLine(await response.Content.ReadAsStringAsync());

                    record NewPost(string Title, string Body, int UserId);
                    """
                },
                new()
                {
                    Id = "snip_learn_http_put",
                    Title = "PUT Request — Update a Resource",
                    Description = "Replace an existing resource with new data.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Net.Http;
                    using System.Text;
                    using System.Text.Json;
                    using System.Threading.Tasks;

                    using var client = new HttpClient();

                    var payload = new UpdatedPost(1, "Updated Title", "Updated body text.", 1);
                    string requestJson = JsonSerializer.Serialize(payload);

                    using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                    var response = await client.PutAsync("https://jsonplaceholder.typicode.com/posts/1", content);

                    Console.WriteLine($"Status: {(int)response.StatusCode} {response.StatusCode}");
                    Console.WriteLine(await response.Content.ReadAsStringAsync());

                    record UpdatedPost(int Id, string Title, string Body, int UserId);
                    """
                },
                new()
                {
                    Id = "snip_learn_http_delete",
                    Title = "DELETE Request",
                    Description = "Remove a resource by its URL.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Net.Http;
                    using System.Threading.Tasks;

                    using var client = new HttpClient();

                    var response = await client.DeleteAsync("https://jsonplaceholder.typicode.com/posts/1");

                    Console.WriteLine(response.IsSuccessStatusCode
                        ? $"Deleted successfully ({(int)response.StatusCode})."
                        : $"Delete failed: {(int)response.StatusCode} {response.StatusCode}");
                    """
                },
                new()
                {
                    Id = "snip_learn_http_download",
                    Title = "Download a File to Disk",
                    Description = "Stream an HTTP response body straight into a file instead of buffering it in memory.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.IO;
                    using System.Net.Http;
                    using System.Threading.Tasks;

                    using var client = new HttpClient();

                    string tempDir = Path.Combine(Path.GetTempPath(), "frypdf-learn-csharp-demo");
                    Directory.CreateDirectory(tempDir);
                    string destination = Path.Combine(tempDir, "downloaded-image.png");

                    using (var response = await client.GetAsync("https://httpbin.org/image/png", HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        await using var httpStream = await response.Content.ReadAsStreamAsync();
                        await using var fileStream = File.Create(destination);
                        await httpStream.CopyToAsync(fileStream);
                    }

                    var info = new FileInfo(destination);
                    Console.WriteLine($"Downloaded {info.Length:N0} bytes to {destination}");
                    """
                },
                new()
                {
                    Id = "snip_learn_http_upload",
                    Title = "Upload a File (Multipart Form Data)",
                    Description = "Send a file to a server the same way an HTML form does.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.IO;
                    using System.Net.Http;
                    using System.Net.Http.Headers;
                    using System.Threading.Tasks;

                    using var client = new HttpClient();

                    string tempDir = Path.Combine(Path.GetTempPath(), "frypdf-learn-csharp-demo");
                    Directory.CreateDirectory(tempDir);
                    string tempFile = Path.Combine(tempDir, "upload-me.txt");
                    await File.WriteAllTextAsync(tempFile, "Sample content uploaded from C# Code Studio.");

                    using var form = new MultipartFormDataContent();
                    using var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(tempFile));
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                    form.Add(fileContent, "file", Path.GetFileName(tempFile));
                    form.Add(new StringContent("demo-upload"), "description");

                    var response = await client.PostAsync("https://httpbin.org/post", form);
                    response.EnsureSuccessStatusCode();

                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                    """
                },
                new()
                {
                    Id = "snip_learn_http_headers_auth_errors",
                    Title = "Headers, Bearer Auth, Timeouts & Error Handling",
                    Description = "Add custom headers and a bearer token, bound the request with a timeout, and handle a failed response safely.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Net.Http;
                    using System.Net.Http.Headers;
                    using System.Threading;
                    using System.Threading.Tasks;

                    using var client = new HttpClient();

                    using var request = new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/headers");
                    request.Headers.UserAgent.ParseAdd("FryPDF-CodeStudio/1.0");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "demo-token-123");

                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                    try
                    {
                        var response = await client.SendAsync(request, timeout.Token);
                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"Request failed: {(int)response.StatusCode} {response.StatusCode}");
                            return;
                        }

                        Console.WriteLine(await response.Content.ReadAsStringAsync());
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("Request timed out after 10 seconds.");
                    }
                    catch (HttpRequestException ex)
                    {
                        Console.WriteLine($"Network error: {ex.Message}");
                    }
                    """
                }
            }
        };
    }
}
