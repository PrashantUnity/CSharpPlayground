package fry

import (
	"bufio"
	"bytes"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"math"
	"net"
	"os"
	"reflect"
	"sort"
	"strconv"
	"strings"
	"sync"
	"sync/atomic"
	"time"
)

const (
	DisplayMarker  = "__FRY_DISPLAY__"
	ChartMime      = "application/vnd.fry.chart.v1+json"
	Plot3DMime     = "application/vnd.fry.plot3d.v1+json"
	VisualizerMime = "application/vnd.fry.visualizer.v1+json"
	TableMime      = "application/vnd.fry.table+json"
)

// TreeNode is a standard binary tree node.
type TreeNode struct {
	Val   any
	Left  *TreeNode
	Right *TreeNode
}

// ListNode is a standard singly-linked list node.
type ListNode struct {
	Val  any
	Next *ListNode
}

// Entry is a key-value pair for ordered maps.
type Entry struct {
	Key   string
	Value any
}

// OrderedMap preserves entry insertion order when marshaled to JSON.
type OrderedMap []Entry

func (om OrderedMap) MarshalJSON() ([]byte, error) {
	var buf bytes.Buffer
	buf.WriteByte('{')
	for i, entry := range om {
		if i > 0 {
			buf.WriteByte(',')
		}
		kb, _ := json.Marshal(entry.Key)
		buf.Write(kb)
		buf.WriteByte(':')
		vb, err := json.Marshal(cleanForJson(entry.Value))
		if err != nil {
			buf.WriteString("null")
		} else {
			buf.Write(vb)
		}
	}
	buf.WriteByte('}')
	return buf.Bytes(), nil
}

// Map constructs an OrderedMap from alternating key-value arguments.
func Map(kvs ...any) OrderedMap {
	var om OrderedMap
	for i := 0; i+1 < len(kvs); i += 2 {
		om = append(om, Entry{Key: fmt.Sprint(kvs[i]), Value: kvs[i+1]})
	}
	return om
}

// --------------------------------------------------------------------------------------------------- Options
type options struct {
	title  string
	bins   int
	kind   string
	labels []string
	extra  map[string]any
}

type Option func(*options)

func Title(t string) Option {
	return func(o *options) { o.title = t }
}

func Bins(b int) Option {
	return func(o *options) { o.bins = b }
}

// nest sets extra[key][field] = value, making the nested object when it isn't there yet.
func nest(extra map[string]any, key, field string, value any) {
	inner, ok := extra[key].(map[string]any)
	if !ok {
		inner = make(map[string]any)
		extra[key] = inner
	}
	inner[field] = value
}

// Kind chooses what Chart draws: line, area, bar, scatter, bubble, pie, donut, radar, polarArea.
func Kind(k string) Option { return func(o *options) { o.kind = k } }

// Labels names the places of the values: the x positions, a radar's spokes, a pie's slices.
func Labels(names ...string) Option { return func(o *options) { o.labels = names } }

// Stacked piles series up (bars, lines, areas); Percent scales every place to 100%.
func Stacked() Option { return func(o *options) { o.extra["stack"] = "stacked" } }
func Percent() Option { return func(o *options) { o.extra["stack"] = "percent" } }

// Horizontal lays bars along the y axis; Gauge draws a donut as a half circle.
func Horizontal() Option { return func(o *options) { o.extra["orientation"] = "horizontal" } }
func Gauge() Option {
	return func(o *options) { o.extra["startAngle"], o.extra["sweep"] = -90, 180 }
}

// LegendAt puts the legend at "top", "bottom", "left" or "right".
func LegendAt(position string) Option {
	return func(o *options) { nest(o.extra, "legend", "position", position) }
}

// RightAxis adds a second value axis up the right side (for series with "axis", "right"): its title and, optionally, min and max.
func RightAxis(title string, bounds ...float64) Option {
	return func(o *options) {
		nest(o.extra, "y2Axis", "title", title)
		if len(bounds) > 0 {
			nest(o.extra, "y2Axis", "min", bounds[0])
		}
		if len(bounds) > 1 {
			nest(o.extra, "y2Axis", "max", bounds[1])
		}
	}
}

// XScale, YScale and Y2Scale choose "linear", "log", "time" (x only) or "category" (x only).
func XScale(scale string) Option  { return func(o *options) { nest(o.extra, "xAxis", "scale", scale) } }
func YScale(scale string) Option  { return func(o *options) { nest(o.extra, "yAxis", "scale", scale) } }
func Y2Scale(scale string) Option { return func(o *options) { nest(o.extra, "y2Axis", "scale", scale) } }

// SuggestedY widens the value axis to at least min..max when the data doesn't reach it.
func SuggestedY(min, max float64) Option {
	return func(o *options) {
		nest(o.extra, "yAxis", "suggestedMin", min)
		nest(o.extra, "yAxis", "suggestedMax", max)
	}
}

// ReverseX and ReverseY run an axis from its largest end.
func ReverseX() Option { return func(o *options) { nest(o.extra, "xAxis", "reverse", true) } }
func ReverseY() Option { return func(o *options) { nest(o.extra, "yAxis", "reverse", true) } }

// Angles sets where a pie, donut or polar area starts (degrees clockwise from the top) and how far round it goes.
func Angles(start, sweep float64) Option {
	return func(o *options) { o.extra["startAngle"], o.extra["sweep"] = start, sweep }
}

// Cutout is a donut's hole, from 0 to 0.95 of its radius.
func Cutout(share float64) Option { return func(o *options) { o.extra["cutout"] = share } }

