package fry

// What a notebook cell does with a value: show it (text, a table, the Object Explorer), keep it for the next cell, share it with
// another language, ask the notebook for a line of input.

import (
	"bufio"
	"encoding/json"
	"fmt"
	"os"
	"reflect"
	"sort"
	"strconv"
	"strings"
	"sync"
)

const (
	InspectorMime = "application/vnd.fry.inspector+json"

	maxInline       = 50   // scalars in a list shown on one line
	maxTableRows    = 1000 // rows of a table (the rest is counted, not sent)
	maxInspectItems = 100  // properties or elements of one object in the Object Explorer
	maxInspectDepth = 3
	maxPersistBytes = 8 << 20
)

// ------------------------------------------------------------------------------------------------------------ Show

// ShowAll shows what an expression gave: the values of a call that returns several (a trailing nil, such as the error of
// strconv.Atoi, is not a result worth showing). A cell's last expression is passed here.
func ShowAll(values ...any) {
	n := len(values)
	if n > 1 && values[n-1] == nil {
		n--
	}
	for _, v := range values[:n] {
		showValue(v, "")
	}
}

// Dump shows a value as a cell's last expression is shown, and returns it.
func Dump(v any) any {
	showValue(v, "")
	return v
}

// Show shows a value as a cell's last expression is shown, and returns it.
func Show(v any) any {
	showValue(v, "")
	return v
}

// Table shows a list of structs, maps or values as a table.
func Table(title string, obj any) {
	if title == "" {
		title = "Table"
	}
	showValue(obj, title)
}

func showValue(v any, title string) {
	if v == nil {
		fmt.Println("<nil>")
		return
	}
	if err, ok := v.(error); ok {
		fmt.Println(err.Error())
		return
	}
	rv := reflect.ValueOf(v)
	if s, ok := v.(fmt.Stringer); ok && !(rv.Kind() == reflect.Ptr && rv.IsNil()) {
		fmt.Println(s.String())
		return
	}
	switch rv.Kind() {
	case reflect.Slice, reflect.Array:
		showList(rv, title)
	case reflect.Map:
		showMap(rv, title)
	case reflect.Struct:
		showInspector(rv)
	case reflect.Ptr, reflect.Interface:
		if rv.IsNil() {
			fmt.Println("<nil>")
		} else if e := indirect(rv); e.Kind() == reflect.Struct {
			showInspector(rv)
		} else {
			showValue(e.Interface(), title)
		}
	default:
		fmt.Println(scalarText(rv, true))
	}
}

func indirect(rv reflect.Value) reflect.Value {
	for (rv.Kind() == reflect.Ptr || rv.Kind() == reflect.Interface) && !rv.IsNil() {
		rv = rv.Elem()
	}
	return rv
}

func isScalar(rv reflect.Value) bool {
	switch rv.Kind() {
	case reflect.Bool, reflect.Int, reflect.Int8, reflect.Int16, reflect.Int32, reflect.Int64,
		reflect.Uint, reflect.Uint8, reflect.Uint16, reflect.Uint32, reflect.Uint64, reflect.Uintptr,
		reflect.Float32, reflect.Float64, reflect.Complex64, reflect.Complex128, reflect.String:
		return true
	}
	return false
}

// A number, text, a name: one cell, one line of text. Works on the unexported fields of a struct too.
func scalarText(rv reflect.Value, quote bool) string {
	switch rv.Kind() {
	case reflect.Bool:
		return strconv.FormatBool(rv.Bool())
	case reflect.Int, reflect.Int8, reflect.Int16, reflect.Int32, reflect.Int64:
		return strconv.FormatInt(rv.Int(), 10)
	case reflect.Uint, reflect.Uint8, reflect.Uint16, reflect.Uint32, reflect.Uint64, reflect.Uintptr:
		return strconv.FormatUint(rv.Uint(), 10)
	case reflect.Float32:
		return strconv.FormatFloat(rv.Float(), 'g', -1, 32)
	case reflect.Float64:
		return strconv.FormatFloat(rv.Float(), 'g', -1, 64)
	case reflect.Complex64, reflect.Complex128:
		return fmt.Sprint(rv.Complex())
	case reflect.String:
		if quote {
			return strconv.Quote(rv.String())
		}
		return rv.String()
	}
	return textOf(rv)
}

