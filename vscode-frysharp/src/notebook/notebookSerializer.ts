import * as vscode from 'vscode';

export class FryNotebookSerializer implements vscode.NotebookSerializer {
  async deserializeNotebook(content: Uint8Array, token: vscode.CancellationToken): Promise<vscode.NotebookData> {
    const text = new TextDecoder().decode(content);
    if (!text.trim()) {
      return new vscode.NotebookData([]);
    }

    let parsed: any;
    try {
      parsed = JSON.parse(text);
    } catch {
      return new vscode.NotebookData([]);
    }

    // 1. Check if standard Jupyter format (.ipynb)
    if (parsed.cells && Array.isArray(parsed.cells) && parsed.nbformat) {
      return this.deserializeJupyter(parsed);
    }

    // 2. Native FrySharp (.frynb / .csnb) format
    // Accommodate both PascalCase (from C# System.Text.Json) and camelCase
    const rawCells: any[] = parsed.Cells || parsed.cells || [];
    const defaultLanguage: string = parsed.Kernel || parsed.kernel || 'csharp';
    const notebookTitle: string = parsed.Title || parsed.title || 'Untitled';
    const notebookId: string = parsed.Id || parsed.id || '';

    const cells: vscode.NotebookCellData[] = rawCells.map((cell: any, idx: number) => {
      const rawType = cell.Type ?? cell.type;
      const isMarkdown = rawType === 1 || rawType === '1' || String(rawType).toLowerCase() === 'markdown';
      const kind = isMarkdown ? vscode.NotebookCellKind.Markup : vscode.NotebookCellKind.Code;

      let source = '';
      if (typeof cell.Source === 'string') {
        source = cell.Source;
      } else if (typeof cell.source === 'string') {
        source = cell.source;
      } else if (Array.isArray(cell.Source)) {
        source = cell.Source.join('');
      } else if (Array.isArray(cell.source)) {
        source = cell.source.join('');
      }

      const explicitLanguage = cell.Language ?? cell.language;
      const languageId = isMarkdown ? 'markdown' : (explicitLanguage || defaultLanguage);

      const cellData = new vscode.NotebookCellData(kind, source, languageId);
      const cellId = cell.Id ?? cell.id ?? `cell-${idx + 1}`;
      cellData.metadata = { id: cellId };

      const execCount = cell.ExecutionCount ?? cell.executionCount;
      if (execCount != null) {
        cellData.executionSummary = { executionOrder: execCount };
      }

      // Restore cell outputs
      const outputs: vscode.NotebookCellOutput[] = [];

      // 2a. OutputText (console stream)
      const outputText = cell.OutputText ?? cell.outputText;
      if (outputText && typeof outputText === 'string' && outputText.length > 0) {
        outputs.push(new vscode.NotebookCellOutput([
          vscode.NotebookCellOutputItem.text(outputText, 'text/plain')
        ]));
      }

      // 2b. HtmlContent
      const htmlContent = cell.HtmlContent ?? cell.htmlContent;
      if (htmlContent && typeof htmlContent === 'string' && htmlContent.length > 0) {
        outputs.push(new vscode.NotebookCellOutput([
          vscode.NotebookCellOutputItem.text(htmlContent, 'text/html')
        ]));
      } else {
        // Fallback: Check if TableSnapshot is saved
        const tableSnapshot = cell.TableSnapshot ?? cell.tableSnapshot;
        if (tableSnapshot && Array.isArray(tableSnapshot.Columns || tableSnapshot.columns)) {
          const tableHtml = FryNotebookSerializer.formatTableSnapshotToHtml(tableSnapshot);
          if (tableHtml) {
            outputs.push(new vscode.NotebookCellOutput([
              vscode.NotebookCellOutputItem.text(tableHtml, 'text/html')
            ]));
          }
        }
      }

      // 2b-2. Visuals array (saved charts & 3D plots)
      const visuals = cell.Visuals ?? cell.visuals;
      if (Array.isArray(visuals)) {
        for (const viz of visuals) {
          const vizHtml = this.formatVisualToHtml(viz);
          if (vizHtml) {
            outputs.push(new vscode.NotebookCellOutput([
              vscode.NotebookCellOutputItem.text(vizHtml, 'text/html')
            ]));
          }
        }
      }

      // 2c. ImageBytes (base64 string or byte array)
      const rawImage = cell.ImageBytes ?? cell.imageBytes;
      if (rawImage) {
        try {
          let bytes: Uint8Array;
          if (typeof rawImage === 'string') {
            bytes = Uint8Array.from(Buffer.from(rawImage, 'base64'));
          } else if (Array.isArray(rawImage)) {
            bytes = new Uint8Array(rawImage);
          } else {
            bytes = new Uint8Array();
          }

          if (bytes.length > 0) {
            const format = (cell.ImageFormat ?? cell.imageFormat ?? 'png').toLowerCase();
            const mime = format.startsWith('image/') ? format : `image/${format}`;
            const b64 = typeof rawImage === 'string' ? rawImage : Buffer.from(bytes).toString('base64');
            const html = `<div class="fry-img-card" style="margin:8px 0;max-width:840px;"><img src="data:${mime};base64,${b64}" style="max-width:100%;height:auto;border-radius:6px;border:1px solid #30363d;box-shadow:0 4px 16px rgba(0,0,0,0.35);display:block;" alt="Synthesized Image" /></div>`;
            outputs.push(new vscode.NotebookCellOutput([
              vscode.NotebookCellOutputItem.text(html, 'text/html'),
              new vscode.NotebookCellOutputItem(bytes, mime)
            ]));
          }
        } catch {
          // ignore corrupted image decode
        }
      }

      // 2d. Explicit outputs array (if present from mixed format)
      const explicitOutputs = cell.outputs ?? cell.Outputs;
      if (Array.isArray(explicitOutputs)) {
        for (const out of explicitOutputs) {
          if (out.kind === 'stream' && out.text) {
            outputs.push(new vscode.NotebookCellOutput([
              vscode.NotebookCellOutputItem.text(out.text, 'text/plain')
            ]));
          } else if (out.kind === 'display' && out.data) {
            if (!out.data['text/html']) {
              if (out.data['application/vnd.fry.table+json']) {
                out.data['text/html'] = FryNotebookSerializer.formatTableJsonToHtml(out.data['application/vnd.fry.table+json']);
              } else {
                const imgKey = Object.keys(out.data).find(k => k.startsWith('image/') && k !== 'image/svg+xml');
                if (imgKey && typeof out.data[imgKey] === 'string') {
                  const b64 = out.data[imgKey];
                  out.data['text/html'] = `<div class="fry-img-card" style="margin:8px 0;max-width:840px;"><img src="data:${imgKey};base64,${b64}" style="max-width:100%;height:auto;border-radius:6px;border:1px solid #30363d;box-shadow:0 4px 16px rgba(0,0,0,0.35);display:block;" alt="Synthesized Image" /></div>`;
                }
              }
            }
            const preferredOrder = ['text/html', 'image/svg+xml', 'image/png', 'image/jpeg', 'image/gif', 'text/markdown', 'text/plain'];
            const entries = Object.entries(out.data).sort(([mimeA], [mimeB]) => {
              const idxA = preferredOrder.indexOf(mimeA);
              const idxB = preferredOrder.indexOf(mimeB);
              const rankA = idxA >= 0 ? idxA : 100;
              const rankB = idxB >= 0 ? idxB : 100;
              return rankA - rankB;
            });

            const items = entries.map(([mime, val]) => {
              let bytes: Uint8Array;
              const str = Array.isArray(val) ? val.join('') : (typeof val === 'string' ? val : JSON.stringify(val));
              if (typeof str === 'string' && mime.startsWith('image/') && mime !== 'image/svg+xml') {
                bytes = Buffer.from(str, 'base64');
              } else {
                bytes = new TextEncoder().encode(str);
              }
              return new vscode.NotebookCellOutputItem(bytes, mime);
            });
            outputs.push(new vscode.NotebookCellOutput(items, out.metadata || undefined));
          }
        }
      }

      if (outputs.length > 0) {
        cellData.outputs = outputs;
      }

      return cellData;
    });

    const notebookData = new vscode.NotebookData(cells);
    notebookData.metadata = {
      isJupyter: false,
      id: notebookId,
      title: notebookTitle,
      kernel: defaultLanguage,
      schemaVersion: parsed.SchemaVersion || parsed.schemaVersion || '1.0'
    };
    return notebookData;
  }

