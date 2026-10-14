// How a value is shown in a notebook: text for a scalar, one line for a short list, the studio's table for a collection of
// records, and the Object Explorer's tree for anything else. Used by the F# kernel (fry_kernel.fsx) for the value a cell
// ends with, and by Display.fsx (Display.Table, Display.Dump). The shapes are the ones the Python, JavaScript and Java
// kernels produce (docs/kernel-protocol.md, "MIME bundles").
#nowarn "3886" // a list of tuples written one per line reads as intended

namespace Fry

open System
open System.Collections
open System.Collections.Generic
open System.Globalization
open System.Reflection
open System.Text
open System.Text.Json.Nodes
open FSharp.Reflection

module Values =

    let jstr (s: string) : JsonNode = JsonValue.Create(s)
    let jint (n: int) : JsonNode = JsonValue.Create(n)
    let jbool (b: bool) : JsonNode = JsonValue.Create(b)

    let jobj (pairs: (string * JsonNode) list) : JsonObject =
        let o = JsonObject()
        for (k, v) in pairs do o.[k] <- v
        o

    let jarr (items: JsonNode seq) : JsonArray =
        let a = JsonArray()
        for i in items do a.Add(i)
        a

    // ─────────────────────────────────────────────────────────────────────────────────────────────── values: text, tree, table

    let invariant = CultureInfo.InvariantCulture

    let maxInline = 50
    let maxItems = 100
    let maxDepth = 3
    let maxRows = 1000

    let isScalarType (t: Type) =
        t.IsPrimitive || t.IsEnum || t = typeof<string> || t = typeof<decimal> || t = typeof<DateTime> || t = typeof<DateTimeOffset>
        || t = typeof<TimeSpan> || t = typeof<Guid> || t = typeof<Uri> || t = typeof<System.Numerics.BigInteger>
        || t = typeof<DateOnly> || t = typeof<TimeOnly>

    let isScalar (o: obj) = isNull o || isScalarType (o.GetType())

    let scalarText (o: obj) =
        match o with
        | null -> "null"
        | :? string as s -> s
        | :? bool as b -> if b then "true" else "false"
        | :? float as f -> (if Double.IsFinite f && f = Math.Floor f && abs f < 1e15 then f.ToString("0.0", invariant) else f.ToString("R", invariant))
        | :? float32 as f -> f.ToString("R", invariant)
        | :? DateTime as d -> d.ToString("yyyy-MM-dd HH:mm:ss", invariant)
        | :? IFormattable as f -> f.ToString(null, invariant)
        | _ -> o.ToString()

    let typeNames =
        dict [ typeof<int>, "int"; typeof<int64>, "int64"; typeof<float>, "float"; typeof<float32>, "float32"; typeof<string>, "string"
               typeof<bool>, "bool"; typeof<char>, "char"; typeof<byte>, "byte"; typeof<int16>, "int16"; typeof<decimal>, "decimal"
               typeof<obj>, "obj"; typeof<unit>, "unit"; typeof<uint32>, "uint"; typeof<uint64>, "uint64"; typeof<sbyte>, "sbyte" ]

    /// A type as F# writes it: int, string list, (int * string)[], Map<string,int>.
    let rec friendly (t: Type) : string =
        match typeNames.TryGetValue t with
        | true, name -> name
        | _ ->
            if t.IsArray then friendly (t.GetElementType()) + "[]"
            elif t.Name.Contains "AnonymousType" then
                // an anonymous record: {| Name: string; Age: int |}
                let fields = t.GetProperties() |> Array.map (fun p -> p.Name + ": " + friendly p.PropertyType)
                "{| " + String.Join("; ", fields) + " |}"
            elif t.IsGenericType then
                let def = t.GetGenericTypeDefinition()
                let args = t.GetGenericArguments()
                if def = typedefof<list<_>> then friendly args.[0] + " list"
                elif def = typedefof<option<_>> then friendly args.[0] + " option"
                elif def = typedefof<Set<_>> then friendly args.[0] + " set"
                elif def = typedefof<Map<_, _>> then "Map<" + String.Join(", ", args |> Array.map friendly) + ">"
                elif t.Name.Contains "@" || t.Name.StartsWith "Generated" then "seq<" + friendly args.[0] + ">"
                elif FSharpType.IsTuple t then String.Join(" * ", args |> Array.map friendly)
                else
                    let name = t.Name
                    let name = (match name.IndexOf('`') with | -1 -> name | i -> name.Substring(0, i))
                    name + "<" + String.Join(",", args |> Array.map friendly) + ">"
            elif FSharpType.IsTuple t then String.Join(" * ", FSharpType.GetTupleElements t |> Array.map friendly)
            else t.Name

    /// What a value reads as on one line, for hosts that can't draw it any better.
    let plain (o: obj) =
        if isNull o then "null"
        else
            let text = (try sprintf "%A" o with _ -> o.ToString())
            if text.Length > 2000 then text.Substring(0, 2000) + " ..." else text

    let oneLine (o: obj) =
        let text = (if isScalar o then scalarText o else (plain o).Replace("\r", " ").Replace("\n", " "))
        let text = System.Text.RegularExpressions.Regex.Replace(text, @"\s{2,}", " ")
        if text.Length > 80 then text.Substring(0, 80) + "…" else text

    let isCollection (o: obj) = (match o with | :? string -> false | :? IDictionary -> false | :? IEnumerable -> true | _ -> false)

    let isDictionary (o: obj) =
        match o with
        | :? IDictionary -> true
        | null -> false
        | _ ->
            let t = o.GetType()
            FSharpType.IsRecord(t, true) |> not
            && (t.IsGenericType && (let d = t.GetGenericTypeDefinition() in d = typedefof<Map<_, _>>)
                || t.GetInterfaces() |> Array.exists (fun i -> i.IsGenericType && i.GetGenericTypeDefinition() = typedefof<IDictionary<_, _>>))

    /// The key/value entries of a Map or dictionary.
    let entriesOf (o: obj) : (obj * obj) list =
        [ for item in (o :?> IEnumerable) do
            if not (isNull item) then
                let t = item.GetType()
                let key = t.GetProperty("Key")
                let value = t.GetProperty("Value")
                if not (isNull key) && not (isNull value) then yield key.GetValue(item), value.GetValue(item) ]

    let publicMembers (t: Type) =
        let props =
            t.GetProperties(BindingFlags.Public ||| BindingFlags.Instance)
            |> Array.filter (fun p -> p.CanRead && p.GetIndexParameters().Length = 0)
            |> Array.map (fun p -> p.Name, (fun (o: obj) -> try p.GetValue(o) with _ -> null))
        let fields =
            t.GetFields(BindingFlags.Public ||| BindingFlags.Instance)
            |> Array.map (fun f -> f.Name, (fun (o: obj) -> try f.GetValue(o) with _ -> null))
        Array.append props fields

    /// The named members of an object: record fields, tuple items, union fields, or the public properties of a class.
    let membersOf (o: obj) : (string * obj) list =
        let t = o.GetType()
        if FSharpType.IsRecord(t, true) then [ for f in FSharpType.GetRecordFields(t, true) -> f.Name, f.GetValue(o) ]
        elif FSharpType.IsTuple t then FSharpValue.GetTupleFields o |> Array.mapi (fun i v -> sprintf "Item%d" (i + 1), v) |> List.ofArray
        elif FSharpType.IsUnion(t, true) then
            let case, fields = FSharpValue.GetUnionFields(o, t, true)
            List.zip (case.GetFields() |> List.ofArray) (List.ofArray fields) |> List.map (fun (f, v) -> f.Name, v)
        else [ for (name, get) in publicMembers t -> name, get o ]

    let titleOf (o: obj) =
        let t = o.GetType()
        if FSharpType.IsUnion(t, true) && not (t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<list<_>>) then
            let case, _ = FSharpValue.GetUnionFields(o, t, true)
            case.Name
        else friendly t

    /// The studio's Object Explorer data for a value: {title, circular?, properties: [{name, value, isNull, isString, child}]}.
    let rec inspector (o: obj) (depth: int) (ancestors: obj list) : JsonObject =
        let title = titleOf o
        // A reference back to an object above is listed again (flagged), down to the depth limit, as the desktop's C# inspector does.
        let circular = ancestors |> List.exists (fun a -> obj.ReferenceEquals(a, o))
        let entries : (string * obj) list * int =
            if isDictionary o then
                let all = entriesOf o
                (all |> List.truncate maxItems |> List.map (fun (k, v) -> scalarText (if isScalar k then k else box (oneLine k)), v)), all.Length
            elif isCollection o then
                let taken = (o :?> IEnumerable) |> Seq.cast<obj> |> Seq.truncate (maxItems + 1) |> List.ofSeq
                (taken |> List.truncate maxItems |> List.mapi (fun i v -> sprintf "[%d]" i, v)), taken.Length
            else
                let all = membersOf o
                (all |> List.truncate maxItems), all.Length
        let rows, total = entries
        let property (name: string, value: obj) : JsonNode =
            if isScalar value then
                upcast jobj [ "name", jstr name; "value", jstr (scalarText value); "isNull", jbool (isNull value); "isString", jbool (value :? string) ]
            elif depth >= maxDepth then
                upcast jobj [ "name", jstr name; "child", jobj [ "title", jstr (titleOf value); "properties", jarr [] ] ]
            else
                upcast jobj [ "name", jstr name; "child", inspector value (depth + 1) (o :: ancestors) ]
        let shown = rows |> List.map property
        let more =
            if total > maxItems then [ (upcast jobj [ "name", jstr "…"; "value", jstr (sprintf "(more than %d shown)" maxItems); "isNull", jbool false; "isString", jbool false ] : JsonNode) ]
            else []
        jobj ([ "title", jstr title ] @ (if circular then [ "circular", jbool true ] else []) @ [ "properties", jarr (shown @ more) ])

    let isNumericValue (o: obj) =
        match o with
        | :? int | :? int64 | :? float | :? float32 | :? decimal | :? byte | :? int16 | :? uint32 | :? uint64 | :? sbyte | :? uint16 -> true
        | _ -> false

    /// The columns of a table of objects: a record's fields, a tuple's items or a class's public properties.
    let columnsOf (sample: obj) : (string * (obj -> obj)) list =
        let t = sample.GetType()
        if FSharpType.IsRecord(t, true) then [ for f in FSharpType.GetRecordFields(t, true) -> f.Name, (fun (o: obj) -> f.GetValue(o)) ]
        elif FSharpType.IsTuple t then [ for i in 0 .. FSharpType.GetTupleElements(t).Length - 1 -> sprintf "Item%d" (i + 1), (fun (o: obj) -> (FSharpValue.GetTupleFields o).[i]) ]
        elif FSharpType.IsUnion(t, true) then []
        else [ for (name, get) in publicMembers t -> name, get ]

    /// A collection of objects as the studio's table, or None when it is not tabular.
    let tableOf (items: obj list) (total: int) (title: string) : JsonObject option =
        match items |> List.tryFind (fun i -> not (isNull i)) with
        | Some sample when not (isScalar sample) && not (isCollection sample) && not (isDictionary sample) ->
            let columns = columnsOf sample
            if columns.IsEmpty then None
            else
                let sampleType = sample.GetType()
                let rows =
                    items |> List.truncate maxRows |> List.map (fun item ->
                        columns |> List.map (fun (_, get) ->
                            if isNull item || item.GetType() <> sampleType then null else (try get item with _ -> null)))
                let numeric = columns |> List.mapi (fun c _ -> rows |> List.forall (fun r -> isNull r.[c] || isNumericValue r.[c]) && rows |> List.exists (fun r -> not (isNull r.[c])))
                Some(jobj [ "title", jstr title
                            "columns", jarr (columns |> List.map (fun (n, _) -> jstr n))
                            "numeric", jarr (numeric |> List.map jbool)
                            "rows", jarr (rows |> List.map (fun r -> upcast jarr (r |> List.map (fun v -> jstr (if isNull v then "<null>" else oneLine v)))))
                            "totalRows", jint (max total rows.Length)
                            "totalColumns", jint columns.Length ])
        | _ -> None

    let INSPECTOR_MIME = "application/vnd.fry.inspector+json"
    let TABLE_MIME = "application/vnd.fry.table+json"

    // (in an F# Interactive session a type's full name has the session's assembly in front: "FSI_0004.Fry.DisplayHandle")
    let isHandle (t: Type) = t.FullName <> null && (t.FullName.EndsWith "Fry.DisplayHandle" || t.FullName.EndsWith "Fry.CanvasVisualizer")

    let elementTypeOf (t: Type) =
        if t.IsArray then t.GetElementType()
        else
            t.GetInterfaces() |> Array.append [| t |]
            |> Array.tryPick (fun i -> if i.IsGenericType && i.GetGenericTypeDefinition() = typedefof<IEnumerable<_>> then Some(i.GetGenericArguments().[0]) else None)
            |> Option.defaultValue typeof<obj>

    /// The bundle for the value a cell ends with, or None when there is nothing to show (unit, a handle the cell already displayed).
    let bundleOf (o: obj) (t: Type) : JsonObject option =
        let text (s: string) = Some(jobj [ "text/plain", jstr s ])
        if t = typeof<unit> then None
        elif isNull o then text "null"
        elif isHandle t then None
        elif isScalar o then text (scalarText o)
        elif FSharpType.IsFunction t || o :? Delegate then text (friendly t)
        elif isDictionary o then
            Some(jobj [ INSPECTOR_MIME, inspector o 0 []; "text/plain", jstr (plain o) ])
        elif isCollection o then
            let taken = (o :?> IEnumerable) |> Seq.cast<obj> |> Seq.truncate (maxRows + 1) |> List.ofSeq
            let total =
                match o with
                | :? ICollection as c -> c.Count
                | _ ->
                    let count = o.GetType().GetProperty("Count")
                    if not (isNull count) && count.PropertyType = typeof<int> then count.GetValue(o) :?> int else taken.Length
            let title = sprintf "%s (%d)" (friendly (elementTypeOf (o.GetType()))) total
            if taken.IsEmpty then text "[ ]"
            elif taken.Length <= maxInline && taken |> List.forall isScalar then
                text ("[ " + String.Join(", ", taken |> List.map (fun v -> if v :? string then "\"" + (v :?> string) + "\"" else scalarText v)) + " ]")
            else
                match tableOf taken total title with
                | Some table -> Some(jobj [ TABLE_MIME, table; "text/plain", jstr title ])
                | None -> Some(jobj [ INSPECTOR_MIME, inspector o 0 []; "text/plain", jstr (plain o) ])
        else
            let t = o.GetType()
            // a union case without data (None, Red) reads best as its name
            let members = membersOf o
            // a union case without data (None, Red), or with a little scalar data (Some 5, Circle 2.0), reads best as it is written
            if FSharpType.IsUnion(t, true) && (members.IsEmpty || (members.Length <= 3 && members |> List.forall (fun (_, v) -> isScalar v))) then text (plain o)
            elif members.IsEmpty then text (plain o)
            else Some(jobj [ INSPECTOR_MIME, inspector o 0 []; "text/plain", jstr (plain o) ])

    /// A collection as the studio's table: its records' fields as columns, or a single column of scalars. None when the value
    /// is not a collection.
    let tableBundle (o: obj) (title: string option) : JsonObject option =
        if isNull o || not (isCollection o || isDictionary o) then None
        else
            let items = (o :?> IEnumerable) |> Seq.cast<obj> |> Seq.truncate (maxRows + 1) |> List.ofSeq
            let total =
                match o with
                | :? ICollection as c -> c.Count
                | _ -> items.Length
            let named = defaultArg title (sprintf "%s (%d)" (friendly (elementTypeOf (o.GetType()))) total)
            let table =
                if isDictionary o then
                    let entries = entriesOf o |> List.truncate maxRows
                    Some(jobj [ "title", jstr named
                                "columns", jarr [ jstr "Key"; jstr "Value" ]
                                "numeric", jarr [ jbool false; jbool false ]
                                "rows", jarr (entries |> List.map (fun (k, v) -> (upcast jarr [ jstr (oneLine k); jstr (oneLine v) ] : JsonNode)))
                                "totalRows", jint (max total entries.Length)
                                "totalColumns", jint 2 ])
                else
                    match tableOf items total named with
                    | Some t -> Some t
                    | None when items |> List.forall isScalar ->
                        let shown = items |> List.truncate maxRows
                        Some(jobj [ "title", jstr named
                                    "columns", jarr [ jstr "Value" ]
                                    "numeric", jarr [ jbool (not shown.IsEmpty && shown |> List.forall isNumericValue) ]
                                    "rows", jarr (shown |> List.map (fun v -> (upcast jarr [ jstr (if isNull v then "<null>" else scalarText v) ] : JsonNode)))
                                    "totalRows", jint (max total shown.Length)
                                    "totalColumns", jint 1 ])
                    | None -> None
            table |> Option.map (fun t -> jobj [ TABLE_MIME, t; "text/plain", jstr named ])