// Any value as one line of text: its String or Error method when it has one (a time.Duration, an error), else Go's own %v.
func textOf(rv reflect.Value) string {
	if !rv.IsValid() {
		return "<nil>"
	}
	if rv.CanInterface() {
		value := rv.Interface()
		switch v := value.(type) {
		case error:
			return v.Error()
		case fmt.Stringer:
			if !(rv.Kind() == reflect.Ptr && rv.IsNil()) {
				return v.String()
			}
		}
		return fmt.Sprintf("%v", value)
	}
	if isScalar(rv) {
		return scalarText(rv, false)
	}
	return fmt.Sprintf("%v", rv)
}

func scalarLike(rv reflect.Value) bool {
	rv = indirectScalar(rv)
	if !rv.IsValid() {
		return true
	}
	if isScalar(rv) {
		return true
	}
	if rv.CanInterface() {
		switch rv.Interface().(type) {
		case error, fmt.Stringer:
			return true
		}
	}
	return false
}

func indirectScalar(rv reflect.Value) reflect.Value {
	for rv.IsValid() && (rv.Kind() == reflect.Interface || rv.Kind() == reflect.Ptr) {
		if rv.IsNil() {
			return reflect.Value{}
		}
		if rv.Kind() == reflect.Ptr && rv.Elem().Kind() == reflect.Struct {
			return rv
		}
		rv = rv.Elem()
	}
	return rv
}

func isRecordValue(rv reflect.Value) bool {
	rv = indirect(rv)
	if !rv.IsValid() {
		return false
	}
	if rv.CanInterface() {
		switch rv.Interface().(type) {
		case error, fmt.Stringer:
			return false
		}
	}
	return rv.Kind() == reflect.Struct || rv.Kind() == reflect.Map
}

func showList(rv reflect.Value, title string) {
	n := rv.Len()
	if n == 0 {
		fmt.Println("[ ]")
		return
	}
	allScalar, allRecord := true, true
	for i := 0; i < n; i++ {
		item := rv.Index(i)
		if !scalarLike(item) {
			allScalar = false
		}
		if !isRecordValue(item) {
			allRecord = false
		}
	}
	if allScalar && n <= maxInline {
		parts := make([]string, n)
		for i := 0; i < n; i++ {
			item := indirectScalar(rv.Index(i))
			if !item.IsValid() {
				parts[i] = "<nil>"
			} else {
				parts[i] = textOf(item)
			}
		}
		fmt.Println("[ " + strings.Join(parts, ", ") + " ]")
		return
	}
	if allRecord {
		items := make([]reflect.Value, n)
		for i := range items {
			items[i] = indirect(rv.Index(i))
		}
		emitTable(title, recordTable(items), n)
		return
	}
	cells := make([][]any, 0, minInt(n, maxTableRows))
	for i := 0; i < n && i < maxTableRows; i++ {
		cells = append(cells, []any{i, cellOf(rv.Index(i))})
	}
	emitTable(title, tableData{columns: []string{"index", "value"}, rows: cells}, n)
}

func showMap(rv reflect.Value, title string) {
	if rv.Len() == 0 {
		fmt.Println("{ }")
		return
	}
	keys := sortedKeys(rv)
	cells := make([][]any, 0, minInt(len(keys), maxTableRows))
	for i, k := range keys {
		if i >= maxTableRows {
			break
		}
		cells = append(cells, []any{cellOf(k), cellOf(rv.MapIndex(k))})
	}
	emitTable(title, tableData{columns: []string{"key", "value"}, rows: cells}, len(keys))
}

