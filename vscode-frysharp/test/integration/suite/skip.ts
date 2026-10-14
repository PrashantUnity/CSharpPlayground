import * as path from 'node:path';
import * as fs from 'node:fs';

/** Thrown by a test that cannot run on this machine (a tool it needs is not installed): reported as skipped, not as passed. */
export class SkipTest extends Error {}

/** True when `program` is on the PATH. */
export function onPath(program: string): boolean {
  const names = process.platform === 'win32' ? [`${program}.exe`, `${program}.cmd`] : [program];
  return (process.env.PATH ?? '')
    .split(path.delimiter)
    .filter(Boolean)
    .some((folder) => names.some((name) => fs.existsSync(path.join(folder, name))));
}

/** Skips the test without `program`, unless `required` names an environment variable set to 1, which makes its absence a failure. */
export function needs(program: string, required: string): void {
  if (process.env[required] === '1') return;
  if (!onPath(program)) throw new SkipTest(`needs ${program} on the PATH (set ${required}=1 to make this a failure)`);
}

/** Like {@link needs} for Rust's `cargo`, which rustup puts in `~/.cargo/bin` (on the PATH of a login shell, not always of a test host). */
export function needsCargo(required: string): void {
  if (process.env[required] === '1') return;
  const cargoHome = process.env.CARGO_HOME ?? path.join(process.env.HOME ?? process.env.USERPROFILE ?? '', '.cargo');
  const name = process.platform === 'win32' ? 'cargo.exe' : 'cargo';
  if (!onPath('cargo') && !fs.existsSync(path.join(cargoHome, 'bin', name))) {
    throw new SkipTest(`needs cargo on the PATH or in ${path.join(cargoHome, 'bin')} (set ${required}=1 to make this a failure)`);
  }
}
