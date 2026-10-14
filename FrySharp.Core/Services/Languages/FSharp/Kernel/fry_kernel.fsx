// The Fry kernel for F#: speaks the Fry kernel protocol (docs/kernel-protocol.md) on stdin and stdout, and runs the cells of a
// notebook in one F# Interactive evaluation session, so what a cell defines the next one sees, as in `dotnet fsi`.
//
//   dotnet fsi --nologo fry_kernel.fsx -- --cwd <folder>
//
// The session is the F# compiler's own (FSharp.Compiler.Service.dll, which ships in the .NET SDK beside fsi, and which fsi
// finds for `#r` by itself, so it is always the one of the fsi that runs this). A cell's
// console output, its Console.ReadLine, and the displays of Display.fsx (Display.table, Display.lineChart ...) are
// routed to the protocol; the protocol itself keeps a private copy of the real stdout.
#r "FSharp.Compiler.Service.dll"
#load "Values.fsx"
#nowarn "3886" // a list of tuples written one per line reads as intended

open System
open System.Collections
open System.Collections.Concurrent
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Reflection
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open System.Threading
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Interactive.Shell
open FSharp.Reflection
open Fry.Values

// ─────────────────────────────────────────────────────────────────────────────────────────────── protocol output

let private protocolOut = new StreamWriter(Console.OpenStandardOutput(), UTF8Encoding(false))
protocolOut.AutoFlush <- true

let private sendLock = obj ()

/// One message, one line. System.Text.Json escapes everything outside ASCII, so a line never breaks inside a character.
let private sendRaw (message: JsonObject) =
    lock sendLock (fun () -> protocolOut.Write(message.ToJsonString() + "\n"))

// ─────────────────────────────────────────────────────────────────────────────────────────── the cell's console output

let mutable private currentId = ""

/// What a cell prints: text is batched (8 KB or 50 ms), and the display lines of Display.fsx become protocol messages in
/// their place in the output, so text, tables and charts stay in the order the cell made them.
type private Hub() =
    let gate = obj ()
    let text = StringBuilder()
    let mutable textName = "stdout"

    let flushText () =
        if text.Length > 0 then
            sendRaw (jobj [ "type", jstr "stream"; "id", jstr currentId; "name", jstr textName; "text", jstr (text.ToString()) ])
            text.Clear() |> ignore

    member _.Text(name: string, s: string) =
        lock gate (fun () ->
            if name <> textName then
                flushText ()
                textName <- name
            text.Append(s) |> ignore
            if text.Length >= 8192 then flushText ())

    /// A protocol message made by the cell (a display): after the text before it.
    member _.Message(message: JsonObject) =
        lock gate (fun () ->
            flushText ()
            message.["id"] <- jstr currentId
            sendRaw message)

    member _.Flush() = lock gate flushText

let private hub = Hub()

let private markers = [| "__FRY_DISPLAY__"; "{\"type\":" |]

let private couldBeMarker (partial: string) =
    partial.Length = 0 || markers |> Array.exists (fun m -> m.StartsWith(partial, StringComparison.Ordinal) || partial.StartsWith(m, StringComparison.Ordinal))

let private displayTypes = set [ "display"; "update_display"; "subscribe"; "unsubscribe" ]

/// The message in a line the cell's code printed ("__FRY_DISPLAY__ {...}" or a bare typed message), or None for ordinary text.
let private protocolLine (line: string) : JsonObject option =
    let trimmed = line.Trim()
    let payload =
        if trimmed.StartsWith("__FRY_DISPLAY__", StringComparison.Ordinal) then trimmed.Substring("__FRY_DISPLAY__".Length).Trim()
        elif trimmed.StartsWith("{\"type\":", StringComparison.Ordinal) then trimmed
        else ""
    if payload.Length = 0 then None
    else
        try
            match JsonNode.Parse(payload) with
            | :? JsonObject as o ->
                let kind = match o.["type"] with | null -> "display" | t -> t.GetValue<string>()
                if displayTypes.Contains kind then
                    o.["type"] <- jstr kind
                    Some o
                else None
            | _ -> None
        with _ -> None