func sortedKeys(rv reflect.Value) []reflect.Value {
	keys := rv.MapKeys()
	sort.SliceStable(keys, func(i, j int) bool {
		a, b := keys[i], keys[j]
		switch {
		case isIntKind(a.Kind()) && isIntKind(b.Kind()):
			return a.Int() < b.Int()
		case isUintKind(a.Kind()) && isUintKind(b.Kind()):
			return a.Uint() < b.Uint()
		case a.Kind() == reflect.Float64 && b.Kind() == reflect.Float64:
			return a.Float() < b.Float()
		}
		return textOf(a) < textOf(b)
	})
	return keys
}

func isIntKind(k reflect.Kind) bool {
	return k == reflect.Int || k == reflect.Int8 || k == reflect.Int16 || k == reflect.Int32 || k == reflect.Int64
}

func isUintKind(k reflect.Kind) bool {
	return k == reflect.Uint || k == reflect.Uint8 || k == reflect.Uint16 || k == reflect.Uint32 || k == reflect.Uint64 || k == reflect.Uintptr
}

func minInt(a, b int) int {
	if a < b {
		return a
	}
	return b
}

// One table cell: a number, a bool or text stays what it is, anything else is its text.
func cellOf(rv reflect.Value) any {
	rv = indirectScalar(rv)
	if !rv.IsValid() {
		return nil
	}
	switch rv.Kind() {
	case reflect.Bool:
		return rv.Bool()
	case reflect.Int, reflect.Int8, reflect.Int16, reflect.Int32, reflect.Int64:
		return rv.Int()
	case reflect.Uint, reflect.Uint8, reflect.Uint16, reflect.Uint32, reflect.Uint64, reflect.Uintptr:
		return rv.Uint()
	case reflect.Float32, reflect.Float64:
		f := rv.Float()
		if f != f || f > 1.7976931348623157e308 || f < -1.7976931348623157e308 {
			return nil
		}
		return f
	case reflect.String:
		return rv.String()
	}
	return textOf(rv)
}

type tableData struct {
	columns []string
	rows    [][]any
}

// The columns of a list of structs are their fields in the order they are declared; of maps, the keys that appear, sorted.
func recordTable(items []reflect.Value) tableData {
	var columns []string
	index := map[string]int{}
	add := func(name string) {
		if _, ok := index[name]; !ok {
			index[name] = len(columns)
			columns = append(columns, name)
		}
	}
	structural := true
	mapKeys := map[string]bool{}
	for _, item := range items {
		if item.Kind() == reflect.Struct {
			t := item.Type()
			for i := 0; i < t.NumField(); i++ {
				add(t.Field(i).Name)
			}
		} else {
			structural = false
			for _, k := range item.MapKeys() {
				mapKeys[textOf(k)] = true
			}
		}
	}
	if !structural {
		names := make([]string, 0, len(mapKeys))
		for k := range mapKeys {
			names = append(names, k)
		}
		sort.Strings(names)
		for _, name := range names {
			add(name)
		}
	}
	rows := make([][]any, 0, minInt(len(items), maxTableRows))
	for r, item := range items {
		if r >= maxTableRows {
			break
		}
		row := make([]any, len(columns))
		if item.Kind() == reflect.Struct {
			t := item.Type()
			for i := 0; i < t.NumField(); i++ {
				row[index[t.Field(i).Name]] = cellOf(item.Field(i))
			}
		} else {
			for _, k := range item.MapKeys() {
				row[index[textOf(k)]] = cellOf(item.MapIndex(k))
			}
		}
		rows = append(rows, row)
	}
	return tableData{columns: columns, rows: rows}
}

