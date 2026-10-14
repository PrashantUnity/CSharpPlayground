using System.Globalization;
using System.Net;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// Renders DumpTableResult and ObjectInspectorNode into an interactive, compact HTML component
/// matching the desktop IDE's DumpTableView and ObjectInspector.
/// </summary>
public static class InteractiveTableHtmlRenderer
{
    private const int MaxRenderedRows = 1000;

    public static string Render(DumpTableResult table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var uid = "fry_tbl_" + Guid.NewGuid().ToString("N")[..8];
        var title = WebUtility.HtmlEncode(!string.IsNullOrWhiteSpace(table.Title) ? table.Title : (!string.IsNullOrWhiteSpace(table.Label) ? table.Label : "Data Table"));
        var rowCount = table.Rows.Count;
        var colCount = table.Columns.Count;
        var renderedRows = Math.Min(rowCount, MaxRenderedRows);

        var headerHtml = new StringBuilder();
        for (var c = 0; c < colCount; c++)
        {
            var col = table.Columns[c];
            var align = col.IsNumeric ? "right" : "left";
            var colHeader = WebUtility.HtmlEncode(col.Header);
            var isNumStr = col.IsNumeric ? "true" : "false";
            headerHtml.Append($$"""
            <th class="sortable" onclick="fryTblSort('{{uid}}', {{c}}, {{isNumStr}})" style="text-align:{{align}};cursor:pointer;" title="Sort by {{colHeader}}">
              {{colHeader}} <span class="fry-sort-ind" style="opacity:0.35;font-size:10px;margin-left:3px;">↕</span>
            </th>
            """);
        }

        var rowsHtml = new StringBuilder();
        for (var r = 0; r < renderedRows; r++)
        {
            var row = table.Rows[r];
            var bg = (r % 2 == 0) ? "background:rgba(255,255,255,0.015);" : "background:transparent;";

            var cellsHtml = new StringBuilder();
            var propCardsHtml = new StringBuilder();

            for (var c = 0; c < colCount; c++)
            {
                var col = table.Columns[c];
                var cell = c < row.Cells.Count ? row.Cells[c] : null;
                var isNum = col.IsNumeric;
                var align = isNum ? "right" : "left";
                var (formatted, cssClass) = FormatCellHtml(cell, isNum);

                cellsHtml.Append($$"""<td style="text-align:{{align}};" class="{{cssClass}}">{{formatted}}</td>""");

                propCardsHtml.Append($$"""
                <div class="fry-prop-card">
                  <div class="fry-prop-key">{{WebUtility.HtmlEncode(col.Header)}}</div>
                  <div class="fry-prop-val">{{formatted}}</div>
                </div>
                """);
            }

            var nestedTablesHtml = new StringBuilder();
            for (var c = 0; c < row.Cells.Count; c++)
            {
                if (row.Cells[c].NestedTable is { } childTable)
                {
                    nestedTablesHtml.Append($$"""
                    <div style="margin-top:10px;">
                      {{Render(childTable)}}
                    </div>
                    """);
                }
            }

            rowsHtml.Append($$"""
            <tr class="fry-data-row" data-row="{{r}}" style="{{bg}}">
              <td style="text-align:center;padding:6px 4px;">
                <button type="button" class="fry-expander-btn" onclick="fryTblToggleRow('{{uid}}', {{r}})" title="Toggle row details">▶</button>
              </td>
              {{cellsHtml}}
            </tr>
            <tr class="fry-detail-row" id="{{uid}}_detail_{{r}}" style="display:none;">
              <td colspan="{{colCount + 1}}" style="padding:10px 14px;border-bottom:1px solid var(--vscode-widget-border,#30363d);">
                <div style="background:var(--vscode-editor-background,#0d1117);border:1px solid var(--vscode-widget-border,#30363d);border-radius:6px;padding:10px 14px;">
                  <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:8px;">
                    <div style="font-size:11.5px;font-weight:600;color:#58a6ff;">🔍 Row #{{r + 1}} Inspector</div>
                    <div style="font-size:10.5px;color:#8b949e;">{{colCount}} properties</div>
                  </div>
                  <div class="fry-prop-grid">
                    {{propCardsHtml}}
                  </div>
                  {{nestedTablesHtml}}
                </div>
              </td>
            </tr>
            """);
        }

        return $$"""
        <div class="fry-tbl-container" id="{{uid}}" style="font-family:system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:var(--vscode-editor-background,#0d1117);border:1px solid var(--vscode-widget-border,#30363d);border-radius:8px;margin:10px 0;max-width:960px;box-shadow:0 4px 16px rgba(0,0,0,0.35);overflow:hidden;">
          <style>
            #{{uid}} .fry-tbl-header {
              display: flex; align-items: center; justify-content: space-between; gap: 8px;
              padding: 8px 12px; background: var(--vscode-sideBar-background, #161b22);
              border-bottom: 1px solid var(--vscode-widget-border, #30363d);
            }
            #{{uid}} .fry-tbl-toggle-btn {
              display: flex; align-items: center; gap: 8px; background: transparent;
              border: none; color: var(--vscode-foreground, #c9d1d9); cursor: pointer;
              padding: 0; font-size: 13px; font-weight: 600; text-align: left;
            }
            #{{uid}} .fry-tbl-pill {
              font-size: 11px; font-weight: 500; color: var(--vscode-descriptionForeground, #8b949e);
              background: var(--vscode-badge-background, #21262d); border: 1px solid var(--vscode-widget-border, #30363d);
              padding: 2px 8px; border-radius: 12px;
            }
            #{{uid}} .fry-tbl-actions {
              display: flex; align-items: center; gap: 8px;
            }
            #{{uid}} .fry-tbl-search-wrap {
              position: relative; display: flex; align-items: center;
            }
            #{{uid}} .fry-tbl-search-input {
              background: var(--vscode-input-background, #0d1117);
              border: 1px solid var(--vscode-input-border, #30363d);
              color: var(--vscode-input-foreground, #c9d1d9);
              padding: 3px 8px 3px 24px; border-radius: 4px; font-size: 11.5px;
              width: 140px; transition: border-color 0.2s, width 0.2s;
            }
            #{{uid}} .fry-tbl-search-input:focus {
              outline: none; border-color: var(--vscode-focusBorder, #58a6ff); width: 180px;
            }
            #{{uid}} .fry-tbl-search-icon {
              position: absolute; left: 7px; font-size: 11px; opacity: 0.6; pointer-events: none;
            }
            #{{uid}} .fry-tbl-btn {
              background: var(--vscode-button-secondaryBackground, #21262d);
              color: var(--vscode-button-secondaryForeground, #c9d1d9);
              border: 1px solid var(--vscode-widget-border, #30363d);
              border-radius: 4px; padding: 3px 8px; font-size: 11px; font-weight: 500;
              cursor: pointer; transition: background 0.15s;
            }
            #{{uid}} .fry-tbl-btn:hover {
              background: var(--vscode-button-secondaryHoverBackground, #30363d);
            }
            #{{uid}} .fry-tbl-scroll {
              max-height: 420px; overflow-y: auto; overflow-x: auto;
            }
            #{{uid}} table.fry-tbl-grid {
              width: 100%; border-collapse: collapse; text-align: left; font-size: 12px;
            }
            #{{uid}} thead th {
              position: sticky; top: 0; z-index: 5; background: var(--vscode-sideBar-background, #161b22);
              padding: 8px 12px; border-bottom: 2px solid var(--vscode-widget-border, #30363d);
              font-weight: 600; color: var(--vscode-foreground, #c9d1d9); user-select: none;
            }
            #{{uid}} thead th.sortable:hover {
              background: var(--vscode-list-hoverBackground, #21262d);
            }
            #{{uid}} tbody tr.fry-data-row {
              border-bottom: 1px solid rgba(255, 255, 255, 0.06);
              transition: background 0.12s;
            }
            #{{uid}} tbody tr.fry-data-row:hover {
              background: var(--vscode-list-hoverBackground, rgba(56, 139, 253, 0.08)) !important;
            }
            #{{uid}} td {
              padding: 7px 12px; vertical-align: middle;
            }
            #{{uid}} .fry-num {
              font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
              text-align: right; color: #79c0ff;
            }
            #{{uid}} .fry-badge {
              display: inline-block; padding: 2px 7px; border-radius: 12px;
              font-size: 11px; font-weight: 500; letter-spacing: 0.2px;
            }
            #{{uid}} .fry-badge-green {
              background: rgba(35, 134, 54, 0.2); color: #3fb950; border: 1px solid rgba(63, 185, 80, 0.4);
            }
            #{{uid}} .fry-badge-amber {
              background: rgba(187, 128, 9, 0.2); color: #d29922; border: 1px solid rgba(210, 153, 34, 0.4);
            }
            #{{uid}} .fry-badge-red {
              background: rgba(218, 54, 51, 0.2); color: #f85149; border: 1px solid rgba(248, 81, 73, 0.4);
            }
            #{{uid}} .fry-badge-blue {
              background: rgba(31, 111, 235, 0.2); color: #58a6ff; border: 1px solid rgba(88, 166, 255, 0.4);
            }
            #{{uid}} .fry-expander-btn {
              background: transparent; border: none; cursor: pointer;
              color: var(--vscode-textLink-foreground, #58a6ff); font-size: 10px;
              padding: 3px 5px; border-radius: 4px; transition: transform 0.15s;
            }
            #{{uid}} .fry-expander-btn:hover {
              background: rgba(255,255,255,0.1);
            }
            #{{uid}} tr.fry-detail-row {
              background: rgba(0, 0, 0, 0.28);
            }
            #{{uid}} .fry-prop-grid {
              display: grid; grid-template-columns: repeat(auto-fill, minmax(210px, 1fr)); gap: 8px; margin-top: 6px;
            }
            #{{uid}} .fry-prop-card {
              background: rgba(255,255,255,0.03); border: 1px solid rgba(255,255,255,0.08);
              border-radius: 4px; padding: 6px 10px;
            }
            #{{uid}} .fry-prop-key {
              font-size: 10px; font-weight: 600; color: #8b949e; text-transform: uppercase; letter-spacing: 0.5px;
            }
            #{{uid}} .fry-prop-val {
              font-size: 12px; color: #c9d1d9; margin-top: 2px; word-break: break-word;
            }
            #{{uid}} .fry-tbl-footer {
              display: flex; align-items: center; justify-content: space-between;
              padding: 6px 12px; font-size: 11px; color: var(--vscode-descriptionForeground, #8b949e);
              background: var(--vscode-sideBar-background, #161b22);
              border-top: 1px solid var(--vscode-widget-border, #30363d);
            }
          </style>

          <div class="fry-tbl-header">
            <button type="button" class="fry-tbl-toggle-btn" onclick="fryTblToggle('{{uid}}')" title="Click to collapse/expand table">
              <span class="fry-tbl-chevron" style="color:#58a6ff;">▼</span>
              <span>📊 {{title}}</span>
              <span class="fry-tbl-pill">{{rowCount}} rows × {{colCount}} cols</span>
            </button>
            <div class="fry-tbl-actions">
              <div class="fry-tbl-search-wrap">
                <span class="fry-tbl-search-icon">🔍</span>
                <input type="text" class="fry-tbl-search-input" placeholder="Filter rows..." oninput="fryTblFilter('{{uid}}', this.value)" />
              </div>
              <button type="button" class="fry-tbl-btn" onclick="fryTblCopy('{{uid}}', 'csv')" title="Copy as CSV">CSV</button>
              <button type="button" class="fry-tbl-btn" onclick="fryTblCopy('{{uid}}', 'json')" title="Copy as JSON">JSON</button>
            </div>
          </div>

          <div class="fry-tbl-scroll">
            <table class="fry-tbl-grid">
              <thead>
                <tr>
                  <th style="width:36px;text-align:center;padding:8px 4px;"></th>
                  {{headerHtml}}
                </tr>
              </thead>
              <tbody>
                {{rowsHtml}}
              </tbody>
            </table>
          </div>

          <div class="fry-tbl-footer">
            <span id="{{uid}}_counter">Showing {{renderedRows}} of {{rowCount}} rows</span>
            <span style="opacity:0.6;font-size:10.5px;">FrySharp Polyglot Data</span>
          </div>

          <script>
            (function() {
              if (window.__fryTblInit) return;
              window.__fryTblInit = true;

              window.fryTblToggle = function(id) {
                var el = document.getElementById(id);
                if (!el) return;
                var body = el.querySelector('.fry-tbl-scroll');
                var footer = el.querySelector('.fry-tbl-footer');
                var chevron = el.querySelector('.fry-tbl-chevron');
                var isHidden = body.style.display === 'none';
                body.style.display = isHidden ? 'block' : 'none';
                footer.style.display = isHidden ? 'flex' : 'none';
                if (chevron) chevron.textContent = isHidden ? '▼' : '▶';
              };

              window.fryTblToggleRow = function(id, rowIdx) {
                var drawer = document.getElementById(id + '_detail_' + rowIdx);
                var el = document.getElementById(id);
                if (!drawer || !el) return;
                var btn = el.querySelector('tr[data-row="' + rowIdx + '"] .fry-expander-btn');
                var isOpen = drawer.style.display !== 'none';
                drawer.style.display = isOpen ? 'none' : 'table-row';
                if (btn) btn.textContent = isOpen ? '▶' : '▼';
              };

              window.fryTblFilter = function(id, query) {
                var el = document.getElementById(id);
                if (!el) return;
                var q = (query || '').toLowerCase().trim();
                var rows = el.querySelectorAll('tbody tr.fry-data-row');
                var count = 0;
                rows.forEach(function(row) {
                  var rIdx = row.getAttribute('data-row');
                  var drawer = document.getElementById(id + '_detail_' + rIdx);
                  var match = !q || row.innerText.toLowerCase().indexOf(q) !== -1;
                  row.style.display = match ? '' : 'none';
                  if (!match && drawer) drawer.style.display = 'none';
                  if (match) count++;
                });
                var cnt = document.getElementById(id + '_counter');
                if (cnt) {
                  cnt.textContent = q ? ('Showing ' + count + ' of ' + rows.length + ' rows (filtered)') : ('Showing ' + rows.length + ' of ' + rows.length + ' rows');
                }
              };

              window.fryTblSort = function(id, colIdx, isNum) {
                var el = document.getElementById(id);
                if (!el) return;
                var tbody = el.querySelector('tbody');
                var rows = Array.from(el.querySelectorAll('tbody tr.fry-data-row'));
                var th = el.querySelectorAll('thead th')[colIdx + 1];
                var dir = th.getAttribute('data-sort-dir') === 'asc' ? 'desc' : 'asc';

                el.querySelectorAll('thead th').forEach(function(h) {
                  h.removeAttribute('data-sort-dir');
                  var s = h.querySelector('.fry-sort-ind');
                  if (s) s.textContent = '↕';
                });

                th.setAttribute('data-sort-dir', dir);
                var ind = th.querySelector('.fry-sort-ind');
                if (ind) ind.textContent = dir === 'asc' ? '▲' : '▼';

                rows.sort(function(a, b) {
                  var vA = a.children[colIdx + 1]?.innerText?.trim() || '';
                  var vB = b.children[colIdx + 1]?.innerText?.trim() || '';
                  if (isNum) {
                    var nA = parseFloat(vA.replace(/[^0-9.-]/g, '')) || 0;
                    var nB = parseFloat(vB.replace(/[^0-9.-]/g, '')) || 0;
                    return dir === 'asc' ? nA - nB : nB - nA;
                  }
                  return dir === 'asc' ? vA.localeCompare(vB) : vB.localeCompare(vA);
                });

                rows.forEach(function(r) {
                  var rIdx = r.getAttribute('data-row');
                  var drawer = document.getElementById(id + '_detail_' + rIdx);
                  tbody.appendChild(r);
                  if (drawer) tbody.appendChild(drawer);
                });
              };

              window.fryTblCopy = function(id, format) {
                var el = document.getElementById(id);
                if (!el) return;
                var ths = Array.from(el.querySelectorAll('thead th')).slice(1);
                var headers = ths.map(function(h) { return h.innerText.replace(/[↕▲▼]/g, '').trim(); });
                var rows = Array.from(el.querySelectorAll('tbody tr.fry-data-row'));
                var data = rows.map(function(r) {
                  return Array.from(r.children).slice(1).map(function(c) { return c.innerText.trim(); });
                });

                var payload = '';
                if (format === 'json') {
                  var list = data.map(function(r) {
                    var o = {};
                    headers.forEach(function(h, idx) { o[h] = r[idx]; });
                    return o;
                  });
                  payload = JSON.stringify(list, null, 2);
                } else {
                  payload = headers.join(',') + '\n' + data.map(function(r) {
                    return r.map(function(val) { return '"' + val.replace(/"/g, '""') + '"'; }).join(',');
                  }).join('\n');
                }

                navigator.clipboard.writeText(payload).then(function() {
                  var btn = el.querySelector('.fry-tbl-btn[onclick*="' + format + '"]');
                  if (btn) {
                    var prev = btn.textContent;
                    btn.textContent = '✓ Copied!';
                    setTimeout(function() { btn.textContent = prev; }, 1500);
                  }
                });
              };
            })();
          </script>
        </div>
        """;
    }