type private CellWriter(name: string) =
    inherit TextWriter()
    let partial = StringBuilder()

    override _.Encoding = UTF8Encoding(false) :> Encoding

    override _.Write(c: char) = base.Write(c)

    override _.Write(s: string) =
        if not (isNull s) && s.Length > 0 then
            lock partial (fun () ->
                for c in s do
                    partial.Append(c) |> ignore
                    if c = '\n' then
                        let line = partial.ToString(0, partial.Length - 1).TrimEnd('\r')
                        partial.Clear() |> ignore
                        match (if name = "stdout" then protocolLine line else None) with
                        | Some message -> hub.Message(message)
                        | None -> hub.Text(name, line + "\n")
                // an interactive prompt ("Name? ") has no newline: show it unless it may still turn out to be a display line
                if partial.Length > 0 && (name <> "stdout" || not (couldBeMarker (partial.ToString()))) then
                    hub.Text(name, partial.ToString())
                    partial.Clear() |> ignore)

    override this.Write(buffer: char[], index: int, count: int) = this.Write(String(buffer, index, count))

    override _.Flush() =
        lock partial (fun () ->
            if partial.Length > 0 then
                hub.Text(name, partial.ToString())
                partial.Clear() |> ignore)
        hub.Flush()

// ─────────────────────────────────────────────────────────────────────────────────────────────────────── input

let private inputReplies = new BlockingCollection<string option>()

/// Console.ReadLine in a cell: the studio is asked for a line, and its answer (or the end of input) comes back on stdin.
type private CellInput() =
    inherit TextReader()
    let mutable buffer = ""

    member private _.Ask() : string option =
        hub.Flush()
        sendRaw (jobj [ "type", jstr "input_request"; "id", jstr currentId; "prompt", jstr ""; "password", jbool false ])
        inputReplies.Take()

    override this.ReadLine() =
        if buffer.Length > 0 then
            let line = buffer
            buffer <- ""
            line
        else
            match this.Ask() with
            | Some line -> line
            | None -> null

    override this.Read() =
        if buffer.Length = 0 then
            match this.Ask() with
            | Some line -> buffer <- line + "\n"
            | None -> buffer <- ""
        if buffer.Length = 0 then -1
        else
            let c = buffer.[0]
            buffer <- buffer.Substring(1)
            int c

    override this.Peek() = if buffer.Length > 0 then int buffer.[0] else -1

// ─────────────────────────────────────────────────────────────────────────────────────── values: JSON in and out (#!share)

let rec private toJson (o: obj) (depth: int) : JsonNode =
    match o with
    | null -> null
    | :? string as s -> jstr s
    | :? bool as b -> jbool b
    | :? int as n -> JsonValue.Create n
    | :? int64 as n -> JsonValue.Create n
    | :? int16 as n -> JsonValue.Create(int n)
    | :? byte as n -> JsonValue.Create(int n)
    | :? uint32 as n -> JsonValue.Create(int64 n)
    | :? float as f -> if Double.IsFinite f then JsonValue.Create f else null
    | :? float32 as f -> if Single.IsFinite f then JsonValue.Create(float f) else null
    | :? decimal as d -> JsonValue.Create d
    | :? char as c -> jstr (string c)
    | :? DateTime as d -> jstr (d.ToString("o", invariant))
    | :? DateTimeOffset as d -> jstr (d.ToString("o", invariant))
    | _ when o.GetType().IsEnum -> jstr (o.ToString())
    | :? IFormattable as f when isScalarType (o.GetType()) -> jstr (f.ToString(null, invariant))
    | _ when depth > 12 -> jstr (oneLine o)
    | _ ->
        let t = o.GetType()
        if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>> then toJson (t.GetProperty("Value").GetValue(o)) (depth + 1)
        elif isDictionary o then
            let obj = JsonObject()
            for (k, v) in entriesOf o do obj.[scalarText k] <- toJson v (depth + 1)
            upcast obj
        elif FSharpType.IsRecord(t, true) then
            let obj = JsonObject()
            for f in FSharpType.GetRecordFields(t, true) do obj.[f.Name] <- toJson (f.GetValue(o)) (depth + 1)
            upcast obj
        elif FSharpType.IsTuple t then upcast jarr (FSharpValue.GetTupleFields o |> Array.map (fun v -> toJson v (depth + 1)))
        elif FSharpType.IsUnion(t, true) && not (t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<list<_>>) then
            let case, fields = FSharpValue.GetUnionFields(o, t, true)
            if fields.Length = 0 then jstr case.Name
            else
                let obj = JsonObject()
                obj.["case"] <- jstr case.Name
                Array.iter2 (fun (f: PropertyInfo) v -> obj.[f.Name] <- toJson v (depth + 1)) (case.GetFields()) fields
                upcast obj
        elif isCollection o then upcast jarr ((o :?> IEnumerable) |> Seq.cast<obj> |> Seq.truncate 100000 |> Seq.map (fun v -> toJson v (depth + 1)) |> Array.ofSeq)
        elif FSharpType.IsFunction t then failwithf "a function (%s) is not data, so it can't be shared" (friendly t)
        else
            let obj = JsonObject()
            for (name, get) in publicMembers t do obj.[name] <- toJson (get o) (depth + 1)
            upcast obj

