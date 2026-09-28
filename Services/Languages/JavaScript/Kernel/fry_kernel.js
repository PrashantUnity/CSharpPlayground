/**
 * The JavaScript (Node.js) kernel of FryPDF's C# Code Studio notebooks.
 * Speaks the Fry kernel protocol over stdin/stdout pipes: one JSON object per line.
 */

const readline = require("readline");
const vm = require("vm");
const util = require("util");
const path = require("path");
const fs = require("fs");

const protocolWrite = process.stdout.write.bind(process.stdout);

let currentRequestId = null;
let executionCount = 0;
let isBusy = false;
let pendingInputResolve = null;
const declaredVariables = new Set();

function send(msg) {
    try {
        const line = JSON.stringify(msg) + "\n";
        protocolWrite(line);
    } catch (e) {
        // Fallback for circular structures
        const line = JSON.stringify({ type: msg.type, id: msg.id, status: "error", message: String(e) }) + "\n";
        protocolWrite(line);
    }
}

function streamOutput(name, text) {
    if (!text || !currentRequestId) return;
    send({
        type: "stream",
        id: currentRequestId,
        name: name,
        text: text
    });
}

// Redirect process stdout and stderr so cell code and libraries cannot corrupt protocol lines
process.stdout.write = (chunk, encoding, callback) => {
    const text = typeof chunk === "string" ? chunk : chunk.toString(encoding || "utf8");
    streamOutput("stdout", text);
    if (typeof callback === "function") callback();
    return true;
};

process.stderr.write = (chunk, encoding, callback) => {
    const text = typeof chunk === "string" ? chunk : chunk.toString(encoding || "utf8");
    streamOutput("stderr", text);
    if (typeof callback === "function") callback();
    return true;
};

function display(data, metadata) {
    if (!currentRequestId) return;
    let bundle = {};
    if (Array.isArray(data)) {
        bundle = formatTable(data);
    } else if (typeof data === "object" && data !== null && !data["text/plain"] && !data["text/html"] && !data["application/vnd.fry.table+json"]) {
        bundle = {
            "application/json": JSON.stringify(data),
            "text/plain": util.inspect(data, { depth: 4 })
        };
    } else if (typeof data === "string") {
        bundle = { "text/plain": data };
    } else {
        bundle = data;
    }

    send({
        type: "display",
        id: currentRequestId,
        data: bundle,
        metadata: metadata || {}
    });
}

function formatTable(rows, title) {
    if (!rows || rows.length === 0) {
        return { "text/plain": "Empty table" };
    }

    const first = rows[0];
    if (typeof first !== "object" || first === null) {
        return {
            "application/vnd.fry.table+json": {
                title: title || "Data",
                columns: ["Value"],
                numeric: [typeof first === "number"],
                rows: rows.map(r => [r]),
                totalRows: rows.length,
                totalColumns: 1
            },
            "text/plain": util.inspect(rows)
        };
    }

    const columns = Object.keys(first);
    const numeric = columns.map(col => typeof first[col] === "number");
    const tableRows = rows.slice(0, 1000).map(row => columns.map(col => row[col] !== undefined ? row[col] : null));

    return {
        "application/vnd.fry.table+json": {
            title: title || "Table",
            columns: columns,
            numeric: numeric,
            rows: tableRows,
            totalRows: rows.length,
            totalColumns: columns.length
        },
        "text/plain": util.inspect(rows, { depth: 2 })
    };
}

function requestInput(promptText) {
    if (!currentRequestId) return Promise.resolve(null);
    return new Promise(resolve => {
        pendingInputResolve = resolve;
        send({
            type: "input_request",
            id: currentRequestId,
            prompt: promptText || "",
            password: false
        });
    });
}

// Create persistent execution sandbox
const contextObj = {
    console: {
        log: (...args) => streamOutput("stdout", util.format(...args) + "\n"),
        info: (...args) => streamOutput("stdout", util.format(...args) + "\n"),
        warn: (...args) => streamOutput("stderr", util.format(...args) + "\n"),
        error: (...args) => streamOutput("stderr", util.format(...args) + "\n"),
        dir: (obj) => streamOutput("stdout", util.inspect(obj, { depth: null }) + "\n")
    },
    display: display,
    input: requestInput,
    prompt: requestInput,
    fry: {
        display: display,
        table: (rows, title) => display(formatTable(rows, title))
    },
    process: process,
    Buffer: Buffer,
    setTimeout: setTimeout,
    clearTimeout: clearTimeout,
    setInterval: setInterval,
    clearInterval: clearInterval
};
contextObj.global = contextObj;
contextObj.globalThis = contextObj;

const context = vm.createContext(contextObj);

function trackDeclaredNames(code) {
    const declRegex = /(?:let|const|var|function|class)\s+([A-Za-z_$][\w$]*)/g;
    let match;
    while ((match = declRegex.exec(code)) !== null) {
        declaredVariables.add(match[1]);
    }
}

