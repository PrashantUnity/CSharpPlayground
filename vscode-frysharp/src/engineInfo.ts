import type { EngineInfo, ToolchainReport } from './sidecar/protocol';

// "FrySharp: Show Engine Info": what the engine runs on and which tool each language found, as a page a person can read and paste into a
// bug report. Pure text, so it is tested without VS Code.

export interface EngineFacts {
  extensionVersion: string;
  vscodeVersion: string;
  /** Which sidecar and which dotnet started the engine. */
  launch?: { description?: string; command: string };
  logFolder?: string;
  /** The settings that change what the engine does, with what they are set to. */
  settings: Array<[name: string, value: string]>;
}

const cell = (text: string | undefined): string => (text ?? '').replace(/\|/g, '\\|').replace(/\r?\n/g, ' ');

function row(report: ToolchainReport): string {
  if (report.found) return `| ${cell(report.displayName)} | ✓ found | ${cell(report.name ?? report.version)} | ${cell(report.path) || 'inside the engine'} | ${cell(report.source)} |`;
  return `| ${cell(report.displayName)} | ✗ **not found** | | | |`;
}

/** The report as Markdown. */
export function engineInfoMarkdown(info: EngineInfo, facts: EngineFacts): string {
  const lines: string[] = ['# FrySharp engine info', '', '| | |', '|---|---|'];
  lines.push(`| Extension | ${facts.extensionVersion} |`);
  lines.push(`| Engine | ${info.serverVersion} (protocol ${info.protocolVersion}) |`);
  lines.push(`| Runtime | ${cell(info.runtime)} |`);
  lines.push(`| System | ${cell(info.operatingSystem)} (${info.architecture}) |`);
  lines.push(`| Editor | VS Code ${facts.vscodeVersion} |`);
  lines.push(`| Workspace | ${cell(info.workspaceRoot)} |`);
  if (facts.launch) lines.push(`| Engine started as | ${cell(facts.launch.description ?? facts.launch.command)} |`);
  if (facts.launch) lines.push(`| .NET | ${cell(facts.launch.command)} |`);
  if (facts.logFolder) lines.push(`| Logs | ${cell(facts.logFolder)} |`);

  lines.push('', '## Languages', '', '| Language | Status | Version | Where | From |', '|---|---|---|---|---|');
  for (const report of info.toolchains) lines.push(row(report));

  const missing = info.toolchains.filter((report) => !report.found);
  if (missing.length > 0) {
    lines.push('', '## What is missing');
    for (const report of missing) {
      lines.push('', `### ${report.displayName}`, '', ...(report.missing ?? 'Its tools were not found.').trimEnd().split('\n').map((line) => `    ${line}`));
    }
  }

  if (facts.settings.length > 0) {
    lines.push('', '## Settings', '');
    for (const [name, value] of facts.settings) lines.push(`- \`${name}\`: ${value || '(not set)'}`);
  }

  return lines.join('\n') + '\n';
}