let private fsString (s: string) =
    let sb = StringBuilder("\"")
    for c in s do
        match c with
        | '"' -> sb.Append("\\\"") |> ignore
        | '\\' -> sb.Append("\\\\") |> ignore
        | '\n' -> sb.Append("\\n") |> ignore
        | '\r' -> sb.Append("\\r") |> ignore
        | '\t' -> sb.Append("\\t") |> ignore
        | c when c < ' ' || c = ' ' || c = ' ' -> sb.AppendFormat("\\u{0:x4}", int c) |> ignore
        | c -> sb.Append(c) |> ignore
    sb.Append("\"").ToString()

let private isIdentifier (name: string) =
    name.Length > 0 && (Char.IsLetter name.[0] || name.[0] = '_') && name |> Seq.forall (fun c -> Char.IsLetterOrDigit c || c = '_' || c = '\'')

let private bindingName (name: string) = if isIdentifier name then name else "``" + name.Replace("``", "") + "``"

/// F# source for a JSON value: numbers, strings, bools, arrays (typed by what they hold) and objects (as `dict`), so that a
/// value shared from another language is an ordinary F# value the next cell can use.
let rec private literal (e: JsonElement) : string =
    match e.ValueKind with
    | JsonValueKind.Null | JsonValueKind.Undefined -> "Unchecked.defaultof<obj>"
    | JsonValueKind.True -> "true"
    | JsonValueKind.False -> "false"
    | JsonValueKind.String -> fsString (e.GetString())
    | JsonValueKind.Number ->
        match e.TryGetInt32() with
        | true, n -> string n
        | _ ->
            match e.TryGetInt64() with
            | true, n -> string n + "L"
            | _ -> let d = e.GetDouble() in (if Double.IsFinite d then d.ToString("R", invariant) + (if d = Math.Floor d && abs d < 1e15 then ".0" else "") else "nan")
    | JsonValueKind.Array ->
        let items = e.EnumerateArray() |> List.ofSeq
        let kinds = items |> List.map (fun i -> i.ValueKind) |> List.distinct
        let allNumbers = not items.IsEmpty && kinds = [ JsonValueKind.Number ]
        if items.IsEmpty then "[||] : obj[]"
        elif allNumbers && items |> List.forall (fun i -> fst (i.TryGetInt32())) then "[| " + String.Join("; ", items |> List.map literal) + " |]"
        elif allNumbers && items |> List.forall (fun i -> fst (i.TryGetInt64())) then "[| " + String.Join("; ", items |> List.map (fun i -> string (i.GetInt64()) + "L")) + " |]"
        elif allNumbers then "[| " + String.Join("; ", items |> List.map (fun i -> let d = i.GetDouble() in d.ToString("R", invariant) + (if d = Math.Floor d && abs d < 1e15 then ".0" else ""))) + " |]"
        elif kinds = [ JsonValueKind.String ] || kinds = [ JsonValueKind.True ] || kinds = [ JsonValueKind.False ] || (kinds |> List.forall (fun k -> k = JsonValueKind.True || k = JsonValueKind.False)) then
            "[| " + String.Join("; ", items |> List.map literal) + " |]"
        elif kinds = [ JsonValueKind.Object ] then "[| " + String.Join("; ", items |> List.map literal) + " |]"
        elif kinds = [ JsonValueKind.Array ] then "[| " + String.Join("; ", items |> List.map literal) + " |]"
        else "[| " + String.Join("; ", items |> List.map (fun i -> "box (" + literal i + ")")) + " |]"
    | _ ->
        let pairs = e.EnumerateObject() |> Seq.map (fun p -> sprintf "(%s, box (%s))" (fsString p.Name) (literal p.Value)) |> List.ofSeq
        if pairs.IsEmpty then "dict (Seq.empty : seq<string * obj>)" else "dict [ " + String.Join("; ", pairs) + " ]"

// ───────────────────────────────────────────────────────────────────────────────────────────────────── the session

let private argAfter (name: string) =
    let args = fsi.CommandLineArgs
    match Array.tryFindIndex ((=) name) args with
    | Some i when i + 1 < args.Length -> Some args.[i + 1]
    | _ -> None

