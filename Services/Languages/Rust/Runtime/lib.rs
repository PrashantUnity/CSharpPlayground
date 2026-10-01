//! Display helpers for C# Code Studio: what a Rust script calls to put charts, 3D plots,
//! visualizers, tables, images, and HTML in the Results deck.

use std::collections::HashMap;
use std::fmt::Debug;
use std::io::{BufRead, BufReader, Write};
use std::net::TcpStream;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Arc, Mutex, Once};
use std::thread;
use std::time::{Duration, Instant};

pub const CHART_MIME: &str = "application/vnd.fry.chart.v1+json";
pub const PLOT3D_MIME: &str = "application/vnd.fry.plot3d.v1+json";
pub const VISUALIZER_MIME: &str = "application/vnd.fry.visualizer.v1+json";
pub const TABLE_MIME: &str = "application/vnd.fry.table+json";

pub mod prelude {
    pub use crate::{
        array, bar_chart, bars, chart, dump, graph, graph3d, histogram, html, image, image_file,
        islands, json, line_chart, linked_list, matrix, pie_chart, process_events, scatter3d,
        scatter_chart, share, show, surface3d, table, tree, wait, DisplayHandle, Event, Json,
        ListNode, TreeNode,
    };
}

// ── TreeNode & ListNode ───────────────────────────────────────────────────

#[derive(Debug, Clone)]
pub struct TreeNode<T = i32> {
    pub val: T,
    pub left: Option<Box<TreeNode<T>>>,
    pub right: Option<Box<TreeNode<T>>>,
}

impl<T> TreeNode<T> {
    pub fn new(val: T) -> Self {
        Self {
            val,
            left: None,
            right: None,
        }
    }

    pub fn with_children(val: T, left: TreeNode<T>, right: TreeNode<T>) -> Self {
        Self {
            val,
            left: Some(Box::new(left)),
            right: Some(Box::new(right)),
        }
    }
}

#[derive(Debug, Clone)]
pub struct ListNode<T = i32> {
    pub val: T,
    pub next: Option<Box<ListNode<T>>>,
}

impl<T> ListNode<T> {
    pub fn new(val: T) -> Self {
        Self { val, next: None }
    }

    pub fn with_next(val: T, next: ListNode<T>) -> Self {
        Self {
            val,
            next: Some(Box::new(next)),
        }
    }
}

// ── Events & Handles ──────────────────────────────────────────────────────

#[derive(Debug, Clone)]
pub struct Event {
    pub kind: String,
    pub target: HashMap<String, String>,
    pub raw: String,
}

impl Event {
    pub fn get(&self, key: &str) -> Option<&str> {
        self.target.get(key).map(|s| s.as_str())
    }
}

static ID_COUNTER: AtomicU64 = AtomicU64::new(1);
static SOCKET_INIT: Once = Once::new();

type ListenerFn = Box<dyn Fn(Event) + Send + Sync + 'static>;

fn listeners() -> &'static Mutex<HashMap<String, HashMap<String, Vec<Arc<ListenerFn>>>>> {
    static ONCE: Once = Once::new();
    static mut MAP: *mut Mutex<HashMap<String, HashMap<String, Vec<Arc<ListenerFn>>>>> = std::ptr::null_mut();
    unsafe {
        ONCE.call_once(|| {
            let boxed = Box::new(Mutex::new(HashMap::new()));
            MAP = Box::into_raw(boxed);
        });
        &*MAP
    }
}

fn new_id() -> String {
    let n = ID_COUNTER.fetch_add(1, Ordering::SeqCst);
    format!("rust_{}_{:x}", n, n * 7919)
}

fn ensure_event_socket() {
    SOCKET_INIT.call_once(|| {
        let addr = match std::env::var("FRY_EVENTS") {
            Ok(a) if !a.is_empty() => a,
            _ => return,
        };
        let token = std::env::var("FRY_EVENTS_TOKEN").unwrap_or_default();

        let mut stream = match TcpStream::connect(&addr) {
            Ok(s) => s,
            Err(_) => return,
        };

        let hello = format!("{{\"type\":\"hello\",\"token\":{}}}\n", json_string(&token));
        let _ = stream.write_all(hello.as_bytes());

        thread::spawn(move || {
            let reader = BufReader::new(stream);
            for line_res in reader.lines() {
                let line = match line_res {
                    Ok(l) => l,
                    Err(_) => break,
                };
                let trimmed = line.trim();
                if trimmed.is_empty() {
                    continue;
                }
                dispatch_event_line(trimmed);
            }
        });
    });
}

fn dispatch_event_line(line: &str) {
    let disp_id = extract_json_field(line, "display_id");
    if disp_id.is_empty() {
        return;
    }
    let event_type = extract_json_nested(line, "event", "event").to_lowercase();
    let index_val = extract_json_nested(line, "target", "index");
    let series_val = extract_json_nested(line, "target", "series");

    let mut target = HashMap::new();
    if !index_val.is_empty() {
        target.insert("index".to_string(), index_val);
    }
    if !series_val.is_empty() {
        target.insert("series".to_string(), series_val);
    }

    let event = Event {
        kind: if event_type.is_empty() {
            "click".to_string()
        } else {
            event_type.clone()
        },
        target,
        raw: line.to_string(),
    };

    let callbacks: Vec<Arc<ListenerFn>> = {
        let lock = listeners().lock().unwrap();
        if let Some(events_map) = lock.get(&disp_id) {
            let key = if event_type.is_empty() {
                "click"
            } else {
                &event_type
            };
            events_map.get(key).cloned().unwrap_or_default()
        } else {
            Vec::new()
        }
    };

    for cb in callbacks {
        cb(event.clone());
    }
}

pub struct DisplayHandle {
    pub mime: String,
    pub display_id: String,
    spec: Arc<Mutex<String>>,
    last_update: Arc<Mutex<Instant>>,
}

impl DisplayHandle {
    pub fn update(&self, title: &str) -> &Self {
        self.update_title(title)
    }

    pub fn update_title(&self, title: &str) -> &Self {
        let new_spec = {
            let mut s = self.spec.lock().unwrap();
            let updated = replace_or_insert_title(&s, title);
            *s = updated.clone();
            updated
        };

        let mut last = self.last_update.lock().unwrap();
        let elapsed = last.elapsed();
        if elapsed >= Duration::from_millis(33) {
            *last = Instant::now();
            emit_update(&self.mime, &new_spec, &self.display_id);
        } else {
            let wait = Duration::from_millis(33) - elapsed;
            let mime = self.mime.clone();
            let spec_copy = new_spec.clone();
            let id = self.display_id.clone();
            let last_ref = Arc::clone(&self.last_update);
            thread::spawn(move || {
                thread::sleep(wait);
                let mut l = last_ref.lock().unwrap();
                *l = Instant::now();
                emit_update(&mime, &spec_copy, &id);
            });
        }
        self
    }

    pub fn on<F>(&self, event: &str, callback: F) -> &Self
    where
        F: Fn(Event) + Send + Sync + 'static,
    {
        ensure_event_socket();
        let ev = event.to_lowercase();
        {
            let mut lock = listeners().lock().unwrap();
            let entry = lock
                .entry(self.display_id.clone())
                .or_insert_with(HashMap::new);
            entry.entry(ev.clone()).or_insert_with(Vec::new).push(Arc::new(Box::new(callback)));
        }

        println!(
            "__FRY_DISPLAY__ {{\"type\":\"subscribe\",\"display_id\":{},\"events\":[{}]}}",
            json_string(&self.display_id),
            json_string(&ev)
        );
        self
    }

    pub fn on_click<F>(&self, callback: F) -> &Self
    where
        F: Fn(Event) + Send + Sync + 'static,
    {
        self.on("click", callback)
    }

    pub fn off(&self, event: &str) -> &Self {
        let ev = event.to_lowercase();
        {
            let mut lock = listeners().lock().unwrap();
            if let Some(entry) = lock.get_mut(&self.display_id) {
                entry.remove(&ev);
            }
        }
        println!(
            "__FRY_DISPLAY__ {{\"type\":\"unsubscribe\",\"display_id\":{},\"events\":[{}]}}",
            json_string(&self.display_id),
            json_string(&ev)
        );
        self
    }

    pub fn close(&self) {
        let mut lock = listeners().lock().unwrap();
        lock.remove(&self.display_id);
    }
}

pub fn wait(seconds: f64) {
    ensure_event_socket();
    let end = Instant::now() + Duration::from_secs_f64(seconds);
    while Instant::now() < end {
        thread::sleep(Duration::from_millis(25));
    }
}

pub fn process_events() {}

fn emit_display(mime: &str, spec_json: &str) -> DisplayHandle {
    let id = new_id();
    let fallback = format!("{}: visual", mime);
    println!(
        "__FRY_DISPLAY__ {{\"type\":\"display\",\"data\":{{{}:{},\"text/plain\":{}}},\"metadata\":{{}},\"transient\":{{\"display_id\":{}}}}}",
        json_string(mime),
        spec_json,
        json_string(&fallback),
        json_string(&id)
    );
    DisplayHandle {
        mime: mime.to_string(),
        display_id: id,
        spec: Arc::new(Mutex::new(spec_json.to_string())),
        last_update: Arc::new(Mutex::new(Instant::now())),
    }
}

fn emit_update(mime: &str, spec_json: &str, display_id: &str) {
    let fallback = format!("{}: visual (updated)", mime);
    println!(
        "__FRY_DISPLAY__ {{\"type\":\"update_display\",\"data\":{{{}:{},\"text/plain\":{}}},\"metadata\":{{}},\"transient\":{{\"display_id\":{}}}}}",
        json_string(mime),
        spec_json,
        json_string(&fallback),
        json_string(display_id)
    );
}

// ── Builders ──────────────────────────────────────────────────────────────

pub struct ChartBuilder {
    kind: &'static str,
    data_debug: String,
    title: Option<String>,
    bins: Option<usize>,
}

impl ChartBuilder {
    pub fn title(mut self, title: impl Into<String>) -> Self {
        self.title = Some(title.into());
        self
    }

    pub fn bins(mut self, bins: usize) -> Self {
        self.bins = Some(bins);
        self
    }

    pub fn show(self) -> DisplayHandle {
        let node = parse(&self.data_debug);
        let spec = build_chart_spec(
            self.kind,
            node.as_ref(),
            self.title.as_deref(),
            self.bins,
        );
        emit_display(CHART_MIME, &spec)
    }
}

pub struct Plot3DBuilder {
    kind: &'static str,
    data_debug: String,
    title: Option<String>,
}