  async serializeNotebook(data: vscode.NotebookData, token: vscode.CancellationToken): Promise<Uint8Array> {
    // 1. If original was Jupyter (.ipynb), serialize back as Jupyter notebook
    if (data.metadata?.isJupyter) {
      const cells = data.cells.map((cell, idx) => {
        const isMarkdown = cell.kind === vscode.NotebookCellKind.Markup;
        const sourceLines = cell.value.split('\n').map((line, i, arr) => i < arr.length - 1 ? line + '\n' : line);

        if (isMarkdown) {
          return {
            cell_type: 'markdown',
            id: cell.metadata?.id || `cell-${idx + 1}`,
            metadata: {},
            source: sourceLines
          };
        }

        const outputs: any[] = [];
        for (const out of cell.outputs || []) {
          for (const item of out.items || []) {
            if (item.mime === 'text/plain') {
              outputs.push({
                output_type: 'stream',
                name: 'stdout',
                text: new TextDecoder().decode(item.data).split('\n').map((l, i, a) => i < a.length - 1 ? l + '\n' : l)
              });
            } else if (item.mime.startsWith('image/')) {
              outputs.push({
                output_type: 'display_data',
                data: {
                  [item.mime]: Buffer.from(item.data).toString('base64'),
                  'text/plain': ['<Figure>']
                },
                metadata: out.metadata || {}
              });
            } else {
              outputs.push({
                output_type: 'display_data',
                data: {
                  [item.mime]: new TextDecoder().decode(item.data)
                },
                metadata: out.metadata || {}
              });
            }
          }
        }

        return {
          cell_type: 'code',
          execution_count: cell.executionSummary?.executionOrder ?? null,
          id: cell.metadata?.id || `cell-${idx + 1}`,
          metadata: {
            polyglot_notebook: { kernelName: cell.languageId }
          },
          outputs,
          source: sourceLines
        };
      });

      const ipynb = {
        cells,
        metadata: data.metadata?.rawMetadata || {
          kernelspec: { display_name: '.NET (C#)', language: 'C#', name: '.net-csharp' },
          language_info: { name: 'csharp' }
        },
        nbformat: data.metadata?.nbformat || 4,
        nbformat_minor: data.metadata?.nbformat_minor || 5
      };

      return new TextEncoder().encode(JSON.stringify(ipynb, null, 2));
    }

    // 2. Native FrySharp (.frynb / .csnb) format matching C# NotebookDocumentItem & NotebookCellItem
    const defaultLanguage = data.metadata?.kernel || 'csharp';
    const cells = data.cells.map((cell, idx) => {
      const isMarkdown = cell.kind === vscode.NotebookCellKind.Markup;
      let outputText = '';
      let imageBytes: string | null = null;
      let imageFormat: string | null = null;
      let htmlContent: string | null = null;

      for (const out of cell.outputs || []) {
        for (const item of out.items || []) {
          if (item.mime === 'text/plain') {
            outputText += new TextDecoder().decode(item.data);
          } else if (item.mime.startsWith('image/')) {
            imageFormat = item.mime.replace('image/', '').toUpperCase();
            imageBytes = Buffer.from(item.data).toString('base64');
          } else if (item.mime === 'text/html') {
            htmlContent = new TextDecoder().decode(item.data);
          }
        }
      }

      return {
        Id: cell.metadata?.id || `cell-${idx + 1}`,
        Type: isMarkdown ? 1 : 0,
        Language: isMarkdown ? undefined : (cell.languageId !== defaultLanguage ? cell.languageId : undefined),
        Source: cell.value,
        OutputText: outputText,
        ExecutionCount: cell.executionSummary?.executionOrder ?? null,
        IsExecuting: false,
        ExecutionTimeText: '',
        HasError: false,
        IsMarkdownPreviewMode: isMarkdown,
        IsInputCollapsed: false,
        IsOutputCollapsed: false,
        IsOutputScrolled: false,
        ImageBytes: imageBytes,
        ImageFormat: imageFormat,
        ImageWidth: null,
        ImageHeight: null,
        HtmlContent: htmlContent,
        TableSnapshot: null,
        InspectorSnapshot: null,
        HadInteractiveControl: false,
        HasImage: !!imageBytes,
        HasHtml: !!htmlContent,
        HasOutput: !!(outputText || imageBytes || htmlContent),
        IsCodeCell: !isMarkdown,
        IsMarkdownCell: isMarkdown
      };
    });

    const frynb = {
      SchemaVersion: data.metadata?.schemaVersion || '1.0',
      Type: 'notebook',
      Id: data.metadata?.id || 'notebook-1',
      Title: data.metadata?.title || 'Notebook',
      Description: data.metadata?.description || '',
      Category: data.metadata?.category || 'Interactive',
      Kernel: defaultLanguage,
      Cells: cells
    };

    return new TextEncoder().encode(JSON.stringify(frynb, null, 2));
  }