// SeriesData is a series' values and how it is drawn: fry.S(margin, "kind", "line", "axis", "right", "dash", "dashed").
type SeriesData struct {
	Values  any
	Options map[string]any
}

// S makes a series with options, to give a chart's Map: fry.Map("Sales", sales, "Margin", fry.S(margin, "axis", "right")).
func S(values any, options ...any) SeriesData {
	opts := make(map[string]any)
	for i := 0; i+1 < len(options); i += 2 {
		opts[fmt.Sprint(options[i])] = options[i+1]
	}
	return SeriesData{Values: values, Options: opts}
}

var seriesOptionNames = map[string]bool{
	"name": true, "color": true, "colors": true, "lineWidth": true, "kind": true, "axis": true, "stack": true, "dash": true,
	"interpolation": true, "tension": true, "step": true, "fill": true, "fillTo": true, "pointStyle": true, "pointRadius": true,
	"colorSegments": true, "sizes": true, "from": true, "cornerRadius": true, "ids": true, "labels": true,
}

func applySeriesOptions(series map[string]any, options map[string]any) map[string]any {
	for name, value := range options {
		if !seriesOptionNames[name] {
			var known []string
			for k := range seriesOptionNames {
				known = append(known, k)
			}
			sort.Strings(known)
			panic(fmt.Sprintf("There is no series option %q: use one of %s.", name, strings.Join(known, ", ")))
		}
		if name == "sizes" || name == "from" {
			var numbers []any
			for _, item := range toSlice(value) {
				numbers = append(numbers, toNum(item))
			}
			value = numbers
		}
		series[name] = value
	}
	return series
}

func parseOptions(args []any) *options {
	opts := &options{extra: make(map[string]any)}
	for _, arg := range args {
		switch v := arg.(type) {
		case string:
			opts.title = v
		case int:
			opts.bins = v
		case Option:
			v(opts)
		case map[string]any:
			for k, val := range v {
				opts.extra[k] = val
			}
		}
	}
	return opts
}

// --------------------------------------------------------------------------------------------------- Sockets & Handles
var (
	idCounter       uint64
	activeHandlesMu sync.RWMutex
	activeHandles   = make(map[string]*DisplayHandle)
	socketOnce      sync.Once
	socketConn      net.Conn
)

func newId() string {
	n := atomic.AddUint64(&idCounter, 1)
	return fmt.Sprintf("go_%d_%x", time.Now().UnixNano(), n)
}

func ensureEventSocket() {
	socketOnce.Do(func() {
		addr := os.Getenv("FRY_EVENTS")
		if addr == "" {
			return
		}
		conn, err := net.Dial("tcp", addr)
		if err != nil {
			return
		}
		socketConn = conn
		token := os.Getenv("FRY_EVENTS_TOKEN")
		helloMsg, _ := json.Marshal(map[string]string{
			"type":  "hello",
			"token": token,
		})
		conn.Write(append(helloMsg, '\n'))

		go func() {
			reader := bufio.NewReader(conn)
			for {
				line, err := reader.ReadString('\n')
				if err != nil {
					return
				}
				line = strings.TrimSpace(line)
				if line == "" {
					continue
				}
				deliverEventLine(line)
			}
		}()
	})
}

func deliverEventLine(line string) {
	var raw map[string]any
	if err := json.Unmarshal([]byte(line), &raw); err != nil {
		return
	}
	dispId, _ := raw["display_id"].(string)
	if dispId == "" {
		return
	}
	activeHandlesMu.RLock()
	h := activeHandles[dispId]
	activeHandlesMu.RUnlock()
	if h != nil {
		h.dispatchRawEvent(raw)
	}
}

func emitRaw(jsonStr string) {
	fmt.Println(DisplayMarker + " " + jsonStr)
}

type DisplayHandle struct {
	Mime          string
	Spec          map[string]any
	DisplayId     string
	mu            sync.Mutex
	listeners     map[string][]func(map[string]any)
	throttleTimer *time.Timer
	pendingUpdate map[string]any
	lastUpdate    time.Time
}

func (h *DisplayHandle) Update(args ...any) *DisplayHandle {
	opts := parseOptions(args)
	h.mu.Lock()

	copySpec := make(map[string]any, len(h.Spec))
	for k, v := range h.Spec {
		copySpec[k] = v
	}
	if opts.title != "" {
		copySpec["title"] = opts.title
	}
	for k, v := range opts.extra {
		copySpec[k] = v
	}
	h.Spec = copySpec
	h.pendingUpdate = copySpec

	elapsed := time.Since(h.lastUpdate)
	if elapsed >= 33*time.Millisecond && h.throttleTimer == nil {
		h.lastUpdate = time.Now()
		h.pendingUpdate = nil
		h.mu.Unlock()
		emitRaw(buildMessage("update_display", h.Mime, copySpec, h.DisplayId))
		return h
	}

	if h.throttleTimer == nil {
		h.throttleTimer = time.AfterFunc(33*time.Millisecond-elapsed, h.flushUpdate)
	}
	h.mu.Unlock()
	return h
}

func (h *DisplayHandle) flushUpdate() {
	h.mu.Lock()
	h.throttleTimer = nil
	h.lastUpdate = time.Now()
	toSend := h.pendingUpdate
	h.pendingUpdate = nil
	h.mu.Unlock()

	if toSend != nil {
		emitRaw(buildMessage("update_display", h.Mime, toSend, h.DisplayId))
	}
}