impl Plot3DBuilder {
    pub fn title(mut self, title: impl Into<String>) -> Self {
        self.title = Some(title.into());
        self
    }

    pub fn show(self) -> DisplayHandle {
        let node = parse(&self.data_debug);
        let spec = build_plot3d_spec(self.kind, node.as_ref(), self.title.as_deref());
        emit_display(PLOT3D_MIME, &spec)
    }
}

pub struct VisualizerBuilder {
    kind: &'static str,
    data_debug: String,
    extra_debug: Option<String>,
    title: Option<String>,
}

impl VisualizerBuilder {
    pub fn title(mut self, title: impl Into<String>) -> Self {
        self.title = Some(title.into());
        self
    }

    pub fn show(self) -> DisplayHandle {
        let node = parse(&self.data_debug);
        let extra_node = self.extra_debug.as_deref().and_then(parse);
        let spec = build_visualizer_spec(
            self.kind,
            node.as_ref(),
            extra_node.as_ref(),
            self.title.as_deref(),
        );
        emit_display(VISUALIZER_MIME, &spec)
    }
}

// ── Top-level API ─────────────────────────────────────────────────────────

pub fn line_chart<T: Debug + ?Sized>(data: &T) -> ChartBuilder {
    ChartBuilder {
        kind: "line",
        data_debug: format!("{:#?}", data),
        title: None,
        bins: None,
    }
}

pub fn scatter_chart<T: Debug + ?Sized>(data: &T) -> ChartBuilder {
    ChartBuilder {
        kind: "scatter",
        data_debug: format!("{:#?}", data),
        title: None,
        bins: None,
    }
}

pub fn bar_chart<T: Debug + ?Sized>(data: &T) -> ChartBuilder {
    ChartBuilder {
        kind: "bar",
        data_debug: format!("{:#?}", data),
        title: None,
        bins: None,
    }
}

pub fn pie_chart<T: Debug + ?Sized>(data: &T) -> ChartBuilder {
    ChartBuilder {
        kind: "pie",
        data_debug: format!("{:#?}", data),
        title: None,
        bins: None,
    }
}

pub fn chart<T: Debug + ?Sized>(data: &T) -> ChartBuilder {
    ChartBuilder {
        kind: "line",
        data_debug: format!("{:#?}", data),
        title: None,
        bins: None,
    }
}

pub fn histogram<T: Debug + ?Sized>(data: &T) -> ChartBuilder {
    ChartBuilder {
        kind: "histogram",
        data_debug: format!("{:#?}", data),
        title: None,
        bins: None,
    }
}

pub fn scatter3d<T: Debug + ?Sized>(data: &T) -> Plot3DBuilder {
    Plot3DBuilder {
        kind: "scatter",
        data_debug: format!("{:#?}", data),
        title: None,
    }
}

pub fn surface3d<T: Debug + ?Sized>(data: &T) -> Plot3DBuilder {
    Plot3DBuilder {
        kind: "surface",
        data_debug: format!("{:#?}", data),
        title: None,
    }
}

pub fn graph3d<T: Debug + ?Sized>(data: &T) -> Plot3DBuilder {
    Plot3DBuilder {
        kind: "graph",
        data_debug: format!("{:#?}", data),
        title: None,
    }
}

pub fn voxel_bars<T: Debug + ?Sized>(data: &T) -> Plot3DBuilder {
    Plot3DBuilder {
        kind: "voxelBar",
        data_debug: format!("{:#?}", data),
        title: None,
    }
}


pub fn matrix<T: Debug + ?Sized>(grid: &T) -> VisualizerBuilder {
    VisualizerBuilder {
        kind: "matrix",
        data_debug: format!("{:#?}", grid),
        extra_debug: None,
        title: None,
    }
}

pub fn islands<T: Debug + ?Sized>(grid: &T) -> VisualizerBuilder {
    VisualizerBuilder {
        kind: "islands",
        data_debug: format!("{:#?}", grid),
        extra_debug: None,
        title: None,
    }
}

pub fn array<T: Debug + ?Sized, P: Debug + ?Sized>(values: &T, pointers: &P) -> VisualizerBuilder {
    VisualizerBuilder {
        kind: "arrayPointers",
        data_debug: format!("{:#?}", values),
        extra_debug: Some(format!("{:#?}", pointers)),
        title: None,
    }
}

pub fn tree<T: Debug + ?Sized>(root: &T) -> VisualizerBuilder {
    VisualizerBuilder {
        kind: "tree",
        data_debug: format!("{:#?}", root),
        extra_debug: None,
        title: None,
    }
}

pub fn graph<T: Debug + ?Sized>(data: &T) -> VisualizerBuilder {
    VisualizerBuilder {
        kind: "graph",
        data_debug: format!("{:#?}", data),
        extra_debug: None,
        title: None,
    }
}

pub fn linked_list<T: Debug + ?Sized>(head: &T) -> VisualizerBuilder {
    VisualizerBuilder {
        kind: "linkedList",
        data_debug: format!("{:#?}", head),
        extra_debug: None,
        title: None,
    }
}

pub fn bars<T: Debug + ?Sized>(values: &T) -> VisualizerBuilder {
    VisualizerBuilder {
        kind: "bars",
        data_debug: format!("{:#?}", values),
        extra_debug: None,
        title: None,
    }
}

// ── Freeform Vector Canvas Visualizer ────────────────────────────────────────

pub struct CanvasVisualizer {
    title: String,
    width: u32,
    height: u32,
    shapes: Vec<String>,
}

impl CanvasVisualizer {
    pub fn new(title: impl Into<String>, width: u32, height: u32) -> Self {
        Self {
            title: title.into(),
            width,
            height,
            shapes: Vec::new(),
        }
    }

    pub fn add_rect(&mut self, x: f64, y: f64, width: f64, height: f64, label: &str, fill: &str, stroke: &str) -> &mut Self {
        let mut s = format!("{{\"type\":\"rect\",\"x\":{},\"y\":{},\"width\":{},\"height\":{}", x, y, width, height);
        if !label.is_empty() { s.push_str(&format!(",\"label\":{}", json_string(label))); }
        if !fill.is_empty() { s.push_str(&format!(",\"fill\":{}", json_string(fill))); }
        if !stroke.is_empty() { s.push_str(&format!(",\"stroke\":{}", json_string(stroke))); }
        s.push('}');
        self.shapes.push(s);
        self
    }

    pub fn add_arrow(&mut self, x1: f64, y1: f64, x2: f64, y2: f64, label: &str, stroke: &str) -> &mut Self {
        let mut s = format!("{{\"type\":\"arrow\",\"x1\":{},\"y1\":{},\"x2\":{},\"y2\":{}", x1, y1, x2, y2);
        if !label.is_empty() { s.push_str(&format!(",\"label\":{}", json_string(label))); }
        if !stroke.is_empty() { s.push_str(&format!(",\"stroke\":{}", json_string(stroke))); }
        s.push('}');
        self.shapes.push(s);
        self
    }

    pub fn add_circle(&mut self, cx: f64, cy: f64, radius: f64, label: &str, fill: &str, stroke: &str) -> &mut Self {
        let mut s = format!("{{\"type\":\"circle\",\"cx\":{},\"cy\":{},\"radius\":{}", cx, cy, radius);
        if !label.is_empty() { s.push_str(&format!(",\"label\":{}", json_string(label))); }
        if !fill.is_empty() { s.push_str(&format!(",\"fill\":{}", json_string(fill))); }
        if !stroke.is_empty() { s.push_str(&format!(",\"stroke\":{}", json_string(stroke))); }
        s.push('}');
        self.shapes.push(s);
        self
    }

    pub fn add_line(&mut self, x1: f64, y1: f64, x2: f64, y2: f64, stroke: &str) -> &mut Self {
        let mut s = format!("{{\"type\":\"line\",\"x1\":{},\"y1\":{},\"x2\":{},\"y2\":{}", x1, y1, x2, y2);
        if !stroke.is_empty() { s.push_str(&format!(",\"stroke\":{}", json_string(stroke))); }
        s.push('}');
        self.shapes.push(s);
        self
    }

    pub fn add_text(&mut self, x: f64, y: f64, text: &str, font_size: u32, color: &str) -> &mut Self {
        let mut s = format!("{{\"type\":\"text\",\"x\":{},\"y\":{},\"text\":{},\"fontSize\":{}", x, y, json_string(text), font_size);
        if !color.is_empty() { s.push_str(&format!(",\"color\":{}", json_string(color))); }
        s.push('}');
        self.shapes.push(s);
        self
    }

    pub fn show(&self) -> DisplayHandle {
        let mut spec = format!(
            "{{\"kind\":\"canvas\",\"state\":{{\"canvas\":{{\"width\":{},\"height\":{},\"shapes\":[{}]}}}}",
            self.width, self.height, self.shapes.join(",")
        );
        if !self.title.is_empty() {
            spec.push_str(&format!(",\"title\":{}", json_string(&self.title)));
        }
        spec.push('}');
        emit_display(VISUALIZER_MIME, &spec)
    }
}

pub fn canvas(title: impl Into<String>, width: u32, height: u32) -> CanvasVisualizer {
    CanvasVisualizer::new(title, width, height)
}

// ── Interactive 3D Surface Function ────────────────────────────────────────

pub struct Surface3D {
    title: String,
    z_grid: Vec<Vec<f64>>,
    min_x: f64,
    max_x: f64,
    min_y: f64,
    max_y: f64,
}

impl Surface3D {
    pub fn new<F: Fn(f64, f64) -> f64>(
        title: impl Into<String>,
        func: F,
        min_x: f64,
        max_x: f64,
        min_y: f64,
        max_y: f64,
        resolution: usize,
    ) -> Self {
        let n = resolution.max(2);
        let mut z_grid = Vec::with_capacity(n);
        for r in 0..n {
            let mut row = Vec::with_capacity(n);
            let y = min_y + (max_y - min_y) * (r as f64) / ((n - 1) as f64);
            for c in 0..n {
                let x = min_x + (max_x - min_x) * (c as f64) / ((n - 1) as f64);
                row.push(func(x, y));
            }
            z_grid.push(row);
        }
        Self {
            title: title.into(),
            z_grid,
            min_x,
            max_x,
            min_y,
            max_y,
        }
    }

    pub fn show(&self) -> DisplayHandle {
        let z_rows: Vec<String> = self.z_grid.iter().map(|row| {
            format!("[{}]", row.iter().map(|v| v.to_string()).collect::<Vec<_>>().join(","))
        }).collect();
        let mut spec = format!(
            "{{\"kind\":\"surface\",\"surface\":{{\"x\":{{\"min\":{},\"max\":{}}},\"y\":{{\"min\":{},\"max\":{}}},\"z\":[{}]}}",
            self.min_x, self.max_x, self.min_y, self.max_y, z_rows.join(",")
        );
        if !self.title.is_empty() {
            spec.push_str(&format!(",\"title\":{}", json_string(&self.title)));
        }
        spec.push('}');
        emit_display(PLOT3D_MIME, &spec)
    }
}