func emitTable(title string, t tableData, total int) {
	numeric := make([]bool, len(t.columns))
	for c := range t.columns {
		seen := false
		numeric[c] = true
		for _, row := range t.rows {
			switch row[c].(type) {
			case int, int64, uint64, float64:
				seen = true
			case nil:
			default:
				numeric[c] = false
			}
		}
		numeric[c] = numeric[c] && seen
	}
	emitMime(TableMime, map[string]any{
		"title":        title,
		"columns":      t.columns,
		"numeric":      numeric,
		"rows":         t.rows,
		"totalRows":    total,
		"totalColumns": len(t.columns),
	})
}

func emitMime(mime string, value any) {
	data, err := json.Marshal(map[string]any{"type": "display", "data": map[string]any{mime: value}, "metadata": map[string]any{}})
	if err == nil {
		emitRaw(string(data))
	}
}

// ------------------------------------------------------------------------------------------------- Object Explorer

type inspectorProperty struct {
	Name     string         `json:"name"`
	Value    *string        `json:"value,omitempty"`
	IsNull   bool           `json:"isNull"`
	IsString bool           `json:"isString"`
	Child    *inspectorNode `json:"child,omitempty"`
}

type inspectorNode struct {
	Title      string              `json:"title"`
	Circular   bool                `json:"circular,omitempty"`
	Properties []inspectorProperty `json:"properties"`
}

func showInspector(rv reflect.Value) {
	node := inspect(rv, 0, map[uintptr]bool{})
	data, err := json.Marshal(map[string]any{
		"type":     "display",
		"data":     map[string]any{InspectorMime: node, "text/plain": textOf(indirect(rv))},
		"metadata": map[string]any{},
	})
	if err == nil {
		emitRaw(string(data))
	}
}

func typeTitle(t reflect.Type) string {
	return strings.ReplaceAll(t.String(), "main.", "")
}

func inspect(rv reflect.Value, depth int, path map[uintptr]bool) *inspectorNode {
	// A pointer back to an object above is listed again (flagged), down to the depth limit, as the desktop's C# inspector does.
	circular := false
	if rv.Kind() == reflect.Ptr && !rv.IsNil() {
		p := rv.Pointer()
		if path[p] {
			circular = true
		} else {
			path[p] = true
			defer delete(path, p)
		}
	}
	rv = indirect(rv)
	node := &inspectorNode{Title: "nil", Circular: circular, Properties: []inspectorProperty{}}
	if !rv.IsValid() {
		return node
	}
	node.Title = typeTitle(rv.Type())
	add := func(name string, v reflect.Value) {
		if len(node.Properties) >= maxInspectItems {
			return
		}
		node.Properties = append(node.Properties, inspectProperty(name, v, depth, path))
	}
	more := func(total int) {
		if total > maxInspectItems {
			text := fmt.Sprintf("(more than %d shown)", maxInspectItems)
			node.Properties = append(node.Properties, inspectorProperty{Name: "…", Value: &text})
		}
	}
	switch rv.Kind() {
	case reflect.Struct:
		t := rv.Type()
		for i := 0; i < t.NumField(); i++ {
			add(t.Field(i).Name, rv.Field(i))
		}
		more(t.NumField())
	case reflect.Slice, reflect.Array:
		for i := 0; i < rv.Len(); i++ {
			add("["+strconv.Itoa(i)+"]", rv.Index(i))
		}
		more(rv.Len())
	case reflect.Map:
		keys := sortedKeys(rv)
		for _, k := range keys {
			add(textOf(k), rv.MapIndex(k))
		}
		more(len(keys))
	}
	return node
}

func inspectProperty(name string, v reflect.Value, depth int, path map[uintptr]bool) inspectorProperty {
	prop := inspectorProperty{Name: name}
	inner := indirectScalar(v)
	switch {
	case !inner.IsValid():
		text := "nil"
		prop.Value, prop.IsNull = &text, true
	case isScalar(inner) || (inner.CanInterface() && isStringerOrError(inner)):
		text := scalarText(inner, false)
		if !isScalar(inner) {
			text = textOf(inner)
		}
		prop.Value, prop.IsString = &text, inner.Kind() == reflect.String
	case depth >= maxInspectDepth:
		prop.Child = &inspectorNode{Title: typeTitle(indirect(v).Type()), Properties: []inspectorProperty{}}
	default:
		prop.Child = inspect(v, depth+1, path)
	}
	return prop
}