func (h *DisplayHandle) On(event string, listener func(map[string]any)) *DisplayHandle {
	ev := strings.ToLower(event)
	h.mu.Lock()
	if h.listeners == nil {
		h.listeners = make(map[string][]func(map[string]any))
	}
	h.listeners[ev] = append(h.listeners[ev], listener)
	h.mu.Unlock()

	ensureEventSocket()
	subMsg, _ := json.Marshal(map[string]any{
		"type":       "subscribe",
		"display_id": h.DisplayId,
		"events":     []string{ev},
	})
	emitRaw(string(subMsg))
	return h
}

func (h *DisplayHandle) OnClick(listener func(map[string]any)) *DisplayHandle  { return h.On("click", listener) }
func (h *DisplayHandle) OnSelect(listener func(map[string]any)) *DisplayHandle { return h.On("select", listener) }
func (h *DisplayHandle) OnStep(listener func(map[string]any)) *DisplayHandle   { return h.On("step", listener) }

func (h *DisplayHandle) Off(event string) *DisplayHandle {
	ev := strings.ToLower(event)
	h.mu.Lock()
	if h.listeners != nil {
		delete(h.listeners, ev)
	}
	h.mu.Unlock()

	unsubMsg, _ := json.Marshal(map[string]any{
		"type":       "unsubscribe",
		"display_id": h.DisplayId,
		"events":     []string{ev},
	})
	emitRaw(string(unsubMsg))
	return h
}

func (h *DisplayHandle) Close() {
	activeHandlesMu.Lock()
	delete(activeHandles, h.DisplayId)
	activeHandlesMu.Unlock()
}

func (h *DisplayHandle) dispatchRawEvent(raw map[string]any) {
	h.mu.Lock()
	defer h.mu.Unlock()
	if h.listeners == nil {
		return
	}
	evData := raw
	if inner, ok := raw["event"].(map[string]any); ok {
		evData = inner
	}
	kind := "click"
	if k, ok := evData["event"].(string); ok {
		kind = strings.ToLower(k)
	}
	for _, l := range h.listeners[kind] {
		go func(fn func(map[string]any)) {
			defer func() { recover() }()
			fn(evData)
		}(l)
	}
}

func sendDisplay(mime string, spec map[string]any) *DisplayHandle {
	id := newId()
	h := &DisplayHandle{
		Mime:      mime,
		Spec:      spec,
		DisplayId: id,
	}
	activeHandlesMu.Lock()
	activeHandles[id] = h
	activeHandlesMu.Unlock()

	emitRaw(buildMessage("display", mime, spec, id))
	return h
}

func buildMessage(msgType, mime string, spec map[string]any, displayId string) string {
	specBytes, _ := json.Marshal(cleanForJson(spec))
	kind, _ := spec["kind"].(string)
	title, _ := spec["title"].(string)
	fallback := kind
	if title != "" {
		fallback += ": " + title
	}
	msg := map[string]any{
		"type": msgType,
		"data": map[string]any{
			mime:         json.RawMessage(specBytes),
			"text/plain": fallback,
		},
		"metadata": map[string]any{},
		"transient": map[string]any{
			"display_id": displayId,
		},
	}
	bytes, _ := json.Marshal(msg)
	return string(bytes)
}

// --------------------------------------------------------------------------------------------------- API
// chartDisplay draws a chart of data as kind, with the options (and the labels they name the places with).
func chartDisplay(kind string, data any, opts *options) *DisplayHandle {
	spec := chartSpec(kind, data, opts.title, opts.extra)
	if len(opts.labels) > 0 {
		names := make([]any, len(opts.labels))
		for i, l := range opts.labels {
			names[i] = l
		}
		if series, ok := spec["series"].([]map[string]any); ok {
			for _, one := range series {
				if y, ok := one["y"].([]any); ok && len(y) == len(names) {
					one["labels"] = names
				}
			}
		}
	}
	return sendDisplay(ChartMime, spec)
}

func LineChart(data any, args ...any) *DisplayHandle    { return chartDisplay("line", data, parseOptions(args)) }
func AreaChart(data any, args ...any) *DisplayHandle    { return chartDisplay("area", data, parseOptions(args)) }
func ScatterChart(data any, args ...any) *DisplayHandle { return chartDisplay("scatter", data, parseOptions(args)) }
func BarChart(data any, args ...any) *DisplayHandle     { return chartDisplay("bar", data, parseOptions(args)) }
func PieChart(data any, args ...any) *DisplayHandle     { return chartDisplay("pie", data, parseOptions(args)) }
func DonutChart(data any, args ...any) *DisplayHandle   { return chartDisplay("donut", data, parseOptions(args)) }

// BubbleChart draws (x, y, size) items; the size is a radius in pixels.
func BubbleChart(data any, args ...any) *DisplayHandle { return chartDisplay("bubble", data, parseOptions(args)) }

// RadarChart draws a polygon for each series over a spoke for each value (name the spokes with fry.Labels).
func RadarChart(data any, args ...any) *DisplayHandle { return chartDisplay("radar", data, parseOptions(args)) }

func PolarAreaChart(data any, args ...any) *DisplayHandle {
	return chartDisplay("polarArea", data, parseOptions(args))
}

func StackedBarChart(data any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	if _, set := opts.extra["stack"]; !set {
		opts.extra["stack"] = "stacked"
	}
	return chartDisplay("bar", data, opts)
}

func HorizontalBarChart(data any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	opts.extra["orientation"] = "horizontal"
	return chartDisplay("bar", data, opts)
}

func Chart(data any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	k := opts.kind
	if k == "" {
		k = "line"
	}
	return chartDisplay(k, data, opts)
}