    public static string RenderInspector(ObjectInspectorNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var title = WebUtility.HtmlEncode(node.HeaderTitle);
        var propRowsHtml = new StringBuilder();
        RenderInspectorProperties(propRowsHtml, node);

        return $$"""
        <div class="fry-inspector-card" style="font-family:system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:var(--vscode-editor-background,#0d1117);border:1px solid var(--vscode-widget-border,#30363d);border-radius:8px;padding:12px;margin:8px 0;max-width:840px;box-shadow:0 4px 14px rgba(0,0,0,0.3);">
          <div style="display:flex;align-items:center;gap:8px;margin-bottom:10px;">
            <span style="font-size:13.5px;font-weight:600;color:#58a6ff;">🔬 {{title}}</span>
          </div>
          {{propRowsHtml}}
        </div>
        """;
    }

    private static void RenderInspectorProperties(StringBuilder sb, ObjectInspectorNode node)
    {
        sb.Append("""<div style="display:flex;flex-direction:column;gap:5px;">""");
        foreach (var prop in node.Properties)
        {
            var name = WebUtility.HtmlEncode(prop.Name);
            if (prop.IsComplexChild && prop.ChildNode != null)
            {
                var childPropsHtml = new StringBuilder();
                RenderInspectorProperties(childPropsHtml, prop.ChildNode);
                sb.Append($$"""
                <details open style="border:1px solid rgba(255,255,255,0.06);border-radius:5px;padding:6px 10px;background:rgba(255,255,255,0.015);">
                  <summary style="cursor:pointer;font-size:12px;font-weight:600;color:#58a6ff;">{{name}}: <span style="font-weight:normal;color:#8b949e;">{{WebUtility.HtmlEncode(prop.SimpleValueText)}}</span></summary>
                  <div style="margin-left:14px;margin-top:6px;border-left:2px solid #30363d;padding-left:10px;">
                    {{childPropsHtml}}
                  </div>
                </details>
                """);
            }
            else
            {
                var (valHtml, _) = FormatValueText(prop.SimpleValueText, prop.IsNull, prop.IsString);
                sb.Append($$"""
                <div style="display:flex;align-items:center;justify-content:space-between;padding:4px 8px;background:rgba(255,255,255,0.02);border-radius:4px;font-size:12px;">
                  <span style="font-weight:600;color:#8b949e;">{{name}}</span>
                  <span>{{valHtml}}</span>
                </div>
                """);
            }
        }
        sb.Append("</div>");
    }