func isStringerOrError(rv reflect.Value) bool {
	switch rv.Interface().(type) {
	case error, fmt.Stringer:
		return true
	}
	return false
}

// ------------------------------------------------------------------------------------- Keeping variables for the next cell

var (
	restoreOnce sync.Once
	restored    = map[string]json.RawMessage{}
	persistMu   sync.Mutex
)

func loadRestored() {
	path := os.Getenv("FRY_VARS_IN")
	if path == "" {
		return
	}
	file, err := os.Open(path)
	if err != nil {
		return
	}
	defer file.Close()
	scanner := bufio.NewScanner(file)
	scanner.Buffer(make([]byte, 1<<20), maxPersistBytes*2)
	for scanner.Scan() {
		var entry struct {
			Name string          `json:"name"`
			Json json.RawMessage `json:"json"`
		}
		if json.Unmarshal(scanner.Bytes(), &entry) == nil && entry.Name != "" {
			restored[entry.Name] = entry.Json
		}
	}
}

// RestoreInto is Restore for a variable a cell declared at the top of the program (var counter int): the value goes into it.
func RestoreInto[T any](target *T, name string) {
	restoreOnce.Do(loadRestored)
	if raw, ok := restored[name]; ok {
		_ = json.Unmarshal(raw, target)
	}
}

// Restore gives a cell the value a variable had when the cell that made it ended (a cell's start does this; it isn't called by hand).
func Restore[T any](name string) T {
	restoreOnce.Do(loadRestored)
	var value T
	if raw, ok := restored[name]; ok {
		_ = json.Unmarshal(raw, &value)
	}
	return value
}

// Persist records a variable the cell made, for the next cell: its name, its type as Go writes it, and its value as JSON.
// A value that wouldn't come back the same (a func, a channel, a struct with fields that start with a lowercase letter) is
// recorded as skipped, with why; the notebook says so.
func Persist(name string, v any) {
	path := os.Getenv("FRY_VARS_OUT")
	if path == "" {
		return
	}
	entry := map[string]any{"name": name}
	t := reflect.TypeOf(v)
	_, isError := v.(error)
	switch {
	case t == nil:
		// nil (an error that didn't happen, a pointer that isn't set) has no type to give the next cell: not worth a note
		entry["skipped"] = "it is nil, so its type isn't known"
		entry["quiet"] = true
	case isError:
		entry["skipped"] = "errors can't be saved"
		entry["quiet"] = true
	default:
		entry["type"] = t.String()
		if !nameable(t) {
			entry["skipped"] = "its type (" + t.String() + ") isn't one a cell can name"
		} else if why := hiddenState(t, map[reflect.Type]bool{}); why != "" {
			entry["skipped"] = why
		} else if data, err := json.Marshal(v); err != nil {
			entry["skipped"] = "its value can't be saved: " + err.Error()
		} else if len(data) > maxPersistBytes {
			entry["skipped"] = "it is too large to keep"
		} else {
			again := reflect.New(t)
			if err := json.Unmarshal(data, again.Interface()); err != nil {
				entry["skipped"] = "it doesn't come back the same: " + err.Error()
			} else if back, err := json.Marshal(again.Elem().Interface()); err != nil || string(back) != string(data) {
				entry["skipped"] = "it doesn't come back the same when it is saved"
			} else {
				entry["json"] = json.RawMessage(data)
			}
		}
	}
	line, err := json.Marshal(entry)
	if err != nil {
		return
	}
	persistMu.Lock()
	defer persistMu.Unlock()
	if file, err := os.OpenFile(path, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0o644); err == nil {
		file.Write(append(line, '\n'))
		file.Close()
	}
}