func Histogram(data any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	extra := make(map[string]any)
	for k, v := range opts.extra {
		extra[k] = v
	}
	if opts.bins > 0 {
		extra["bins"] = opts.bins
	}
	return sendDisplay(ChartMime, chartSpec("histogram", data, opts.title, extra))
}

func Scatter3D(data any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	return sendDisplay(Plot3DMime, plot3dSpec("scatter", data, opts.title))
}

func Surface3D(data any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	return sendDisplay(Plot3DMime, plot3dSpec("surface", data, opts.title))
}

func Surface3DFunc(title string, f func(x, y float64) float64, minX, maxX, minY, maxY float64, resolution int) *DisplayHandle {
	n := resolution
	if n < 2 {
		n = 2
	}
	var zGrid [][]any
	for r := 0; r < n; r++ {
		y := minY + (maxY-minY)*float64(r)/float64(n-1)
		var row []any
		for c := 0; c < n; c++ {
			x := minX + (maxX-minX)*float64(c)/float64(n-1)
			row = append(row, f(x, y))
		}
		zGrid = append(zGrid, row)
	}
	spec := map[string]any{
		"kind": "surface",
		"surface": map[string]any{
			"x": map[string]any{"min": minX, "max": maxX},
			"y": map[string]any{"min": minY, "max": maxY},
			"z": zGrid,
		},
	}
	if title != "" {
		spec["title"] = title
	}
	return sendDisplay(Plot3DMime, spec)
}

func VoxelBars(data any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	return sendDisplay(Plot3DMime, plot3dSpec("voxelBar", data, opts.title))
}

type CanvasVisualizer struct {
	title  string
	width  int
	height int
	shapes []map[string]any
}

func Canvas(title string, width, height int) *CanvasVisualizer {
	return &CanvasVisualizer{title: title, width: width, height: height}
}

func (c *CanvasVisualizer) AddRect(x, y, width, height float64, opts ...string) *CanvasVisualizer {
	shape := map[string]any{
		"type": "rect", "x": x, "y": y, "width": width, "height": height,
	}
	if len(opts) > 0 && opts[0] != "" { shape["label"] = opts[0] }
	if len(opts) > 1 && opts[1] != "" { shape["fill"] = opts[1] }
	if len(opts) > 2 && opts[2] != "" { shape["stroke"] = opts[2] }
	c.shapes = append(c.shapes, shape)
	return c
}

func (c *CanvasVisualizer) AddArrow(x1, y1, x2, y2 float64, opts ...string) *CanvasVisualizer {
	shape := map[string]any{
		"type": "arrow", "x1": x1, "y1": y1, "x2": x2, "y2": y2,
	}
	if len(opts) > 0 && opts[0] != "" { shape["label"] = opts[0] }
	if len(opts) > 1 && opts[1] != "" { shape["stroke"] = opts[1] }
	c.shapes = append(c.shapes, shape)
	return c
}

func (c *CanvasVisualizer) AddCircle(cx, cy, radius float64, opts ...string) *CanvasVisualizer {
	shape := map[string]any{
		"type": "circle", "cx": cx, "cy": cy, "radius": radius,
	}
	if len(opts) > 0 && opts[0] != "" { shape["label"] = opts[0] }
	if len(opts) > 1 && opts[1] != "" { shape["fill"] = opts[1] }
	if len(opts) > 2 && opts[2] != "" { shape["stroke"] = opts[2] }
	c.shapes = append(c.shapes, shape)
	return c
}

func (c *CanvasVisualizer) AddLine(x1, y1, x2, y2 float64, stroke ...string) *CanvasVisualizer {
	shape := map[string]any{
		"type": "line", "x1": x1, "y1": y1, "x2": x2, "y2": y2,
	}
	if len(stroke) > 0 && stroke[0] != "" { shape["stroke"] = stroke[0] }
	c.shapes = append(c.shapes, shape)
	return c
}

func (c *CanvasVisualizer) AddText(x, y float64, text string, fontSize int, color ...string) *CanvasVisualizer {
	shape := map[string]any{
		"type": "text", "x": x, "y": y, "text": text, "fontSize": fontSize,
	}
	if len(color) > 0 && color[0] != "" { shape["color"] = color[0] }
	c.shapes = append(c.shapes, shape)
	return c
}

func (c *CanvasVisualizer) Show() *DisplayHandle {
	spec := map[string]any{
		"kind": "canvas",
		"state": map[string]any{
			"canvas": map[string]any{
				"width":  c.width,
				"height": c.height,
				"shapes": c.shapes,
			},
		},
	}
	if c.title != "" {
		spec["title"] = c.title
	}
	return sendDisplay(VisualizerMime, spec)
}

func Graph3D(data any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	return sendDisplay(Plot3DMime, plot3dSpec("graph", data, opts.title))
}

func Matrix(grid any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	return sendDisplay(VisualizerMime, matrixSpec(grid, opts.title))
}

func Islands(grid any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	return sendDisplay(VisualizerMime, islandsSpec(grid, opts.title))
}

func Array(values any, pointers any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	return sendDisplay(VisualizerMime, arraySpec(values, pointers, opts.title))
}

func Tree(root any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	return sendDisplay(VisualizerMime, treeSpec(root, opts.title))
}

func Graph(data any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	return sendDisplay(VisualizerMime, graphVisualizerSpec(data, opts.title))
}

func LinkedList(head any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	return sendDisplay(VisualizerMime, linkedListSpec(head, opts.title))
}

func Bars(values any, args ...any) *DisplayHandle {
	opts := parseOptions(args)
	return sendDisplay(VisualizerMime, barsSpec(values, opts.title))
}

