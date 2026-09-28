using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

public static class HttpInspectionHelper
{
    private const int MaxBodyPreviewChars = 20000;

    public static bool IsHttpType(object? val) =>
        val is HttpResponseMessage or HttpRequestMessage or HttpContent or HttpHeaders or HttpStatusCode;

    public static string FormatResponseSummary(HttpResponseMessage resp)
    {
        int code = (int)resp.StatusCode;
        string phrase = !string.IsNullOrWhiteSpace(resp.ReasonPhrase) ? resp.ReasonPhrase : resp.StatusCode.ToString();
        var ct = resp.Content?.Headers.ContentType?.MediaType ?? "no content-type";

        long? len = resp.Content?.Headers.ContentLength;
        string lenStr = len.HasValue ? FormatBytes(len.Value) : string.Empty;

        return string.IsNullOrEmpty(lenStr)
            ? $"{code} {phrase} • {ct}"
            : $"{code} {phrase} • {ct} • {lenStr}";
    }

    public static string FormatRequestSummary(HttpRequestMessage req)
    {
        string method = req.Method.Method;
        string uri = req.RequestUri?.ToString() ?? "(no url)";
        var ct = req.Content?.Headers.ContentType?.MediaType;

        return !string.IsNullOrEmpty(ct)
            ? $"{method} {uri} • {ct}"
            : $"{method} {uri}";
    }

    public static string FormatContentSummary(HttpContent content)
    {
        string typeName = content.GetType().Name;
        var mediaType = content.Headers.ContentType?.MediaType;
        long? len = content.Headers.ContentLength;

        string lenStr = len.HasValue ? FormatBytes(len.Value) : string.Empty;

        if (mediaType != null && !string.IsNullOrEmpty(lenStr))
            return $"{typeName} • {mediaType} • {lenStr}";

        if (mediaType != null)
            return $"{typeName} • {mediaType}";

        if (!string.IsNullOrEmpty(lenStr))
            return $"{typeName} • {lenStr}";

        return typeName;
    }

    public static string FormatHeadersSummary(HttpHeaders headers)
    {
        int count = headers.Count();
        return $"{count} header{(count == 1 ? string.Empty : "s")}";
    }

    public static string FormatStatusCodeSummary(HttpStatusCode code) =>
        $"{(int)code} {code}";

    public static List<(string Name, object? Value)> GetResponseChildren(HttpResponseMessage resp)
    {
        var list = new List<(string Name, object? Value)>();

        // 1. High-level status & method
        list.Add(("StatusCode", $"{(int)resp.StatusCode} {resp.StatusCode}"));
        list.Add(("IsSuccessStatusCode", resp.IsSuccessStatusCode));

        if (resp.RequestMessage != null)
        {
            list.Add(("Method", resp.RequestMessage.Method.Method));
            list.Add(("RequestUri", resp.RequestMessage.RequestUri?.ToString() ?? string.Empty));
        }

        // 2. Extracted Body payload
        string? body = TryReadHttpContentBody(resp.Content);
        if (body != null)
        {
            list.Add(("[Body]", body));
        }

        // 3. Response Headers (flattened)
        if (resp.Headers.Any())
        {
            list.Add(("Headers", resp.Headers));
        }

        // 4. Content (with Content-Headers & Body)
        if (resp.Content != null)
        {
            list.Add(("Content", resp.Content));
        }

        // 5. Raw Request Message
        if (resp.RequestMessage != null)
        {
            list.Add(("RequestMessage", resp.RequestMessage));
        }

        list.Add(("Version", resp.Version.ToString()));

        return list;
    }

    public static List<(string Name, object? Value)> GetRequestChildren(HttpRequestMessage req)
    {
        var list = new List<(string Name, object? Value)>();

        list.Add(("Method", req.Method.Method));
        list.Add(("RequestUri", req.RequestUri?.ToString() ?? string.Empty));

        // Extracted Request Body (for POST, PUT, PATCH)
        string? body = TryReadHttpContentBody(req.Content);
        if (body != null)
        {
            list.Add(("[Body]", body));
        }

        if (req.Headers.Any())
        {
            list.Add(("Headers", req.Headers));
        }

        if (req.Content != null)
        {
            list.Add(("Content", req.Content));
        }

        list.Add(("Version", req.Version.ToString()));

        return list;
    }

    public static List<(string Name, object? Value)> GetContentChildren(HttpContent content)
    {
        var list = new List<(string Name, object? Value)>();

        string? body = TryReadHttpContentBody(content);
        if (body != null)
        {
            list.Add(("[Body]", body));

            // Check if SSE stream
            var sseEvents = ParseSseEvents(body);
            if (sseEvents.Count > 0)
            {
                list.Add(("[SSE Events]", sseEvents));
            }
        }

        if (content.Headers.Any())
        {
            list.Add(("Headers", content.Headers));
        }

        return list;
    }

    public static List<(string Name, object? Value)> GetHeaderChildren(HttpHeaders headers)
    {
        var list = new List<(string Name, object? Value)>();
        foreach (var header in headers)
        {
            string val = string.Join(", ", header.Value);
            list.Add((header.Key, val));
        }
        return list;
    }

    public static string? TryReadHttpContentBody(HttpContent? content)
    {
        if (content == null) return null;

        try
        {
            // If already completed or in-memory (StringContent, ByteArrayContent, JsonContent, buffered response)
            var task = content.ReadAsStringAsync();
            if (task.IsCompleted)
            {
                string text = task.GetAwaiter().GetResult();
                return TruncateIfNeeded(text);
            }

            // If not completed yet, wait up to 100ms for memory buffers without freezing
            if (task.Wait(100))
            {
                string text = task.GetAwaiter().GetResult();
                return TruncateIfNeeded(text);
            }
        }
        catch
        {
            // Content might be a streaming body not yet read or disposed
        }

        return null;
    }

    public static List<Dictionary<string, string>> ParseSseEvents(string rawText)
    {
        var events = new List<Dictionary<string, string>>();
        if (string.IsNullOrWhiteSpace(rawText) || (!rawText.Contains("data:") && !rawText.Contains("event:")))
        {
            return events;
        }

        var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var currentEvent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentEvent.Count > 0)
                {
                    events.Add(new Dictionary<string, string>(currentEvent));
                    currentEvent.Clear();
                }
                continue;
            }

            int colonIdx = line.IndexOf(':');
            if (colonIdx > 0)
            {
                string key = line.Substring(0, colonIdx).Trim();
                string val = line.Substring(colonIdx + 1).TrimStart();
                if (currentEvent.TryGetValue(key, out var existing))
                {
                    currentEvent[key] = existing + "\n" + val;
                }
                else
                {
                    currentEvent[key] = val;
                }
            }
        }

        if (currentEvent.Count > 0)
        {
            events.Add(currentEvent);
        }

        return events;
    }

    private static string TruncateIfNeeded(string text)
    {
        if (text.Length > MaxBodyPreviewChars)
        {
            return text.Substring(0, MaxBodyPreviewChars) + $" … [truncated, {text.Length:N0} total characters]";
        }
        return text;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