pub fn plot3d_surface<F: Fn(f64, f64) -> f64>(
    title: impl Into<String>,
    func: F,
    min_x: f64,
    max_x: f64,
    min_y: f64,
    max_y: f64,
    resolution: usize,
) -> DisplayHandle {
    Surface3D::new(title, func, min_x, max_x, min_y, max_y, resolution).show()
}

#[derive(Debug, Clone)]
pub struct Point3D {
    pub x: f64,
    pub y: f64,
    pub z: f64,
    pub label: Option<String>,
}

impl Point3D {
    pub fn new(x: f64, y: f64, z: f64) -> Self {
        Self { x, y, z, label: None }
    }
    pub fn with_label(x: f64, y: f64, z: f64, label: impl Into<String>) -> Self {
        Self { x, y, z, label: Some(label.into()) }
    }
}

// ── Spec Generation ───────────────────────────────────────────────────────

fn build_chart_spec(
    kind: &str,
    node: Option<&Node>,
    title: Option<&str>,
    bins: Option<usize>,
) -> String {
    let mut out = String::new();
    out.push_str("{\"kind\":");
    out.push_str(&json_string(kind));

    if kind == "histogram" {
        if let Some(b) = bins {
            out.push_str(&format!(",\"bins\":{}", b));
        }
        out.push_str(",\"series\":[{\"values\":");
        if let Some(n) = node {
            out.push_str(&extract_numbers_array(n));
        } else {
            out.push_str("[]");
        }
        out.push_str("}]");
    } else {
        out.push_str(",\"series\":");
        out.push_str(&build_chart_series(node));
    }

    if let Some(t) = title {
        out.push_str(",\"title\":");
        out.push_str(&json_string(t));
    }
    out.push('}');
    out
}

fn build_chart_series(node: Option<&Node>) -> String {
    let node = match node {
        Some(n) => n,
        None => return "[]".to_string(),
    };

    if let Some(entries) = extract_key_value_entries(node) {
        // Multi-series or single labeled series
        let is_multi = entries
            .iter()
            .any(|(_, v)| matches!(v, Node::List(_) | Node::Tuple(_, _)));
        if is_multi {
            let mut s_out = String::from("[");
            for (i, (k, v)) in entries.iter().enumerate() {
                if i > 0 {
                    s_out.push(',');
                }
                s_out.push_str(&format!(
                    "{{\"name\":{},\"y\":{}}}",
                    json_string(k),
                    extract_numbers_array(v)
                ));
            }
            s_out.push(']');
            return s_out;
        } else {
            let mut labels = Vec::new();
            let mut ys = Vec::new();
            for (k, v) in entries {
                labels.push(json_string(&k));
                ys.push(node_number_or_null(&v));
            }
            return format!(
                "[{{\"y\":[{}],\"labels\":[{}]}}]",
                ys.join(","),
                labels.join(",")
            );
        }
    }

    // Check list of items
    let items = match node {
        Node::List(list) | Node::Tuple(None, list) => list,
        _ => return "[]".to_string(),
    };

    if items.is_empty() {
        return "[]".to_string();
    }

    // Records like [{"name":"Jan","value":10}, ...]
    if let Some(first_record) = extract_record_fields(&items[0]) {
        let (val_key, label_key) = find_record_keys(&first_record);
        let mut labels = Vec::new();
        let mut ys = Vec::new();
        for item in items {
            if let Some(fields) = extract_record_fields(item) {
                let y = fields
                    .iter()
                    .find(|(k, _)| k == &val_key)
                    .map(|(_, v)| node_number_or_null(v))
                    .unwrap_or_else(|| "null".to_string());
                let lbl = fields
                    .iter()
                    .find(|(k, _)| k == &label_key)
                    .map(|(_, v)| v.render_plain())
                    .unwrap_or_default();
                ys.push(y);
                labels.push(json_string(&lbl));
            }
        }
        return format!(
            "[{{\"y\":[{}],\"labels\":[{}]}}]",
            ys.join(","),
            labels.join(",")
        );
    }

    // Pairs [[1, 2], [2, 4.5]]
    if let Some(pair) = as_pair_or_slice(&items[0]) {
        if pair.len() >= 2 {
            let mut xs = Vec::new();
            let mut ys = Vec::new();
            for item in items {
                if let Some(p) = as_pair_or_slice(item) {
                    if p.len() >= 2 {
                        xs.push(node_number_or_null(&p[0]));
                        ys.push(node_number_or_null(&p[1]));
                    }
                }
            }
            return format!("[{{\"x\":[{}],\"y\":[{}]}}]", xs.join(","), ys.join(","));
        }
    }

    // Single array of y numbers
    let mut ys = Vec::new();
    for item in items {
        ys.push(node_number_or_null(item));
    }
    format!("[{{\"y\":[{}]}}]", ys.join(","))
}

fn build_plot3d_spec(kind: &str, node: Option<&Node>, title: Option<&str>) -> String {
    let mut out = String::new();
    out.push_str("{\"kind\":");
    out.push_str(&json_string(kind));

    match kind {
        "scatter" => {
            let mut xs = Vec::new();
            let mut ys = Vec::new();
            let mut zs = Vec::new();
            if let Some(Node::List(items) | Node::Tuple(None, items)) = node {
                for item in items {
                    if let Some(pts) = as_pair_or_slice(item) {
                        if pts.len() >= 3 {
                            xs.push(node_number_or_null(&pts[0]));
                            ys.push(node_number_or_null(&pts[1]));
                            zs.push(node_number_or_null(&pts[2]));
                        }
                    } else if let Node::Struct(_, fields) = item {
                        let mut x_val = "0".to_string();
                        let mut y_val = "0".to_string();
                        let mut z_val = "0".to_string();
                        for (fname, fnode) in fields {
                            match fname.as_str() {
                                "x" => x_val = node_number_or_null(fnode),
                                "y" => y_val = node_number_or_null(fnode),
                                "z" => z_val = node_number_or_null(fnode),
                                _ => {}
                            }
                        }
                        xs.push(x_val);
                        ys.push(y_val);
                        zs.push(z_val);
                    }
                }
            }
            out.push_str(&format!(
                ",\"series\":[{{\"x\":[{}],\"y\":[{}],\"z\":[{}]}}]",
                xs.join(","),
                ys.join(","),
                zs.join(",")
            ));
        }
        "voxelBar" => {
            let mut xs = Vec::new();
            let mut ys = Vec::new();
            let mut zs = Vec::new();
            let mut labels = Vec::new();
            if let Some(Node::List(rows) | Node::Tuple(None, rows)) = node {
                for (r_idx, r) in rows.iter().enumerate() {
                    if let Node::List(cols) | Node::Tuple(None, cols) = r {
                        for (c_idx, c) in cols.iter().enumerate() {
                            let num_str = node_number_or_null(c);
                            if num_str != "null" {
                                xs.push(r_idx.to_string());
                                ys.push(c_idx.to_string());
                                zs.push(num_str.clone());
                                labels.push(format!("\"[{},{}]={}\"", r_idx, c_idx, num_str));
                            }
                        }
                    }
                }
            }
            out.push_str(&format!(
                ",\"series\":[{{\"x\":[{}],\"y\":[{}],\"z\":[{}],\"labels\":[{}]}}]",
                xs.join(","),
                ys.join(","),
                zs.join(","),
                labels.join(",")
            ));
        }
        "surface" => {
            let mut z_rows = Vec::new();
            if let Some(Node::List(rows) | Node::Tuple(None, rows)) = node {
                for r in rows {
                    z_rows.push(extract_numbers_array(r));
                }
            }
            let height = z_rows.len().saturating_sub(1);
            let width = if !z_rows.is_empty() {
                if let Some(Node::List(rows)) = node {
                    if let Some(Node::List(first)) = rows.first() {
                        first.len().saturating_sub(1)
                    } else {
                        1
                    }
                } else {
                    1
                }
            } else {
                1
            };
            out.push_str(&format!(
                ",\"surface\":{{\"x\":{{\"min\":0,\"max\":{}}},\"y\":{{\"min\":0,\"max\":{}}},\"z\":[{}]}}",
                width,
                height,
                z_rows.join(",")
            ));
        }
        "graph" => {
            let (nodes, edges) = extract_graph_nodes_and_edges(node);
            out.push_str(&format!(
                ",\"graph\":{{\"directed\":true,\"nodes\":[{}],\"edges\":[{}]}}",
                nodes.join(","),
                edges.join(",")
            ));
        }
        _ => {}
    }

    if let Some(t) = title {
        out.push_str(",\"title\":");
        out.push_str(&json_string(t));
    }
    out.push('}');
    out
}

fn build_visualizer_spec(
    kind: &str,
    node: Option<&Node>,
    extra: Option<&Node>,
    title: Option<&str>,
) -> String {
    let mut out = String::new();
    out.push_str("{\"kind\":");
    out.push_str(&json_string(kind));
    out.push_str(",\"state\":{");

    match kind {
        "matrix" | "islands" => {
            let (grid_vals, cells) = extract_matrix_cells(node);
            out.push_str(&format!(
                "\"grid\":{{\"values\":[{}],\"cells\":[{}],\"inferTerrain\":false}}",
                grid_vals.join(","),
                cells.join(",")
            ));
        }
        "bars" => {
            let vals = extract_numbers_array(node.unwrap_or(&Node::List(Vec::new())));
            let (min_v, max_v) = compute_min_max(node);
            out.push_str(&format!(
                "\"bars\":{{\"values\":{},\"min\":{},\"max\":{}}}",
                vals, min_v, max_v
            ));
        }
        "arrayPointers" => {
            let vals = extract_numbers_array(node.unwrap_or(&Node::List(Vec::new())));
            out.push_str(&format!("\"array\":{{\"values\":{}}}", vals));
        }
        "tree" => {
            let tree_json = build_tree_json(node);
            out.push_str(&format!("\"tree\":{}", tree_json));
        }
        "linkedList" => {
            let list_nodes = build_linked_list_json(node);
            out.push_str(&format!(
                "\"linkedList\":{{\"nodes\":[{}],\"markCycle\":false}}",
                list_nodes.join(",")
            ));
        }
        "graph" => {
            let (nodes, edges) = extract_graph_nodes_and_edges(node);
            out.push_str(&format!(
                "\"graph\":{{\"directed\":true,\"nodes\":[{}],\"edges\":[{}]}}",
                nodes.join(","),
                edges.join(",")
            ));
        }
        _ => {}
    }
    out.push('}');

    if kind == "arrayPointers" {
        if let Some(ptr_node) = extra {
            let ptrs = extract_pointers(ptr_node);
            out.push_str(&format!(",\"pointers\":[{}]", ptrs.join(",")));
        }
    }

    out.push_str(",\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true");
    if let Some(t) = title {
        out.push_str(",\"title\":");
        out.push_str(&json_string(t));
    }
    out.push('}');
    out
}