func Table(title string, obj any) {
	bytes, _ := json.Marshal(map[string]any{
		"type": "display",
		"data": map[string]any{
			TableMime: formatTable(title, obj),
		},
		"metadata": map[string]any{},
	})
	emitRaw(string(bytes))
}

func Html(htmlContent string) {
	bytes, _ := json.Marshal(map[string]any{
		"type": "display",
		"data": map[string]any{
			"text/html": htmlContent,
		},
		"metadata": map[string]any{},
	})
	emitRaw(string(bytes))
}

func Markdown(content string) {
	bytes, _ := json.Marshal(map[string]any{
		"type": "display",
		"data": map[string]any{
			"text/markdown": content,
		},
		"metadata": map[string]any{},
	})
	emitRaw(string(bytes))
}

func Image(pathOrBytes any) {
	var b64, mime string
	switch v := pathOrBytes.(type) {
	case []byte:
		b64 = base64.StdEncoding.EncodeToString(v)
		mime = "image/png"
	case string:
		if strings.HasPrefix(v, "data:image/") {
			idx := strings.Index(v, ",")
			if idx >= 0 {
				b64 = v[idx+1:]
				mime = v[5:idx]
			}
		} else if fileBytes, err := os.ReadFile(v); err == nil {
			b64 = base64.StdEncoding.EncodeToString(fileBytes)
			lower := strings.ToLower(v)
			if strings.HasSuffix(lower, ".jpg") || strings.HasSuffix(lower, ".jpeg") {
				mime = "image/jpeg"
			} else if strings.HasSuffix(lower, ".svg") {
				mime = "image/svg+xml"
			} else {
				mime = "image/png"
			}
		}
	}
	if b64 != "" {
		bytes, _ := json.Marshal(map[string]any{
			"type": "display",
			"data": map[string]any{
				mime: b64,
			},
			"metadata": map[string]any{},
		})
		emitRaw(string(bytes))
	}
}

func Json(jsonString string) {
	bytes, _ := json.Marshal(map[string]any{
		"type": "display",
		"data": map[string]any{
			"text/plain": jsonString,
		},
		"metadata": map[string]any{},
	})
	emitRaw(string(bytes))
}

func Dump(v any) any {
	Table("", v)
	return v
}

func Show(v any) any {
	return Dump(v)
}

func Wait(seconds float64) {
	ensureEventSocket()
	end := time.Now().Add(time.Duration(seconds * float64(time.Second)))
	for time.Now().Before(end) {
		time.Sleep(25 * time.Millisecond)
	}
}

func WaitFor(seconds float64) {
	Wait(seconds)
}

func ProcessEvents() {
	// Handled asynchronously by event reader goroutine
}

// --------------------------------------------------------------------------------------------------- Specs
func chartSpec(kind string, data any, title string, extra map[string]any) map[string]any {
	spec := map[string]any{"kind": kind}
	for k, v := range extra {
		spec[k] = v
	}
	var series []map[string]any

	if kind == "bubble" {
		spec["series"] = bubbleSeries(data)
		if title != "" {
			spec["title"] = title
		}
		return spec
	}

	slice := toSlice(data)
	if slice != nil {
		firstPair := toSlice(safeIndex(slice, 0))
		if firstPair != nil && len(firstPair) >= 2 {
			var x, y []any
			for _, item := range slice {
				p := toSlice(item)
				if p != nil && len(p) >= 2 {
					x = append(x, toNum(p[0]))
					y = append(y, toNum(p[1]))
				}
			}
			series = append(series, map[string]any{"x": x, "y": y})
		} else if isRecord(safeIndex(slice, 0)) {
			var y []any
			var labels []string
			valKey, labelKey := findRecordKeys(safeIndex(slice, 0))
			for _, item := range slice {
				m := toMap(item)
				if m != nil {
					y = append(y, toNum(m[valKey]))
					labels = append(labels, fmt.Sprint(m[labelKey]))
				}
			}
			series = append(series, map[string]any{"y": y, "labels": labels})
		} else if kind == "histogram" {
			var values []any
			for _, item := range slice {
				values = append(values, toNum(item))
			}
			series = append(series, map[string]any{"values": values})
		} else {
			var y []any
			for _, item := range slice {
				y = append(y, toNum(item))
			}
			series = append(series, map[string]any{"y": y})
		}
	} else if om, ok := data.(OrderedMap); ok {
		isMultiSeries := false
		for _, e := range om {
			if _, styled := e.Value.(SeriesData); styled || toSlice(e.Value) != nil {
				isMultiSeries = true
				break
			}
		}
		if isMultiSeries {
			for _, e := range om {
				value := e.Value
				styled, hasOptions := value.(SeriesData)
				if hasOptions {
					value = styled.Values
				}
				yVals := toSlice(value)
				var y []any
				if yVals != nil {
					for _, item := range yVals {
						y = append(y, toNum(item))
					}
				}
				one := map[string]any{"name": e.Key, "y": y}
				if hasOptions {
					applySeriesOptions(one, styled.Options)
				}
				series = append(series, one)
			}
		} else {
			var labels []string
			var y []any
			for _, e := range om {
				labels = append(labels, e.Key)
				y = append(y, toNum(e.Value))
			}
			series = append(series, map[string]any{"y": y, "labels": labels})
		}
	} else if m := toMap(data); m != nil {
		isMultiSeries := false
		for _, v := range m {
			if toSlice(v) != nil {
				isMultiSeries = true
				break
			}
		}
		if isMultiSeries {
			for k, v := range m {
				yVals := toSlice(v)
				var y []any
				if yVals != nil {
					for _, item := range yVals {
						y = append(y, toNum(item))
					}
				}
				series = append(series, map[string]any{"name": k, "y": y})
			}
		} else {
			var labels []string
			var y []any
			for k, v := range m {
				labels = append(labels, k)
				y = append(y, toNum(v))
			}
			series = append(series, map[string]any{"y": y, "labels": labels})
		}
	}

	spec["series"] = series
	if title != "" {
		spec["title"] = title
	}
	return spec
}

