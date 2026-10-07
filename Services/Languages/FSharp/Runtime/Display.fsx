namespace Fry

open System
open System.IO
open System.Text
open System.Text.Json
open System.Collections.Generic

// ── TreeNode & ListNode ───────────────────────────────────────────────────

type TreeNode<'T>(value: 'T, ?left: TreeNode<'T>, ?right: TreeNode<'T>) =
    member _.``val`` = value
    member _.left = left
    member _.right = right
    member _.Val = value
    member _.Left = left
    member _.Right = right

type ListNode<'T>(value: 'T, ?next: ListNode<'T>) =
    member _.``val`` = value
    member _.next = next
    member _.Val = value
    member _.Next = next

// ── Events & Handles ──────────────────────────────────────────────────────

type Event =
    { Kind: string
      Target: Map<string, string>
      Raw: string }
    member this.Get(key: string) =
        this.Target.TryFind(key)
    member this.Item with get(key: string) = this.Target.TryFind(key)

module private Detail =
    let CHART_MIME = "application/vnd.fry.chart.v1+json"
    let PLOT3D_MIME = "application/vnd.fry.plot3d.v1+json"
    let VISUALIZER_MIME = "application/vnd.fry.visualizer.v1+json"
    let TABLE_MIME = "application/vnd.fry.table+json"

    let private idCounter = ref 1L
    let newId() =
        let n = System.Threading.Interlocked.Increment(idCounter)
        sprintf "fs_%d_%x" n (n * 7919L)

    let listenersLock = obj()
    let listeners = Dictionary<string, Dictionary<string, List<Event -> unit>>>()

    let dispatchEventLine (line: string) =
        try
            using (JsonDocument.Parse(line)) (fun doc ->
                let root = doc.RootElement
                let mutable dispIdElem = Unchecked.defaultof<JsonElement>
                if root.TryGetProperty("display_id", &dispIdElem) then
                    let dispId = dispIdElem.GetString()
                    let mutable evKind = "click"
                    let mutable targetMap = Map.empty

                    let mutable evElem = Unchecked.defaultof<JsonElement>
                    if root.TryGetProperty("event", &evElem) then
                        if evElem.ValueKind = JsonValueKind.Object then
                            let mutable kindElem = Unchecked.defaultof<JsonElement>
                            if evElem.TryGetProperty("kind", &kindElem) then
                                evKind <- kindElem.GetString().ToLowerInvariant()
                            let mutable targetElem = Unchecked.defaultof<JsonElement>
                            if evElem.TryGetProperty("target", &targetElem) && targetElem.ValueKind = JsonValueKind.Object then
                                for prop in targetElem.EnumerateObject() do
                                    let vStr =
                                        match prop.Value.ValueKind with
                                        | JsonValueKind.String -> prop.Value.GetString()
                                        | JsonValueKind.Number -> prop.Value.GetRawText()
                                        | JsonValueKind.True -> "true"
                                        | JsonValueKind.False -> "false"
                                        | _ -> prop.Value.GetRawText()
                                    targetMap <- targetMap.Add(prop.Name, vStr)
                        elif evElem.ValueKind = JsonValueKind.String then
                            evKind <- evElem.GetString().ToLowerInvariant()

                    let event = { Kind = evKind; Target = targetMap; Raw = line }

                    let callbacks =
                        lock listenersLock (fun () ->
                            match listeners.TryGetValue(dispId) with
                            | true, dict ->
                                let cbs = List<Event -> unit>()
                                match dict.TryGetValue(evKind) with
                                | true, l -> cbs.AddRange(l)
                                | false, _ -> ()
                                if evKind <> "click" then
                                    match dict.TryGetValue("click") with
                                    | true, l -> if cbs.Count = 0 then cbs.AddRange(l)
                                    | false, _ -> ()
                                cbs.ToArray()
                            | false, _ -> Array.empty
                        )
                    for cb in callbacks do
                        try cb event with _ -> ()
            )
        with _ -> ()

    let mutable private socketInitialized = 0

    let ensureEventSocket() =
        if System.Threading.Interlocked.CompareExchange(&socketInitialized, 1, 0) = 0 then
            let addr = Environment.GetEnvironmentVariable("FRY_EVENTS")
            if not (String.IsNullOrEmpty(addr)) then
                let token = Environment.GetEnvironmentVariable("FRY_EVENTS_TOKEN")
                let tokenStr = if isNull token then "" else token
                try
                    let parts = addr.Split(':')
                    let host = if parts.Length > 0 then parts.[0] else "127.0.0.1"
                    let port = if parts.Length > 1 then Int32.Parse(parts.[1]) else 0
                    if port > 0 then
                        let client = new System.Net.Sockets.TcpClient()
                        client.Connect(host, port)
                        let stream = client.GetStream()
                        let writer = new StreamWriter(stream, new UTF8Encoding(false))
                        writer.AutoFlush <- true
                        let reader = new StreamReader(stream, Encoding.UTF8)
                        let hello = sprintf "{\"type\":\"hello\",\"token\":%s}" (JsonSerializer.Serialize(tokenStr))
                        writer.WriteLine(hello)
                        writer.Flush()

                        let rec readLoop() =
                            async {
                                try
                                    let! line = reader.ReadLineAsync() |> Async.AwaitTask
                                    if not (isNull line) then
                                        if line.Trim().Length > 0 then
                                            dispatchEventLine (line.Trim())
                                        do! readLoop()
                                with _ -> ()
                            }
                        Async.Start(readLoop())
                with _ -> ()

    let emitProtocol (json: string) =
        printfn "%s" json
        stdout.Flush()

    let emitUpdate (mime: string) (specJson: string) (displayId: string) =
        let fallback = mime + ": visual (updated)"
        let json = sprintf "__FRY_DISPLAY__ {\"type\":\"update_display\",\"data\":{\"%s\":%s,\"text/plain\":%s},\"metadata\":{},\"transient\":{\"display_id\":%s}}"
                       mime specJson (JsonSerializer.Serialize(fallback)) (JsonSerializer.Serialize(displayId))
        emitProtocol json

    let replaceOrInsertTitle (spec: string) (title: string) =
        let titleIdx = spec.IndexOf("\"title\":")
        if titleIdx <> -1 then
            let valStart = spec.IndexOf('"', titleIdx + 8)
            if valStart <> -1 then
                let mutable valEnd = spec.IndexOf('"', valStart + 1)
                while valEnd <> -1 && spec.[valEnd - 1] = '\\' do
                    valEnd <- spec.IndexOf('"', valEnd + 1)
                if valEnd <> -1 then
                    spec.Substring(0, valStart) + JsonSerializer.Serialize(title) + spec.Substring(valEnd + 1)
                else spec
            else spec
        else
            let lastBrace = spec.LastIndexOf('}')
            if lastBrace <> -1 then
                let prefix = spec.Substring(0, lastBrace)
                let sep = if prefix.Contains(":") then ",\"title\":" else "\"title\":"
                prefix + sep + JsonSerializer.Serialize(title) + "}"
            else spec

    let rec toJsonVal (o: obj) : string =
        match o with
        | null -> "null"
        | :? string as s -> JsonSerializer.Serialize(s)
        | :? bool as b -> if b then "true" else "false"
        | :? double as d ->
            if Double.IsNaN(d) || Double.IsInfinity(d) then "null"
            else d.ToString(System.Globalization.CultureInfo.InvariantCulture)
        | :? float32 as f ->
            let d = double f
            if Double.IsNaN(d) || Double.IsInfinity(d) then "null"
            else d.ToString(System.Globalization.CultureInfo.InvariantCulture)
        | :? decimal as dec -> dec.ToString(System.Globalization.CultureInfo.InvariantCulture)
        | :? int as i -> string i
        | :? int64 as l -> string l
        | :? int16 as s -> string s
        | :? byte as b -> string b
        | _ ->
            let t = o.GetType()
            if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>> then
                let valueProp = t.GetProperty("Value")
                if isNull o then "null"
                else
                    let tagProp = t.GetProperty("Tag")
                    let isSome = if isNull tagProp then not (isNull o) else (tagProp.GetValue(o) :?> int) = 1
                    if isSome then toJsonVal (valueProp.GetValue(o)) else "null"
            elif Microsoft.FSharp.Reflection.FSharpType.IsTuple(t) then
                let fields = Microsoft.FSharp.Reflection.FSharpValue.GetTupleFields(o)
                let sb = StringBuilder("[")
                let mutable first = true
                for f in fields do
                    if not first then sb.Append(",") |> ignore
                    first <- false
                    sb.Append(toJsonVal f) |> ignore
                sb.Append("]").ToString()
            elif typeof<System.Collections.IEnumerable>.IsAssignableFrom(t) then
                let items = (o :?> System.Collections.IEnumerable)
                let sb = StringBuilder("[")
                let mutable first = true
                for item in items do
                    if not first then sb.Append(",") |> ignore
                    first <- false
                    sb.Append(toJsonVal item) |> ignore
                sb.Append("]").ToString()
            else
                JsonSerializer.Serialize(string o)

    let toJsonArray (items: seq<'T>) : string =
        let sb = StringBuilder("[")
        let mutable first = true
        for item in items do
            if not first then sb.Append(",") |> ignore
            first <- false
            sb.Append(toJsonVal (box item)) |> ignore
        sb.Append("]").ToString()

    let tryGetKeyValue (item: obj) : (string * obj) option =
        if isNull item then None
        else
            let t = item.GetType()
            if Microsoft.FSharp.Reflection.FSharpType.IsTuple(t) then
                let fields = Microsoft.FSharp.Reflection.FSharpValue.GetTupleFields(item)
                if fields.Length >= 2 then
                    Some (string fields.[0], fields.[1])
                else None
            else
                let keyProp = t.GetProperty("Key")
                let valProp = t.GetProperty("Value")
                if not (isNull keyProp) && not (isNull valProp) then
                    Some (string (keyProp.GetValue(item)), valProp.GetValue(item))
                else None

    let tryGetDictionaryEntries (data: obj) : (string * obj) list option =
        if isNull data then None
        else
            let t = data.GetType()
            if typeof<System.Collections.IDictionary>.IsAssignableFrom(t) then
                let dict = data :?> System.Collections.IDictionary
                let list = [ for k in dict.Keys -> (string k, dict.[k]) ]
                Some list
            elif typeof<System.Collections.IEnumerable>.IsAssignableFrom(t) && not (data :? string) then
                let items = (data :?> System.Collections.IEnumerable) |> Seq.cast<obj> |> Seq.toList
                if items.IsEmpty then None
                else
                    let kvs = items |> List.choose tryGetKeyValue
                    if kvs.Length = items.Length then
                        Some kvs
                    else None
            else None

    type InternalTreeNode =
        { Id: string
          Value: string
          Left: string
          Right: string }

    type BNode =
        { Value: string
          mutable Left: BNode option
          mutable Right: BNode option }

open Detail

type DisplayHandle(mime: string, displayId: string, initialSpec: string) =
    let specLock = obj()
    let mutable currentSpec = initialSpec
    let mutable lastUpdate = DateTime.MinValue

    member _.Mime = mime
    member _.DisplayId = displayId

    member this.Update(title: string) =
        let newSpec =
            lock specLock (fun () ->
                currentSpec <- replaceOrInsertTitle currentSpec title
                currentSpec)
        let now = DateTime.UtcNow
        let elapsed = (now - lastUpdate).TotalMilliseconds
        if elapsed >= 33.0 then
            lastUpdate <- now
            emitUpdate mime newSpec displayId
        else
            let waitMs = int (33.0 - elapsed)
            Async.Start(async {
                do! Async.Sleep waitMs
                lastUpdate <- DateTime.UtcNow
                emitUpdate mime newSpec displayId
            })
        this

    member this.update(title: string) = this.Update(title)

    member this.On(eventName: string, callback: Event -> unit) =
        ensureEventSocket()
        let ev = eventName.ToLowerInvariant()
        lock listenersLock (fun () ->
            let dict =
                match listeners.TryGetValue(displayId) with
                | true, d -> d
                | false, _ ->
                    let d = Dictionary<string, List<Event -> unit>>()
                    listeners.[displayId] <- d
                    d
            let list =
                match dict.TryGetValue(ev) with
                | true, l -> l
                | false, _ ->
                    let l = List<Event -> unit>()
                    dict.[ev] <- l
                    l
            list.Add(callback)
        )
        printfn "__FRY_DISPLAY__ {\"type\":\"subscribe\",\"display_id\":\"%s\",\"events\":[\"%s\"]}" displayId ev
        stdout.Flush()
        this

    member this.on(eventName: string, callback: Event -> unit) = this.On(eventName, callback)
    member this.OnClick(callback: Event -> unit) = this.On("click", callback)
    member this.on_click(callback: Event -> unit) = this.On("click", callback)
    member this.OnSelect(callback: Event -> unit) = this.On("select", callback)
    member this.on_select(callback: Event -> unit) = this.On("select", callback)
    member this.OnStep(callback: Event -> unit) = this.On("step", callback)
    member this.on_step(callback: Event -> unit) = this.On("step", callback)

    member this.Off(eventName: string) =
        let ev = eventName.ToLowerInvariant()
        lock listenersLock (fun () ->
            match listeners.TryGetValue(displayId) with
            | true, d -> d.Remove(ev) |> ignore
            | false, _ -> ()
        )
        printfn "__FRY_DISPLAY__ {\"type\":\"unsubscribe\",\"display_id\":\"%s\",\"events\":[\"%s\"]}" displayId ev
        this

    member this.off(eventName: string) = this.Off(eventName)

    member this.Close() =
        lock listenersLock (fun () ->
            listeners.Remove(displayId) |> ignore
        )
    member this.close() = this.Close()

    member this.Show() = this
    member this.show() = this

module private Helpers =
    let emitDisplay (mime: string) (specJson: string) : DisplayHandle =
        let id = newId()
        let fallback = mime + ": visual"
        let json = sprintf "__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"%s\":%s,\"text/plain\":%s},\"metadata\":{},\"transient\":{\"display_id\":%s}}"
                       mime specJson (JsonSerializer.Serialize(fallback)) (JsonSerializer.Serialize(id))
        emitProtocol json
        DisplayHandle(mime, id, specJson)

type CanvasVisualizer(title: string, width: int, height: int) =
    let shapes = List<string>()

    member this.AddRect(x: float, y: float, w: float, h: float, ?label: string, ?fill: string, ?stroke: string) =
        let sb = StringBuilder(sprintf "{\"type\":\"rect\",\"x\":%s,\"y\":%s,\"width\":%s,\"height\":%s"
                                  (x.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (y.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (w.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (h.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        label |> Option.iter (fun l -> sb.Append(sprintf ",\"label\":%s" (JsonSerializer.Serialize(l))) |> ignore)
        fill |> Option.iter (fun f -> sb.Append(sprintf ",\"fill\":%s" (JsonSerializer.Serialize(f))) |> ignore)
        stroke |> Option.iter (fun s -> sb.Append(sprintf ",\"stroke\":%s" (JsonSerializer.Serialize(s))) |> ignore)
        sb.Append("}") |> ignore
        shapes.Add(sb.ToString())
        this

    member this.add_rect(x: float, y: float, w: float, h: float, ?label: string, ?fill: string, ?stroke: string) =
        this.AddRect(x, y, w, h, ?label = label, ?fill = fill, ?stroke = stroke)

    member this.AddArrow(x1: float, y1: float, x2: float, y2: float, ?label: string, ?stroke: string) =
        let sb = StringBuilder(sprintf "{\"type\":\"arrow\",\"x1\":%s,\"y1\":%s,\"x2\":%s,\"y2\":%s"
                                  (x1.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (y1.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (x2.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (y2.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        label |> Option.iter (fun l -> sb.Append(sprintf ",\"label\":%s" (JsonSerializer.Serialize(l))) |> ignore)
        stroke |> Option.iter (fun s -> sb.Append(sprintf ",\"stroke\":%s" (JsonSerializer.Serialize(s))) |> ignore)
        sb.Append("}") |> ignore
        shapes.Add(sb.ToString())
        this

    member this.add_arrow(x1: float, y1: float, x2: float, y2: float, ?label: string, ?stroke: string) =
        this.AddArrow(x1, y1, x2, y2, ?label = label, ?stroke = stroke)

    member this.AddCircle(cx: float, cy: float, radius: float, ?label: string, ?fill: string, ?stroke: string) =
        let sb = StringBuilder(sprintf "{\"type\":\"circle\",\"cx\":%s,\"cy\":%s,\"radius\":%s"
                                  (cx.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (cy.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (radius.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        label |> Option.iter (fun l -> sb.Append(sprintf ",\"label\":%s" (JsonSerializer.Serialize(l))) |> ignore)
        fill |> Option.iter (fun f -> sb.Append(sprintf ",\"fill\":%s" (JsonSerializer.Serialize(f))) |> ignore)
        stroke |> Option.iter (fun s -> sb.Append(sprintf ",\"stroke\":%s" (JsonSerializer.Serialize(s))) |> ignore)
        sb.Append("}") |> ignore
        shapes.Add(sb.ToString())
        this

    member this.add_circle(cx: float, cy: float, radius: float, ?label: string, ?fill: string, ?stroke: string) =
        this.AddCircle(cx, cy, radius, ?label = label, ?fill = fill, ?stroke = stroke)

    member this.AddLine(x1: float, y1: float, x2: float, y2: float, ?stroke: string) =
        let sb = StringBuilder(sprintf "{\"type\":\"line\",\"x1\":%s,\"y1\":%s,\"x2\":%s,\"y2\":%s"
                                  (x1.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (y1.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (x2.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (y2.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        stroke |> Option.iter (fun s -> sb.Append(sprintf ",\"stroke\":%s" (JsonSerializer.Serialize(s))) |> ignore)
        sb.Append("}") |> ignore
        shapes.Add(sb.ToString())
        this

    member this.add_line(x1: float, y1: float, x2: float, y2: float, ?stroke: string) =
        this.AddLine(x1, y1, x2, y2, ?stroke = stroke)

    member this.AddText(x: float, y: float, text: string, ?fontSize: int, ?color: string) =
        let fs = defaultArg fontSize 14
        let sb = StringBuilder(sprintf "{\"type\":\"text\",\"x\":%s,\"y\":%s,\"text\":%s,\"fontSize\":%d"
                                  (x.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (y.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (JsonSerializer.Serialize(text)) fs)
        color |> Option.iter (fun c -> sb.Append(sprintf ",\"color\":%s" (JsonSerializer.Serialize(c))) |> ignore)
        sb.Append("}") |> ignore
        shapes.Add(sb.ToString())
        this

    member this.add_text(x: float, y: float, text: string, ?fontSize: int, ?color: string) =
        this.AddText(x, y, text, ?fontSize = fontSize, ?color = color)

    member this.Show() : DisplayHandle =
        let shapesJson = String.Join(",", shapes)
        let sb = StringBuilder(sprintf "{\"kind\":\"canvas\",\"state\":{\"canvas\":{\"width\":%d,\"height\":%d,\"shapes\":[%s]}}" width height shapesJson)
        if not (String.IsNullOrEmpty(title)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(title)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay Detail.VISUALIZER_MIME (sb.ToString())

    member this.show() = this.Show()

// ── Newer chart features: combos, stacks, scales, line styles, bubbles, radial kinds ──────────────────────────

module private Num =
    let text (v: float) = v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)

/// A series' values and how it is drawn: Display.series(margin).Kind("line").RightAxis().Dash("dashed").
type SeriesData(values: obj) =
    let fields = List<string * string>()
    let set (key: string) (json: string) =
        fields.RemoveAll(fun (k, _) -> k = key) |> ignore
        fields.Add((key, json))
    member _.Values = values
    member _.Fields : seq<string * string> = fields :> seq<_>
    /// Draws this series as another kind than the chart's: line, area, bar or scatter.
    member this.Kind(k: string) = set "kind" (JsonSerializer.Serialize k); this
    /// Measures this series on the second value axis, up the right side.
    member this.RightAxis() = set "axis" "\"right\""; this
    member this.Color(c: string) = set "color" (JsonSerializer.Serialize c); this
    member this.LineWidth(pixels: float) = set "lineWidth" (Num.text pixels); this
    /// "dashed" or "dotted".
    member this.Dash(d: string) = set "dash" (JsonSerializer.Serialize d); this
    /// A curve through the values; tension is from 0 to 1.
    member this.Smooth(tension: float) = set "interpolation" "\"smooth\""; set "tension" (Num.text tension); this
    member this.Monotone() = set "interpolation" "\"monotone\""; this
    /// "before", "after" or "middle".
    member this.Step(s: string) = set "step" (JsonSerializer.Serialize s); this
    member this.Fill(on: bool) = set "fill" (if on then "true" else "false"); this
    member this.FillTo(series: int) = set "fillTo" (string series); this
    /// "circle", "triangle", "square", "diamond", "cross" or "star", and the marker radius.
    member this.PointStyle(shape: string, radius: float) = set "pointStyle" (JsonSerializer.Serialize shape); set "pointRadius" (Num.text radius); this
    member this.ColorSegments() = set "colorSegments" "true"; this
    member this.Stack(name: string) = set "stack" (JsonSerializer.Serialize name); this
    member this.CornerRadius(pixels: float) = set "cornerRadius" (Num.text pixels); this

/// What a chart is told besides its data: ChartStyle().Title("Sales").Labels(months).Stacked().Y2Axis("%", 0.0, 100.0).
type ChartStyle() =
    let top = List<string * string>()
    let axes = List<string * string * string>()
    let mutable title = ""
    let mutable labels : string list = []
    let setTop (key: string) (json: string) =
        top.RemoveAll(fun (k, _) -> k = key) |> ignore
        top.Add((key, json))
    let setAxis (axis: string) (field: string) (json: string) =
        axes.RemoveAll(fun (a, f, _) -> a = axis && f = field) |> ignore
        axes.Add((axis, field, json))
    member _.TitleText = title
    member _.LabelNames = labels
    member _.Top : seq<string * string> = top :> seq<_>
    member _.Axes : seq<string * string * string> = axes :> seq<_>
    member this.Title(t: string) = title <- t; this
    /// Names the places of the values: x positions, a radar's spokes, a pie's slices.
    member this.Labels(names: seq<string>) = labels <- List.ofSeq names; this
    member this.Stacked() = setTop "stack" "\"stacked\""; this
    member this.PercentStacked() = setTop "stack" "\"percent\""; this
    member this.Horizontal() = setTop "orientation" "\"horizontal\""; this
    member this.Gauge() = setTop "startAngle" "-90"; setTop "sweep" "180"; this
    member this.Angles(start: float, sweep: float) = setTop "startAngle" (Num.text start); setTop "sweep" (Num.text sweep); this
    member this.Cutout(share: float) = setTop "cutout" (Num.text share); this
    /// "top", "bottom", "left" or "right".
    member this.LegendAt(position: string) = setTop "legend" ("{\"position\":" + JsonSerializer.Serialize position + "}"); this
    /// A second value axis, up the right side, for the series set RightAxis().
    member this.Y2Axis(title: string, min: float, max: float) =
        setAxis "y2Axis" "title" (JsonSerializer.Serialize title)
        setAxis "y2Axis" "min" (Num.text min)
        setAxis "y2Axis" "max" (Num.text max)
        this
    /// "linear", "log", "time" (x only) or "category" (x only).
    member this.XScale(scale: string) = setAxis "xAxis" "scale" (JsonSerializer.Serialize scale); this
    member this.YScale(scale: string) = setAxis "yAxis" "scale" (JsonSerializer.Serialize scale); this
    member this.SuggestedY(min: float, max: float) =
        setAxis "yAxis" "suggestedMin" (Num.text min)
        setAxis "yAxis" "suggestedMax" (Num.text max)
        this
    member this.ReverseX() = setAxis "xAxis" "reverse" "true"; this
    member this.ReverseY() = setAxis "yAxis" "reverse" "true"; this

module private ChartJson =
    type One =
        { Name: string option
          X: string
          Y: string
          Labels: string
          Sizes: string
          Count: int
          Fields: seq<string * string> }

    let private isSeq (v: obj) = not (isNull v) && not (v :? string) && (v :? System.Collections.IEnumerable)
    let private items (v: obj) = (v :?> System.Collections.IEnumerable) |> Seq.cast<obj> |> Seq.toArray

    // (x, y, size) items: tuples or sequences of three numbers.
    let private bubble (name: string option) (values: obj) (fields: seq<string * string>) : One =
        let rows =
            items values
            |> Array.map (fun item ->
                let parts =
                    if isNull item then [||]
                    elif Microsoft.FSharp.Reflection.FSharpType.IsTuple(item.GetType()) then Microsoft.FSharp.Reflection.FSharpValue.GetTupleFields item
                    elif isSeq item then items item
                    else [||]
                if parts.Length >= 3 then [| toJsonVal parts.[0]; toJsonVal parts.[1]; toJsonVal parts.[2] |] else [| "null"; "null"; "null" |])
        let column (k: int) = "[" + String.Join(",", rows |> Array.map (fun r -> r.[k])) + "]"
        { Name = name; X = column 0; Y = column 1; Labels = ""; Sizes = column 2; Count = rows.Length; Fields = fields }

    let build (kind: string) (data: obj) (style: ChartStyle) : string =
        let pairs =
            match tryGetDictionaryEntries data with
            | Some entries -> Some entries
            | None when isSeq data ->
                let found = items data |> Array.map tryGetKeyValue
                if found.Length > 0 && found |> Array.forall Option.isSome then Some (found |> Array.map Option.get |> List.ofArray) else None
            | None -> None
        let isSeries (v: obj) = (v :? SeriesData) || isSeq v
        let series : One list =
            if kind = "bubble" then
                match pairs with
                | Some entries when entries |> List.forall (fun (_, v) -> isSeq v) && not (items data |> Array.exists (fun i -> not (isNull i) && Microsoft.FSharp.Reflection.FSharpType.IsTuple(i.GetType()) && (Microsoft.FSharp.Reflection.FSharpValue.GetTupleFields i).Length >= 3)) ->
                    [ for (k, v) in entries -> bubble (Some k) v Seq.empty ]
                | _ -> [ bubble None data Seq.empty ]
            else
                match pairs with
                | Some entries when entries |> List.exists (fun (_, v) -> isSeries v) ->
                    [ for (k, v) in entries ->
                        let values, fields = match v with | :? SeriesData as sd -> sd.Values, sd.Fields | _ -> v, Seq.empty
                        let ys = items values
                        { Name = Some k; X = ""; Y = toJsonArray ys; Labels = ""; Sizes = ""; Count = ys.Length; Fields = fields } ]
                | Some entries ->
                    [ { Name = None
                        X = ""
                        Y = toJsonArray (entries |> List.map snd)
                        Labels = toJsonArray (entries |> List.map fst)
                        Sizes = ""
                        Count = entries.Length
                        Fields = Seq.empty } ]
                | None when isSeq data ->
                    let ys = items data
                    [ { Name = None; X = ""; Y = toJsonArray ys; Labels = ""; Sizes = ""; Count = ys.Length; Fields = Seq.empty } ]
                | None -> []
        let sb = StringBuilder("{\"kind\":")
        sb.Append(JsonSerializer.Serialize kind).Append(",\"series\":[") |> ignore
        series
        |> List.iteri (fun i one ->
            if i > 0 then sb.Append(",") |> ignore
            sb.Append("{") |> ignore
            let parts = List<string>()
            match one.Name with
            | Some n -> parts.Add("\"name\":" + JsonSerializer.Serialize n)
            | None -> ()
            if one.X <> "" then parts.Add("\"x\":" + one.X)
            parts.Add("\"y\":" + one.Y)
            if one.Labels <> "" then parts.Add("\"labels\":" + one.Labels)
            elif not style.LabelNames.IsEmpty && style.LabelNames.Length = one.Count then
                parts.Add("\"labels\":" + toJsonArray style.LabelNames)
            if one.Sizes <> "" then parts.Add("\"sizes\":" + one.Sizes)
            for (key, json) in one.Fields do parts.Add("\"" + key + "\":" + json)
            sb.Append(String.Join(",", parts)).Append("}") |> ignore)
        sb.Append("]") |> ignore
        if style.TitleText <> "" then sb.Append(",\"title\":").Append(JsonSerializer.Serialize style.TitleText) |> ignore
        for (key, json) in style.Top do sb.Append(",\"").Append(key).Append("\":").Append(json) |> ignore
        for axis in [ "xAxis"; "yAxis"; "y2Axis" ] do
            let fields = style.Axes |> Seq.filter (fun (a, _, _) -> a = axis) |> Seq.map (fun (_, f, v) -> "\"" + f + "\":" + v) |> Seq.toList
            if not fields.IsEmpty then sb.Append(",\"").Append(axis).Append("\":{").Append(String.Join(",", fields)).Append("}") |> ignore
        sb.Append("}").ToString()

type Display private () =
    // ── Basic Display Primitives ──────────────────────────────────────────

    static member Html(htmlContent: string) =
        let json = sprintf "__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"text/html\":%s},\"metadata\":{}}"
                       (JsonSerializer.Serialize(htmlContent))
        Detail.emitProtocol json

    static member html(content: string) = Display.Html(content)

    static member Image(pathOrBase64: string, ?format: string) =
        let fmt = defaultArg format "PNG"
        let mutable b64 = pathOrBase64
        let mutable mime = if fmt.Equals("JPEG", StringComparison.OrdinalIgnoreCase) || fmt.Equals("JPG", StringComparison.OrdinalIgnoreCase) then "image/jpeg" else "image/png"

        if b64.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) then
            let idx = b64.IndexOf(',')
            if idx <> -1 then b64 <- b64.Substring(idx + 1)
        elif File.Exists(pathOrBase64) then
            let bytes = File.ReadAllBytes(pathOrBase64)
            b64 <- Convert.ToBase64String(bytes)
            let lower = pathOrBase64.ToLowerInvariant()
            if lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") then mime <- "image/jpeg"
            elif lower.EndsWith(".svg") then mime <- "image/svg+xml"

        let json = sprintf "__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"%s\":\"%s\"},\"metadata\":{}}" mime b64
        Detail.emitProtocol json

    static member image(pathOrBase64: string, ?format: string) = Display.Image(pathOrBase64, ?format = format)

    static member Json(jsonString: string, ?title: string) =
        let json = sprintf "__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"text/plain\":%s},\"metadata\":{}}"
                       (JsonSerializer.Serialize(jsonString))
        Detail.emitProtocol json

    static member json(jsonString: string, ?title: string) = Display.Json(jsonString, ?title = title)

    static member Dump(value: obj, ?title: string) =
        try
            let t = defaultArg title "Data"
            let options = JsonSerializerOptions(WriteIndented = true)
            let j = JsonSerializer.Serialize(value, options)
            Display.Json(j, t)
        with _ ->
            printfn "%A" value
        value

    static member dump(value: obj, ?title: string) = Display.Dump(value, ?title = title)

    static member Table(value: obj, ?title: string) =
        Display.Dump(value, ?title = title) |> ignore

    static member table(value: obj, ?title: string) = Display.Table(value, ?title = title)

    // ── Canonical Visual API ──────────────────────────────────────────────

    // 1. Line Chart
    static member LineChart(data: seq<'T>, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let sb = StringBuilder("{\"kind\":\"line\",\"series\":[")
        let mutable isTuple = false
        let firstOpt = data |> Seq.tryHead
        match firstOpt with
        | Some firstItem ->
            let t = (box firstItem).GetType()
            if Microsoft.FSharp.Reflection.FSharpType.IsTuple(t) then
                isTuple <- true
        | None -> ()

        if isTuple then
            let xs = StringBuilder("[")
            let ys = StringBuilder("[")
            let mutable first = true
            for pt in data do
                if not first then
                    xs.Append(",") |> ignore
                    ys.Append(",") |> ignore
                first <- false
                let fields = Microsoft.FSharp.Reflection.FSharpValue.GetTupleFields(box pt)
                if fields.Length >= 2 then
                    xs.Append(toJsonVal fields.[0]) |> ignore
                    ys.Append(toJsonVal fields.[1]) |> ignore
                elif fields.Length = 1 then
                    ys.Append(toJsonVal fields.[0]) |> ignore
            xs.Append("]") |> ignore
            ys.Append("]") |> ignore
            sb.Append(sprintf "{\"x\":%s,\"y\":%s}" (xs.ToString()) (ys.ToString())) |> ignore
        else
            sb.Append("{\"y\":") |> ignore
            sb.Append(toJsonArray data) |> ignore
            sb.Append("}") |> ignore
        sb.Append("]") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay CHART_MIME (sb.ToString())

    static member line_chart(data: seq<'T>, ?title: string) = Display.LineChart(data, ?title = title)
    static member lineChart(data: seq<'T>, ?title: string) = Display.LineChart(data, ?title = title)

    // 2. Scatter Chart
    static member ScatterChart(data: seq<'T>, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let xs = StringBuilder("[")
        let ys = StringBuilder("[")
        let mutable first = true
        for pt in data do
            if not first then
                xs.Append(",") |> ignore
                ys.Append(",") |> ignore
            first <- false
            let ptObj = box pt
            let t = ptObj.GetType()
            if Microsoft.FSharp.Reflection.FSharpType.IsTuple(t) then
                let fields = Microsoft.FSharp.Reflection.FSharpValue.GetTupleFields(ptObj)
                xs.Append(toJsonVal fields.[0]) |> ignore
                ys.Append(toJsonVal fields.[1]) |> ignore
            elif typeof<System.Collections.IEnumerable>.IsAssignableFrom(t) then
                let enum = (ptObj :?> System.Collections.IEnumerable).GetEnumerator()
                if enum.MoveNext() then xs.Append(toJsonVal enum.Current) |> ignore
                if enum.MoveNext() then ys.Append(toJsonVal enum.Current) |> ignore
        xs.Append("]") |> ignore
        ys.Append("]") |> ignore

        let sb = StringBuilder("{\"kind\":\"scatter\",\"series\":[{\"x\":")
        sb.Append(xs.ToString()) |> ignore
        sb.Append(",\"y\":") |> ignore
        sb.Append(ys.ToString()) |> ignore
        sb.Append("}]") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay CHART_MIME (sb.ToString())

    static member scatter_chart(data: seq<'T>, ?title: string) = Display.ScatterChart(data, ?title = title)
    static member scatterChart(data: seq<'T>, ?title: string) = Display.ScatterChart(data, ?title = title)

    // 3. Bar Chart
    static member BarChart(data: seq<'T>, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let ys = StringBuilder("[")
        let ls = StringBuilder("[")
        let mutable first = true
        let mutable hasLabels = false
        for item in data do
            if not first then
                ys.Append(",") |> ignore
            first <- false
            let itemObj = box item
            let t = itemObj.GetType()
            match tryGetKeyValue itemObj with
            | Some (k, v) ->
                if hasLabels then ls.Append(",") |> ignore
                hasLabels <- true
                ls.Append(toJsonVal k) |> ignore
                ys.Append(toJsonVal v) |> ignore
            | None ->
                if Microsoft.FSharp.Reflection.FSharpType.IsTuple(t) then
                    let fields = Microsoft.FSharp.Reflection.FSharpValue.GetTupleFields(itemObj)
                    if hasLabels then ls.Append(",") |> ignore
                    hasLabels <- true
                    ls.Append(toJsonVal fields.[0]) |> ignore
                    ys.Append(toJsonVal fields.[1]) |> ignore
                elif Microsoft.FSharp.Reflection.FSharpType.IsRecord(t) then
                    let fields = Microsoft.FSharp.Reflection.FSharpType.GetRecordFields(t)
                    let values = Microsoft.FSharp.Reflection.FSharpValue.GetRecordFields(itemObj)
                    let nameIdx = fields |> Array.tryFindIndex (fun f -> f.Name.Equals("name", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("label", StringComparison.OrdinalIgnoreCase))
                    let valIdx = fields |> Array.tryFindIndex (fun f -> f.Name.Equals("value", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("y", StringComparison.OrdinalIgnoreCase))
                    let nVal = match nameIdx with Some i -> values.[i] | None -> values.[0]
                    let vVal = match valIdx with Some i -> values.[i] | None -> values.[1]
                    if hasLabels then ls.Append(",") |> ignore
                    hasLabels <- true
                    ls.Append(toJsonVal nVal) |> ignore
                    ys.Append(toJsonVal vVal) |> ignore
                else
                    ys.Append(toJsonVal itemObj) |> ignore
        ys.Append("]") |> ignore
        ls.Append("]") |> ignore

        let sb = StringBuilder("{\"kind\":\"bar\",\"series\":[{\"y\":")
        sb.Append(ys.ToString()) |> ignore
        if hasLabels then
            sb.Append(",\"labels\":") |> ignore
            sb.Append(ls.ToString()) |> ignore
        sb.Append("}]") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay CHART_MIME (sb.ToString())

    static member bar_chart(data: seq<'T>, ?title: string) = Display.BarChart(data, ?title = title)
    static member barChart(data: seq<'T>, ?title: string) = Display.BarChart(data, ?title = title)

    // 4. Pie Chart
    static member PieChart(data: seq<'T>, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let ys = StringBuilder("[")
        let ls = StringBuilder("[")
        let mutable first = true
        let mutable hasLabels = false
        for item in data do
            if not first then
                ys.Append(",") |> ignore
            first <- false
            let itemObj = box item
            let t = itemObj.GetType()
            match tryGetKeyValue itemObj with
            | Some (k, v) ->
                if hasLabels then ls.Append(",") |> ignore
                hasLabels <- true
                ls.Append(toJsonVal k) |> ignore
                ys.Append(toJsonVal v) |> ignore
            | None ->
                if Microsoft.FSharp.Reflection.FSharpType.IsTuple(t) then
                    let fields = Microsoft.FSharp.Reflection.FSharpValue.GetTupleFields(itemObj)
                    if hasLabels then ls.Append(",") |> ignore
                    hasLabels <- true
                    ls.Append(toJsonVal fields.[0]) |> ignore
                    ys.Append(toJsonVal fields.[1]) |> ignore
                elif Microsoft.FSharp.Reflection.FSharpType.IsRecord(t) then
                    let fields = Microsoft.FSharp.Reflection.FSharpType.GetRecordFields(t)
                    let values = Microsoft.FSharp.Reflection.FSharpValue.GetRecordFields(itemObj)
                    let nameIdx = fields |> Array.tryFindIndex (fun f -> f.Name.Equals("name", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("label", StringComparison.OrdinalIgnoreCase))
                    let valIdx = fields |> Array.tryFindIndex (fun f -> f.Name.Equals("value", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("y", StringComparison.OrdinalIgnoreCase))
                    let nVal = match nameIdx with Some i -> values.[i] | None -> values.[0]
                    let vVal = match valIdx with Some i -> values.[i] | None -> values.[1]
                    if hasLabels then ls.Append(",") |> ignore
                    hasLabels <- true
                    ls.Append(toJsonVal nVal) |> ignore
                    ys.Append(toJsonVal vVal) |> ignore
                else
                    ys.Append(toJsonVal itemObj) |> ignore
        ys.Append("]") |> ignore
        ls.Append("]") |> ignore

        let sb = StringBuilder("{\"kind\":\"pie\",\"series\":[{\"y\":")
        sb.Append(ys.ToString()) |> ignore
        if hasLabels then
            sb.Append(",\"labels\":") |> ignore
            sb.Append(ls.ToString()) |> ignore
        sb.Append("}]") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay CHART_MIME (sb.ToString())

    static member pie_chart(data: seq<'T>, ?title: string) = Display.PieChart(data, ?title = title)
    static member pieChart(data: seq<'T>, ?title: string) = Display.PieChart(data, ?title = title)

    // 5. Chart (Multi-series map or records)
    static member Chart(data: obj, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let dataObj = box data
        match tryGetDictionaryEntries dataObj with
        | Some entries when entries |> List.forall (fun (_, v) -> not (isNull v) && typeof<System.Collections.IEnumerable>.IsAssignableFrom(v.GetType()) && not (v :? string)) ->
            let sb = StringBuilder("{\"kind\":\"line\",\"series\":[")
            let mutable first = true
            for (key, valsObj) in entries do
                if not first then sb.Append(",") |> ignore
                first <- false
                let vals = (valsObj :?> System.Collections.IEnumerable) |> Seq.cast<obj>
                sb.Append("{\"name\":") |> ignore
                sb.Append(JsonSerializer.Serialize(key)) |> ignore
                sb.Append(",\"y\":") |> ignore
                sb.Append(toJsonArray vals) |> ignore
                sb.Append("}") |> ignore
            sb.Append("]") |> ignore
            if not (String.IsNullOrEmpty(titleStr)) then
                sb.Append(",\"title\":") |> ignore
                sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
            sb.Append("}") |> ignore
            Helpers.emitDisplay CHART_MIME (sb.ToString())
        | _ ->
            let t = dataObj.GetType()
            if typeof<System.Collections.IEnumerable>.IsAssignableFrom(t) then
                let items = (dataObj :?> System.Collections.IEnumerable) |> Seq.cast<obj> |> Seq.toArray
                let ys = StringBuilder("[")
                let ls = StringBuilder("[")
                let mutable hasLabels = false
                let mutable first = true
                for item in items do
                    if not first then
                        ys.Append(",") |> ignore
                        if hasLabels then ls.Append(",") |> ignore
                    first <- false
                    match tryGetKeyValue item with
                    | Some (k, v) ->
                        hasLabels <- true
                        ls.Append(toJsonVal k) |> ignore
                        ys.Append(toJsonVal v) |> ignore
                    | None ->
                        let it = item.GetType()
                        if Microsoft.FSharp.Reflection.FSharpType.IsRecord(it) then
                            let fields = Microsoft.FSharp.Reflection.FSharpType.GetRecordFields(it)
                            let values = Microsoft.FSharp.Reflection.FSharpValue.GetRecordFields(item)
                            let nameIdx = fields |> Array.tryFindIndex (fun f -> f.Name.Equals("name", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("label", StringComparison.OrdinalIgnoreCase))
                            let valIdx = fields |> Array.tryFindIndex (fun f -> f.Name.Equals("value", StringComparison.OrdinalIgnoreCase) || f.Name.Equals("y", StringComparison.OrdinalIgnoreCase))
                            let nVal = match nameIdx with Some i -> values.[i] | None -> values.[0]
                            let vVal = match valIdx with Some i -> values.[i] | None -> values.[1]
                            hasLabels <- true
                            ls.Append(toJsonVal nVal) |> ignore
                            ys.Append(toJsonVal vVal) |> ignore
                        else
                            ys.Append(toJsonVal item) |> ignore
                ys.Append("]") |> ignore
                ls.Append("]") |> ignore

                let sb = StringBuilder("{\"kind\":\"line\",\"series\":[{\"y\":")
                sb.Append(ys.ToString()) |> ignore
                if hasLabels then
                    sb.Append(",\"labels\":") |> ignore
                    sb.Append(ls.ToString()) |> ignore
                sb.Append("}]") |> ignore
                if not (String.IsNullOrEmpty(titleStr)) then
                    sb.Append(",\"title\":") |> ignore
                    sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
                sb.Append("}") |> ignore
                Helpers.emitDisplay CHART_MIME (sb.ToString())
            else
                Display.LineChart([ data ], ?title = title)

    static member chart(data: obj, ?title: string) = Display.Chart(data, ?title = title)

    // 6. Histogram
    static member Histogram(data: seq<'T>, ?title: string, ?bins: int) : DisplayHandle =
        let titleStr = defaultArg title ""
        let bVal = defaultArg bins 0
        let sb = StringBuilder("{\"kind\":\"histogram\"")
        if bVal > 0 then
            sb.Append(sprintf ",\"bins\":%d" bVal) |> ignore
        sb.Append(",\"series\":[{\"values\":") |> ignore
        sb.Append(toJsonArray data) |> ignore
        sb.Append("}]") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay CHART_MIME (sb.ToString())

    static member histogram(data: seq<'T>, ?title: string, ?bins: int) = Display.Histogram(data, ?title = title, ?bins = bins)

    // Newer chart features: each takes a ChartStyle (title, labels, stacking, axes…); a series with options is Display.series(values).
    static member Series(values: obj) : SeriesData = SeriesData(values)
    static member series(values: obj) : SeriesData = SeriesData(values)

    static member private ShowChart(kind: string, data: obj, style: ChartStyle) : DisplayHandle =
        Helpers.emitDisplay CHART_MIME (ChartJson.build kind data style)

    static member LineChart(data: obj, style: ChartStyle) = Display.ShowChart("line", data, style)
    static member lineChart(data: obj, style: ChartStyle) = Display.ShowChart("line", data, style)
    static member AreaChart(data: obj, style: ChartStyle) = Display.ShowChart("area", data, style)
    static member areaChart(data: obj, style: ChartStyle) = Display.ShowChart("area", data, style)
    static member BarChart(data: obj, style: ChartStyle) = Display.ShowChart("bar", data, style)
    static member barChart(data: obj, style: ChartStyle) = Display.ShowChart("bar", data, style)
    static member PieChart(data: obj, style: ChartStyle) = Display.ShowChart("pie", data, style)
    static member pieChart(data: obj, style: ChartStyle) = Display.ShowChart("pie", data, style)
    static member DonutChart(data: obj, style: ChartStyle) = Display.ShowChart("donut", data, style)
    static member donutChart(data: obj, style: ChartStyle) = Display.ShowChart("donut", data, style)
    /// (x, y, size) items; the size is a radius in pixels.
    static member BubbleChart(data: obj, style: ChartStyle) = Display.ShowChart("bubble", data, style)
    static member bubbleChart(data: obj, style: ChartStyle) = Display.ShowChart("bubble", data, style)
    /// A polygon for each series over a spoke for each value (name the spokes with ChartStyle.Labels).
    static member RadarChart(data: obj, style: ChartStyle) = Display.ShowChart("radar", data, style)
    static member radarChart(data: obj, style: ChartStyle) = Display.ShowChart("radar", data, style)
    static member PolarAreaChart(data: obj, style: ChartStyle) = Display.ShowChart("polarArea", data, style)
    static member polarAreaChart(data: obj, style: ChartStyle) = Display.ShowChart("polarArea", data, style)
    static member StackedBarChart(data: obj, style: ChartStyle) = Display.ShowChart("bar", data, style.Stacked())
    static member stackedBarChart(data: obj, style: ChartStyle) = Display.ShowChart("bar", data, style.Stacked())
    static member HorizontalBarChart(data: obj, style: ChartStyle) = Display.ShowChart("bar", data, style.Horizontal())
    static member horizontalBarChart(data: obj, style: ChartStyle) = Display.ShowChart("bar", data, style.Horizontal())

    // 7. Scatter 3D
    static member Scatter3D(data: seq<'T>, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let xs = StringBuilder("[")
        let ys = StringBuilder("[")
        let zs = StringBuilder("[")
        let mutable first = true
        for pt in data do
            if not first then
                xs.Append(",") |> ignore
                ys.Append(",") |> ignore
                zs.Append(",") |> ignore
            first <- false
            let ptObj = box pt
            let t = ptObj.GetType()
            if Microsoft.FSharp.Reflection.FSharpType.IsTuple(t) then
                let fields = Microsoft.FSharp.Reflection.FSharpValue.GetTupleFields(ptObj)
                xs.Append(toJsonVal fields.[0]) |> ignore
                ys.Append(toJsonVal fields.[1]) |> ignore
                zs.Append(toJsonVal fields.[2]) |> ignore
            elif typeof<System.Collections.IEnumerable>.IsAssignableFrom(t) then
                let enum = (ptObj :?> System.Collections.IEnumerable).GetEnumerator()
                if enum.MoveNext() then xs.Append(toJsonVal enum.Current) |> ignore
                if enum.MoveNext() then ys.Append(toJsonVal enum.Current) |> ignore
                if enum.MoveNext() then zs.Append(toJsonVal enum.Current) |> ignore
        xs.Append("]") |> ignore
        ys.Append("]") |> ignore
        zs.Append("]") |> ignore

        let sb = StringBuilder("{\"kind\":\"scatter\",\"series\":[{\"x\":")
        sb.Append(xs.ToString()) |> ignore
        sb.Append(",\"y\":") |> ignore
        sb.Append(ys.ToString()) |> ignore
        sb.Append(",\"z\":") |> ignore
        sb.Append(zs.ToString()) |> ignore
        sb.Append("}]") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay PLOT3D_MIME (sb.ToString())

    static member scatter3d(data: seq<'T>, ?title: string) = Display.Scatter3D(data, ?title = title)
    static member scatter3D(data: seq<'T>, ?title: string) = Display.Scatter3D(data, ?title = title)

    // 8. Surface 3D
    static member Surface3D(data: seq<#seq<'T>>, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let rows = data |> Seq.map (fun r -> r |> Seq.toArray) |> Seq.toArray
        let rowCount = rows.Length
        let colCount = if rowCount > 0 then rows.[0].Length else 0

        let maxX = if colCount > 1 then float (colCount - 1) else 1.0
        let maxY = if rowCount > 1 then float (rowCount - 1) else 1.0

        let zs = StringBuilder("[")
        let mutable firstRow = true
        for r in rows do
            if not firstRow then zs.Append(",") |> ignore
            firstRow <- false
            zs.Append(toJsonArray r) |> ignore
        zs.Append("]") |> ignore

        let sb = StringBuilder(sprintf "{\"kind\":\"surface\",\"surface\":{\"x\":{\"min\":0,\"max\":%s},\"y\":{\"min\":0,\"max\":%s},\"z\":%s}"
                                  (maxX.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (maxY.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (zs.ToString()))
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay PLOT3D_MIME (sb.ToString())

    static member surface3d(data: seq<#seq<'T>>, ?title: string) = Display.Surface3D(data, ?title = title)
    static member surface3D(data: seq<#seq<'T>>, ?title: string) = Display.Surface3D(data, ?title = title)

    // 8b. Surface 3D from Function
    static member Surface3D(title: string, func: float -> float -> float, minX: float, maxX: float, minY: float, maxY: float, ?resolution: int) : DisplayHandle =
        let n = defaultArg resolution 30 |> max 2
        let zRows = StringBuilder("[")
        for r in 0 .. n - 1 do
            if r > 0 then zRows.Append(",") |> ignore
            zRows.Append("[") |> ignore
            let y = minY + (maxY - minY) * float r / float (n - 1)
            for c in 0 .. n - 1 do
                if c > 0 then zRows.Append(",") |> ignore
                let x = minX + (maxX - minX) * float c / float (n - 1)
                let z = func x y
                zRows.Append(z.ToString(System.Globalization.CultureInfo.InvariantCulture)) |> ignore
            zRows.Append("]") |> ignore
        zRows.Append("]") |> ignore

        let sb = StringBuilder(sprintf "{\"kind\":\"surface\",\"surface\":{\"x\":{\"min\":%s,\"max\":%s},\"y\":{\"min\":%s,\"max\":%s},\"z\":%s}"
                                  (minX.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (maxX.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (minY.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (maxY.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                  (zRows.ToString()))
        if not (String.IsNullOrEmpty(title)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(title)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay PLOT3D_MIME (sb.ToString())

    static member surface3d(title: string, func: float -> float -> float, minX: float, maxX: float, minY: float, maxY: float, ?resolution: int) =
        Display.Surface3D(title, func, minX, maxX, minY, maxY, ?resolution = resolution)

    // 8c. Voxel Bars
    static member VoxelBars(data: seq<#seq<'T>>, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let xs = StringBuilder("[")
        let ys = StringBuilder("[")
        let zs = StringBuilder("[")
        let ls = StringBuilder("[")
        let mutable first = true
        let mutable r = 0
        for row in data do
            let mutable c = 0
            for cell in row do
                if not first then
                    xs.Append(",") |> ignore
                    ys.Append(",") |> ignore
                    zs.Append(",") |> ignore
                    ls.Append(",") |> ignore
                first <- false
                xs.Append(r) |> ignore
                ys.Append(c) |> ignore
                let zVal = toJsonVal (box cell)
                zs.Append(zVal) |> ignore
                ls.Append(sprintf "\"[%d,%d]=%s\"" r c zVal) |> ignore
                c <- c + 1
            r <- r + 1
        xs.Append("]") |> ignore
        ys.Append("]") |> ignore
        zs.Append("]") |> ignore
        ls.Append("]") |> ignore

        let sb = StringBuilder("{\"kind\":\"voxelBar\",\"series\":[{\"x\":")
        sb.Append(xs.ToString()) |> ignore
        sb.Append(",\"y\":") |> ignore
        sb.Append(ys.ToString()) |> ignore
        sb.Append(",\"z\":") |> ignore
        sb.Append(zs.ToString()) |> ignore
        sb.Append(",\"labels\":") |> ignore
        sb.Append(ls.ToString()) |> ignore
        sb.Append("}]") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay PLOT3D_MIME (sb.ToString())

    static member voxelBars(data: seq<#seq<'T>>, ?title: string) = Display.VoxelBars(data, ?title = title)
    static member voxel_bars(data: seq<#seq<'T>>, ?title: string) = Display.VoxelBars(data, ?title = title)

    // Canvas Visualizer
    static member Canvas(title: string, ?width: int, ?height: int) =
        let w = defaultArg width 600
        let h = defaultArg height 300
        CanvasVisualizer(title, w, h)

    static member canvas(title: string, ?width: int, ?height: int) = Display.Canvas(title, ?width = width, ?height = height)

    // 9. Graph 3D
    static member Graph3D(data: obj, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let nodesSet = HashSet<string>()
        let edgesList = List<string * string>()

        let dataObj = box data
        match tryGetDictionaryEntries dataObj with
        | Some entries ->
            for (src, targetsObj) in entries do
                nodesSet.Add(src) |> ignore
                if not (isNull targetsObj) && typeof<System.Collections.IEnumerable>.IsAssignableFrom(targetsObj.GetType()) && not (targetsObj :? string) then
                    let targets = (targetsObj :?> System.Collections.IEnumerable) |> Seq.cast<obj>
                    for t in targets do
                        let dst = string t
                        nodesSet.Add(dst) |> ignore
                        edgesList.Add((src, dst))
                elif not (isNull targetsObj) then
                    let dst = string targetsObj
                    nodesSet.Add(dst) |> ignore
                    edgesList.Add((src, dst))
        | None -> ()

        let sortedNodes = nodesSet |> Seq.sort |> Seq.toArray
        let sb = StringBuilder("{\"kind\":\"graph\",\"graph\":{\"directed\":true,\"nodes\":[")
        for i = 0 to sortedNodes.Length - 1 do
            if i > 0 then sb.Append(",") |> ignore
            sb.Append(sprintf "{\"id\":%s}" (JsonSerializer.Serialize(sortedNodes.[i]))) |> ignore
        sb.Append("],\"edges\":[") |> ignore
        for i = 0 to edgesList.Count - 1 do
            if i > 0 then sb.Append(",") |> ignore
            let (s, d) = edgesList.[i]
            sb.Append(sprintf "{\"from\":%s,\"to\":%s}" (JsonSerializer.Serialize(s)) (JsonSerializer.Serialize(d))) |> ignore
        sb.Append("]}") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay PLOT3D_MIME (sb.ToString())

    static member graph3d(data: obj, ?title: string) = Display.Graph3D(data, ?title = title)
    static member graph3D(data: obj, ?title: string) = Display.Graph3D(data, ?title = title)

    // 10. Matrix
    static member Matrix(data: seq<#seq<'T>>, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let valSb = StringBuilder("[")
        let cellSb = StringBuilder("[")
        let mutable r = 0
        let mutable cellCount = 0
        for row in data do
            if r > 0 then valSb.Append(",") |> ignore
            valSb.Append("[") |> ignore
            let mutable c = 0
            for cell in row do
                if c > 0 then valSb.Append(",") |> ignore
                valSb.Append(toJsonVal (box cell)) |> ignore
                if cellCount > 0 then cellSb.Append(",") |> ignore
                cellCount <- cellCount + 1
                let v = try Convert.ToInt32(cell) with _ -> 0
                let terrain = if v > 0 then "land" else "water"
                cellSb.Append(sprintf "{\"row\":%d,\"col\":%d,\"terrain\":\"%s\"}" r c terrain) |> ignore
                c <- c + 1
            valSb.Append("]") |> ignore
            r <- r + 1
        valSb.Append("]") |> ignore
        cellSb.Append("]") |> ignore

        let sb = StringBuilder("{\"kind\":\"matrix\",\"state\":{\"grid\":{\"values\":")
        sb.Append(valSb.ToString()) |> ignore
        sb.Append(",\"cells\":") |> ignore
        sb.Append(cellSb.ToString()) |> ignore
        sb.Append(",\"inferTerrain\":false}},\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay VISUALIZER_MIME (sb.ToString())

    static member matrix(data: seq<#seq<'T>>, ?title: string) = Display.Matrix(data, ?title = title)

    // 11. Islands
    static member Islands(data: seq<#seq<'T>>, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let valSb = StringBuilder("[")
        let cellSb = StringBuilder("[")
        let mutable r = 0
        let mutable cellCount = 0
        for row in data do
            if r > 0 then valSb.Append(",") |> ignore
            valSb.Append("[") |> ignore
            let mutable c = 0
            for cell in row do
                if c > 0 then valSb.Append(",") |> ignore
                valSb.Append(toJsonVal (box cell)) |> ignore
                if cellCount > 0 then cellSb.Append(",") |> ignore
                cellCount <- cellCount + 1
                let v = try Convert.ToInt32(cell) with _ -> 0
                let terrain = if v > 0 then "land" else "water"
                cellSb.Append(sprintf "{\"row\":%d,\"col\":%d,\"terrain\":\"%s\"}" r c terrain) |> ignore
                c <- c + 1
            valSb.Append("]") |> ignore
            r <- r + 1
        valSb.Append("]") |> ignore
        cellSb.Append("]") |> ignore

        let sb = StringBuilder("{\"kind\":\"islands\",\"state\":{\"grid\":{\"values\":")
        sb.Append(valSb.ToString()) |> ignore
        sb.Append(",\"cells\":") |> ignore
        sb.Append(cellSb.ToString()) |> ignore
        sb.Append(",\"inferTerrain\":false}},\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay VISUALIZER_MIME (sb.ToString())

    static member islands(data: seq<#seq<'T>>, ?title: string) = Display.Islands(data, ?title = title)

    // 12. Array with Pointers
    static member Array(values: seq<'T>, pointers: obj, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let colors = [| "#38bdf8"; "#a855f7"; "#f43f5e"; "#10b981"; "#eab308"; "#06b6d4" |]

        let ptSb = StringBuilder("[")
        let mutable pi = 0
        let ptObj = box pointers
        match tryGetDictionaryEntries ptObj with
        | Some entries ->
            for (name, atVal) in entries do
                if pi > 0 then ptSb.Append(",") |> ignore
                let at = Convert.ToInt32(atVal)
                let color = colors.[pi % colors.Length]
                ptSb.Append(sprintf "{\"name\":%s,\"at\":%d,\"color\":\"%s\"}" (JsonSerializer.Serialize(name)) at color) |> ignore
                pi <- pi + 1
        | None -> ()
        ptSb.Append("]") |> ignore

        let sb = StringBuilder("{\"kind\":\"arrayPointers\",\"state\":{\"array\":{\"values\":")
        sb.Append(toJsonArray values) |> ignore
        sb.Append("}},\"pointers\":") |> ignore
        sb.Append(ptSb.ToString()) |> ignore
        sb.Append(",\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay VISUALIZER_MIME (sb.ToString())

    static member array(values: seq<'T>, pointers: obj, ?title: string) = Display.Array(values, pointers, ?title = title)

    // 13. Tree
    static member Tree(data: obj, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let nodesList = List<Detail.InternalTreeNode>()
        let mutable counter = 1

        let dataObj = box data
        let t = dataObj.GetType()

        let isTreeType =
            t.GetProperty("Left") <> null || t.GetProperty("left") <> null ||
            t.GetProperty("Val") <> null || t.GetProperty("val") <> null

        if isTreeType then
            let rec collectPreorder (node: obj) (id: string) =
                if not (isNull node) then
                    let nt = node.GetType()
                    let valProp = match nt.GetProperty("Val") with null -> nt.GetProperty("val") | p -> p
                    let leftProp = match nt.GetProperty("Left") with null -> nt.GetProperty("left") | p -> p
                    let rightProp = match nt.GetProperty("Right") with null -> nt.GetProperty("right") | p -> p

                    let vVal = if isNull valProp then "" else string (valProp.GetValue(node))
                    let leftVal = if isNull leftProp then null else leftProp.GetValue(node)
                    let rightVal = if isNull rightProp then null else rightProp.GetValue(node)

                    let unwrap (o: obj) =
                        if isNull o then null
                        else
                            let ot = o.GetType()
                            if ot.IsGenericType && ot.GetGenericTypeDefinition() = typedefof<option<_>> then
                                let tagProp = ot.GetProperty("Tag")
                                let isSome = if isNull tagProp then not (isNull o) else (tagProp.GetValue(o) :?> int) = 1
                                if isSome then ot.GetProperty("Value").GetValue(o) else null
                            else o

                    let leftUnwrapped = unwrap leftVal
                    let rightUnwrapped = unwrap rightVal

                    let leftId =
                        if not (isNull leftUnwrapped) then
                            counter <- counter + 1
                            sprintf "node_%d" counter
                        else ""
                    let rightId =
                        if not (isNull rightUnwrapped) then
                            counter <- counter + 1
                            sprintf "node_%d" counter
                        else ""

                    nodesList.Add({ Id = id; Value = vVal; Left = leftId; Right = rightId })
                    if not (isNull leftUnwrapped) then collectPreorder leftUnwrapped leftId
                    if not (isNull rightUnwrapped) then collectPreorder rightUnwrapped rightId

            collectPreorder dataObj "node_1"
        else
            let items = (dataObj :?> System.Collections.IEnumerable) |> Seq.cast<obj> |> Seq.toArray
            let unwrapVal (o: obj) =
                if isNull o then None
                else
                    let ot = o.GetType()
                    if ot.IsGenericType && ot.GetGenericTypeDefinition() = typedefof<option<_>> then
                        let tagProp = ot.GetProperty("Tag")
                        let isSome = if isNull tagProp then not (isNull o) else (tagProp.GetValue(o) :?> int) = 1
                        if isSome then Some (string (ot.GetProperty("Value").GetValue(o))) else None
                    else Some (string o)

            let optItems = items |> Array.map unwrapVal

            if optItems.Length > 0 && optItems.[0].IsSome then
                let root = { Detail.BNode.Value = optItems.[0].Value; Left = None; Right = None }
                let q = Queue<Detail.BNode>()
                q.Enqueue(root)
                let mutable idx = 1
                while q.Count > 0 && idx < optItems.Length do
                    let curr = q.Dequeue()
                    if idx < optItems.Length then
                        if optItems.[idx].IsSome then
                            let l = { Detail.BNode.Value = optItems.[idx].Value; Left = None; Right = None }
                            curr.Left <- Some l
                            q.Enqueue(l)
                        idx <- idx + 1
                    if idx < optItems.Length then
                        if optItems.[idx].IsSome then
                            let r = { Detail.BNode.Value = optItems.[idx].Value; Left = None; Right = None }
                            curr.Right <- Some r
                            q.Enqueue(r)
                        idx <- idx + 1

                let rec dfs (n: Detail.BNode) (id: string) =
                    let leftId =
                        match n.Left with
                        | Some _ ->
                            counter <- counter + 1
                            sprintf "node_%d" counter
                        | None -> ""
                    let rightId =
                        match n.Right with
                        | Some _ ->
                            counter <- counter + 1
                            sprintf "node_%d" counter
                        | None -> ""
                    nodesList.Add({ Id = id; Value = n.Value; Left = leftId; Right = rightId })
                    match n.Left with Some l -> dfs l leftId | None -> ()
                    match n.Right with Some r -> dfs r rightId | None -> ()

                dfs root "node_1"

        let sb = StringBuilder("{\"kind\":\"tree\",\"state\":{\"tree\":{\"root\":\"node_1\",\"nodes\":[")
        for i = 0 to nodesList.Count - 1 do
            if i > 0 then sb.Append(",") |> ignore
            let n = nodesList.[i]
            sb.Append(sprintf "{\"id\":\"%s\",\"value\":%s" n.Id (JsonSerializer.Serialize(n.Value))) |> ignore
            if not (String.IsNullOrEmpty(n.Left)) then
                sb.Append(sprintf ",\"left\":\"%s\"" n.Left) |> ignore
            if not (String.IsNullOrEmpty(n.Right)) then
                sb.Append(sprintf ",\"right\":\"%s\"" n.Right) |> ignore
            sb.Append("}") |> ignore
        sb.Append("]}},\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay VISUALIZER_MIME (sb.ToString())

    static member tree(data: obj, ?title: string) = Display.Tree(data, ?title = title)

    // 14. Graph Visualizer
    static member Graph(data: obj, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let nodesSet = HashSet<string>()
        let edgesList = List<string * string>()

        let dataObj = box data
        match tryGetDictionaryEntries dataObj with
        | Some entries ->
            for (src, targetsObj) in entries do
                nodesSet.Add(src) |> ignore
                if not (isNull targetsObj) && typeof<System.Collections.IEnumerable>.IsAssignableFrom(targetsObj.GetType()) && not (targetsObj :? string) then
                    let targets = (targetsObj :?> System.Collections.IEnumerable) |> Seq.cast<obj>
                    for t in targets do
                        let dst = string t
                        nodesSet.Add(dst) |> ignore
                        edgesList.Add((src, dst))
                elif not (isNull targetsObj) then
                    let dst = string targetsObj
                    nodesSet.Add(dst) |> ignore
                    edgesList.Add((src, dst))
        | None -> ()

        let sortedNodes = nodesSet |> Seq.sort |> Seq.toArray
        let sb = StringBuilder("{\"kind\":\"graph\",\"state\":{\"graph\":{\"directed\":true,\"nodes\":[")
        for i = 0 to sortedNodes.Length - 1 do
            if i > 0 then sb.Append(",") |> ignore
            sb.Append(sprintf "{\"id\":%s}" (JsonSerializer.Serialize(sortedNodes.[i]))) |> ignore
        sb.Append("],\"edges\":[") |> ignore
        for i = 0 to edgesList.Count - 1 do
            if i > 0 then sb.Append(",") |> ignore
            let (s, d) = edgesList.[i]
            sb.Append(sprintf "{\"from\":%s,\"to\":%s}" (JsonSerializer.Serialize(s)) (JsonSerializer.Serialize(d))) |> ignore
        sb.Append("]}},\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay VISUALIZER_MIME (sb.ToString())

    static member graph(data: obj, ?title: string) = Display.Graph(data, ?title = title)

    // 15. Linked List
    static member LinkedList(data: obj, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let sb = StringBuilder("{\"kind\":\"linkedList\",\"state\":{\"linkedList\":{\"nodes\":[")
        let mutable curr = box data
        let mutable i = 0
        while not (isNull curr) do
            if i > 0 then sb.Append(",") |> ignore
            let ct = curr.GetType()
            let valProp = match ct.GetProperty("Val") with null -> ct.GetProperty("val") | p -> p
            let nextProp = match ct.GetProperty("Next") with null -> ct.GetProperty("next") | p -> p

            let vVal = if isNull valProp then "" else string (valProp.GetValue(curr))
            let nextRaw = if isNull nextProp then null else nextProp.GetValue(curr)

            let nextObj =
                if isNull nextRaw then null
                else
                    let nt = nextRaw.GetType()
                    if nt.IsGenericType && nt.GetGenericTypeDefinition() = typedefof<option<_>> then
                        let tagProp = nt.GetProperty("Tag")
                        let isSome = if isNull tagProp then not (isNull nextRaw) else (tagProp.GetValue(nextRaw) :?> int) = 1
                        if isSome then nt.GetProperty("Value").GetValue(nextRaw) else null
                    else nextRaw

            sb.Append(sprintf "{\"id\":\"n%d\",\"value\":%s" i (JsonSerializer.Serialize(vVal))) |> ignore
            if not (isNull nextObj) then
                sb.Append(sprintf ",\"next\":\"n%d\"" (i + 1)) |> ignore
            sb.Append("}") |> ignore
            curr <- nextObj
            i <- i + 1
        sb.Append("],\"markCycle\":false}},\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay VISUALIZER_MIME (sb.ToString())

    static member linked_list(data: obj, ?title: string) = Display.LinkedList(data, ?title = title)
    static member linkedList(data: obj, ?title: string) = Display.LinkedList(data, ?title = title)

    // 16. Bars
    static member Bars(data: seq<'T>, ?title: string) : DisplayHandle =
        let titleStr = defaultArg title ""
        let items = data |> Seq.map (fun x -> Convert.ToInt32(x)) |> Seq.toArray
        let minVal = if items.Length > 0 then items |> Array.min else 0
        let maxVal = if items.Length > 0 then items |> Array.max else 0
        let effectiveMin = if minVal > 0 then 0 else minVal

        let sb = StringBuilder("{\"kind\":\"bars\",\"state\":{\"bars\":{\"values\":")
        sb.Append(toJsonArray items) |> ignore
        sb.Append(sprintf ",\"min\":%d,\"max\":%d" effectiveMin maxVal) |> ignore
        sb.Append("}},\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true") |> ignore
        if not (String.IsNullOrEmpty(titleStr)) then
            sb.Append(",\"title\":") |> ignore
            sb.Append(JsonSerializer.Serialize(titleStr)) |> ignore
        sb.Append("}") |> ignore
        Helpers.emitDisplay VISUALIZER_MIME (sb.ToString())

    static member bars(data: seq<'T>, ?title: string) = Display.Bars(data, ?title = title)

    // ── Timing & Process Events ───────────────────────────────────────────

    static member Wait(seconds: float) =
        Detail.ensureEventSocket()
        stdout.Flush()
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let limit = seconds * 1000.0
        while sw.Elapsed.TotalMilliseconds < limit do
            System.Threading.Thread.Sleep(25)

    static member wait(seconds: float) = Display.Wait(seconds)

    static member ProcessEvents() =
        Detail.ensureEventSocket()
        stdout.Flush()

    static member process_events() = Display.ProcessEvents()