fn build_tree_json(node: Option<&Node>) -> String {
    let node = match node {
        Some(n) => n,
        None => return "{\"nodes\":[]}".to_string(),
    };

    // Check if level-order array: [1, 2, 3, None, 4]
    if let Node::List(items) | Node::Tuple(None, items) = node {
        let is_level_order = items.iter().any(|it| {
            matches!(
                it,
                Node::Tuple(Some(w), _) if w == "Some" || matches!(it, Node::Ident(w) if w == "None")
            )
        });
        if is_level_order {
            return build_tree_level_order(items);
        }
    }

    // Recursive TreeNode struct
    let mut nodes = Vec::new();
    let mut counter = 0;
    fn walk(n: &Node, counter: &mut usize, nodes: &mut Vec<String>) -> Option<String> {
        let fields = match n {
            Node::Struct(_, f) => f,
            Node::Tuple(Some(name), items) if name == "Some" && items.len() == 1 => {
                return walk(&items[0], counter, nodes);
            }
            _ => return None,
        };
        *counter += 1;
        let id = format!("node_{}", counter);
        let val = fields
            .iter()
            .find(|(k, _)| k == "val" || k == "value")
            .map(|(_, v)| v.render_plain())
            .unwrap_or_default();
        let my_idx = nodes.len();
        nodes.push(String::new());
        let left_child = fields
            .iter()
            .find(|(k, _)| k == "left")
            .and_then(|(_, v)| walk(v, counter, nodes));
        let right_child = fields
            .iter()
            .find(|(k, _)| k == "right")
            .and_then(|(_, v)| walk(v, counter, nodes));

        let mut node_obj = format!("{{\"id\":{},\"value\":{}", json_string(&id), json_string(&val));
        if let Some(l) = left_child {
            node_obj.push_str(&format!(",\"left\":{}", json_string(&l)));
        }
        if let Some(r) = right_child {
            node_obj.push_str(&format!(",\"right\":{}", json_string(&r)));
        }
        node_obj.push('}');
        nodes[my_idx] = node_obj;
        Some(id)
    }

    if let Some(_) = walk(node, &mut counter, &mut nodes) {
        format!("{{\"root\":\"node_1\",\"nodes\":[{}]}}", nodes.join(","))
    } else {
        "{\"nodes\":[]}".to_string()
    }
}

fn build_tree_level_order(items: &[Node]) -> String {
    if items.is_empty() {
        return "{\"nodes\":[]}".to_string();
    }
    struct QItem {
        id: String,
        val: String,
        left: Option<String>,
        right: Option<String>,
    }
    let mut values: Vec<Option<String>> = Vec::new();
    for it in items {
        match it {
            Node::Tuple(Some(name), sub) if name == "Some" && sub.len() == 1 => {
                values.push(Some(sub[0].render_plain()));
            }
            Node::Ident(w) if w == "None" => {
                values.push(None);
            }
            Node::Number(n) => {
                values.push(Some(n.clone()));
            }
            _ => values.push(None),
        }
    }

    if values.is_empty() || values[0].is_none() {
        return "{\"nodes\":[]}".to_string();
    }

    let mut counter = 1;
    let mut q = Vec::new();
    let root = QItem {
        id: "node_1".to_string(),
        val: values[0].clone().unwrap(),
        left: None,
        right: None,
    };
    q.push(root);

    let mut idx = 1;
    let mut q_idx = 0;
    while q_idx < q.len() && idx < values.len() {
        // left
        if idx < values.len() {
            if let Some(v) = &values[idx] {
                counter += 1;
                let child_id = format!("node_{}", counter);
                q[q_idx].left = Some(child_id.clone());
                q.push(QItem {
                    id: child_id,
                    val: v.clone(),
                    left: None,
                    right: None,
                });
            }
            idx += 1;
        }
        // right
        if idx < values.len() {
            if let Some(v) = &values[idx] {
                counter += 1;
                let child_id = format!("node_{}", counter);
                q[q_idx].right = Some(child_id.clone());
                q.push(QItem {
                    id: child_id,
                    val: v.clone(),
                    left: None,
                    right: None,
                });
            }
            idx += 1;
        }
        q_idx += 1;
    }

    // Depth-first traversal from root to match preorder fixture
    let mut preorder = Vec::new();
    fn dfs(curr_id: &str, items: &[QItem], out: &mut Vec<String>) {
        if let Some(item) = items.iter().find(|it| it.id == curr_id) {
            let mut obj = format!(
                "{{\"id\":{},\"value\":{}",
                json_string(&item.id),
                json_string(&item.val)
            );
            if let Some(l) = &item.left {
                obj.push_str(&format!(",\"left\":{}", json_string(l)));
            }
            if let Some(r) = &item.right {
                obj.push_str(&format!(",\"right\":{}", json_string(r)));
            }
            obj.push('}');
            out.push(obj);

            if let Some(l) = &item.left {
                dfs(l, items, out);
            }
            if let Some(r) = &item.right {
                dfs(r, items, out);
            }
        }
    }
    dfs("node_1", &q, &mut preorder);

    format!(
        "{{\"root\":\"node_1\",\"nodes\":[{}]}}",
        preorder.join(",")
    )
}

fn build_linked_list_json(node: Option<&Node>) -> Vec<String> {
    let mut nodes = Vec::new();
    let mut curr = node;
    let mut idx = 0;

    while let Some(n) = curr {
        let fields = match n {
            Node::Struct(_, f) => f,
            Node::Tuple(Some(name), items) if name == "Some" && items.len() == 1 => match &items[0] {
                Node::Struct(_, f) => f,
                _ => break,
            },
            _ => break,
        };

        let val = fields
            .iter()
            .find(|(k, _)| k == "val" || k == "value")
            .map(|(_, v)| v.render_plain())
            .unwrap_or_default();
        let next_node = fields.iter().find(|(k, _)| k == "next").map(|(_, v)| v);

        let id = format!("n{}", idx);
        idx += 1;
        let mut obj = format!("{{\"id\":{},\"value\":{}", json_string(&id), json_string(&val));

        let has_next = match next_node {
            Some(Node::Tuple(Some(w), sub)) if w == "Some" && !sub.is_empty() => true,
            Some(Node::Struct(_, _)) => true,
            _ => false,
        };

        if has_next {
            obj.push_str(&format!(",\"next\":{}", json_string(&format!("n{}", idx))));
        }
        obj.push('}');
        nodes.push(obj);

        curr = match next_node {
            Some(Node::Tuple(Some(w), sub)) if w == "Some" && !sub.is_empty() => Some(&sub[0]),
            Some(st @ Node::Struct(_, _)) => Some(st),
            _ => None,
        };
    }

    nodes
}

fn extract_matrix_cells(node: Option<&Node>) -> (Vec<String>, Vec<String>) {
    let mut grid_vals = Vec::new();
    let mut cells = Vec::new();

    if let Some(Node::List(rows) | Node::Tuple(None, rows)) = node {
        for (r_idx, r) in rows.iter().enumerate() {
            if let Node::List(cols) | Node::Tuple(None, cols) = r {
                let mut row_vals = Vec::new();
                for (c_idx, c) in cols.iter().enumerate() {
                    let num_str = node_number_or_null(c);
                    let terrain = if num_str == "1" || num_str == "1.0" {
                        "land"
                    } else {
                        "water"
                    };
                    row_vals.push(num_str);
                    cells.push(format!(
                        "{{\"row\":{},\"col\":{},\"terrain\":{}}}",
                        r_idx,
                        c_idx,
                        json_string(terrain)
                    ));
                }
                grid_vals.push(format!("[{}]", row_vals.join(",")));
            }
        }
    }
    (grid_vals, cells)
}

fn extract_pointers(node: &Node) -> Vec<String> {
    let colors = [
        "#38bdf8", "#a855f7", "#22c55e", "#f59e0b", "#ec4899", "#14b8a6", "#f97316", "#6366f1",
    ];
    let mut ptrs = Vec::new();
    if let Some(entries) = extract_key_value_entries(node) {
        for (i, (name, at_node)) in entries.iter().enumerate() {
            let at_val = node_number_or_null(at_node);
            let color = colors[i % colors.len()];
            ptrs.push(format!(
                "{{\"name\":{},\"at\":{},\"color\":{}}}",
                json_string(name),
                at_val,
                json_string(color)
            ));
        }
    }
    ptrs
}

fn extract_graph_nodes_and_edges(node: Option<&Node>) -> (Vec<String>, Vec<String>) {
    let mut nodes_set = Vec::new();
    let mut edges = Vec::new();

    if let Some(n) = node {
        if let Some(entries) = extract_key_value_entries(n) {
            for (from_k, targets_node) in entries {
                if !nodes_set.contains(&from_k) {
                    nodes_set.push(from_k.clone());
                }
                if let Node::List(targets) | Node::Tuple(None, targets) = targets_node {
                    for tgt in targets {
                        let to_id = tgt.render_plain();
                        if !nodes_set.contains(&to_id) {
                            nodes_set.push(to_id.clone());
                        }
                        edges.push(format!(
                            "{{\"from\":{},\"to\":{}}}",
                            json_string(&from_k),
                            json_string(&to_id)
                        ));
                    }
                }
            }
        }
    }

    let node_objs = nodes_set
        .into_iter()
        .map(|id| format!("{{\"id\":{}}}", json_string(&id)))
        .collect();
    (node_objs, edges)
}

fn compute_min_max(node: Option<&Node>) -> (f64, f64) {
    let mut min = 0.0f64;
    let mut max = 0.0f64;
    if let Some(Node::List(items) | Node::Tuple(None, items)) = node {
        for it in items {
            if let Some(num) = it.as_f64() {
                if num < min {
                    min = num;
                }
                if num > max {
                    max = num;
                }
            }
        }
    }
    (min, max)
}

fn node_number_or_null(n: &Node) -> String {
    match n {
        Node::Number(num) => num.clone(),
        Node::Tuple(Some(w), sub) if w == "Some" && sub.len() == 1 => node_number_or_null(&sub[0]),
        Node::Ident(w) if w == "None" || w == "NaN" || w == "inf" => "null".to_string(),
        _ => n.to_json(),
    }
}

