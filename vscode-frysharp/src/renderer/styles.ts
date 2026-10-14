// The look of everything the renderer draws: built from the editor's own colours (--vscode-*) so it follows the theme, light,
// dark or high contrast; the fallbacks are Dark+ for where there are none (a page opened outside VS Code).

export const STYLE_ID = 'fry-renderer-styles';

export const CSS = `
.fry { font-family: var(--vscode-font-family, system-ui, sans-serif); font-size: var(--vscode-font-size, 13px); color: var(--vscode-foreground, #cccccc); line-height: 1.45; }
.fry *, .fry *::before, .fry *::after { box-sizing: border-box; }
.fry-mono { font-family: var(--vscode-editor-font-family, Menlo, Consolas, monospace); font-size: var(--vscode-editor-font-size, 13px); }
.fry-card { margin: 6px 0; border: 1px solid var(--vscode-widget-border, var(--vscode-panel-border, #454545)); border-radius: 6px; background: var(--vscode-editor-background, #1e1e1e); overflow: hidden; }
.fry-head { display: flex; align-items: center; gap: 8px; padding: 6px 10px; background: var(--vscode-sideBar-background, var(--vscode-editorWidget-background, #252526)); border-bottom: 1px solid var(--vscode-widget-border, var(--vscode-panel-border, #454545)); }
.fry-head .fry-title { font-weight: 600; flex: none; }
.fry-head .fry-spacer { flex: 1; }
.fry-pill { font-size: 11px; padding: 1px 8px; border-radius: 10px; background: var(--vscode-badge-background, #4d4d4d); color: var(--vscode-badge-foreground, #ffffff); white-space: nowrap; }
.fry-button { font: inherit; font-size: 12px; padding: 2px 8px; border-radius: 3px; cursor: pointer; border: 1px solid var(--vscode-button-border, transparent); background: var(--vscode-button-secondaryBackground, #3a3d41); color: var(--vscode-button-secondaryForeground, #cccccc); }
.fry-button:hover { background: var(--vscode-button-secondaryHoverBackground, #45494e); }
.fry-button:focus-visible, .fry-toggle:focus-visible, .fry-sortable:focus-visible, .fry-filter:focus-visible { outline: 1px solid var(--vscode-focusBorder, #007fd4); outline-offset: -1px; }
.fry-filter { font: inherit; font-size: 12px; width: 150px; padding: 2px 8px; border-radius: 3px; border: 1px solid var(--vscode-input-border, var(--vscode-widget-border, #454545)); background: var(--vscode-input-background, #3c3c3c); color: var(--vscode-input-foreground, #cccccc); }
.fry-foot { padding: 4px 10px; font-size: 11px; color: var(--vscode-descriptionForeground, #9d9d9d); border-top: 1px solid var(--vscode-widget-border, var(--vscode-panel-border, #454545)); background: var(--vscode-sideBar-background, #252526); }
.fry-error { margin: 6px 0; padding: 8px 10px; white-space: pre-wrap; color: var(--vscode-errorForeground, #f48771); border: 1px solid var(--vscode-inputValidation-errorBorder, #be1100); border-radius: 4px; }

/* values, by kind */
.fry-v-string { color: var(--vscode-debugTokenExpression-string, #ce9178); }
.fry-v-number { color: var(--vscode-debugTokenExpression-number, #b5cea8); }
.fry-v-boolean { color: var(--vscode-debugTokenExpression-boolean, #569cd6); }
.fry-v-list { color: var(--vscode-symbolIcon-classForeground, #4ec9b0); }
.fry-v-null { color: var(--vscode-descriptionForeground, #9d9d9d); font-style: italic; }
.fry-v-plain { color: var(--vscode-foreground, #cccccc); }

/* the Object Explorer */
.fry-tree { padding: 6px 10px 8px; }
.fry-node-head, .fry-row { display: flex; align-items: baseline; gap: 4px; min-height: 22px; }
.fry-children { margin-left: 11px; padding-left: 12px; border-left: 1px solid var(--vscode-tree-indentGuidesStroke, rgba(128,128,128,.35)); }
.fry-toggle { width: 18px; flex: none; padding: 0; border: 0; background: none; color: var(--vscode-foreground, #cccccc); cursor: pointer; font-size: 10px; line-height: 18px; text-align: center; }
.fry-toggle[disabled] { cursor: default; opacity: .35; }
.fry-spacer-toggle { width: 18px; flex: none; }
.fry-chip { display: inline-flex; align-items: center; gap: 5px; padding: 0 8px; border-radius: 10px; background: var(--vscode-badge-background, #4d4d4d); color: var(--vscode-badge-foreground, #ffffff); font-size: 12px; cursor: default; }
.fry-chip.fry-circular { opacity: .75; }
.fry-name { color: var(--vscode-debugTokenExpression-name, #c586c0); white-space: nowrap; }
.fry-colon { opacity: .7; }
.fry-value { white-space: pre-wrap; word-break: break-word; min-width: 0; }

/* the table */
.fry-scroll { max-height: 420px; overflow: auto; }
.fry-table table { width: 100%; border-collapse: collapse; font-size: 12px; }
.fry-table th { position: sticky; top: 0; z-index: 1; text-align: left; font-weight: 600; padding: 6px 12px; white-space: nowrap; user-select: none; background: var(--vscode-sideBar-background, #252526); border-bottom: 2px solid var(--vscode-widget-border, var(--vscode-panel-border, #454545)); }
.fry-table th.fry-sortable { cursor: pointer; }
.fry-table th.fry-sortable:hover { background: var(--vscode-list-hoverBackground, #2a2d2e); }
.fry-table th .fry-arrow { display: inline-block; width: 12px; margin-left: 4px; font-size: 10px; opacity: .5; }
.fry-table th[aria-sort] .fry-arrow { opacity: 1; color: var(--vscode-textLink-foreground, #3794ff); }
.fry-table td { padding: 5px 12px; vertical-align: middle; border-bottom: 1px solid var(--vscode-widget-border, rgba(128,128,128,.2)); }
.fry-table td.fry-num, .fry-table th.fry-num { text-align: right; font-variant-numeric: tabular-nums; }
.fry-table tbody tr.fry-data:hover { background: var(--vscode-list-hoverBackground, #2a2d2e); }
.fry-table tbody tr.fry-data:nth-child(4n+1) { background: var(--vscode-notebook-cellEditorBackground, rgba(128,128,128,.05)); }
.fry-table .fry-expander { width: 28px; padding: 5px 4px; text-align: center; }
.fry-table .fry-detail td { padding: 8px 14px 12px 40px; background: var(--vscode-editor-background, #1e1e1e); }
.fry-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(190px, 1fr)); gap: 6px; }
.fry-grid > div { padding: 4px 8px; border: 1px solid var(--vscode-widget-border, rgba(128,128,128,.3)); border-radius: 4px; }
.fry-grid .fry-key { font-size: 10px; text-transform: uppercase; letter-spacing: .4px; color: var(--vscode-descriptionForeground, #9d9d9d); }
.fry-empty { padding: 12px; color: var(--vscode-descriptionForeground, #9d9d9d); }

/* a cell's output as tabs */
.fry-tabs { margin: 6px 0; }
.fry-tabbar { padding: 4px 6px 4px 6px; flex-wrap: wrap; row-gap: 4px; }
.fry-tabstrip { display: flex; gap: 4px; flex-wrap: wrap; }
.fry-tab { display: inline-flex; align-items: center; gap: 6px; font: inherit; font-size: 12px; padding: 3px 10px; border-radius: 4px; cursor: pointer; border: 1px solid transparent; background: transparent; color: var(--vscode-foreground, #cccccc); }
.fry-tab:hover { background: var(--vscode-toolbar-hoverBackground, var(--vscode-list-hoverBackground, #2a2d2e)); }
.fry-tab:focus-visible { outline: 1px solid var(--vscode-focusBorder, #007fd4); outline-offset: -1px; }
.fry-tab-selected { border-color: var(--vscode-focusBorder, #007fd4); background: var(--vscode-list-activeSelectionBackground, rgba(0, 127, 212, .18)); color: var(--vscode-list-activeSelectionForeground, var(--vscode-foreground, #ffffff)); }
.fry-tab-icon { font-family: var(--vscode-editor-font-family, Menlo, Consolas, monospace); font-size: 11px; opacity: .85; }
.fry-tab-count { background: var(--vscode-badge-background, #4d4d4d); }
.fry-tab-selected .fry-tab-count { background: var(--vscode-focusBorder, #007fd4); color: #ffffff; }
.fry-tabactions { display: flex; gap: 4px; align-items: center; }
.fry-fold { min-width: 24px; }
.fry-tabbody { padding: 0; }
.fry-tabbody .fry-card { margin: 0; border: 0; border-radius: 0; }
.fry-tabbody .fry-slot + .fry-slot { border-top: 1px solid var(--vscode-widget-border, var(--vscode-panel-border, #454545)); }
.fry-tabbody .fry-frame { border: 0; border-radius: 0; }

/* printed text */
.fry-console { max-height: 420px; overflow: auto; padding: 6px 10px; }
.fry-pre { margin: 0; white-space: pre-wrap; word-break: break-word; font: inherit; }
.fry-note { margin-bottom: 4px; font-size: 11px; color: var(--vscode-descriptionForeground, #9d9d9d); }
.fry-more { margin: 0 0 6px; }
.fry-markdown { padding: 6px 12px; }
.fry-markdown h1, .fry-markdown h2, .fry-markdown h3 { margin: 10px 0 6px; font-weight: 600; }
.fry-markdown p, .fry-markdown ul, .fry-markdown ol, .fry-markdown blockquote { margin: 6px 0; }
.fry-markdown blockquote { padding-left: 10px; border-left: 3px solid var(--vscode-textBlockQuote-border, rgba(128,128,128,.5)); color: var(--vscode-descriptionForeground, #9d9d9d); }
.fry-markdown code { padding: 0 4px; border-radius: 3px; background: var(--vscode-textCodeBlock-background, rgba(128,128,128,.2)); }
.fry-markdown a { color: var(--vscode-textLink-foreground, #3794ff); }
.fry-markdown hr { border: 0; border-top: 1px solid var(--vscode-widget-border, rgba(128,128,128,.4)); }

/* an engine page (a chart) in a frame of its own */
.fry-frame { display: block; width: 100%; height: 360px; min-height: 160px; border: 1px solid var(--vscode-widget-border, var(--vscode-panel-border, #454545)); border-radius: 6px; resize: vertical; background: transparent; }
`;
