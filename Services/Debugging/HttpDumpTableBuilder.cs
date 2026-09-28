using System;
using System.Collections.Generic;
using System.Net.Http;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

public static class HttpDumpTableBuilder
{
    public static bool TryCreate(object? obj, string? label, out DumpTableResult? result)
    {
        result = null;
        if (obj == null) return false;

        if (obj is HttpResponseMessage resp)
        {
            result = CreateResponseTable(resp, label);
            return true;
        }

        if (obj is HttpRequestMessage req)
        {
            result = CreateRequestTable(req, label);
            return true;
        }

        if (obj is HttpContent content)
        {
            result = CreateContentTable(content, label);
            return true;
        }

        return false;
    }

    private static DumpTableResult CreateResponseTable(HttpResponseMessage resp, string? label)
    {
        int code = (int)resp.StatusCode;
        string phrase = !string.IsNullOrWhiteSpace(resp.ReasonPhrase) ? resp.ReasonPhrase : resp.StatusCode.ToString();
        string title = $"HTTP {code} {phrase}";

        var table = new DumpTableResult(title, label);
        table.Columns.Add(new DumpTableColumn { Header = "Field", IsNumeric = false });
        table.Columns.Add(new DumpTableColumn { Header = "Value", IsNumeric = false });

        int rowIdx = 0;
        AddRow(table, rowIdx++, "Status", $"{code} {phrase}");
        AddRow(table, rowIdx++, "Success", resp.IsSuccessStatusCode ? "true" : "false");

        if (resp.RequestMessage != null)
        {
            AddRow(table, rowIdx++, "Request Method", resp.RequestMessage.Method.Method);
            AddRow(table, rowIdx++, "Request URI", resp.RequestMessage.RequestUri?.ToString() ?? string.Empty);
        }

        foreach (var h in resp.Headers)
        {
            AddRow(table, rowIdx++, $"Header: {h.Key}", string.Join(", ", h.Value));
        }

        if (resp.Content != null)
        {
            foreach (var ch in resp.Content.Headers)
            {
                AddRow(table, rowIdx++, $"Content Header: {ch.Key}", string.Join(", ", ch.Value));
            }

            string? body = HttpInspectionHelper.TryReadHttpContentBody(resp.Content);
            if (!string.IsNullOrEmpty(body))
            {
                AddRow(table, rowIdx++, "Body Payload", body);
            }
        }

        AddRow(table, rowIdx++, "Version", resp.Version.ToString());
        return table;
    }

    private static DumpTableResult CreateRequestTable(HttpRequestMessage req, string? label)
    {
        string title = $"HTTP Request {req.Method.Method} {req.RequestUri}";
        var table = new DumpTableResult(title, label);
        table.Columns.Add(new DumpTableColumn { Header = "Field", IsNumeric = false });
        table.Columns.Add(new DumpTableColumn { Header = "Value", IsNumeric = false });

        int rowIdx = 0;
        AddRow(table, rowIdx++, "Method", req.Method.Method);
        AddRow(table, rowIdx++, "Request URI", req.RequestUri?.ToString() ?? string.Empty);

        foreach (var h in req.Headers)
        {
            AddRow(table, rowIdx++, $"Header: {h.Key}", string.Join(", ", h.Value));
        }

        if (req.Content != null)
        {
            foreach (var ch in req.Content.Headers)
            {
                AddRow(table, rowIdx++, $"Content Header: {ch.Key}", string.Join(", ", ch.Value));
            }

            string? body = HttpInspectionHelper.TryReadHttpContentBody(req.Content);
            if (!string.IsNullOrEmpty(body))
            {
                AddRow(table, rowIdx++, "Body Payload", body);
            }
        }

        AddRow(table, rowIdx++, "Version", req.Version.ToString());
        return table;
    }

    private static DumpTableResult CreateContentTable(HttpContent content, string? label)
    {
        string title = $"HttpContent: {content.GetType().Name}";
        var table = new DumpTableResult(title, label);
        table.Columns.Add(new DumpTableColumn { Header = "Field", IsNumeric = false });
        table.Columns.Add(new DumpTableColumn { Header = "Value", IsNumeric = false });

        int rowIdx = 0;
        AddRow(table, rowIdx++, "Type", content.GetType().Name);

        foreach (var ch in content.Headers)
        {
            AddRow(table, rowIdx++, $"Header: {ch.Key}", string.Join(", ", ch.Value));
        }

        string? body = HttpInspectionHelper.TryReadHttpContentBody(content);
        if (!string.IsNullOrEmpty(body))
        {
            AddRow(table, rowIdx++, "Body Payload", body);
        }

        return table;
    }

    private static void AddRow(DumpTableResult table, int index, string field, string value)
    {
        var fieldCell = new DumpTableCell { DisplayText = field, RawValue = field };
        var valCell = new DumpTableCell { DisplayText = value, RawValue = value };
        table.Rows.Add(new DumpTableRow(index, new[] { fieldCell, valCell }));
    }
}