fn extract_numbers_array(n: &Node) -> String {
    match n {
        Node::List(items) | Node::Tuple(None, items) => {
            let nums: Vec<String> = items.iter().map(node_number_or_null).collect();
            format!("[{}]", nums.join(","))
        }
        _ => "[]".to_string(),
    }
}

fn extract_key_value_entries(n: &Node) -> Option<Vec<(String, Node)>> {
    match n {
        Node::Map(entries) => {
            let mut list = Vec::new();
            for (k, v) in entries {
                list.push((k.render_plain(), v.clone()));
            }
            Some(list)
        }
        Node::List(items) | Node::Tuple(None, items) => {
            let mut list = Vec::new();
            for it in items {
                if let Node::Tuple(None, pair) = it {
                    if pair.len() == 2 {
                        list.push((pair[0].render_plain(), pair[1].clone()));
                    } else {
                        return None;
                    }
                } else {
                    return None;
                }
            }
            if !list.is_empty() {
                Some(list)
            } else {
                None
            }
        }
        _ => None,
    }
}

fn extract_record_fields(n: &Node) -> Option<Vec<(String, Node)>> {
    match n {
        Node::Struct(_, fields) => Some(fields.clone()),
        _ => None,
    }
}

fn find_record_keys(fields: &[(String, Node)]) -> (String, String) {
    let mut val_key = String::new();
    let mut label_key = String::new();
    for (k, _) in fields {
        let lk = k.to_lowercase();
        if lk == "value" || lk == "y" || lk == "amount" || lk == "count" {
            val_key = k.clone();
        } else if lk == "name" || lk == "label" || lk == "x" || lk == "key" {
            label_key = k.clone();
        }
    }
    if val_key.is_empty() && !fields.is_empty() {
        val_key = fields[0].0.clone();
    }
    if label_key.is_empty() && fields.len() > 1 {
        label_key = fields[1].0.clone();
    }
    (val_key, label_key)
}

fn as_pair_or_slice<'a>(n: &'a Node) -> Option<&'a [Node]> {
    match n {
        Node::List(items) | Node::Tuple(None, items) => Some(items),
        _ => None,
    }
}

fn replace_or_insert_title(spec: &str, title: &str) -> String {
    let title_prop = format!("\"title\":{}", json_string(title));
    if let Some(pos) = spec.find("\"title\":") {
        let after = &spec[pos..];
        let end_pos = if let Some(comma_pos) = after.find(',') {
            pos + comma_pos
        } else if let Some(brace_pos) = after.find('}') {
            pos + brace_pos
        } else {
            pos + after.len()
        };
        format!("{}{}{}", &spec[..pos], title_prop, &spec[end_pos..])
    } else if let Some(last_brace) = spec.rfind('}') {
        if last_brace > 1 && spec.as_bytes()[last_brace - 1] != b'{' {
            format!("{},{}}}", &spec[..last_brace], title_prop)
        } else {
            format!("{}{}}}", &spec[..last_brace], title_prop)
        }
    } else {
        spec.to_string()
    }
}

fn extract_json_field(json: &str, field: &str) -> String {
    let pattern = format!("\"{}\":", field);
    if let Some(pos) = json.find(&pattern) {
        let rest = json[pos + pattern.len()..].trim_start();
        if rest.starts_with('"') {
            if let Some(end) = rest[1..].find('"') {
                return rest[1..end + 1].to_string();
            }
        } else {
            let end = rest
                .find(|c: char| c == ',' || c == '}' || c == ']' || c.is_whitespace())
                .unwrap_or(rest.len());
            return rest[..end].to_string();
        }
    }
    String::new()
}

fn extract_json_nested(json: &str, parent: &str, field: &str) -> String {
    let pattern = format!("\"{}\":", parent);
    if let Some(pos) = json.find(&pattern) {
        let sub = &json[pos + pattern.len()..];
        return extract_json_field(sub, field);
    }
    String::new()
}

// ── Existing Helpers (table, dump, html, json, image, share, Persist) ───

const MAX_ROWS: usize = 500;
const MAX_COLUMNS: usize = 40;
const MAX_DEPTH: usize = 48;

pub fn table<T: Debug + ?Sized>(data: &T, title: &str) {
    let text = format!("{:#?}", data);
    let table = match parse(&text) {
        Some(node) => shape(&node),
        None => Table::new(vec!["value".to_string()], vec![vec![Cell::Text(text)]]),
    };
    emit_table(title, &table);
}

pub fn dump<T: Debug + ?Sized>(data: &T, title: &str) {
    let text = format!("{:#?}", data);
    match parse(&text) {
        Some(node) if node.is_simple() => {
            emit("text/plain", &json_string(&format!("{}: {}\n", title, node.render(false))));
        }
        Some(node) => emit_table(title, &shape(&node)),
        None => emit("text/plain", &json_string(&format!("{}: {}\n", title, text))),
    }
}

pub fn html(markup: &str) {
    emit("text/html", &json_string(markup));
}

pub fn json(text: &str) {
    emit("text/plain", &json_string(text));
}

pub fn image(bytes: &[u8]) {
    if bytes.starts_with(&[0xFF, 0xD8, 0xFF]) {
        emit("image/jpeg", &json_string(&base64(bytes)));
    } else if bytes.starts_with(b"<svg") || bytes.starts_with(b"<?xml") {
        emit("image/svg+xml", &json_string(&String::from_utf8_lossy(bytes)));
    } else {
        emit("image/png", &json_string(&base64(bytes)));
    }
}

pub fn image_file(path: &str) -> std::io::Result<()> {
    let bytes = std::fs::read(path)?;
    image(&bytes);
    Ok(())
}

#[macro_export]
macro_rules! dump {
    ($value:expr) => {
        $crate::dump(&$value, stringify!($value))
    };
    ($value:expr, $title:expr) => {
        $crate::dump(&$value, $title)
    };
}

#[macro_export]
macro_rules! table {
    ($value:expr) => {
        $crate::table(&$value, stringify!($value))
    };
    ($value:expr, $title:expr) => {
        $crate::table(&$value, $title)
    };
}

pub fn show<T: Debug + ?Sized>(data: &T) {
    let text = format!("{:#?}", data);
    if text == "()" {
        return;
    }
    match parse(&text) {
        Some(node) if node.is_simple() || node.wraps_simple() => {
            emit("text/plain", &json_string(&format!("{}\n", node.render(true))))
        }
        Some(node) => emit_table("", &shape(&node)),
        None => emit("text/plain", &json_string(&format!("{}\n", text))),
    }
}

pub fn share<T: Debug + ?Sized>(name: &str, data: &T) {
    let text = format!("{:#?}", data);
    let json = match parse(&text) {
        Some(node) => node.to_json(),
        None => json_string(&text),
    };
    println!(
        "__FRY_SHARE__ {{\"name\":{},\"json\":{}}}",
        json_string(name),
        json_string(&json)
    );
}

#[macro_export]
macro_rules! share {
    ($value:ident) => {
        $crate::share(stringify!($value), &$value)
    };
    ($value:expr, $name:expr) => {
        $crate::share($name, &$value)
    };
}

#[doc(hidden)]
pub mod __auto {
    pub struct Show<'a, T: ?Sized>(pub &'a T);

    pub trait ViaDebug {
        fn __fry_show(&self);
    }

    impl<'a, T: ::std::fmt::Debug + ?Sized> ViaDebug for Show<'a, T> {
        fn __fry_show(&self) {
            crate::show(self.0);
        }
    }

    pub trait ViaNothing {
        fn __fry_show(&self);
    }

    impl<'a, T: ?Sized> ViaNothing for &Show<'a, T> {
        fn __fry_show(&self) {}
    }
}

#[doc(hidden)]
#[macro_export]
macro_rules! __auto {
    ($value:expr) => {{
        #[allow(unused_imports)]
        use $crate::__auto::{ViaDebug as _, ViaNothing as _};
        (&$crate::__auto::Show(&$value)).__fry_show()
    }};
}

pub trait Persist {
    fn persist_type() -> String;
    fn persist_source(&self) -> String;
}

macro_rules! persist_integer {
    ($($t:ty),*) => {$(
        impl Persist for $t {
            fn persist_type() -> String {
                stringify!($t).to_string()
            }
            fn persist_source(&self) -> String {
                format!("{}{}", self, stringify!($t))
            }
        }
    )*};
}
persist_integer!(i8, i16, i32, i64, i128, isize, u8, u16, u32, u64, u128, usize);

macro_rules! persist_float {
    ($($t:ident),*) => {$(
        impl Persist for $t {
            fn persist_type() -> String {
                stringify!($t).to_string()
            }
            fn persist_source(&self) -> String {
                if self.is_nan() {
                    format!("{}::NAN", stringify!($t))
                } else if self.is_infinite() {
                    format!("{}::{}", stringify!($t), if *self > 0.0 { "INFINITY" } else { "NEG_INFINITY" })
                } else {
                    format!("{:?}{}", self, stringify!($t))
                }
            }
        }
    )*};
}
persist_float!(f32, f64);

impl Persist for bool {
    fn persist_type() -> String {
        "bool".to_string()
    }
    fn persist_source(&self) -> String {
        self.to_string()
    }
}

impl Persist for char {
    fn persist_type() -> String {
        "char".to_string()
    }
    fn persist_source(&self) -> String {
        format!("{:?}", self)
    }
}

impl Persist for String {
    fn persist_type() -> String {
        "String".to_string()
    }
    fn persist_source(&self) -> String {
        format!("String::from({:?})", self)
    }
}

impl Persist for &str {
    fn persist_type() -> String {
        "&'static str".to_string()
    }
    fn persist_source(&self) -> String {
        format!("{:?}", self)
    }
}

fn persist_items<'a, T: Persist + 'a>(items: impl Iterator<Item = &'a T>) -> String {
    items.map(|item| item.persist_source()).collect::<Vec<_>>().join(", ")
}

impl<T: Persist> Persist for Vec<T> {
    fn persist_type() -> String {
        format!("Vec<{}>", T::persist_type())
    }
    fn persist_source(&self) -> String {
        if self.is_empty() {
            format!("Vec::<{}>::new()", T::persist_type())
        } else {
            format!("vec![{}]", persist_items(self.iter()))
        }
    }
}

impl<T: Persist, const N: usize> Persist for [T; N] {
    fn persist_type() -> String {
        format!("[{}; {}]", T::persist_type(), N)
    }
    fn persist_source(&self) -> String {
        format!("[{}]", persist_items(self.iter()))
    }
}

