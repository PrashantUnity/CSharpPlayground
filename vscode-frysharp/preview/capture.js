// Captures what the real engine sends for a handful of cells (an object, tables, charts, an ECharts chart, HTML), into
// preview/samples.json, for index.html to draw with the real renderer. Needs the sidecar built: dotnet build FrySharp.Sidecar
const fs = require('fs');
const os = require('os');
const path = require('path');
const { SidecarClient } = require('../out-test/src/sidecar/sidecarClient');

const dll = path.resolve(__dirname, '../../FrySharp.Sidecar/bin/Debug/net10.0/frysharp-sidecar.dll');
const workspace = fs.mkdtempSync(path.join(os.tmpdir(), 'preview-'));

const cells = [
  ['Object Explorer', `public class Address { public string City { get; set; } public int Zip { get; set; } public bool Primary { get; set; } }
public class People { public string Name { get; set; } public string Class { get; set; } public int[] Numbers { get; set; } public Address Home { get; set; } public People Another { get; set; } public string Nothing { get; set; } }
var people = new People { Name = "Code", Class = "II", Numbers = new[] { 1, 34, 45, 235, 25 }, Home = new Address { City = "Zurich", Zip = 8001, Primary = true } };
people.Another = people;
people`],
  ['Table', `Display.Table(new[] {
  new { Name = "Ada Lovelace", Age = 36, Active = true, Score = 98.6, City = "London" },
  new { Name = "Grace Hopper", Age = 85, Active = false, Score = 1234.5, City = (string)null },
  new { Name = "Linus Torvalds", Age = 54, Active = true, Score = 77.25, City = "Portland, OR" },
  new { Name = "Margaret Hamilton", Age = 88, Active = true, Score = 100.0, City = "Boston" },
  new { Name = "Alan Turing", Age = 41, Active = false, Score = 64.0, City = "Wilmslow" } }, "People");`],
  ['Line chart', 'Display.LineChart(new[] { 3.0, 1.0, 4.0, 1.0, 5.0, 9.0, 2.0, 6.0 }, "Latency (ms)");'],
  ['Bar chart', 'Display.BarChart(new[] { 12.0, 19.0, 3.0, 5.0, 2.0 }, "Weekly sales");'],
  ['3D surface', 'Display.Surface3D(new double[,] { {0,1,2,1,0}, {1,2,4,2,1}, {2,4,8,4,2}, {1,2,4,2,1}, {0,1,2,1,0} }, "Hill");'],
  ['ECharts', 'EChart.Line(new[] { 3.0, 5.0, 2.0, 8.0, 6.0, 9.0 }, "Sales").Title("Sales").Smooth().Show();'],
  ['HTML', 'Display.Html("<h3 style=\\"color:orange;margin:4px\\">Hello <i>html</i></h3><button id=\\"b\\">Click me</button><script>document.getElementById(\\"b\\").onclick = function () { this.textContent = \\"clicked\\"; };</script>");'],
  ['Markdown', 'Display.Markdown("# A heading\\nSome **bold** text.");']
];

(async () => {
  const client = new SidecarClient({
    log: () => undefined,
    prepare: async () => ({ command: 'dotnet', args: [dll, '--workspace', workspace], cwd: workspace })
  });
  const samples = [];
  let current = [];
  client.onNotebookOutput((n) => current.push(n));
  for (const [name, source] of cells) {
    current = [];
    const result = await client.executeCell({ path: '', cellId: name, source, language: 'csharp', notebookId: 'preview', executionId: `p-${samples.length}` });
    const displays = current.filter((n) => n.output.kind === 'display').map((n) => ({ data: n.output.data, metadata: n.output.metadata }));
    const text = current.filter((n) => n.output.kind === 'stream').map((n) => n.output.text).join('');
    console.log(`${result.success ? 'ok  ' : 'FAIL'} ${name}: ${displays.length} display(s)${text ? ', text: ' + JSON.stringify(text.slice(0, 120)) : ''}`);
    for (const display of displays) samples.push({ name, ...display });
  }
  client.dispose();
  fs.writeFileSync(path.join(__dirname, 'samples.json'), JSON.stringify(samples, null, 1));
  console.log(`${samples.length} samples written`);
})().catch((error) => {
  console.error(error);
  process.exit(1);
});