  private deserializeJupyter(ipynb: any): vscode.NotebookData {
    const defaultLanguage = ipynb.metadata?.language_info?.name || 'csharp';
    const cells: vscode.NotebookCellData[] = (ipynb.cells || []).map((cell: any, idx: number) => {
      const isMarkdown = cell.cell_type === 'markdown';
      const kind = isMarkdown ? vscode.NotebookCellKind.Markup : vscode.NotebookCellKind.Code;
      const source = Array.isArray(cell.source) ? cell.source.join('') : (cell.source || '');
      const cellLang = cell.metadata?.polyglot_notebook?.kernelName || cell.metadata?.dotnet_interactive?.language || defaultLanguage;
      const languageId = isMarkdown ? 'markdown' : cellLang;

      const cellData = new vscode.NotebookCellData(kind, source, languageId);
      cellData.metadata = { id: cell.id || `cell-${idx + 1}`, ...cell.metadata };

      if (cell.execution_count != null) {
        cellData.executionSummary = { executionOrder: cell.execution_count };
      }

      if (cell.outputs && Array.isArray(cell.outputs)) {
        const outputs: vscode.NotebookCellOutput[] = [];
        for (const out of cell.outputs) {
          if (out.output_type === 'stream') {
            const text = Array.isArray(out.text) ? out.text.join('') : (out.text || '');
            outputs.push(new vscode.NotebookCellOutput([
              vscode.NotebookCellOutputItem.text(text, 'text/plain')
            ]));
          } else if (out.output_type === 'error') {
            const traceback = Array.isArray(out.traceback) ? out.traceback.join('\n') : (out.traceback || out.evalue || '');
            outputs.push(new vscode.NotebookCellOutput([
              vscode.NotebookCellOutputItem.error({
                name: out.ename || 'Error',
                message: out.evalue || '',
                stack: traceback
              })
            ]));
          } else if (out.data && typeof out.data === 'object') {
            const items = Object.entries(out.data).map(([mime, val]) => {
              let bytes: Uint8Array;
              const str = Array.isArray(val) ? val.join('') : (typeof val === 'string' ? val : JSON.stringify(val));
              if (typeof str === 'string' && mime.startsWith('image/') && mime !== 'image/svg+xml') {
                bytes = Buffer.from(str, 'base64');
              } else {
                bytes = new TextEncoder().encode(str);
              }
              return new vscode.NotebookCellOutputItem(bytes, mime);
            });
            outputs.push(new vscode.NotebookCellOutput(items, out.metadata));
          }
        }
        if (outputs.length > 0) {
          cellData.outputs = outputs;
        }
      }

      return cellData;
    });

    const notebookData = new vscode.NotebookData(cells);
    notebookData.metadata = {
      isJupyter: true,
      rawMetadata: ipynb.metadata,
      nbformat: ipynb.nbformat || 4,
      nbformat_minor: ipynb.nbformat_minor || 5
    };
    return notebookData;
  }