impl<T: Persist> Persist for Option<T> {
    fn persist_type() -> String {
        format!("Option<{}>", T::persist_type())
    }
    fn persist_source(&self) -> String {
        match self {
            Some(v) => format!("Some({})", v.persist_source()),
            None => format!("None::<{}>", T::persist_type()),
        }
    }
}

macro_rules! persist_tuple {
    ($(($($name:ident $index:tt),+))+) => {$(
        impl<$($name: Persist),+> Persist for ($($name,)+) {
            fn persist_type() -> String {
                format!("({})", [$($name::persist_type()),+].join(", "))
            }
            fn persist_source(&self) -> String {
                format!("({})", [$(self.$index.persist_source()),+].join(", "))
            }
        }
    )+};
}
persist_tuple! { (A 0, B 1) (A 0, B 1, C 2) (A 0, B 1, C 2, D 3) }

impl<T: Persist> Persist for std::collections::VecDeque<T> {
    fn persist_type() -> String {
        format!("std::collections::VecDeque<{}>", T::persist_type())
    }
    fn persist_source(&self) -> String {
        format!("std::collections::VecDeque::<{}>::from(vec![{}])", T::persist_type(), persist_items(self.iter()))
    }
}

macro_rules! persist_set {
    ($($set:ident),*) => {$(
        impl<T: Persist> Persist for std::collections::$set<T> {
            fn persist_type() -> String {
                format!("std::collections::{}<{}>", stringify!($set), T::persist_type())
            }
            fn persist_source(&self) -> String {
                format!("std::collections::{}::<{}>::from([{}])", stringify!($set), T::persist_type(), persist_items(self.iter()))
            }
        }
    )*};
}
persist_set!(HashSet, BTreeSet);

macro_rules! persist_map {
    ($($map:ident),*) => {$(
        impl<K: Persist, V: Persist> Persist for std::collections::$map<K, V> {
            fn persist_type() -> String {
                format!("std::collections::{}<{}, {}>", stringify!($map), K::persist_type(), V::persist_type())
            }
            fn persist_source(&self) -> String {
                let pairs: Vec<String> = self.iter().map(|(key, value)| format!("({}, {})", key.persist_source(), value.persist_source())).collect();
                format!("std::collections::{}::<{}, {}>::from([{}])", stringify!($map), K::persist_type(), V::persist_type(), pairs.join(", "))
            }
        }
    )*};
}
persist_map!(HashMap, BTreeMap);

const MAX_KEPT_BYTES: usize = 200_000;

/// Hands one variable to the notebook (through the file it names in `FRY_VARS_FILE`): what the studio adds at the end of a cell.
#[doc(hidden)]
pub fn __keep(name: &str, type_name: String, source: String) {
    use std::io::Write;
    let Ok(path) = std::env::var("FRY_VARS_FILE") else {
        return;
    };
    let line = if source.len() > MAX_KEPT_BYTES {
        format!("{{\"name\":{},\"skipped\":\"too large\"}}\n", json_string(name))
    } else {
        format!("{{\"name\":{},\"type\":{},\"source\":{}}}\n", json_string(name), json_string(&type_name), json_string(&source))
    };
    if let Ok(mut file) = std::fs::OpenOptions::new().create(true).append(true).open(path) {
        let _ = file.write_all(line.as_bytes());
    }
}

/// What the studio adds after a notebook cell for each variable it made with `let`: it keeps the variable for the cells after
/// it when its type is `Persist`, and does nothing when it isn't, so a variable of a type of your own isn't an error.
#[doc(hidden)]
pub mod __persist_probe {
    pub struct Probe<'a, T: ?Sized>(pub &'a T);

    pub trait ViaPersist {
        fn __fry_persist(&self, name: &str);
    }

    impl<'a, T: crate::Persist> ViaPersist for Probe<'a, T> {
        fn __fry_persist(&self, name: &str) {
            crate::__keep(name, T::persist_type(), self.0.persist_source());
        }
    }

    pub trait ViaNothing {
        fn __fry_persist(&self, name: &str);
    }

    impl<'a, T: ?Sized> ViaNothing for &Probe<'a, T> {
        fn __fry_persist(&self, _name: &str) {}
    }
}

#[doc(hidden)]
#[macro_export]
macro_rules! __persist {
    ($value:ident) => {{
        #[allow(unused_imports)]
        use $crate::__persist_probe::{ViaNothing as _, ViaPersist as _};
        (&$crate::__persist_probe::Probe(&$value)).__fry_persist(stringify!($value))
    }};
}

/// A JSON value, which is how a notebook cell receives a list or an object another language shared with `#!share`.
#[derive(Debug, Clone, PartialEq)]
pub enum Json {
    Null,
    Bool(bool),
    Number(f64),
    Str(String),
    Array(Vec<Json>),
    Object(Vec<(String, Json)>),
}

static JSON_NULL: Json = Json::Null;

impl Json {
    /// Reads JSON text. The studio only passes valid JSON; anything else panics and says where.
    pub fn parse(text: &str) -> Json {
        let mut reader = JsonReader { chars: text.chars().collect(), at: 0 };
        let value = reader.value();
        reader.skip();
        if reader.at != reader.chars.len() {
            reader.fail("unexpected text after the value");
        }
        value
    }

    pub fn get(&self, key: &str) -> Option<&Json> {
        match self {
            Json::Object(fields) => fields.iter().find(|(name, _)| name == key).map(|(_, value)| value),
            _ => None,
        }
    }

    pub fn at(&self, index: usize) -> Option<&Json> {
        match self {
            Json::Array(items) => items.get(index),
            _ => None,
        }
    }

    pub fn is_null(&self) -> bool {
        matches!(self, Json::Null)
    }

    pub fn as_bool(&self) -> Option<bool> {
        if let Json::Bool(b) = self { Some(*b) } else { None }
    }

    pub fn as_f64(&self) -> Option<f64> {
        if let Json::Number(n) = self { Some(*n) } else { None }
    }

    pub fn as_i64(&self) -> Option<i64> {
        match self {
            Json::Number(n) if n.fract() == 0.0 && n.abs() < 9.0e15 => Some(*n as i64),
            _ => None,
        }
    }

    pub fn as_str(&self) -> Option<&str> {
        if let Json::Str(s) = self { Some(s) } else { None }
    }

    pub fn as_array(&self) -> Option<&Vec<Json>> {
        if let Json::Array(items) = self { Some(items) } else { None }
    }
}

impl std::ops::Index<&str> for Json {
    type Output = Json;
    fn index(&self, key: &str) -> &Json {
        self.get(key).unwrap_or(&JSON_NULL)
    }
}

impl std::ops::Index<usize> for Json {
    type Output = Json;
    fn index(&self, index: usize) -> &Json {
        self.at(index).unwrap_or(&JSON_NULL)
    }
}

struct JsonReader {
    chars: Vec<char>,
    at: usize,
}

impl JsonReader {
    fn skip(&mut self) {
        while self.at < self.chars.len() && self.chars[self.at].is_whitespace() {
            self.at += 1;
        }
    }

    fn fail(&self, what: &str) -> ! {
        panic!("invalid JSON at character {}: {}", self.at, what)
    }

    fn expect(&mut self, c: char) {
        self.skip();
        if self.chars.get(self.at) != Some(&c) {
            self.fail(&format!("expected '{}'", c));
        }
        self.at += 1;
    }

    fn value(&mut self) -> Json {
        self.skip();
        match self.chars.get(self.at).copied() {
            Some('{') => {
                self.at += 1;
                let mut fields = Vec::new();
                loop {
                    self.skip();
                    if self.chars.get(self.at) == Some(&'}') {
                        self.at += 1;
                        return Json::Object(fields);
                    }
                    if !fields.is_empty() {
                        self.expect(',');
                    }
                    self.skip();
                    let key = match self.value() {
                        Json::Str(key) => key,
                        _ => self.fail("expected a string key"),
                    };
                    self.expect(':');
                    fields.push((key, self.value()));
                }
            }
            Some('[') => {
                self.at += 1;
                let mut items = Vec::new();
                loop {
                    self.skip();
                    if self.chars.get(self.at) == Some(&']') {
                        self.at += 1;
                        return Json::Array(items);
                    }
                    if !items.is_empty() {
                        self.expect(',');
                    }
                    items.push(self.value());
                }
            }
            Some('"') => Json::Str(self.string()),
            Some('t') => self.word("true", Json::Bool(true)),
            Some('f') => self.word("false", Json::Bool(false)),
            Some('n') => self.word("null", Json::Null),
            Some(c) if c == '-' || c.is_ascii_digit() => {
                let start = self.at;
                self.at += 1;
                while self.at < self.chars.len() && matches!(self.chars[self.at], '0'..='9' | '.' | 'e' | 'E' | '+' | '-') {
                    self.at += 1;
                }
                let text: String = self.chars[start..self.at].iter().collect();
                Json::Number(text.parse().unwrap_or_else(|_| self.fail("not a number")))
            }
            _ => self.fail("unexpected character"),
        }
    }

    fn word(&mut self, word: &str, value: Json) -> Json {
        for expected in word.chars() {
            if self.chars.get(self.at) != Some(&expected) {
                self.fail(&format!("expected {}", word));
            }
            self.at += 1;
        }
        value
    }

    fn string(&mut self) -> String {
        self.at += 1;
        let mut out = String::new();
        loop {
            let c = match self.chars.get(self.at).copied() {
                Some(c) => c,
                None => self.fail("the string never ends"),
            };
            self.at += 1;
            match c {
                '"' => return out,
                '\\' => {
                    let escape = self.chars.get(self.at).copied().unwrap_or_else(|| self.fail("a backslash ends the text"));
                    self.at += 1;
                    match escape {
                        'n' => out.push('\n'),
                        'r' => out.push('\r'),
                        't' => out.push('\t'),
                        'b' => out.push('\u{8}'),
                        'f' => out.push('\u{c}'),
                        'u' => {
                            let mut code = self.hex4();
                            if (0xD800..0xDC00).contains(&code) && self.chars.get(self.at) == Some(&'\\') && self.chars.get(self.at + 1) == Some(&'u') {
                                self.at += 2;
                                let low = self.hex4();
                                code = 0x10000 + ((code - 0xD800) << 10) + (low.wrapping_sub(0xDC00) & 0x3FF);
                            }
                            out.push(char::from_u32(code).unwrap_or('\u{FFFD}'));
                        }
                        other => out.push(other),
                    }
                }
                c => out.push(c),
            }
        }
    }