// bubbleSeries reads (x, y, size) items: a slice of them, or a map of names to slices (a series each).
func bubbleSeries(data any) []map[string]any {
	one := func(items []any, name string) map[string]any {
		series := map[string]any{}
		if name != "" {
			series["name"] = name
		}
		var x, y, sizes []any
		for _, item := range items {
			parts := toSlice(item)
			if len(parts) >= 3 {
				x, y, sizes = append(x, toNum(parts[0])), append(y, toNum(parts[1])), append(sizes, toNum(parts[2]))
			} else {
				x, y, sizes = append(x, nil), append(y, nil), append(sizes, nil)
			}
		}
		series["x"], series["y"], series["sizes"] = x, y, sizes
		return series
	}
	if om, ok := data.(OrderedMap); ok {
		var all []map[string]any
		for _, e := range om {
			all = append(all, one(toSlice(e.Value), e.Key))
		}
		return all
	}
	return []map[string]any{one(toSlice(data), "")}
}

func plot3dSpec(kind string, data any, title string) map[string]any {
	spec := map[string]any{"kind": kind}
	switch kind {
	case "scatter":
		var x, y, z []any
		slice := toSlice(data)
		if slice != nil {
			for _, item := range slice {
				p := toSlice(item)
				if p != nil && len(p) >= 3 {
					x = append(x, toNum(p[0]))
					y = append(y, toNum(p[1]))
					z = append(z, toNum(p[2]))
				}
			}
		}
		spec["series"] = []map[string]any{{"x": x, "y": y, "z": z}}
	case "surface":
		rows := toSlice(data)
		numRows := len(rows)
		numCols := 0
		var zGrid [][]any
		for _, r := range rows {
			cols := toSlice(r)
			if cols != nil {
				if len(cols) > numCols {
					numCols = len(cols)
				}
				var rowVals []any
				for _, cell := range cols {
					rowVals = append(rowVals, toNum(cell))
				}
				zGrid = append(zGrid, rowVals)
			}
		}
		maxCols := 0
		if numCols > 0 {
			maxCols = numCols - 1
		}
		maxRows := 0
		if numRows > 0 {
			maxRows = numRows - 1
		}
		spec["surface"] = map[string]any{
			"x": map[string]any{"min": 0, "max": maxCols},
			"y": map[string]any{"min": 0, "max": maxRows},
			"z": zGrid,
		}
	case "voxelBar":
		rows := toSlice(data)
		var xs, ys, zs, labels []any
		if rows != nil {
			for r, row := range rows {
				cols := toSlice(row)
				if cols != nil {
					for c, cell := range cols {
						val := toNum(cell)
						if val != nil {
							xs = append(xs, r)
							ys = append(ys, c)
							zs = append(zs, val)
							labels = append(labels, fmt.Sprintf("[%d,%d]=%v", r, c, val))
						}
					}
				}
			}
		}
		spec["series"] = []map[string]any{{"x": xs, "y": ys, "z": zs, "labels": labels}}
	case "graph":
		spec["graph"] = parseGraphData(data)
	}
	if title != "" {
		spec["title"] = title
	}
	return spec
}

func parseGraphData(data any) map[string]any {
	nodeSet := make(map[string]bool)
	var edges []map[string]any
	var nodeOrder []string

	processMap := func(m map[string]any) {
		for k, v := range m {
			if !nodeSet[k] {
				nodeSet[k] = true
				nodeOrder = append(nodeOrder, k)
			}
			targets := toSlice(v)
			if targets != nil {
				for _, t := range targets {
					ts := fmt.Sprint(t)
					if !nodeSet[ts] {
						nodeSet[ts] = true
						nodeOrder = append(nodeOrder, ts)
					}
					edges = append(edges, map[string]any{"from": k, "to": ts})
				}
			}
		}
	}

	if om, ok := data.(OrderedMap); ok {
		for _, e := range om {
			if !nodeSet[e.Key] {
				nodeSet[e.Key] = true
				nodeOrder = append(nodeOrder, e.Key)
			}
			targets := toSlice(e.Value)
			if targets != nil {
				for _, t := range targets {
					ts := fmt.Sprint(t)
					if !nodeSet[ts] {
						nodeSet[ts] = true
						nodeOrder = append(nodeOrder, ts)
					}
					edges = append(edges, map[string]any{"from": e.Key, "to": ts})
				}
			}
		}
	} else if m := toMap(data); m != nil {
		processMap(m)
	}

	var nodes []map[string]any
	for _, n := range nodeOrder {
		nodes = append(nodes, map[string]any{"id": n})
	}
	return map[string]any{
		"directed": true,
		"nodes":    nodes,
		"edges":    edges,
	}
}

func matrixSpec(grid any, title string) map[string]any {
	spec := map[string]any{
		"kind":  "matrix",
		"state": map[string]any{"grid": map[string]any{"values": to2DSlice(grid)}},
	}
	if title != "" {
		spec["title"] = title
	}
	return spec
}