  public static formatTableJsonToHtml(tableData: any): string {
    if (!tableData) return '';
    const cols = (tableData.columns || tableData.Columns || []).map((c: any, i: number) => ({
      Header: typeof c === 'string' ? c : (c.Header || c.header || `Col ${i + 1}`),
      IsNumeric: !!(tableData.numeric?.[i] || c.IsNumeric || c.isNumeric)
    }));
    const rows = (tableData.rows || tableData.Rows || []).map((r: any) => ({
      Cells: Array.isArray(r)
        ? r.map((cellVal: any) => ({ DisplayText: cellVal == null ? '' : String(cellVal), IsNull: cellVal == null }))
        : (r.Cells || r.cells || [])
    }));
    return FryNotebookSerializer.formatTableSnapshotToHtml({
      Title: tableData.title || tableData.Title,
      Columns: cols,
      Rows: rows
    });
  }

  public static formatTableSnapshotToHtml(table: any): string {
    const cols = table.Columns || table.columns;
    const rows = table.Rows || table.rows;
    if (!Array.isArray(cols) || !Array.isArray(rows)) return '';

    const uid = 'fry_tbl_' + Math.random().toString(36).slice(2, 9);
    const title = table.Title || table.title || table.Label || table.label || 'Data Table';
    const escape = (s: string) => String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    const rowCount = rows.length;
    const colCount = cols.length;

    const formatCell = (val: any, isNum: boolean): { html: string; css: string } => {
      if (val == null || val?.IsNull || val?.isNull) {
        return { html: '<span style="opacity:0.35;font-style:italic;">null</span>', css: '' };
      }
      const text = String(val?.DisplayText ?? val?.displayText ?? val);
      const lower = text.trim().toLowerCase();
      if (['online', 'active', 'healthy', 'optimal', 'passed', 'success', 'true', 'running', 'ready', 'connected'].includes(lower)) {
        return { html: `<span class="fry-badge fry-badge-green">${escape(text)}</span>`, css: '' };
      }
      if (['warning', 'degraded', 'under load', 'pending', 'slow', 'paused', 'retrying'].includes(lower)) {
        return { html: `<span class="fry-badge fry-badge-amber">${escape(text)}</span>`, css: '' };
      }
      if (['error', 'failed', 'critical', 'offline', 'down', 'false', 'stopped'].includes(lower)) {
        return { html: `<span class="fry-badge fry-badge-red">${escape(text)}</span>`, css: '' };
      }
      if (isNum) {
        return { html: escape(text), css: 'fry-num' };
      }
      return { html: escape(text), css: '' };
    };

    let headerHtml = '';
    cols.forEach((col: any, cIdx: number) => {
      const isNum = !!(col.IsNumeric ?? col.isNumeric);
      const align = isNum ? 'right' : 'left';
      const colHeader = escape(col.Header || col.header || `Col ${cIdx + 1}`);
      headerHtml += `<th class="sortable" onclick="fryTblSort('${uid}', ${cIdx}, ${isNum})" style="text-align:${align};cursor:pointer;" title="Sort by ${colHeader}">${colHeader} <span class="fry-sort-ind" style="opacity:0.35;font-size:10px;margin-left:3px;">↕</span></th>`;
    });

    let rowsHtml = '';
    rows.forEach((row: any, rIdx: number) => {
      const bg = (rIdx % 2 === 0) ? 'background:rgba(255,255,255,0.015);' : 'background:transparent;';
      const cells = row.Cells || row.cells || [];
      let cellsHtml = '';
      let propCardsHtml = '';

      cols.forEach((col: any, cIdx: number) => {
        const isNum = !!(col.IsNumeric ?? col.isNumeric);
        const align = isNum ? 'right' : 'left';
        const cell = cells[cIdx];
        const { html, css } = formatCell(cell, isNum);
        cellsHtml += `<td style="text-align:${align};" class="${css}">${html}</td>`;
        propCardsHtml += `<div class="fry-prop-card"><div class="fry-prop-key">${escape(col.Header || col.header || '')}</div><div class="fry-prop-val">${html}</div></div>`;
      });

      rowsHtml += `<tr class="fry-data-row" data-row="${rIdx}" style="${bg}">
        <td style="text-align:center;padding:6px 4px;">
          <button type="button" class="fry-expander-btn" onclick="fryTblToggleRow('${uid}', ${rIdx})" title="Toggle row details">▶</button>
        </td>
        ${cellsHtml}
      </tr>
      <tr class="fry-detail-row" id="${uid}_detail_${rIdx}" style="display:none;">
        <td colspan="${colCount + 1}" style="padding:10px 14px;border-bottom:1px solid var(--vscode-widget-border,#30363d);">
          <div style="background:var(--vscode-editor-background,#0d1117);border:1px solid var(--vscode-widget-border,#30363d);border-radius:6px;padding:10px 14px;">
            <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:8px;">
              <div style="font-size:11.5px;font-weight:600;color:#58a6ff;">🔍 Row #${rIdx + 1} Inspector</div>
              <div style="font-size:10.5px;color:#8b949e;">${colCount} properties</div>
            </div>
            <div class="fry-prop-grid">${propCardsHtml}</div>
          </div>
        </td>
      </tr>`;
    });

    return `<div class="fry-tbl-container" id="${uid}" style="font-family:system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:var(--vscode-editor-background,#0d1117);border:1px solid var(--vscode-widget-border,#30363d);border-radius:8px;margin:10px 0;max-width:960px;box-shadow:0 4px 16px rgba(0,0,0,0.35);overflow:hidden;">
      <style>
        #${uid} .fry-tbl-header { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 8px 12px; background: var(--vscode-sideBar-background, #161b22); border-bottom: 1px solid var(--vscode-widget-border, #30363d); }
        #${uid} .fry-tbl-toggle-btn { display: flex; align-items: center; gap: 8px; background: transparent; border: none; color: var(--vscode-foreground, #c9d1d9); cursor: pointer; padding: 0; font-size: 13px; font-weight: 600; text-align: left; }
        #${uid} .fry-tbl-pill { font-size: 11px; font-weight: 500; color: var(--vscode-descriptionForeground, #8b949e); background: var(--vscode-badge-background, #21262d); border: 1px solid var(--vscode-widget-border, #30363d); padding: 2px 8px; border-radius: 12px; }
        #${uid} .fry-tbl-actions { display: flex; align-items: center; gap: 8px; }
        #${uid} .fry-tbl-search-wrap { position: relative; display: flex; align-items: center; }
        #${uid} .fry-tbl-search-input { background: var(--vscode-input-background, #0d1117); border: 1px solid var(--vscode-input-border, #30363d); color: var(--vscode-input-foreground, #c9d1d9); padding: 3px 8px 3px 24px; border-radius: 4px; font-size: 11.5px; width: 140px; transition: border-color 0.2s, width 0.2s; }
        #${uid} .fry-tbl-search-input:focus { outline: none; border-color: var(--vscode-focusBorder, #58a6ff); width: 180px; }
        #${uid} .fry-tbl-search-icon { position: absolute; left: 7px; font-size: 11px; opacity: 0.6; pointer-events: none; }
        #${uid} .fry-tbl-btn { background: var(--vscode-button-secondaryBackground, #21262d); color: var(--vscode-button-secondaryForeground, #c9d1d9); border: 1px solid var(--vscode-widget-border, #30363d); border-radius: 4px; padding: 3px 8px; font-size: 11px; font-weight: 500; cursor: pointer; }
        #${uid} .fry-tbl-btn:hover { background: var(--vscode-button-secondaryHoverBackground, #30363d); }
        #${uid} .fry-tbl-scroll { max-height: 420px; overflow-y: auto; overflow-x: auto; }
        #${uid} table.fry-tbl-grid { width: 100%; border-collapse: collapse; text-align: left; font-size: 12px; }
        #${uid} thead th { position: sticky; top: 0; z-index: 5; background: var(--vscode-sideBar-background, #161b22); padding: 8px 12px; border-bottom: 2px solid var(--vscode-widget-border, #30363d); font-weight: 600; color: var(--vscode-foreground, #c9d1d9); user-select: none; }
        #${uid} tbody tr.fry-data-row { border-bottom: 1px solid rgba(255, 255, 255, 0.06); }
        #${uid} tbody tr.fry-data-row:hover { background: var(--vscode-list-hoverBackground, rgba(56, 139, 253, 0.08)) !important; }
        #${uid} td { padding: 7px 12px; vertical-align: middle; }
        #${uid} .fry-num { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; text-align: right; color: #79c0ff; }
        #${uid} .fry-badge { display: inline-block; padding: 2px 7px; border-radius: 12px; font-size: 11px; font-weight: 500; }
        #${uid} .fry-badge-green { background: rgba(35, 134, 54, 0.2); color: #3fb950; border: 1px solid rgba(63, 185, 80, 0.4); }
        #${uid} .fry-badge-amber { background: rgba(187, 128, 9, 0.2); color: #d29922; border: 1px solid rgba(210, 153, 34, 0.4); }
        #${uid} .fry-badge-red { background: rgba(218, 54, 51, 0.2); color: #f85149; border: 1px solid rgba(248, 81, 73, 0.4); }
        #${uid} .fry-expander-btn { background: transparent; border: none; cursor: pointer; color: var(--vscode-textLink-foreground, #58a6ff); font-size: 10px; padding: 3px 5px; border-radius: 4px; }
        #${uid} .fry-prop-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(210px, 1fr)); gap: 8px; margin-top: 6px; }
        #${uid} .fry-prop-card { background: rgba(255,255,255,0.03); border: 1px solid rgba(255,255,255,0.08); border-radius: 4px; padding: 6px 10px; }
        #${uid} .fry-prop-key { font-size: 10px; font-weight: 600; color: #8b949e; text-transform: uppercase; letter-spacing: 0.5px; }
        #${uid} .fry-prop-val { font-size: 12px; color: #c9d1d9; margin-top: 2px; word-break: break-word; }
        #${uid} .fry-tbl-footer { display: flex; align-items: center; justify-content: space-between; padding: 6px 12px; font-size: 11px; color: var(--vscode-descriptionForeground, #8b949e); background: var(--vscode-sideBar-background, #161b22); border-top: 1px solid var(--vscode-widget-border, #30363d); }
      </style>
      <div class="fry-tbl-header">
        <button type="button" class="fry-tbl-toggle-btn" onclick="fryTblToggle('${uid}')">
          <span class="fry-tbl-chevron" style="color:#58a6ff;">▼</span>
          <span>📊 ${escape(title)}</span>
          <span class="fry-tbl-pill">${rowCount} rows × ${colCount} cols</span>
        </button>
        <div class="fry-tbl-actions">
          <div class="fry-tbl-search-wrap">
            <span class="fry-tbl-search-icon">🔍</span>
            <input type="text" class="fry-tbl-search-input" placeholder="Filter rows..." oninput="fryTblFilter('${uid}', this.value)" />
          </div>
          <button type="button" class="fry-tbl-btn" onclick="fryTblCopy('${uid}', 'csv')">CSV</button>
          <button type="button" class="fry-tbl-btn" onclick="fryTblCopy('${uid}', 'json')">JSON</button>
        </div>
      </div>
      <div class="fry-tbl-scroll">
        <table class="fry-tbl-grid">
          <thead><tr><th style="width:36px;text-align:center;padding:8px 4px;"></th>${headerHtml}</tr></thead>
          <tbody>${rowsHtml}</tbody>
        </table>
      </div>
      <div class="fry-tbl-footer">
        <span id="${uid}_counter">Showing ${rowCount} of ${rowCount} rows</span>
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
            if (cnt) cnt.textContent = q ? ('Showing ' + count + ' of ' + rows.length + ' rows (filtered)') : ('Showing ' + rows.length + ' of ' + rows.length + ' rows');
          };
          window.fryTblSort = function(id, colIdx, isNum) {
            var el = document.getElementById(id);
            if (!el) return;
            var tbody = el.querySelector('tbody');
            var rows = Array.from(el.querySelectorAll('tbody tr.fry-data-row'));
            var th = el.querySelectorAll('thead th')[colIdx + 1];
            var dir = th.getAttribute('data-sort-dir') === 'asc' ? 'desc' : 'asc';
            el.querySelectorAll('thead th').forEach(function(h) { h.removeAttribute('data-sort-dir'); var s = h.querySelector('.fry-sort-ind'); if (s) s.textContent = '↕'; });
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
            var data = rows.map(function(r) { return Array.from(r.children).slice(1).map(function(c) { return c.innerText.trim(); }); });
            var payload = '';
            if (format === 'json') {
              payload = JSON.stringify(data.map(function(r) { var o = {}; headers.forEach(function(h, idx) { o[h] = r[idx]; }); return o; }), null, 2);
            } else {
              payload = headers.join(',') + '\n' + data.map(function(r) { return r.map(function(val) { return '\"' + val.replace(/\"/g, '\"\"') + '\"'; }).join(','); }).join('\n');
            }
            navigator.clipboard.writeText(payload).then(function() {
              var btn = el.querySelector('.fry-tbl-btn[onclick*="' + format + '"]');
              if (btn) { var prev = btn.textContent; btn.textContent = '✓ Copied!'; setTimeout(function() { btn.textContent = prev; }, 1500); }
            });
          };
        })();
      </script>
    </div>`;
  }