    fn hex4(&mut self) -> u32 {
        if self.at + 4 > self.chars.len() {
            self.fail("a \\u escape needs four digits");
        }
        let digits: String = self.chars[self.at..self.at + 4].iter().collect();
        self.at += 4;
        u32::from_str_radix(&digits, 16).unwrap_or_else(|_| self.fail("a \\u escape needs four hex digits"))
    }
}

fn emit(mime: &str, value_json: &str) {
    println!(
        "__FRY_DISPLAY__ {{\"type\":\"display\",\"data\":{{{}:{}}},\"metadata\":{{}}}}",
        json_string(mime),
        value_json
    );
}

fn emit_table(title: &str, table: &Table) {
    let numeric: Vec<bool> = (0..table.columns.len())
        .map(|column| {
            let mut any = false;
            for row in &table.rows {
                match row.get(column) {
                    Some(Cell::Number(_)) => any = true,
                    Some(Cell::Null) | None => {}
                    _ => return false,
                }
            }
            any
        })
        .collect();

    let mut out = String::new();
    out.push_str("{\"title\":");
    out.push_str(&json_string(title));
    out.push_str(",\"columns\":[");
    out.push_str(
        &table
            .columns
            .iter()
            .map(|c| json_string(c))
            .collect::<Vec<_>>()
            .join(","),
    );
    out.push_str("],\"numeric\":[");
    out.push_str(
        &numeric
            .iter()
            .map(|n| n.to_string())
            .collect::<Vec<_>>()
            .join(","),
    );
    out.push_str("],\"rows\":[");
    for (i, row) in table.rows.iter().enumerate() {
        if i > 0 {
            out.push(',');
        }
        out.push('[');
        for (j, cell) in row.iter().enumerate() {
            if j > 0 {
                out.push(',');
            }
            out.push_str(&cell.to_json());
        }
        out.push(']');
    }
    out.push_str("]}");
    emit(TABLE_MIME, &out);
}

pub fn json_string(text: &str) -> String {
    let mut out = String::with_capacity(text.len() + 2);
    out.push('"');
    for c in text.chars() {
        match c {
            '"' => out.push_str("\\\""),
            '\\' => out.push_str("\\\\"),
            '\n' => out.push_str("\\n"),
            '\r' => out.push_str("\\r"),
            '\t' => out.push_str("\\t"),
            c if (c as u32) < 0x20 => out.push_str(&format!("\\u{:04x}", c as u32)),
            c => out.push(c),
        }
    }
    out.push('"');
    out
}

fn base64(bytes: &[u8]) -> String {
    const TABLE: &[u8; 64] =
        b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    let mut out = String::with_capacity((bytes.len() + 2) / 3 * 4);
    for chunk in bytes.chunks(3) {
        let b0 = chunk[0] as usize;
        let b1 = chunk.get(1).copied().unwrap_or(0) as usize;
        let b2 = chunk.get(2).copied().unwrap_or(0) as usize;
        out.push(TABLE[b0 >> 2] as char);
        out.push(TABLE[((b0 & 3) << 4) | (b1 >> 4)] as char);
        out.push(if chunk.len() > 1 {
            TABLE[((b1 & 0xF) << 2) | (b2 >> 6)] as char
        } else {
            '='
        });
        out.push(if chunk.len() > 2 {
            TABLE[b2 & 0x3F] as char
        } else {
            '='
        });
    }
    out
}

// ── Tables from Values ───────────────────────────────────────────────────

struct Table {
    columns: Vec<String>,
    rows: Vec<Vec<Cell>>,
}

impl Table {
    fn new(columns: Vec<String>, rows: Vec<Vec<Cell>>) -> Self {
        Self { columns, rows }
    }
}

#[derive(Clone)]
enum Cell {
    Null,
    Text(String),
    Number(String),
    Boolean(bool),
}

impl Cell {
    fn to_json(&self) -> String {
        match self {
            Cell::Null => "null".to_string(),
            Cell::Text(s) => json_string(s),
            Cell::Number(n) => n.clone(),
            Cell::Boolean(b) => b.to_string(),
        }
    }
}

fn shape(root: &Node) -> Table {
    match root {
        Node::List(items) | Node::Set(items) if !items.is_empty() => shape_list(items),
        Node::Map(entries) if !entries.is_empty() => shape_map(entries),
        Node::Struct(_, fields) if !fields.is_empty() => shape_struct(fields),
        _ => Table::new(
            vec!["value".to_string()],
            vec![vec![node_to_cell(root, 0)]],
        ),
    }
}

fn shape_list(items: &[Node]) -> Table {
    let items = &items[..items.len().min(MAX_ROWS)];
    if items.iter().all(|it| matches!(it, Node::Struct(_, _))) {
        let mut columns = Vec::new();
        for it in items {
            if let Node::Struct(_, fields) = it {
                for (name, _) in fields {
                    if !columns.contains(name) && columns.len() < MAX_COLUMNS {
                        columns.push(name.clone());
                    }
                }
            }
        }
        let rows = items
            .iter()
            .map(|it| {
                if let Node::Struct(_, fields) = it {
                    columns
                        .iter()
                        .map(|col| {
                            fields
                                .iter()
                                .find(|(name, _)| name == col)
                                .map(|(_, val)| node_to_cell(val, 0))
                                .unwrap_or(Cell::Null)
                        })
                        .collect()
                } else {
                    vec![Cell::Null; columns.len()]
                }
            })
            .collect();
        return Table::new(columns, rows);
    }
    Table::new(
        vec!["index".to_string(), "value".to_string()],
        items
            .iter()
            .enumerate()
            .map(|(i, it)| vec![Cell::Number(i.to_string()), node_to_cell(it, 0)])
            .collect(),
    )
}

fn shape_map(entries: &[(Node, Node)]) -> Table {
    let entries = &entries[..entries.len().min(MAX_ROWS)];
    let rows = entries
        .iter()
        .map(|(k, v)| vec![node_to_cell(k, 0), node_to_cell(v, 0)])
        .collect();
    Table::new(vec!["key".to_string(), "value".to_string()], rows)
}

fn shape_struct(fields: &[(String, Node)]) -> Table {
    let rows = fields
        .iter()
        .take(MAX_ROWS)
        .map(|(k, v)| vec![Cell::Text(k.clone()), node_to_cell(v, 0)])
        .collect();
    Table::new(vec!["field".to_string(), "value".to_string()], rows)
}

fn node_to_cell(node: &Node, depth: usize) -> Cell {
    if depth > MAX_DEPTH {
        return Cell::Text("...".to_string());
    }
    match node {
        Node::Bool(b) => Cell::Boolean(*b),
        Node::Number(n) if is_json_number(n) => Cell::Number(n.clone()),
        Node::Number(n) => Cell::Text(n.clone()),
        Node::Text(t) => Cell::Text(t.clone()),
        Node::Ident(w) if w == "None" => Cell::Null,
        Node::Ident(w) => Cell::Text(w.clone()),
        Node::Tuple(Some(w), items) if w == "Some" && items.len() == 1 => {
            node_to_cell(&items[0], depth)
        }
        _ => Cell::Text(node.render(false)),
    }
}

fn is_json_number(text: &str) -> bool {
    let bytes = text.as_bytes();
    if bytes.is_empty() {
        return false;
    }
    let mut i = 0;
    if bytes[0] == b'-' {
        i += 1;
    }
    if i >= bytes.len() {
        return false;
    }
    let mut has_digit = false;
    while i < bytes.len() && bytes[i].is_ascii_digit() {
        has_digit = true;
        i += 1;
    }
    if !has_digit {
        return false;
    }
    if i < bytes.len() && bytes[i] == b'.' {
        i += 1;
        let frac_start = i;
        while i < bytes.len() && bytes[i].is_ascii_digit() {
            i += 1;
        }
        if i == frac_start {
            return false;
        }
    }
    if i < bytes.len() && (bytes[i] == b'e' || bytes[i] == b'E') {
        i += 1;
        if i < bytes.len() && (bytes[i] == b'+' || bytes[i] == b'-') {
            i += 1;
        }
        let exp_start = i;
        while i < bytes.len() && bytes[i].is_ascii_digit() {
            i += 1;
        }
        if i == exp_start {
            return false;
        }
    }
    i == bytes.len()
}

// ── Node AST ──────────────────────────────────────────────────────────────

#[derive(Debug, Clone)]
pub enum Node {
    Bool(bool),
    Number(String),
    Text(String),
    Ident(String),
    List(Vec<Node>),
    Set(Vec<Node>),
    Map(Vec<(Node, Node)>),
    Tuple(Option<String>, Vec<Node>),
    Struct(String, Vec<(String, Node)>),
}

impl Node {
    fn is_simple(&self) -> bool {
        matches!(
            self,
            Node::Bool(_) | Node::Number(_) | Node::Text(_) | Node::Ident(_)
        )
    }

    fn wraps_simple(&self) -> bool {
        matches!(self, Node::Tuple(Some(_), items) if items.len() == 1 && items[0].is_simple())
    }

    fn as_f64(&self) -> Option<f64> {
        match self {
            Node::Number(s) => s.parse::<f64>().ok(),
            Node::Tuple(Some(w), sub) if w == "Some" && sub.len() == 1 => sub[0].as_f64(),
            _ => None,
        }
    }

    fn render_plain(&self) -> String {
        match self {
            Node::Text(s) => s.clone(),
            Node::Number(n) => n.clone(),
            Node::Ident(w) => w.clone(),
            Node::Bool(b) => b.to_string(),
            Node::Tuple(Some(w), items) if w == "Some" && items.len() == 1 => {
                items[0].render_plain()
            }
            _ => self.render(false),
        }
    }

    fn render(&self, pretty: bool) -> String {
        let mut out = String::new();
        self.render_into(&mut out, pretty, 0);
        out
    }

    fn render_into(&self, out: &mut String, pretty: bool, indent: usize) {
        match self {
            Node::Bool(b) => out.push_str(&b.to_string()),
            Node::Number(n) => out.push_str(n),
            Node::Text(t) => out.push_str(&format!("{:?}", t)),
            Node::Ident(w) => out.push_str(w),
            Node::List(items) => self.render_sequence(out, "[", "]", items, pretty, indent),
            Node::Set(items) => self.render_sequence(out, "{", "}", items, pretty, indent),
            Node::Tuple(None, items) => self.render_sequence(out, "(", ")", items, pretty, indent),
            Node::Tuple(Some(name), items) => {
                out.push_str(name);
                self.render_sequence(out, "(", ")", items, pretty, indent);
            }
            Node::Map(entries) => {
                out.push('{');
                for (i, (k, v)) in entries.iter().enumerate() {
                    if i > 0 {
                        out.push_str(", ");
                    }
                    k.render_into(out, pretty, indent);
                    out.push_str(": ");
                    v.render_into(out, pretty, indent);
                }
                out.push('}');
            }
            Node::Struct(name, fields) => {
                out.push_str(name);
                out.push_str(" { ");
                for (i, (fname, fval)) in fields.iter().enumerate() {
                    if i > 0 {
                        out.push_str(", ");
                    }
                    out.push_str(fname);
                    out.push_str(": ");
                    fval.render_into(out, pretty, indent);
                }
                out.push_str(" }");
            }
        }
    }