func islandsSpec(grid any, title string) map[string]any {
	spec := map[string]any{
		"kind":  "islands",
		"state": map[string]any{"grid": map[string]any{"values": to2DSlice(grid)}},
	}
	if title != "" {
		spec["title"] = title
	}
	return spec
}

func arraySpec(values, pointers any, title string) map[string]any {
	spec := map[string]any{
		"kind":  "arrayPointers",
		"state": map[string]any{"array": map[string]any{"values": toCleanSlice(values)}},
	}
	var ptrList []map[string]any
	if om, ok := pointers.(OrderedMap); ok {
		for _, e := range om {
			ptrList = append(ptrList, map[string]any{"name": e.Key, "at": toNum(e.Value)})
		}
	} else if pm := toMap(pointers); pm != nil {
		for k, v := range pm {
			ptrList = append(ptrList, map[string]any{"name": k, "at": toNum(v)})
		}
	}
	spec["pointers"] = ptrList
	if title != "" {
		spec["title"] = title
	}
	return spec
}

func treeSpec(root any, title string) map[string]any {
	spec := map[string]any{"kind": "tree"}
	nodes := []map[string]any{}
	counter := 1

	slice := toSlice(root)
	if slice != nil {
		if len(slice) > 0 {
			var queue []string
			var nodeMap = make(map[string]map[string]any)

			for i := 0; i < len(slice); i++ {
				val := slice[i]
				if val == nil {
					continue
				}
				id := fmt.Sprintf("node_%d", counter)
				counter++
				m := map[string]any{"id": id, "value": fmt.Sprint(cleanScalar(val))}
				nodeMap[id] = m
				nodes = append(nodes, m)

				if len(queue) == 0 {
					queue = append(queue, id)
				} else {
					parentID := queue[0]
					parent := nodeMap[parentID]
					if parent["left"] == nil && parent["right"] == nil {
						if (i-1)%2 == 0 {
							parent["left"] = id
						} else {
							parent["right"] = id
							queue = queue[1:]
						}
					} else if parent["left"] != nil && parent["right"] == nil {
						parent["right"] = id
						queue = queue[1:]
					}
					queue = append(queue, id)
				}
			}
			rootId := "node_1"
			spec["state"] = map[string]any{"tree": map[string]any{"root": rootId, "nodes": nodes}}
		}
	} else {
		var walk func(n any) string
		walk = func(n any) string {
			if n == nil {
				return ""
			}
			rv := reflect.ValueOf(n)
			if rv.Kind() == reflect.Pointer {
				if rv.IsNil() {
					return ""
				}
				rv = rv.Elem()
			}
			if rv.Kind() != reflect.Struct {
				return ""
			}

			valField := rv.FieldByName("Val")
			if !valField.IsValid() {
				valField = rv.FieldByName("Value")
			}
			val := ""
			if valField.IsValid() {
				val = fmt.Sprint(cleanScalar(valField.Interface()))
			}

			id := fmt.Sprintf("node_%d", counter)
			counter++
			m := map[string]any{"id": id, "value": val}

			leftField := rv.FieldByName("Left")
			if leftField.IsValid() && !leftField.IsNil() {
				leftId := walk(leftField.Interface())
				if leftId != "" {
					m["left"] = leftId
				}
			}
			rightField := rv.FieldByName("Right")
			if rightField.IsValid() && !rightField.IsNil() {
				rightId := walk(rightField.Interface())
				if rightId != "" {
					m["right"] = rightId
				}
			}
			nodes = append(nodes, m)
			return id
		}
		rootId := walk(root)
		spec["state"] = map[string]any{"tree": map[string]any{"root": rootId, "nodes": nodes}}
	}

	if title != "" {
		spec["title"] = title
	}
	return spec
}

func graphVisualizerSpec(data any, title string) map[string]any {
	spec := map[string]any{
		"kind":  "graph",
		"state": map[string]any{"graph": parseGraphData(data)},
	}
	if title != "" {
		spec["title"] = title
	}
	return spec
}

func linkedListSpec(head any, title string) map[string]any {
	spec := map[string]any{"kind": "linkedList"}
	var nodes []map[string]any
	idx := 0
	curr := head

	for curr != nil {
		rv := reflect.ValueOf(curr)
		if rv.Kind() == reflect.Pointer {
			if rv.IsNil() {
				break
			}
			rv = rv.Elem()
		}
		if rv.Kind() != reflect.Struct {
			break
		}

		valField := rv.FieldByName("Val")
		if !valField.IsValid() {
			valField = rv.FieldByName("Value")
		}
		val := ""
		if valField.IsValid() {
			val = fmt.Sprint(cleanScalar(valField.Interface()))
		}
		id := fmt.Sprintf("n%d", idx)
		nextId := fmt.Sprintf("n%d", idx+1)
		nextField := rv.FieldByName("Next")

		hasNext := nextField.IsValid() && !nextField.IsNil()
		m := map[string]any{"id": id, "value": val}
		if hasNext {
			m["next"] = nextId
			curr = nextField.Interface()
		} else {
			curr = nil
		}
		nodes = append(nodes, m)
		idx++
	}

	spec["state"] = map[string]any{"linkedList": map[string]any{"nodes": nodes}}
	if title != "" {
		spec["title"] = title
	}
	return spec
}

func barsSpec(values any, title string) map[string]any {
	spec := map[string]any{
		"kind":  "bars",
		"state": map[string]any{"bars": map[string]any{"values": toCleanSlice(values)}},
	}
	if title != "" {
		spec["title"] = title
	}
	return spec
}

