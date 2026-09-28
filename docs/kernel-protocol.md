# The Fry kernel protocol

A notebook language that runs as its own program (Python today) talks to the studio with this protocol. The studio side is `Services/Kernels/ProtocolKernel.cs`. The Python side is `Services/Languages/Python/Kernel/fry_kernel.py`, which is a working reference for any new kernel. To give a language notebook cells, write a program that speaks this protocol and an `IKernelLauncher` that starts it. [adding-a-language.md](adding-a-language.md) has the rest of the checklist.

It is deliberately small next to Jupyter's: no ZeroMQ and no connection files, just two pipes. A Jupyter kernel could still be supported later through an adapter that implements `INotebookKernel`.

## Transport

- **Pipes:** the studio starts the program with stdin and stdout redirected. Each line on them is one message.
- **Messages:** each message is a UTF-8 JSON object on a single line, ending with `\n`. Non-ASCII text should be escaped (`é`), which is what `ensure_ascii` does, so a line never breaks inside a character.
- **stderr:** anything written there shows in the running cell's output. It covers warnings, C libraries writing to fd 2, and child processes. The last few KB are kept, so if the program dies, the message says why.
- **Reserved channels:** the protocol owns stdout and stdin, so user code must not reach them. `fry_kernel.py` keeps private copies of fd 1 and fd 0 for the protocol. It points fd 1 at fd 2, so `os.write(1, …)` and a child's `print` land in stderr and show as output. It points fd 0 at `/dev/null`. It also redirects Windows' standard handles. Stray output would otherwise land in the middle of a protocol line.
- **Lines that aren't JSON:** the studio shows them as output rather than dropping them, but a kernel should never send any.
- **End of input:** when stdin closes, the kernel must exit and take anything it started with it (`fry_kernel.py` kills its process group). The studio closes stdin when a notebook closes and kills the process tree to make sure.

## Studio → kernel

| `type` | Fields | Meaning |
|---|---|---|
| `execute` | `id`, `code`, `cell` | Run a cell. `code` has its directive lines (`#!share`, `%pip`…) blanked, so line numbers still match the editor. `cell` is how the notebook labels the cell, e.g. `"[3]"`; use it to name the code in tracebacks (`File "<Cell [3]>", line 2`). |
| `interrupt` | — | Stop the running cell (Python raises `KeyboardInterrupt` in it). Must be handled while a cell runs, so read stdin on a thread of its own. |
| `input_reply` | `value` | The answer to the pending `input_request`: a string, or `null` for end of input. |
| `variables` | `id` | List the variables, for the Variables panel. |
| `get_value` | `id`, `name` | A variable's value as JSON, for `#!share`. |
| `set_value` | `id`, `name`, `json` | Set (or create) a variable from JSON, for `#!share`. |
| `add_search_path` | `id`, `path` | Add a folder to the import path while running. This lets a package just installed into a new environment work without a restart. Optional: implement it only if the language has one. |
| `shutdown` | — | Exit now. Optional, because closing stdin means the same. |

The studio sends one request at a time for each notebook, and each `execute` finishes before the next begins. `variables`, `get_value`, `set_value` and `add_search_path` are sent only between cells. So only `interrupt` and `input_reply` arrive while a cell runs.

## Kernel → studio

| `type` | Fields | Meaning |
|---|---|---|
| `ready` | `language`, `version`, `executable` | First message, sent once the kernel can take requests. The notebook header then shows e.g. "Python 3.14.6". The studio waits up to 60 s for it. |
| `stream` | `id`, `name` (`stdout`/`stderr`), `text` | Output of the running cell. Send it as it's produced, batched so a tight `print` loop doesn't send a message per character. `fry_kernel.py` flushes at a newline, at 8 KB or after 50 ms. |
| `display` | `id`, `data`, `metadata` | A rich output: a MIME bundle, as in Jupyter (see below). |
| `input_request` | `id`, `prompt`, `password` | The cell asks for a line of input. Wait for `input_reply`. The studio echoes the prompt and the answer into the cell's output. |
| `error` | `id`, `ename`, `evalue`, `traceback`, `line`, `missingModule`, `missingName` | The cell failed. `traceback` is the text to show. `line` is the failing line in the cell, if it's known. `missingModule` (e.g. `"cv2"`) makes the cell offer to install the package. `missingName` makes it offer to run the cells above. |
| `reply` | `id`, `status`, … | Ends a request. `status` is `ok`, `error` (with `message` for requests other than `execute`) or `interrupted`. An `execute` reply carries `executionCount`, a `variables` reply `variables` (`[{name, type, value, kind}]`), and a `get_value` reply `json`. |

Send everything that belongs to an execution before its `reply`: flush buffered output, then any pending `display`. The reply is what ends the cell in the studio.

## MIME bundles

`data` maps MIME types to values, and the studio shows the richest one it knows:

1. `application/vnd.fry.table+json`: the studio's own table (column sorting, number alignment). The shape is `{"title", "columns": [...], "numeric": [bool...], "rows": [[...]], "totalRows", "totalColumns"}`. Send at most about 1,000 rows. The studio notes "showing the first N of M rows" when `totalRows` is larger.
2. `image/png` or `image/jpeg`, base64. `metadata["image/png"] = {"width", "height"}` gives its size in pixels.
3. `image/svg+xml`, drawn by the HTML view.
4. `text/html`.
5. `text/markdown` or `text/plain`, which is shown as text.

Always include a `text/plain` fallback.

## Values for `#!share`

`get_value` and `set_value` carry values as JSON, so a kernel decides what its values look like as JSON:

- **The Python kernel:** numpy arrays become lists, pandas DataFrames lists of records, Series lists, and NaN or infinity `null`. Dataclasses and plain objects become dicts. Functions, modules and other non-data values are an error whose message tells the user why.
- **The C# kernel:** declares a shared value with the C# type that fits it. `[1,2,3]` becomes `int[]`, `[[1.5]]` becomes `double[][]`, and an object becomes `Dictionary<string, object>`. If a variable of that name already exists, it keeps its type and gets the value. See `Services/Kernels/KernelValueSharing.cs`.

An error `reply` for `get_value` or `set_value` should explain in its `message` what went wrong, e.g. "Python has no variable named 'x'".

## Stopping and crashes

- **Stop:** the studio sends `interrupt`. If the cell hasn't replied within 2 s, it kills the kernel and says the variables are gone. A kernel should make sure an interrupt reaches code that's sleeping or waiting, not only running Python. `fry_kernel.py` uses `pthread_kill(SIGINT)` on the main thread and masks SIGINT while it writes a protocol line.
- **Crashes:** if the program exits, every pending request fails, so a cell never hangs. The cell shows the exit code and the end of stderr. The next cell starts a new kernel with a note that the variables from before are gone.
- **Restart:** Restart kernels ends the program without a note, and the next cell starts a new one.

## Trying a kernel by hand

Send lines on stdin and read stdout:

```bash
python3 -u Services/Languages/Python/Kernel/fry_kernel.py --cwd /tmp
```

Then type lines such as:

```json
{"type": "execute", "id": "1", "code": "x = 6 * 7\nx", "cell": "[1]"}
{"type": "get_value", "id": "2", "name": "x"}
```
