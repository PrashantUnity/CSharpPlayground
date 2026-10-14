import { ensureStyles, el } from './dom';
import { renderHtml } from './html';
import { renderInspector } from './inspector';
import { InspectorNode } from './inspectorModel';
import { INSPECTOR_MIME, TABLE_MIME } from './chooseMime';
import { renderTable } from './table';
import { TableData } from './tableModel';
import { CELL_OUTPUT_MIME, renderTabs } from './tabs';
import { PackedCell } from '../shared/outputTabs';

export { renderBundle } from './render';

// What VS Code gives a notebook renderer (the parts used: types/vscode-notebook-renderer is not needed for these).
interface OutputItem {
  id: string;
  mime: string;
  text(): string;
  json(): unknown;
}

interface RendererContext {
  postMessage?(message: unknown): void;
}

/**
 * The FrySharp output renderer: the Object Explorer and the table as interactive pieces of the notebook, and the engine's
 * HTML (charts, visuals) with its libraries and the editor's theme. It tells the extension each time it has drawn something,
 * so a test (or a bug report) can see that VS Code really ran it.
 */
export function activate(context?: RendererContext) {
  return {
    async renderOutputItem(item: OutputItem, element: HTMLElement): Promise<void> {
      ensureStyles();
      try {
        let chartDrawn: boolean | undefined;
        let tabs: { tabs: string[]; selected: string } | undefined;
        switch (item.mime) {
          case CELL_OUTPUT_MIME: {
            const drawn = await renderTabs(item.json() as PackedCell, element);
            chartDrawn = drawn.chartDrawn;
            tabs = drawn;
            break;
          }
          case TABLE_MIME:
            renderTable(item.json() as TableData, element);
            break;
          case INSPECTOR_MIME:
            renderInspector(item.json() as InspectorNode, element);
            break;
          default:
            chartDrawn = (await renderHtml(item.text(), element)).chartDrawn;
        }
        context?.postMessage?.({ type: 'rendered', mime: item.mime, id: item.id, chartDrawn, tabs: tabs?.tabs, selected: tabs?.selected });
      } catch (error) {
        element.replaceChildren(el('div', 'fry fry-error', `FrySharp could not draw this output: ${error instanceof Error ? error.message : String(error)}`));
        context?.postMessage?.({ type: 'error', mime: item.mime, id: item.id, message: error instanceof Error ? error.message : String(error) });
      }
    }
  };
}