match argAfter "--cwd" with
| Some folder when Directory.Exists folder -> Directory.SetCurrentDirectory folder
| _ -> ()

let private cellOut = new CellWriter("stdout")
let private cellErr = new CellWriter("stderr")
let private cellIn = new CellInput()
Console.SetOut cellOut
Console.SetError cellErr
Console.SetIn cellIn

// events the studio sends for the visuals a cell listens to: Display.fsx drains this queue
let private events = ConcurrentQueue<string>()
AppDomain.CurrentDomain.SetData("fry.events", events)

let private session =
    let argv = [| "fsi.exe"; "--noninteractive"; "--nologo"; "--gui-" |]
    FsiEvaluationSession.Create(FsiEvaluationSession.GetDefaultConfiguration(), argv, new StringReader(""), new StringWriter(), new StringWriter(), collectible = false)

let private mainThread = Thread.CurrentThread

/// Runs F# in the session: the value of its last expression (when it has one), and what the compiler said.
let private eval (code: string) =
    let result, diagnostics = session.EvalInteractionNonThrowing(code)
    result, diagnostics

let private isError (d: FSharpDiagnostic) = d.Severity = FSharpDiagnosticSeverity.Error

let private diagnosticJson (d: FSharpDiagnostic) : JsonNode =
    upcast jobj [ "severity", jstr (if isError d then "error" else "warning")
                  "code", jstr d.ErrorNumberText
                  "message", jstr (d.Message.Trim())
                  "line", jint d.StartLine
                  "column", jint (d.StartColumn + 1)
                  "endLine", jint d.EndLine
                  "endColumn", jint (d.EndColumn + 1) ]

let private prelude =
    [ "open System"; "open System.IO"; "open System.Collections.Generic"; "open System.Linq" ] |> String.concat "\n"

eval prelude |> ignore

let mutable private displayLoaded = false

let private displayFsx = Path.Combine(__SOURCE_DIRECTORY__, "Display.fsx")

/// Display.fsx (Display.table, Display.lineChart ...) is compiled in the first time a cell mentions it: it takes a moment.
let private ensureDisplay (code: string) =
    if not displayLoaded && (code.Contains "Display" || code.Contains "Fry.") && File.Exists displayFsx then
        displayLoaded <- true
        let result, diagnostics = eval (sprintf "#load %s\nopen Fry" (fsString (displayFsx.Replace('\\', '/'))))
        match result with
        | Choice2Of2 _ ->
            displayLoaded <- false
            let text = String.Join("\n", diagnostics |> Array.filter isError |> Array.map (fun d -> d.Message))
            hub.Text("stderr", "Display.fsx did not load: " + text + "\n")
        | _ -> ()