async function handleExecute(req) {
    currentRequestId = req.id;
    isBusy = true;
    executionCount++;

    const code = req.code || "";
    trackDeclaredNames(code);

    try {
        let result;
        if (code.includes("await ") || code.includes("await\n")) {
            // Async execution wrapper: attach top-level variable declarations to globalThis so they persist across cells
            const transformed = code.replace(/^(const|let|var)\s+([A-Za-z_$][\w$]*)\s*=/gm, "globalThis.$2 =");
            const wrapped = `(async () => {\n${transformed}\n})()`;
            result = await vm.runInContext(wrapped, context, { filename: req.cell ? `<Cell ${req.cell}>` : "<Cell>" });
        } else {
            const script = new vm.Script(code, { filename: req.cell ? `<Cell ${req.cell}>` : "<Cell>" });
            result = script.runInContext(context);
        }

        if (result !== undefined) {
            streamOutput("stdout", util.inspect(result, { depth: 3 }) + "\n");
        }

        send({
            type: "reply",
            id: req.id,
            status: "ok",
            executionCount: executionCount
        });
    } catch (err) {
        const ename = err && err.name ? err.name : "Error";
        const evalue = err && err.message ? err.message : String(err);
        const traceback = err && err.stack ? err.stack : String(err);

        let line = 1;
        let missingModule = null;
        let missingName = null;

        if (err && err.stack) {
            const lineMatch = /<Cell[^>]*>:(\d+)/.exec(err.stack);
            if (lineMatch) line = parseInt(lineMatch[1], 10);
        }

        const modMatch = /Cannot find (?:module|package) '([^']+)'/.exec(evalue);
        if (modMatch) missingModule = modMatch[1];

        const refMatch = /([A-Za-z_$][\w$]*) is not defined/.exec(evalue);
        if (refMatch) missingName = refMatch[1];

        send({
            type: "error",
            id: req.id,
            ename: ename,
            evalue: evalue,
            traceback: traceback,
            line: line,
            missingModule: missingModule,
            missingName: missingName
        });

        send({
            type: "reply",
            id: req.id,
            status: "error"
        });
    } finally {
        isBusy = false;
        currentRequestId = null;
    }
}

function handleVariables(req) {
    const list = [];
    const allNames = new Set([...Object.getOwnPropertyNames(context), ...declaredVariables]);
    const hidden = new Set(["console", "display", "input", "prompt", "fry", "process", "Buffer", "setTimeout", "clearTimeout", "setInterval", "clearInterval", "global", "globalThis"]);

    for (const name of allNames) {
        if (hidden.has(name) || name.startsWith("_")) continue;
        try {
            const val = vm.runInContext(name, context);
            const type = typeof val;
            let valStr = "";
            try {
                valStr = util.inspect(val, { depth: 1, maxArrayLength: 5, breakLength: 40 });
            } catch (e) {
                valStr = String(val);
            }
            list.push({
                name: name,
                type: type === "object" && val !== null ? val.constructor?.name || "object" : type,
                value: valStr,
                kind: type === "function" ? "function" : "variable"
            });
        } catch (e) {
            // Ignored if unresolvable
        }
    }

    send({
        type: "reply",
        id: req.id,
        status: "ok",
        variables: list
    });
}

function handleGetValue(req) {
    try {
        const val = vm.runInContext(req.name, context);
        const json = JSON.stringify(val);
        if (json === undefined) {
            send({
                type: "reply",
                id: req.id,
                status: "error",
                message: `JavaScript variable '${req.name}' of type ${typeof val} cannot be serialized to JSON.`
            });
            return;
        }

        send({
            type: "reply",
            id: req.id,
            status: "ok",
            json: json
        });
    } catch (e) {
        send({
            type: "reply",
            id: req.id,
            status: "error",
            message: `JavaScript has no variable named '${req.name}'.`
        });
    }
}

function handleSetValue(req) {
    try {
        const parsed = JSON.parse(req.json);
        context[req.name] = parsed;
        declaredVariables.add(req.name);
        send({
            type: "reply",
            id: req.id,
            status: "ok"
        });
    } catch (e) {
        send({
            type: "reply",
            id: req.id,
            status: "error",
            message: `Failed to set JavaScript variable '${req.name}': ${e.message}`
        });
    }
}

function handleInputReply(req) {
    if (pendingInputResolve) {
        const resolve = pendingInputResolve;
        pendingInputResolve = null;
        resolve(req.value);
    }
}

// Command line arguments: --cwd <dir>
for (let i = 2; i < process.argv.length; i++) {
    if (process.argv[i] === "--cwd" && i + 1 < process.argv.length) {
        try {
            process.chdir(process.argv[i + 1]);
        } catch (e) {}
        break;
    }
}

// Ready handshake
send({
    type: "ready",
    language: "javascript",
    version: process.version,
    executable: process.execPath
});

// Stdin protocol reader
const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    terminal: false
});

rl.on("line", (line) => {
    const trimmed = line.trim();
    if (!trimmed) return;
    try {
        const msg = JSON.parse(trimmed);
        switch (msg.type) {
            case "execute":
                handleExecute(msg);
                break;
            case "variables":
                handleVariables(msg);
                break;
            case "get_value":
                handleGetValue(msg);
                break;
            case "set_value":
                handleSetValue(msg);
                break;
            case "input_reply":
                handleInputReply(msg);
                break;
            case "interrupt":
                if (pendingInputResolve) {
                    const resolve = pendingInputResolve;
                    pendingInputResolve = null;
                    resolve(null);
                }
                break;
            case "shutdown":
                process.exit(0);
                break;
        }
    } catch (e) {
        // Ignore unparseable lines
    }
});

rl.on("close", () => {
    process.exit(0);
});
