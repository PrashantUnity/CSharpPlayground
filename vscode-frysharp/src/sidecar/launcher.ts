import * as path from 'path';

export interface LaunchInputs {
  /** The frysharp.sidecarPath setting: a frysharp-sidecar.dll to use instead of the packaged one. */
  setting?: string;
  extensionPath: string;
  /** The extension is running from the repository (the Extension Development Host), not from an installed package. */
  development: boolean;
  exists(file: string): boolean;
}

export interface SidecarLaunch {
  /** The arguments for `dotnet`. */
  args: string[];
  description: string;
}

const DLL = 'frysharp-sidecar.dll';

/**
 * Which sidecar to start. The one the setting names comes first. Running from the repository (the Extension Development
 * Host) the one built there is next, or the project run by `dotnet run`: a developer's own build is what they mean, and a
 * packaged copy left in the folder may be old. Otherwise, and when there is no build, the one packaged with the extension.
 * An installed extension never runs a build it did not bring.
 */
export function resolveSidecarLaunch(inputs: LaunchInputs, workspaceRoot: string): SidecarLaunch {
  const workspace = ['--workspace', workspaceRoot];

  if (inputs.setting) {
    if (!inputs.exists(inputs.setting)) {
      throw new Error(`frysharp.sidecarPath names '${inputs.setting}', which does not exist.`);
    }
    return { args: [inputs.setting, ...workspace], description: `the sidecar from the frysharp.sidecarPath setting (${inputs.setting})` };
  }

  if (inputs.development) {
    const repo = path.resolve(inputs.extensionPath, '..');
    const built = path.join(repo, 'FrySharp.Sidecar', 'bin', 'Debug', 'net10.0', DLL);
    if (inputs.exists(built)) {
      return { args: [built, ...workspace], description: `the sidecar built in the repository (${built})` };
    }
    const project = path.join(repo, 'FrySharp.Sidecar', 'FrySharp.Sidecar.csproj');
    if (inputs.exists(project)) {
      return { args: ['run', '--project', project, '--', ...workspace], description: `the sidecar project, run by dotnet (${project})` };
    }
  }

  const packaged = path.join(inputs.extensionPath, 'bin', 'sidecar', DLL);
  if (inputs.exists(packaged)) {
    return { args: [packaged, ...workspace], description: `the packaged sidecar (${packaged})` };
  }

  throw new Error('The FrySharp engine is not part of this installation. Reinstall the extension, or set frysharp.sidecarPath.');
}