func formatTable(title string, obj any) map[string]any {
	tbl := map[string]any{"title": title}
	if title == "" {
		tbl["title"] = "Table"
	}
	slice := toSlice(obj)
	var cols []string
	var numeric []bool
	var rows [][]any

	if slice != nil && len(slice) > 0 {
		first := toMap(slice[0])
		if first != nil {
			for k, v := range first {
				cols = append(cols, k)
				_, isNum := v.(int)
				if !isNum {
					_, isNum = v.(float64)
				}
				numeric = append(numeric, isNum)
			}
			for _, item := range slice {
				m := toMap(item)
				var row []any
				for _, col := range cols {
					row = append(row, m[col])
				}
				rows = append(rows, row)
			}
		}
	}
	tbl["columns"] = cols
	tbl["numeric"] = numeric
	tbl["rows"] = rows
	return tbl
}

// --------------------------------------------------------------------------------------------------- Helpers
func toSlice(v any) []any {
	if v == nil {
		return nil
	}
	if _, ok := v.(OrderedMap); ok {
		return nil
	}
	if _, ok := v.(*OrderedMap); ok {
		return nil
	}
	if s, ok := v.([]any); ok {
		return s
	}
	rv := reflect.ValueOf(v)
	if rv.Kind() == reflect.Slice || rv.Kind() == reflect.Array {
		res := make([]any, rv.Len())
		for i := 0; i < rv.Len(); i++ {
			res[i] = rv.Index(i).Interface()
		}
		return res
	}
	return nil
}

func to2DSlice(v any) [][]any {
	rows := toSlice(v)
	if rows == nil {
		return nil
	}
	var res [][]any
	for _, r := range rows {
		cols := toSlice(r)
		if cols != nil {
			var row []any
			for _, c := range cols {
				row = append(row, cleanScalar(c))
			}
			res = append(res, row)
		}
	}
	return res
}

func toCleanSlice(v any) []any {
	s := toSlice(v)
	if s == nil {
		return nil
	}
	res := make([]any, len(s))
	for i, item := range s {
		res[i] = cleanScalar(item)
	}
	return res
}

func toMap(v any) map[string]any {
	if v == nil {
		return nil
	}
	if m, ok := v.(map[string]any); ok {
		return m
	}
	if om, ok := v.(OrderedMap); ok {
		m := make(map[string]any, len(om))
		for _, e := range om {
			m[e.Key] = e.Value
		}
		return m
	}
	rv := reflect.ValueOf(v)
	if rv.Kind() == reflect.Map {
		m := make(map[string]any, rv.Len())
		for _, k := range rv.MapKeys() {
			m[fmt.Sprint(k.Interface())] = rv.MapIndex(k).Interface()
		}
		return m
	}
	return nil
}

func safeIndex(s []any, idx int) any {
	if idx >= 0 && idx < len(s) {
		return s[idx]
	}
	return nil
}

func isRecord(v any) bool {
	return toMap(v) != nil
}

func findRecordKeys(rec any) (string, string) {
	m := toMap(rec)
	if m == nil {
		return "value", "name"
	}
	valKey := findKey(m, "value", "y", "amount", "count", "total")
	labelKey := findKey(m, "name", "x", "label", "key", "title")
	return valKey, labelKey
}

func findKey(m map[string]any, candidates ...string) string {
	for _, c := range candidates {
		if _, ok := m[c]; ok {
			return c
		}
	}
	for k := range m {
		return k
	}
	return ""
}

func toNum(v any) any {
	if v == nil {
		return nil
	}
	switch val := v.(type) {
	case int:
		return val
	case int64:
		return val
	case float64:
		if math.IsNaN(val) || math.IsInf(val, 0) {
			return nil
		}
		return val
	case float32:
		f := float64(val)
		if math.IsNaN(f) || math.IsInf(f, 0) {
			return nil
		}
		return f
	case string:
		if f, err := strconv.ParseFloat(val, 64); err == nil {
			if math.IsNaN(f) || math.IsInf(f, 0) {
				return nil
			}
			return f
		}
	}
	return nil
}

func cleanScalar(v any) any {
	if v == nil {
		return nil
	}
	switch val := v.(type) {
	case int, int64, float64, float32:
		return toNum(val)
	case bool, string:
		return val
	}
	return fmt.Sprint(v)
}

func cleanForJson(v any) any {
	if v == nil {
		return nil
	}
	switch val := v.(type) {
	case float64:
		if math.IsNaN(val) || math.IsInf(val, 0) {
			return nil
		}
		return val
	case float32:
		f := float64(val)
		if math.IsNaN(f) || math.IsInf(f, 0) {
			return nil
		}
		return f
	case []any:
		res := make([]any, len(val))
		for i, item := range val {
			res[i] = cleanForJson(item)
		}
		return res
	case map[string]any:
		res := make(map[string]any, len(val))
		for k, item := range val {
			res[k] = cleanForJson(item)
		}
		return res
	case OrderedMap:
		return val
	}
	rv := reflect.ValueOf(v)
	if rv.Kind() == reflect.Slice || rv.Kind() == reflect.Array {
		res := make([]any, rv.Len())
		for i := 0; i < rv.Len(); i++ {
			res[i] = cleanForJson(rv.Index(i).Interface())
		}
		return res
	}
	if rv.Kind() == reflect.Map {
		res := make(map[string]any, rv.Len())
		for _, k := range rv.MapKeys() {
			res[fmt.Sprint(k.Interface())] = cleanForJson(rv.MapIndex(k).Interface())
		}
		return res
	}
	return v
}
