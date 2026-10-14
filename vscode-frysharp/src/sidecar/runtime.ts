import * as cp from 'child_process';

/** The .NET the engine needs. */
export const REQUIRED_MAJOR = 10;

/** True when the output of `dotnet --list-runtimes` has the shared runtime (Microsoft.NETCore.App) of this major version or newer. */
export function hasRuntime(listing: string, major: number = REQUIRED_MAJOR): boolean {
  return listing.split(/\r?\n/).some((line) => {
    const found = /^Microsoft\.NETCore\.App (\d+)\./.exec(line.trim());
    return found !== null && parseInt(found[1], 10) >= major;
  });
}

/** Runs `dotnet --list-runtimes` with this executable; null when it cannot be run. */
export function listRuntimes(dotnet: string): Promise<string | null> {
  return new Promise((resolve) => {
    cp.execFile(dotnet, ['--list-runtimes'], { timeout: 15_000 }, (error, stdout) => resolve(error ? null : stdout));
  });
}

export class DotnetMissingError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'DotnetMissingError';
  }
}

export interface RuntimeSources {
  /** The frysharp.dotnetPath setting. */
  setting?: string;
  /** Asks the .NET Install Tool (ms-dotnettools.vscode-dotnet-runtime) for a runtime; undefined when it is not installed. */
  acquire?: () => Promise<string | undefined>;
  log(message: string): void;
}

/**
 * The `dotnet` to run the engine with: the one the setting names; else the `dotnet` on the PATH when it has a runtime new
 * enough (nothing to download); else the one the .NET Install Tool provides. Says what is missing when none does.
 */
export async function resolveDotnet(sources: RuntimeSources): Promise<string> {
  if (sources.setting) {
    const listing = await listRuntimes(sources.setting);
    if (listing === null) throw new DotnetMissingError(`frysharp.dotnetPath names '${sources.setting}', which cannot be run.`);
    if (!hasRuntime(listing)) throw new DotnetMissingError(`'${sources.setting}' has no .NET ${REQUIRED_MAJOR} runtime.`);
    return sources.setting;
  }

  const onPath = await listRuntimes('dotnet');
  if (onPath !== null && hasRuntime(onPath)) return 'dotnet';

  if (sources.acquire) {
    try {
      const acquired = await sources.acquire();
      if (acquired) return acquired;
    } catch (error) {
      sources.log(`The .NET Install Tool could not provide .NET ${REQUIRED_MAJOR}: ${error instanceof Error ? error.message : String(error)}`);
    }
  }

  throw new DotnetMissingError(
    onPath === null
      ? `FrySharp needs .NET ${REQUIRED_MAJOR}, and no 'dotnet' could be run.`
      : `FrySharp needs .NET ${REQUIRED_MAJOR}; the 'dotnet' on the PATH has no runtime that new.`
  );
}