/// `%nuget install Package [version]` is the Jupyter way to say what fsi says as `#r "nuget: Package, version"`: the same line, in
/// the same place, so the lines of the cell stay where they were.
let private nugetMagic = System.Text.RegularExpressions.Regex(@"^\s*[%!#]nuget\s+(?:install|add)\s+([A-Za-z0-9_.\-]+)(?:\s+(\S+))?\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)

let private withNuGetMagic (code: string) =
    code.Split('\n')
    |> Array.map (fun line ->
        let m = nugetMagic.Match(line.TrimEnd('\r'))
        if not m.Success then line
        elif m.Groups.[2].Success then sprintf "#r \"nuget: %s, %s\"" m.Groups.[1].Value m.Groups.[2].Value
        else sprintf "#r \"nuget: %s\"" m.Groups.[1].Value)
    |> String.concat "\n"

let private firstLine (text: string) = (text.Split('\n').[0]).Trim()

/// A runtime exception's text without the plumbing of the session, and the line of the cell it went through.
let private exceptionReport (ex: exn) : string * string * string * int option =
    let ex = (match ex with | :? TargetInvocationException as t when not (isNull t.InnerException) -> t.InnerException | _ -> ex)
    let lines = ex.ToString().Split('\n') |> Array.map (fun l -> l.TrimEnd('\r'))
    let shown = lines |> Array.takeWhile (fun l -> not (l.TrimStart().StartsWith("at System.Reflection.")))
    let cleaned = shown |> Array.map (fun l -> System.Text.RegularExpressions.Regex.Replace(l, @" in .*?input\.fsx:line (\d+)", " in cell:line $1"))
    let own =
        lines |> Array.choose (fun l ->
            let m = System.Text.RegularExpressions.Regex.Match(l, @"StartupCode\$FSI_\d+.*? in .*?:line (\d+)")
            if m.Success then Some(int m.Groups.[1].Value) else None)
    ex.GetType().Name, ex.Message, String.Join("\n", cleaned), (if own.Length > 0 then Some own.[own.Length - 1] else None)

let private warningsText (diagnostics: FSharpDiagnostic[]) =
    diagnostics
    |> Array.filter (fun d -> d.Severity = FSharpDiagnosticSeverity.Warning)
    |> Array.map (fun d -> sprintf "(%d,%d): warning %s: %s\n" d.StartLine (d.StartColumn + 1) d.ErrorNumberText (firstLine d.Message))
    |> String.concat ""

let mutable private interrupted = false
let mutable private executing = false
let mutable private executionCount = 0

let private sendReply (id: string) (status: string) (extra: (string * JsonNode) list) =
    hub.Flush()
    sendRaw (jobj ([ "type", jstr "reply"; "id", jstr id; "status", jstr status ] @ extra))

let private execute (id: string) (code: string) =
    currentId <- id
    interrupted <- false
    executing <- true
    executionCount <- executionCount + 1
    let mutable status = "ok"
    try
        try
            let code = withNuGetMagic code
            ensureDisplay code
            let mutable result, diagnostics = eval code
            // `failwith "x"` as the last expression has a generic type that FSI tolerates and the evaluation API does not
            if (match result with Choice2Of2 _ -> diagnostics |> Array.exists (fun d -> isError d && d.ErrorNumber = 30) | _ -> false) then
                let retried, retriedDiagnostics = eval (code + "\n()")
                result <- retried
                diagnostics <- retriedDiagnostics |> Array.filter (fun d -> d.ErrorNumber <> 20)

            hub.Text("stderr", warningsText diagnostics)
            match result with
            | Choice1Of2 value ->
                match value with
                | Some v ->
                    match bundleOf v.ReflectionValue v.ReflectionType with
                    | Some data -> hub.Message(jobj [ "type", jstr "display"; "data", data; "metadata", jobj [] ])
                    | None -> ()
                | None -> ()
            | Choice2Of2 ex ->
                status <- "error"
                let errors = diagnostics |> Array.filter isError
                if interrupted then status <- "interrupted"
                elif errors.Length > 0 then
                    let first = errors.[0]
                    let trace = errors |> Array.map (fun d -> sprintf "(%d,%d): error %s: %s" d.StartLine (d.StartColumn + 1) d.ErrorNumberText (d.Message.Trim())) |> String.concat "\n"
                    hub.Message(jobj [ "type", jstr "error"; "ename", jstr first.ErrorNumberText; "evalue", jstr (first.Message.Trim())
                                       "traceback", jstr (trace + "\n"); "line", jint first.StartLine
                                       "diagnostics", jarr (errors |> Array.map diagnosticJson) ])
                else
                    let name, message, trace, line = exceptionReport ex
                    hub.Message(jobj ([ "type", jstr "error"; "ename", jstr name; "evalue", jstr message; "traceback", jstr (trace + "\n") ]
                                      @ (match line with Some l -> [ "line", jint l ] | None -> [])))
        with
        | :? ThreadInterruptedException -> status <- "interrupted"
        | ex ->
            status <- "error"
            hub.Message(jobj [ "type", jstr "error"; "ename", jstr (ex.GetType().Name); "evalue", jstr ex.Message; "traceback", jstr (ex.ToString() + "\n") ])
    finally
        executing <- false
        // an interrupt that came as the cell ended is still pending on this thread: let it fire here, not in the next cell
        (try Thread.Sleep 1 with :? ThreadInterruptedException -> ())
        cellOut.Flush()
        cellErr.Flush()
    sendReply id status (if status = "ok" then [ "executionCount", jint executionCount ] else [])

// ──────────────────────────────────────────────────────────────────────────────────────────────── other requests

let private boundValues () =
    session.GetBoundValues() |> List.filter (fun b -> b.Name <> "it")

let private variables (id: string) =
    let list =
        boundValues ()
        |> List.map (fun b ->
            let v = b.Value
            let isFunction = FSharpType.IsFunction v.ReflectionType
            let value = (if isFunction then friendly v.ReflectionType else oneLine v.ReflectionValue)
            upcast jobj [ "name", jstr b.Name; "type", jstr (friendly v.ReflectionType); "value", jstr value; "kind", jstr (if isFunction then "Function" else "Variable") ] : JsonNode)
    sendReply id "ok" [ "variables", jarr list ]

let private failReply (id: string) (message: string) = sendReply id "error" [ "message", jstr message ]

let private getValue (id: string) (name: string) =
    match boundValues () |> List.tryFind (fun b -> b.Name = name) with
    | None -> failReply id (sprintf "F# has no variable named '%s'." name)
    | Some b ->
        try
            let json = (match toJson b.Value.ReflectionValue 0 with | null -> "null" | node -> node.ToJsonString())
            sendReply id "ok" [ "json", jstr json ]
        with ex -> failReply id ex.Message

let private setValue (id: string) (name: string) (json: string) =
    try
        use document = JsonDocument.Parse(json)
        let source = sprintf "let %s = %s" (bindingName name) (literal document.RootElement)
        let result, diagnostics = eval source
        match result with
        | Choice2Of2 _ -> failReply id (sprintf "F# could not take '%s': %s" name (String.Join(" ", diagnostics |> Array.filter isError |> Array.map (fun d -> firstLine d.Message))))
        | _ -> sendReply id "ok" []
    with ex -> failReply id ex.Message

/// The events of the studio that came while no cell was running: their callbacks run now, and what they print belongs to the
/// cell that showed the visual (the event says which).
let private drainEvents () =
    let mutable line = Unchecked.defaultof<string>
    while events.TryDequeue(&line) do
        if displayLoaded then
            currentId <- (try JsonDocument.Parse(line).RootElement.GetProperty("id").GetString() with _ -> currentId)
            eval (sprintf "Fry.Display.FeedEvent %s" (fsString line)) |> ignore
            cellOut.Flush()
            cellErr.Flush()

// ────────────────────────────────────────────────────────────────────────────────────────────────── the request loop

let private requests = new BlockingCollection<JsonElement>()

let private stdinReader =
    Thread((fun () ->
        let input = new StreamReader(Console.OpenStandardInput(), UTF8Encoding(false))
        let mutable line = input.ReadLine()
        while not (isNull line) do
            if line.Trim().Length > 0 then
                try
                    let message = JsonDocument.Parse(line).RootElement.Clone()
                    let text (n: string) = (match message.TryGetProperty n with | true, v when v.ValueKind = JsonValueKind.String -> v.GetString() | _ -> "")
                    match text "type" with
                    | "interrupt" ->
                        // Thread.Interrupt ends a cell that waits (a sleep, a read) and keeps the session; a cell that only
                        // computes can't be stopped from here, and the studio ends the kernel after its grace period
                        if executing then
                            interrupted <- true
                            mainThread.Interrupt()
                    | "input_reply" ->
                        let value = (match message.TryGetProperty "value" with | true, v when v.ValueKind = JsonValueKind.String -> Some(v.GetString()) | _ -> None)
                        inputReplies.Add value
                    | "event" ->
                        let display = text "display_id"
                        let ev = (match message.TryGetProperty "event" with | true, v -> v.GetRawText() | _ -> "{}")
                        events.Enqueue(sprintf "{\"id\":%s,\"display_id\":%s,\"event\":%s}" (JsonSerializer.Serialize(text "id")) (JsonSerializer.Serialize display) ev)
                    | _ -> requests.Add message
                with _ -> ()
            line <- input.ReadLine()
        // the studio closed stdin: end
        inputReplies.Add None
        requests.CompleteAdding()), IsBackground = true)

let private version =
    let core = typeof<list<int>>.Assembly.GetName().Version
    sprintf "F# %d.%d (.NET %s)" core.Major core.Minor (Environment.Version.ToString(3))

let private run () =
    sendRaw (jobj [ "type", jstr "ready"; "language", jstr "fsharp"; "version", jstr version; "executable", jstr (Environment.ProcessPath |> Option.ofObj |> Option.defaultValue "dotnet") ])
    stdinReader.Start()
    let mutable go = true
    while go do
        try
            let mutable message = Unchecked.defaultof<JsonElement>
            if requests.TryTake(&message, 200) then
                let text (n: string) = (match message.TryGetProperty n with | true, v when v.ValueKind = JsonValueKind.String -> v.GetString() | _ -> "")
                let id = text "id"
                match text "type" with
                | "execute" -> execute id (text "code")
                | "variables" -> variables id
                | "get_value" -> getValue id (text "name")
                | "set_value" -> setValue id (text "name") (text "json")
                | "shutdown" -> go <- false
                | _ -> ()
            elif requests.IsCompleted then go <- false
            else drainEvents ()
        with
        | :? ThreadInterruptedException -> ()
    hub.Flush()
    exit 0

run ()