    private static (string html, string cssClass) FormatCellHtml(DumpTableCell? cell, bool isColumnNumeric)
    {
        if (cell == null || cell.IsNull)
        {
            return ("""<span style="opacity:0.35;font-style:italic;">null</span>""", "");
        }

        var text = cell.DisplayText ?? string.Empty;
        var lower = text.Trim().ToLowerInvariant();

        switch (lower)
        {
            case "online" or "active" or "healthy" or "optimal" or "passed" or "success" or "true" or "running" or "ready" or "connected":
                return ($$"""<span class="fry-badge fry-badge-green">{{WebUtility.HtmlEncode(text)}}</span>""", "");
            case "warning" or "degraded" or "under load" or "pending" or "slow" or "paused" or "retrying":
                return ($$"""<span class="fry-badge fry-badge-amber">{{WebUtility.HtmlEncode(text)}}</span>""", "");
            case "error" or "failed" or "critical" or "offline" or "down" or "false" or "stopped":
                return ($$"""<span class="fry-badge fry-badge-red">{{WebUtility.HtmlEncode(text)}}</span>""", "");
        }

        if (cell.IsNumeric || isColumnNumeric)
        {
            return (WebUtility.HtmlEncode(text), "fry-num");
        }

        return (WebUtility.HtmlEncode(text), "");
    }

    private static (string html, string cssClass) FormatValueText(string text, bool isNull, bool isString)
    {
        if (isNull) return ("""<span style="opacity:0.35;font-style:italic;">null</span>""", "");
        var lower = text.Trim().ToLowerInvariant();
        if (lower is "true" or "false")
        {
            var badgeClass = lower == "true" ? "fry-badge-green" : "fry-badge-red";
            return ($$"""<span class="fry-badge {{badgeClass}}">{{text}}</span>""", "");
        }
        if (isString)
        {
            return ($$"""<span style="color:#a5d6ff;">"{{WebUtility.HtmlEncode(text)}}"</span>""", "");
        }
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            return (WebUtility.HtmlEncode(text), "fry-num");
        }
        return (WebUtility.HtmlEncode(text), "");
    }
}
