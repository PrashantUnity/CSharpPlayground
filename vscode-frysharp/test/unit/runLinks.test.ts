import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { findLinks } from '../../src/run/runLinks';

const place = (line: string) => findLinks(line).map((l) => [line.slice(l.start, l.start + l.length), l.file, l.line, l.column]);

describe('places a line of output names', () => {
  it('finds a .NET error: file(line,column)', () => {
    assert.deepStrictEqual(place('Program.cs(12,5): error CS1002: ; expected'), [['Program.cs(12,5)', 'Program.cs', 12, 5]]);
    assert.deepStrictEqual(place('src/Models/Order.cs(3,1): warning CS8618'), [['src/Models/Order.cs(3,1)', 'src/Models/Order.cs', 3, 1]]);
  });

  it('finds a Python traceback line', () => {
    assert.deepStrictEqual(place('  File "/work/tool.py", line 3, in <module>'), [['File "/work/tool.py", line 3', '/work/tool.py', 3, 1]]);
  });

  it('finds file:line:column, as Node, Go and Rust write it', () => {
    assert.deepStrictEqual(place('    at run (/work/app.js:10:15)'), [['/work/app.js:10:15', '/work/app.js', 10, 15]]);
    assert.deepStrictEqual(place('./main.go:5:2: undefined: x'), [['./main.go:5:2', './main.go', 5, 2]]);
    assert.deepStrictEqual(place(' --> src/main.rs:3:5'), [['src/main.rs:3:5', 'src/main.rs', 3, 5]]);
    assert.deepStrictEqual(place('script.py:7'), [['script.py:7', 'script.py', 7, 1]]);
  });

  it('finds a Windows path', () => {
    assert.deepStrictEqual(place('C:\\work\\Program.cs(4,9): error CS0103'), [['C:\\work\\Program.cs(4,9)', 'C:\\work\\Program.cs', 4, 9]]);
    assert.deepStrictEqual(place('at C:\\work\\app.js:2:3'), [['C:\\work\\app.js:2:3', 'C:\\work\\app.js', 2, 3]]);
  });

  it('finds several in one line, in order', () => {
    assert.deepStrictEqual(place('a.cs(1,1) calls b.cs(2,2)').map((l) => l[1]), ['a.cs', 'b.cs']);
  });

  it('does not make a link of a time, a version, a URL or a sentence', () => {
    for (const line of ['12:30:45 started', 'version 1.2.3 ready', 'see https://example.com:8080/path', 'Finished in 3.5 s: ok', 'x = 3', 'line 5']) {
      assert.deepStrictEqual(place(line), [], line);
    }
  });

  it('does not link line 0', () => {
    assert.deepStrictEqual(place('thing.cs(0,0)'), []);
  });
});