// A type the next cell can write down: not one a package keeps to itself (*errors.errorString), and not a func or a channel.
func nameable(t reflect.Type) bool {
	if t.Name() != "" {
		if t.PkgPath() != "" && t.PkgPath() != "main" && !isExportedName(t.Name()) {
			return false
		}
		return t.Kind() != reflect.Func && t.Kind() != reflect.Chan && t.Kind() != reflect.UnsafePointer
	}
	switch t.Kind() {
	case reflect.Ptr, reflect.Slice, reflect.Array:
		return nameable(t.Elem())
	case reflect.Map:
		return nameable(t.Key()) && nameable(t.Elem())
	case reflect.Struct:
		for i := 0; i < t.NumField(); i++ {
			if !nameable(t.Field(i).Type) {
				return false
			}
		}
		return true
	case reflect.Interface:
		return t.NumMethod() == 0
	}
	return false
}

func isExportedName(name string) bool {
	return len(name) > 0 && name[0] >= 'A' && name[0] <= 'Z'
}

var jsonMarshaler = reflect.TypeOf((*json.Marshaler)(nil)).Elem()

// Fields JSON can't see (a lowercase name) would be lost: a type with them (that doesn't write itself as JSON) isn't kept.
func hiddenState(t reflect.Type, seen map[reflect.Type]bool) string {
	if seen[t] {
		return ""
	}
	seen[t] = true
	if t.Implements(jsonMarshaler) || reflect.PtrTo(t).Implements(jsonMarshaler) {
		return ""
	}
	switch t.Kind() {
	case reflect.Ptr, reflect.Slice, reflect.Array:
		return hiddenState(t.Elem(), seen)
	case reflect.Map:
		return hiddenState(t.Elem(), seen)
	case reflect.Struct:
		for i := 0; i < t.NumField(); i++ {
			f := t.Field(i)
			if !f.IsExported() {
				return "it has a field (" + f.Name + ") that starts with a lowercase letter, which can't be saved"
			}
			if why := hiddenState(f.Type, seen); why != "" {
				return why
			}
		}
	}
	return ""
}

// ----------------------------------------------------------------------------------------------- Sharing with other languages

// Share offers a value to the cells of other languages (#!share --from go name): it goes as JSON.
func Share(name string, v any) {
	data, err := json.Marshal(v)
	if err != nil {
		fmt.Println("⚠️ " + name + " can't be shared: " + err.Error())
		return
	}
	line, err := json.Marshal(map[string]any{"name": name, "json": string(data)})
	if err == nil {
		fmt.Println("__FRY_SHARE__ " + string(line))
	}
}

// ------------------------------------------------------------------------------------------------------------------ Input

var (
	stdin      = bufio.NewReader(os.Stdin)
	inputEnded bool
	inputMu    sync.Mutex
)

// Input asks for a line of input and returns it, without its line break; false is the end of input. In a notebook cell the
// notebook shows the prompt and the user answers there; a plain read of os.Stdin can't ask, and sees the end of input at once.
// In a program run on its own the prompt is printed and the line is read from the terminal.
func Input(prompt string) (string, bool) { return ask(prompt, false) }

// Password is Input for a secret: what is typed is not shown.
func Password(prompt string) (string, bool) { return ask(prompt, true) }

func ask(prompt string, secret bool) (string, bool) {
	inputMu.Lock()
	defer inputMu.Unlock()
	if inputEnded {
		return "", false
	}
	if os.Getenv("FRY_INPUT_REQUESTS") != "" {
		line, _ := json.Marshal(map[string]any{"prompt": prompt, "password": secret})
		fmt.Println("__FRY_INPUT__ " + string(line))
	} else {
		fmt.Print(prompt)
	}
	text, err := stdin.ReadString('\n')
	if err != nil && text == "" {
		inputEnded = true
		return "", false
	}
	return strings.TrimRight(text, "\r\n"), true
}