    fn render_sequence(
        &self,
        out: &mut String,
        open: &str,
        close: &str,
        items: &[Node],
        pretty: bool,
        indent: usize,
    ) {
        out.push_str(open);
        for (i, item) in items.iter().enumerate() {
            if i > 0 {
                out.push_str(", ");
            }
            item.render_into(out, pretty, indent);
        }
        out.push_str(close);
    }

    fn to_json(&self) -> String {
        let array = |items: &[Node]| {
            format!(
                "[{}]",
                items
                    .iter()
                    .map(Node::to_json)
                    .collect::<Vec<_>>()
                    .join(",")
            )
        };
        match self {
            Node::Bool(b) => b.to_string(),
            Node::Number(n) if is_json_number(n) => n.clone(),
            Node::Number(_) => "null".to_string(),
            Node::Text(t) => json_string(t),
            Node::Ident(w) if w == "None" || w == "NaN" || w == "inf" => "null".to_string(),
            Node::Ident(w) => json_string(w),
            Node::List(items) | Node::Set(items) => array(items),
            Node::Map(entries)
                if entries.iter().all(|(k, _)| matches!(k, Node::Text(_))) =>
            {
                format!(
                    "{{{}}}",
                    entries
                        .iter()
                        .map(|(k, v)| format!("{}:{}", k.to_json(), v.to_json()))
                        .collect::<Vec<_>>()
                        .join(",")
                )
            }
            Node::Map(entries) => format!(
                "[{}]",
                entries
                    .iter()
                    .map(|(k, v)| format!("[{},{}]", k.to_json(), v.to_json()))
                    .collect::<Vec<_>>()
                    .join(",")
            ),
            Node::Tuple(Some(name), items) if name == "Some" && items.len() == 1 => {
                items[0].to_json()
            }
            Node::Tuple(_, items) => array(items),
            Node::Struct(_, fields) => format!(
                "{{{}}}",
                fields
                    .iter()
                    .map(|(k, v)| format!("{}:{}", json_string(k), v.to_json()))
                    .collect::<Vec<_>>()
                    .join(",")
            ),
        }
    }
}

// ── Tokenizer & Parser ───────────────────────────────────────────────────

#[derive(Debug, PartialEq, Clone)]
enum Tok {
    Open(char),
    Close(char),
    Colon,
    Comma,
    DotDot,
    Word(String),
    Num(String),
    Str(String),
    Ch(char),
}

fn tokenize(text: &str) -> Option<Vec<Tok>> {
    let mut tok = Tokenizer {
        chars: text.chars().collect(),
        at: 0,
    };
    let mut tokens = Vec::new();
    while let Some(t) = tok.next_token()? {
        tokens.push(t);
    }
    Some(tokens)
}

struct Tokenizer {
    chars: Vec<char>,
    at: usize,
}

impl Tokenizer {
    fn peek(&self) -> Option<char> {
        self.chars.get(self.at).copied()
    }

    fn next_char(&mut self) -> Option<char> {
        let c = self.peek()?;
        self.at += 1;
        Some(c)
    }

    fn next_token(&mut self) -> Option<Option<Tok>> {
        while let Some(c) = self.peek() {
            if c.is_whitespace() {
                self.at += 1;
            } else {
                break;
            }
        }
        let c = match self.next_char() {
            Some(c) => c,
            None => return Some(None),
        };
        match c {
            '(' | '[' | '{' => Some(Some(Tok::Open(c))),
            ')' | ']' | '}' => Some(Some(Tok::Close(c))),
            ':' => Some(Some(Tok::Colon)),
            ',' => Some(Some(Tok::Comma)),
            '.' if self.peek() == Some('.') => {
                self.at += 1;
                Some(Some(Tok::DotDot))
            }
            '"' => Some(Some(Tok::Str(self.read_string()?))),
            '\'' => Some(Some(Tok::Ch(self.read_char()?))),
            '-' | '0'..='9' => Some(Some(Tok::Num(self.read_number(c)?))),
            c if is_ident_start(c) => Some(Some(Tok::Word(self.read_word(c)))),
            _ => None,
        }
    }

    fn read_string(&mut self) -> Option<String> {
        let mut s = String::new();
        loop {
            match self.next_char()? {
                '"' => return Some(s),
                '\\' => match self.next_char()? {
                    '"' => s.push('"'),
                    '\\' => s.push('\\'),
                    'n' => s.push('\n'),
                    'r' => s.push('\r'),
                    't' => s.push('\t'),
                    'u' => s.push(char::from_u32(self.read_hex_escape()?)?),
                    other => {
                        s.push('\\');
                        s.push(other);
                    }
                },
                c => s.push(c),
            }
        }
    }

    fn read_char(&mut self) -> Option<char> {
        let c = match self.next_char()? {
            '\\' => match self.next_char()? {
                '\'' => '\'',
                '\\' => '\\',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'u' => char::from_u32(self.read_hex_escape()?)?,
                other => other,
            },
            c => c,
        };
        if self.next_char()? == '\'' {
            Some(c)
        } else {
            None
        }
    }

    fn read_number(&mut self, first: char) -> Option<String> {
        let mut s = String::new();
        s.push(first);
        while let Some(c) = self.peek() {
            if c.is_ascii_digit()
                || c == '.'
                || c == 'e'
                || c == 'E'
                || c == '+'
                || c == '-'
                || c == '_'
            {
                if c != '_' {
                    s.push(c);
                }
                self.at += 1;
            } else {
                break;
            }
        }
        Some(s)
    }

    fn read_word(&mut self, first: char) -> String {
        let mut s = String::new();
        s.push(first);
        while let Some(c) = self.peek() {
            if is_ident_continue(c) {
                s.push(c);
                self.at += 1;
            } else {
                break;
            }
        }
        s
    }

    fn read_hex_escape(&mut self) -> Option<u32> {
        if self.next_char()? != '{' {
            return None;
        }
        let mut digits = String::new();
        loop {
            match self.next_char()? {
                '}' => break,
                c if c.is_ascii_hexdigit() => digits.push(c),
                _ => return None,
            }
        }
        u32::from_str_radix(&digits, 16).ok()
    }
}

fn is_ident_start(c: char) -> bool {
    c.is_alphabetic() || c == '_'
}

fn is_ident_continue(c: char) -> bool {
    c.is_alphanumeric() || c == '_'
}

struct Parser {
    tokens: Vec<Tok>,
    at: usize,
    depth: usize,
}

impl Parser {
    fn peek(&self) -> Option<&Tok> {
        self.tokens.get(self.at)
    }

    fn next(&mut self) -> Option<Tok> {
        let t = self.peek()?.clone();
        self.at += 1;
        Some(t)
    }

    fn value(&mut self) -> Option<Node> {
        if self.depth > MAX_DEPTH {
            return None;
        }
        self.depth += 1;
        let node = match self.next()? {
            Tok::Word(w) => {
                let word = w.clone();
                self.word(word)?
            }
            Tok::Num(n) => Node::Number(n.clone()),
            Tok::Str(s) => Node::Text(s.clone()),
            Tok::Ch(c) => Node::Text(c.to_string()),
            Tok::Open('(') => Node::Tuple(None, self.items(')')?),
            Tok::Open('[') => Node::List(self.items(']')?),
            Tok::Open('{') => self.brace()?,
            _ => return None,
        };
        self.depth -= 1;
        Some(node)
    }

    fn word(&mut self, word: String) -> Option<Node> {
        match word.as_str() {
            "true" => return Some(Node::Bool(true)),
            "false" => return Some(Node::Bool(false)),
            _ => {}
        }
        match self.peek() {
            Some(Tok::Open('{')) => {
                self.at += 1;
                let fields = self.fields()?;
                Some(Node::Struct(word, fields))
            }
            Some(Tok::Open('(')) => {
                self.at += 1;
                let items = self.items(')')?;
                Some(Node::Tuple(Some(word), items))
            }
            _ => Some(Node::Ident(word)),
        }
    }

    fn items(&mut self, close: char) -> Option<Vec<Node>> {
        let mut items = Vec::new();
        loop {
            match self.peek()? {
                Tok::Close(c) if *c == close => {
                    self.at += 1;
                    return Some(items);
                }
                Tok::Comma | Tok::DotDot => self.at += 1,
                _ => items.push(self.value()?),
            }
        }
    }

    fn fields(&mut self) -> Option<Vec<(String, Node)>> {
        let mut fields = Vec::new();
        loop {
            match self.next()? {
                Tok::Close('}') => return Some(fields),
                Tok::Comma | Tok::DotDot => {}
                Tok::Word(name) | Tok::Num(name) => {
                    let name = name.clone();
                    if self.next()? != Tok::Colon {
                        return None;
                    }
                    fields.push((name, self.value()?));
                }
                _ => return None,
            }
        }
    }

    fn brace(&mut self) -> Option<Node> {
        if matches!(self.peek()?, Tok::Close('}')) {
            self.at += 1;
            return Some(Node::Map(Vec::new()));
        }
        let first = self.value()?;
        if matches!(self.peek()?, Tok::Colon) {
            self.at += 1;
            let mut entries = vec![(first, self.value()?)];
            loop {
                match self.peek()? {
                    Tok::Close('}') => {
                        self.at += 1;
                        return Some(Node::Map(entries));
                    }
                    Tok::Comma => self.at += 1,
                    _ => {
                        let key = self.value()?;
                        if self.next()? != Tok::Colon {
                            return None;
                        }
                        entries.push((key, self.value()?));
                    }
                }
            }
        }
        let mut items = vec![first];
        loop {
            match self.peek()? {
                Tok::Close('}') => {
                    self.at += 1;
                    return Some(Node::Set(items));
                }
                Tok::Comma => self.at += 1,
                _ => items.push(self.value()?),
            }
        }
    }
}

pub fn parse(text: &str) -> Option<Node> {
    let tokens = tokenize(text)?;
    let mut parser = Parser {
        tokens,
        at: 0,
        depth: 0,
    };
    let node = parser.value()?;
    if parser.at == parser.tokens.len() {
        Some(node)
    } else {
        None
    }
}