  private formatVisualToHtml(visual: any): string {
    const data = visual?.Data || visual?.data;
    if (!data) return '';
    const mime = visual?.MimeType || visual?.mimeType || '';
    const escape = (s: string) => String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    const title = escape(data.title || data.Title || (mime.includes('plot3d') ? '3D Plot' : 'Chart'));
    const palette = ['#58a6ff', '#3fb950', '#d29922', '#f85149', '#bc8cff', '#2dd4bf'];

    if (mime.includes('chart') && Array.isArray(data.series)) {
      const isBar = data.kind === 'bar';
      const isPie = data.kind === 'pie' || data.kind === 'donut';
      if (isPie) {
        const s = data.series[0] || {};
        const yVals = (s.y || []).filter((v: any) => typeof v === 'number' && v > 0);
        const total = yVals.reduce((a: number, b: number) => a + b, 0) || 1;
        let angle = -Math.PI / 2;
        let paths = '';
        let leg = '';
        (s.y || []).forEach((v: number, idx: number) => {
          if (typeof v !== 'number' || v <= 0) return;
          const slice = (v / total) * 2 * Math.PI;
          const next = angle + slice;
          const col = palette[idx % palette.length];
          const x1 = 260 + 110 * Math.cos(angle);
          const y1 = 150 + 110 * Math.sin(angle);
          const x2 = 260 + 110 * Math.cos(next);
          const y2 = 150 + 110 * Math.sin(next);
          const lbl = s.labels?.[idx] || `Item ${idx + 1}`;
          const pct = ((v / total) * 100).toFixed(1);
          const large = slice > Math.PI ? 1 : 0;
          paths += `<path class="fry-pie-slice" d="M 260 150 L ${x1.toFixed(1)} ${y1.toFixed(1)} A 110 110 0 ${large} 1 ${x2.toFixed(1)} ${y2.toFixed(1)} Z" fill="${col}" stroke="#161b22" stroke-width="2" style="transition:transform 0.2s cubic-bezier(0.4,0,0.2,1);transform-origin:260px 150px;cursor:pointer;"><title>${escape(lbl)}: ${v} (${pct}%)</title></path>`;
          leg += `<div style="display:flex;align-items:center;gap:6px;margin:3px 0;font-size:11px;color:#c9d1d9;"><span style="width:10px;height:10px;background:${col};border-radius:2px;"></span><span>${escape(lbl)} (${pct}%)</span></div>`;
          angle = next;
        });
        const uid = 'fry_pie_' + Math.random().toString(36).slice(2, 9);
        return `<div class="fry-visual-card" id="${uid}" style="font-family:system-ui,sans-serif;background:#0d1117;border:1px solid #30363d;border-radius:10px;padding:14px;margin:8px 0;max-width:840px;">
          <style>
            #${uid} path.fry-pie-slice:hover { transform: scale(1.05) !important; filter: drop-shadow(0 2px 8px rgba(0,0,0,0.5)); }
          </style>
          <div style="font-weight:600;font-size:13.5px;color:#58a6ff;margin-bottom:8px;">📊 ${title}</div>
          <div style="display:flex;align-items:center;gap:20px;">
            <svg viewBox="0 0 520 300" style="width:340px;height:auto;background:#161b22;border-radius:6px;">${paths}</svg>
            <div style="flex:1;">${leg}</div>
          </div>
        </div>`;
      }

      let minY = Infinity, maxY = -Infinity;
      let maxLen = 0;
      data.series.forEach((s: any) => {
        const y = s.y || [];
        maxLen = Math.max(maxLen, y.length);
        y.forEach((v: any) => {
          if (typeof v === 'number' && Number.isFinite(v)) {
            minY = Math.min(minY, v);
            maxY = Math.max(maxY, v);
          }
        });
      });
      if (minY > maxY) { minY = 0; maxY = 100; }
      if (Math.abs(maxY - minY) < 1e-9) { maxY += 10; minY -= 10; }
      if (minY > 0 && isBar) minY = 0;

      let seriesSvg = '';
      if (isBar && maxLen > 0) {
        const gw = 640 / maxLen;
        const bw = Math.max(4, (gw * 0.7) / data.series.length);
        data.series.forEach((s: any, sIdx: number) => {
          const col = s.color || palette[sIdx % palette.length];
          (s.y || []).forEach((y: any, pIdx: number) => {
            if (typeof y !== 'number') return;
            const bx = 60 + pIdx * gw + (gw * 0.15) + sIdx * bw;
            const zy = 30 + ((maxY - 0) / (maxY - minY)) * 260;
            const by = 30 + ((maxY - y) / (maxY - minY)) * 260;
            const top = Math.min(zy, by);
            const h = Math.abs(zy - by);
            seriesSvg += `<rect x="${bx.toFixed(1)}" y="${top.toFixed(1)}" width="${(bw * 0.9).toFixed(1)}" height="${h.toFixed(1)}" fill="${col}" rx="3"><title>${escape(s.name || '')}: ${y}</title></rect>`;
          });
        });
      } else if (maxLen > 1) {
        data.series.forEach((s: any, sIdx: number) => {
          const col = s.color || palette[sIdx % palette.length];
          const y = s.y || [];
          const pts: string[] = [];
          y.forEach((val: any, pIdx: number) => {
            if (typeof val !== 'number') return;
            const px = 60 + (pIdx / (maxLen - 1)) * 640;
            const py = 30 + ((maxY - val) / (maxY - minY)) * 260;
            pts.push(`${px.toFixed(1)},${py.toFixed(1)}`);
          });
          if (pts.length > 1) {
            seriesSvg += `<polyline fill="none" stroke="${col}" stroke-width="2.5" stroke-linejoin="round" stroke-linecap="round" points="${pts.join(' ')}"/>`;
          }
        });
      }

      return `<div class="fry-visual-card" style="font-family:system-ui,sans-serif;background:#0d1117;border:1px solid #30363d;border-radius:10px;padding:14px;margin:8px 0;max-width:840px;">
        <div style="font-weight:600;font-size:13.5px;color:#58a6ff;margin-bottom:8px;">📊 ${title}</div>
        <svg viewBox="0 0 740 330" width="100%" height="auto" style="display:block;background:#161b22;border-radius:6px;">
          <line x1="60" y1="290" x2="700" y2="290" stroke="#30363d" stroke-width="1.5"/>
          ${seriesSvg}
        </svg>
      </div>`;
    }

    if (mime.includes('plot3d') && data.surface?.z) {
      const zGrid = data.surface.z;
      const rows = zGrid.length;
      const cols = zGrid[0]?.length || 0;
      let minZ = Infinity, maxZ = -Infinity;
      zGrid.forEach((row: any[]) => row.forEach(v => {
        if (typeof v === 'number') { minZ = Math.min(minZ, v); maxZ = Math.max(maxZ, v); }
      }));
      if (minZ >= maxZ) { minZ = -1; maxZ = 1; }

      const uid = 'fry_p3d_' + Math.random().toString(36).slice(2, 9);
      const gridData = JSON.stringify(zGrid.map((row: any[], r: number) =>
        row.map((zv: any, c: number) => {
          const u = (c / (cols - 1)) * 2.0 - 1.0;
          const v = (r / (rows - 1)) * 2.0 - 1.0;
          const zNorm = ((typeof zv === 'number' ? zv : 0) - minZ) / (maxZ - minZ);
          return [Number(u.toFixed(3)), Number(v.toFixed(3)), Number(zNorm.toFixed(3))];
        })
      ));

      return `<div class="fry-visual-card" id="${uid}" style="font-family:system-ui,sans-serif;background:#0d1117;border:1px solid #30363d;border-radius:10px;padding:14px;margin:8px 0;max-width:840px;position:relative;user-select:none;">
        <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:8px;">
          <div style="font-weight:600;font-size:13.5px;color:#58a6ff;">🏔️ ${title}</div>
          <div style="display:flex;align-items:center;gap:12px;font-size:11px;color:#8b949e;">
            <div style="display:flex;align-items:center;gap:6px;">
              <span>🔄 Rotate</span>
              <input type="range" class="fry-3d-az-slider" min="0" max="360" value="30" style="width:80px;height:4px;accent-color:#58a6ff;cursor:pointer;" oninput="window.fryRotate3D && window.fryRotate3D('${uid}', this.value, null)" />
            </div>
            <div style="display:flex;align-items:center;gap:6px;">
              <span>📐 Tilt</span>
              <input type="range" class="fry-3d-el-slider" min="15" max="80" value="35" style="width:70px;height:4px;accent-color:#58a6ff;cursor:pointer;" oninput="window.fryRotate3D && window.fryRotate3D('${uid}', null, this.value)" />
            </div>
          </div>
        </div>
        <svg class="fry-3d-canvas" viewBox="0 0 740 360" width="100%" height="auto" style="display:block;background:#161b22;border-radius:6px;cursor:grab;">
          <g class="fry-3d-mesh"></g>
        </svg>
        <script>
          (function() {
            const root = document.getElementById('${uid}');
            if (!root) return;
            const grid = ${gridData};
            const rows = grid.length;
            const cols = grid[0].length;
            const svgMesh = root.querySelector('.fry-3d-mesh');
            const canvas = root.querySelector('.fry-3d-canvas');
            let azDeg = 30, elDeg = 35;
            const cx = 370, cy = 190;
            const scaleX = 180 * 0.7, scaleY = 90 * 0.7, scaleZ = 120 * 0.6;

            function colorMap(t) {
              const hue = 240.0 - (Math.max(0, Math.min(1, t)) * 195.0);
              return 'hsl(' + Math.round(hue) + ', 85%, 55%)';
            }

            function render(az, el) {
              const azRad = az * Math.PI / 180.0;
              const elRad = el * Math.PI / 180.0;
              const cosAz = Math.cos(azRad), sinAz = Math.sin(azRad), sinEl = Math.sin(elRad);

              function project(u, v, z) {
                const zScaled = (z * 1.8) - 0.9;
                const xRot = (u * cosAz - v * sinAz);
                const yRot = (u * sinAz + v * cosAz);
                const px = cx + xRot * scaleX;
                const py = cy + (yRot * sinEl * scaleY) - (zScaled * scaleZ);
                return [px, py];
              }

              let svgContent = '';
              for (let r = 0; r < rows; r++) {
                let path = '', rowZSum = 0;
                for (let c = 0; c < cols; c++) {
                  const pt = grid[r][c];
                  rowZSum += pt[2];
                  const p = project(pt[0], pt[1], pt[2]);
                  path += (c === 0 ? 'M ' : 'L ') + p[0].toFixed(1) + ' ' + p[1].toFixed(1) + ' ';
                }
                const col = colorMap(rowZSum / cols);
                svgContent += '<path d="' + path + '" fill="none" stroke="' + col + '" stroke-width="1.4" opacity="0.85" />';
              }

              const colStep = Math.max(1, Math.floor(cols / 25));
              for (let c = 0; c < cols; c += colStep) {
                let path = '';
                for (let r = 0; r < rows; r++) {
                  const pt = grid[r][c];
                  const p = project(pt[0], pt[1], pt[2]);
                  path += (r === 0 ? 'M ' : 'L ') + p[0].toFixed(1) + ' ' + p[1].toFixed(1) + ' ';
                }
                svgContent += '<path d="' + path + '" fill="none" stroke="#58a6ff" stroke-width="0.8" opacity="0.35" />';
              }

              svgMesh.innerHTML = svgContent;
            }

            render(azDeg, elDeg);

            window.fryRotate3D = function(id, az, el) {
              if (id !== '${uid}') return;
              if (az !== null) azDeg = parseFloat(az);
              if (el !== null) elDeg = parseFloat(el);
              render(azDeg, elDeg);
            };

            let isDragging = false, startX = 0, startY = 0;
            canvas.addEventListener('mousedown', function(e) {
              isDragging = true; startX = e.clientX; startY = e.clientY;
              canvas.style.cursor = 'grabbing';
            });
            window.addEventListener('mousemove', function(e) {
              if (!isDragging) return;
              const dx = e.clientX - startX;
              const dy = e.clientY - startY;
              startX = e.clientX; startY = e.clientY;
              azDeg = (azDeg + dx * 0.6 + 360) % 360;
              elDeg = Math.max(15, Math.min(80, elDeg - dy * 0.4));
              const azS = root.querySelector('.fry-3d-az-slider');
              if (azS) azS.value = Math.round(azDeg);
              const elS = root.querySelector('.fry-3d-el-slider');
              if (elS) elS.value = Math.round(elDeg);
              render(azDeg, elDeg);
            });
            window.addEventListener('mouseup', function() {
              if (isDragging) { isDragging = false; canvas.style.cursor = 'grab'; }
            });
          })();
        </script>
      </div>`;
    }

    return '';
  }
}

