import { cppTests } from './cpp';
import { fsharpTests } from './fsharp';
import { javaTests } from './java';
import { goTests } from './go';
import { rustTests } from './rust';
import { languageTests } from './language';
import { tests as notebookTests } from './notebooks';
import { scriptTests } from './scripts';
import { SkipTest } from './skip';
import { sqlTests } from './sql';

const tests = [...notebookTests, ...languageTests, ...sqlTests, ...fsharpTests, ...javaTests, ...rustTests, ...goTests, ...cppTests, ...scriptTests];

// Runs in the Extension Host: each test in turn, so the notebooks they open do not meet. A failure names the test.
export async function run(): Promise<void> {
  // FRY_IT_FILTER=java,lens runs the tests whose name has one of those words (a quick look at one language or feature)
  const filters = process.env.FRY_IT_FILTER?.toLowerCase().split(',').map((word) => word.trim()).filter(Boolean) ?? [];
  const selected = filters.length > 0 ? tests.filter(([name]) => filters.some((word) => name.toLowerCase().includes(word))) : tests;
  const failures: string[] = [];
  const skipped: string[] = [];
  for (const [name, test] of selected) {
    const started = Date.now();
    try {
      await test();
      console.log(`  ok   ${name} (${Date.now() - started} ms)`);
    } catch (error) {
      if (error instanceof SkipTest) {
        console.log(`  SKIP ${name}: ${error.message}`);
        skipped.push(name);
        continue;
      }
      console.log(`  FAIL ${name}\n       ${error instanceof Error ? error.stack ?? error.message : String(error)}`);
      failures.push(name);
    }
  }
  if (failures.length > 0) throw new Error(`${failures.length} of ${selected.length} failed: ${failures.join('; ')}`);
  console.log(`${selected.length - skipped.length} extension tests passed${skipped.length > 0 ? `, ${skipped.length} skipped` : ''}.`);
}
