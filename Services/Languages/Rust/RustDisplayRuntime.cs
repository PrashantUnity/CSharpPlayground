namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// The display crate every Rust script can use, with no setup: <c>fry::table(&amp;rows, "Title")</c>, <c>fry::dump!(value)</c>,
/// <c>fry::html(..)</c> and <c>fry::image(..)</c> print <c>__FRY_DISPLAY__</c> lines that <c>ExternalOutputProcessor</c>
/// turns into tables, images and HTML in the Results deck. The crate is written once under the Rust folder and added to every
/// staged package as a path dependency, so cargo compiles it once for all scripts. Values only need <c>Debug</c>: the crate
/// reads the shape of <c>{:#?}</c> output, so any list of structs becomes a table with a column per field.
/// </summary>
public static class RustDisplayRuntime
{
    /// <summary>Writes the crate under <c>rustRoot/fry</c> (only when it changed) and returns its folder, or null if it can't be written.</summary>
    public static async Task<string?> EnsureCrateAsync(string rustRoot, CancellationToken ct = default)
    {
        var crateDir = Path.Combine(rustRoot, "fry");
        try
        {
            Directory.CreateDirectory(Path.Combine(crateDir, "src"));
            await WriteIfChangedAsync(Path.Combine(crateDir, "Cargo.toml"), CargoToml, ct).ConfigureAwait(false);
            await WriteIfChangedAsync(Path.Combine(crateDir, "src", "lib.rs"), LibRs, ct).ConfigureAwait(false);
            return crateDir;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // An unchanged file keeps its timestamp, so cargo doesn't rebuild the crate.
    private static async Task WriteIfChangedAsync(string path, string content, CancellationToken ct)
    {
        if (File.Exists(path) && await File.ReadAllTextAsync(path, ct).ConfigureAwait(false) == content) return;
        await File.WriteAllTextAsync(path, content, new System.Text.UTF8Encoding(false), ct).ConfigureAwait(false);
    }

    public const string CargoToml = """
[package]
name = "fry"
version = "0.1.0"
edition = "2021"
publish = false
description = "Display helpers for C# Code Studio scripts"

[workspace]
""";

    public const string LibRs = """
//! Display helpers for C# Code Studio: what a Rust script calls to put a table, an image or
//! HTML in the Results deck. Each call prints one `__FRY_DISPLAY__` line, which the studio turns
//! into rich output. Values only need `Debug`; nothing here needs another crate.
//!
//! ```ignore
//! use fry::prelude::*;
//! table(&vec![(1, "one"), (2, "two")], "Numbers");
//! dump!(some_struct);
//! ```

use std::fmt::Debug;

pub mod prelude {
    pub use crate::{dump, html, image, image_file, json, share, show, table, Json};
}

const MAX_ROWS: usize = 500;
const MAX_COLUMNS: usize = 40;
const MAX_DEPTH: usize = 48;

/// Shows any `Debug` value as a table: a list of structs gets a column per field, a map gets key
/// and value columns, a list of lists gets one column per position, and a plain list gets an
/// index and a value.
pub fn table<T: Debug + ?Sized>(data: &T, title: &str) {
    let text = format!("{:#?}", data);
    let table = match parse(&text) {
        Some(node) => shape(&node),
        None => Table::new(vec!["value".to_string()], vec![vec![Cell::Text(text)]]),
    };
    emit_table(title, &table);
}

/// Like [`table`], but a lone number, string or other simple value is printed as text.
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

/// Shows HTML in the Results deck.
pub fn html(markup: &str) {
    emit("text/html", &json_string(markup));
}

/// Shows text in the Results deck.
pub fn json(text: &str) {
    emit("text/plain", &json_string(text));
}

/// Shows a PNG or JPEG (its bytes) or an SVG (its text as bytes) in the Results deck.
pub fn image(bytes: &[u8]) {
    if bytes.starts_with(&[0xFF, 0xD8, 0xFF]) {
        emit("image/jpeg", &json_string(&base64(bytes)));
    } else if bytes.starts_with(b"<svg") || bytes.starts_with(b"<?xml") {
        emit("image/svg+xml", &json_string(&String::from_utf8_lossy(bytes)));
    } else {
        emit("image/png", &json_string(&base64(bytes)));
    }
}

/// Shows an image file (PNG, JPEG or SVG) in the Results deck.
pub fn image_file(path: &str) -> std::io::Result<()> {
    let bytes = std::fs::read(path)?;
    image(&bytes);
    Ok(())
}

/// `dump!(value)` shows `value` titled with its own source text; `dump!(value, "Title")` titles it.
#[macro_export]
macro_rules! dump {
    ($value:expr) => {
        $crate::dump(&$value, stringify!($value))
    };
    ($value:expr, $title:expr) => {
        $crate::dump(&$value, $title)
    };
}

/// `table!(value)` shows `value` as a table titled with its own source text.
#[macro_export]
macro_rules! table {
    ($value:expr) => {
        $crate::table(&$value, stringify!($value))
    };
    ($value:expr, $title:expr) => {
        $crate::table(&$value, $title)
    };
}

/// Shows the value of a notebook cell's last expression the way a REPL does: text for a number, a string or another
/// simple value, a table for a list, a map or a struct, and nothing for `()`.
pub fn show<T: Debug + ?Sized>(data: &T) {
    let text = format!("{:#?}", data);
    if text == "()" {
        return;
    }
    match parse(&text) {
        Some(node) if node.is_simple() || node.wraps_simple() => emit("text/plain", &json_string(&format!("{}\n", node.render(true)))),
        Some(node) => emit_table("", &shape(&node)),
        None => emit("text/plain", &json_string(&format!("{}\n", text))),
    }
}

/// Offers `data` to the notebook's other languages: a later cell writes `#!share --from rust name` to use it. Values only
/// need `Debug`; a struct becomes an object, a list an array, a number a number.
pub fn share<T: Debug + ?Sized>(name: &str, data: &T) {
    let text = format!("{:#?}", data);
    let json = match parse(&text) {
        Some(node) => node.to_json(),
        None => json_string(&text),
    };
    println!("__FRY_SHARE__ {{\"name\":{},\"json\":{}}}", json_string(name), json_string(&json));
}

/// `share!(value)` shares a variable under its own name; `share!(value, "name")` under another.
#[macro_export]
macro_rules! share {
    ($value:ident) => {
        $crate::share(stringify!($value), &$value)
    };
    ($value:expr, $name:expr) => {
        $crate::share($name, &$value)
    };
}

/// What the studio adds after a notebook cell's last expression: it shows the value when it can be shown with `Debug` and
/// does nothing when it can't, so a value of a type without `Debug` isn't an error.
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

/// What a notebook keeps of a `let` for the cells after it: the variable's type and Rust source that builds the same value again.
/// Numbers, `bool`, `char`, text, lists, arrays, options, tuples, maps and sets of those can be kept; anything else (a struct of
/// your own, a closure, a reference) is left behind when the cell ends.
pub trait Persist {
    /// The type as it is written in a `let`.
    fn persist_type() -> String;
    /// An expression that builds this value.
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

impl<T: Persist> Persist for Option<T> {
    fn persist_type() -> String {
        format!("Option<{}>", T::persist_type())
    }
    fn persist_source(&self) -> String {
        match self {
            Some(value) => format!("Some({})", value.persist_source()),
            None => format!("None::<{}>", T::persist_type()),
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

// ── Output ────────────────────────────────────────────────────────────────

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
    out.push_str(&table.columns.iter().map(|c| json_string(c)).collect::<Vec<_>>().join(","));
    out.push_str("],\"numeric\":[");
    out.push_str(&numeric.iter().map(|n| n.to_string()).collect::<Vec<_>>().join(","));
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
    out.push_str(&format!(
        "],\"totalRows\":{},\"totalColumns\":{}}}",
        table.total_rows, table.total_columns
    ));
    emit("application/vnd.fry.table+json", &out);
}

fn json_string(text: &str) -> String {
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
    const ALPHABET: &[u8; 64] = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    let mut out = String::with_capacity((bytes.len() + 2) / 3 * 4);
    for chunk in bytes.chunks(3) {
        let b1 = *chunk.get(1).unwrap_or(&0) as u32;
        let b2 = *chunk.get(2).unwrap_or(&0) as u32;
        let n = (chunk[0] as u32) << 16 | b1 << 8 | b2;
        out.push(ALPHABET[(n >> 18) as usize & 63] as char);
        out.push(ALPHABET[(n >> 12) as usize & 63] as char);
        out.push(if chunk.len() > 1 { ALPHABET[(n >> 6) as usize & 63] as char } else { '=' });
        out.push(if chunk.len() > 2 { ALPHABET[n as usize & 63] as char } else { '=' });
    }
    out
}

// ── Tables ────────────────────────────────────────────────────────────────

enum Cell {
    Null,
    Bool(bool),
    Number(String),
    Text(String),
}

impl Cell {
    fn to_json(&self) -> String {
        match self {
            Cell::Null => "null".to_string(),
            Cell::Bool(b) => b.to_string(),
            Cell::Number(n) => n.clone(),
            Cell::Text(t) => json_string(t),
        }
    }
}

struct Table {
    columns: Vec<String>,
    rows: Vec<Vec<Cell>>,
    total_rows: usize,
    total_columns: usize,
}

impl Table {
    fn new(mut columns: Vec<String>, mut rows: Vec<Vec<Cell>>) -> Table {
        let total_rows = rows.len();
        let total_columns = columns.len();
        columns.truncate(MAX_COLUMNS);
        rows.truncate(MAX_ROWS);
        for row in rows.iter_mut() {
            row.truncate(MAX_COLUMNS);
        }
        Table { columns, rows, total_rows, total_columns }
    }
}

fn shape(node: &Node) -> Table {
    match node {
        Node::List(items) | Node::Set(items) => shape_items(items),
        Node::Map(entries) => shape_map(entries),
        Node::Struct(_, fields) => Table::new(
            vec!["field".to_string(), "value".to_string()],
            fields
                .iter()
                .map(|(name, value)| vec![Cell::Text(name.clone()), value.cell()])
                .collect(),
        ),
        Node::Tuple(name, items) if !items.is_empty() && name.as_deref() != Some("Some") => Table::new(
            (0..items.len()).map(|i| i.to_string()).collect(),
            vec![items.iter().map(Node::cell).collect()],
        ),
        other => Table::new(vec!["value".to_string()], vec![vec![other.cell()]]),
    }
}

fn record_columns<'a>(records: impl Iterator<Item = &'a Vec<(String, Node)>>) -> Vec<String> {
    let mut columns: Vec<String> = Vec::new();
    for fields in records {
        for (name, _) in fields {
            if !columns.contains(name) {
                columns.push(name.clone());
            }
        }
    }
    columns
}

fn record_row(columns: &[String], fields: &[(String, Node)]) -> Vec<Cell> {
    columns
        .iter()
        .map(|column| {
            fields
                .iter()
                .find(|(name, _)| name == column)
                .map(|(_, value)| value.cell())
                .unwrap_or(Cell::Null)
        })
        .collect()
}

fn shape_items(items: &[Node]) -> Table {
    if items.is_empty() {
        return Table::new(vec!["value".to_string()], Vec::new());
    }

    if items.iter().all(|n| matches!(n, Node::Struct(..))) {
        let records: Vec<&Vec<(String, Node)>> = items
            .iter()
            .filter_map(|n| if let Node::Struct(_, fields) = n { Some(fields) } else { None })
            .collect();
        let columns = record_columns(records.iter().copied());
        let rows = records.iter().map(|fields| record_row(&columns, fields)).collect();
        return Table::new(columns, rows);
    }

    let tuple_name = |node: &Node| -> Option<Option<String>> {
        if let Node::Tuple(name, _) = node { Some(name.clone()) } else { None }
    };
    let same_tuples = items.iter().all(|n| tuple_name(n).is_some())
        && items.iter().all(|n| tuple_name(n) == tuple_name(&items[0]))
        && tuple_name(&items[0]) != Some(Some("Some".to_string()));
    let positions = |node: &Node| -> Option<Vec<Cell>> {
        match node {
            Node::List(values) | Node::Set(values) if !values.is_empty() => Some(values.iter().map(Node::cell).collect()),
            Node::Tuple(_, values) if same_tuples && !values.is_empty() => Some(values.iter().map(Node::cell).collect()),
            _ => None,
        }
    };
    let grid: Vec<Option<Vec<Cell>>> = items.iter().map(positions).collect();
    if grid.iter().all(Option::is_some) {
        let rows: Vec<Vec<Cell>> = grid.into_iter().flatten().collect();
        let width = rows.iter().map(Vec::len).max().unwrap_or(0);
        let columns = (0..width).map(|i| i.to_string()).collect();
        let rows = rows
            .into_iter()
            .map(|mut row| {
                while row.len() < width {
                    row.push(Cell::Null);
                }
                row
            })
            .collect();
        return Table::new(columns, rows);
    }

    let rows = items
        .iter()
        .enumerate()
        .map(|(i, item)| vec![Cell::Number(i.to_string()), item.cell()])
        .collect();
    Table::new(vec!["#".to_string(), "value".to_string()], rows)
}

fn shape_map(entries: &[(Node, Node)]) -> Table {
    if !entries.is_empty() && entries.iter().all(|(_, v)| matches!(v, Node::Struct(..))) {
        let records: Vec<&Vec<(String, Node)>> = entries
            .iter()
            .filter_map(|(_, v)| if let Node::Struct(_, fields) = v { Some(fields) } else { None })
            .collect();
        let fields = record_columns(records.iter().copied());
        let mut columns = vec!["key".to_string()];
        columns.extend(fields.iter().cloned());
        let rows = entries
            .iter()
            .zip(records.iter())
            .map(|((key, _), record)| {
                let mut row = vec![key.cell()];
                row.extend(record_row(&fields, record));
                row
            })
            .collect();
        return Table::new(columns, rows);
    }

    let rows = entries.iter().map(|(key, value)| vec![key.cell(), value.cell()]).collect();
    Table::new(vec!["key".to_string(), "value".to_string()], rows)
}

// ── A parsed `{:#?}` ───────────────────────────────────────────────────────

enum Node {
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
        matches!(self, Node::Bool(_) | Node::Number(_) | Node::Text(_) | Node::Ident(_))
    }

    /// `Some(3)`, `Ok("x")`: one simple value in a named wrapper, which reads better as text than as a table.
    fn wraps_simple(&self) -> bool {
        matches!(self, Node::Tuple(Some(_), items) if items.len() == 1 && items[0].is_simple())
    }

    /// The value as JSON text, for `share`: a struct is an object, a list or tuple an array, `None` is null.
    fn to_json(&self) -> String {
        let array = |items: &[Node]| format!("[{}]", items.iter().map(Node::to_json).collect::<Vec<_>>().join(","));
        match self {
            Node::Bool(b) => b.to_string(),
            Node::Number(n) if is_json_number(n) => n.clone(),
            Node::Number(_) => "null".to_string(),
            Node::Text(t) => json_string(t),
            Node::Ident(w) if w == "None" || w == "NaN" || w == "inf" => "null".to_string(),
            Node::Ident(w) => json_string(w),
            Node::List(items) | Node::Set(items) => array(items),
            Node::Map(entries) if entries.iter().all(|(k, _)| matches!(k, Node::Text(_))) => format!(
                "{{{}}}",
                entries
                    .iter()
                    .map(|(k, v)| format!("{}:{}", k.to_json(), v.to_json()))
                    .collect::<Vec<_>>()
                    .join(",")
            ),
            Node::Map(entries) => format!(
                "[{}]",
                entries.iter().map(|(k, v)| format!("[{},{}]", k.to_json(), v.to_json())).collect::<Vec<_>>().join(",")
            ),
            Node::Tuple(Some(name), items) if name == "Some" && items.len() == 1 => items[0].to_json(),
            Node::Tuple(Some(_), items) if items.len() == 1 => items[0].to_json(),
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

    fn cell(&self) -> Cell {
        match self {
            Node::Bool(b) => Cell::Bool(*b),
            Node::Number(n) if is_json_number(n) => Cell::Number(n.clone()),
            Node::Number(n) => Cell::Text(n.clone()),
            Node::Text(t) => Cell::Text(t.clone()),
            Node::Ident(w) if w == "None" => Cell::Null,
            Node::Ident(w) => Cell::Text(w.clone()),
            Node::Tuple(Some(name), items) if name == "Some" && items.len() == 1 => items[0].cell(),
            other => Cell::Text(other.render(true)),
        }
    }

    fn render(&self, quote: bool) -> String {
        let join = |items: &[Node]| items.iter().map(|n| n.render(true)).collect::<Vec<_>>().join(", ");
        match self {
            Node::Bool(b) => b.to_string(),
            Node::Number(n) => n.clone(),
            Node::Text(t) if quote => format!("{:?}", t),
            Node::Text(t) => t.clone(),
            Node::Ident(w) => w.clone(),
            Node::List(items) => format!("[{}]", join(items)),
            Node::Set(items) => format!("{{{}}}", join(items)),
            Node::Map(entries) => format!(
                "{{{}}}",
                entries
                    .iter()
                    .map(|(k, v)| format!("{}: {}", k.render(true), v.render(true)))
                    .collect::<Vec<_>>()
                    .join(", ")
            ),
            Node::Tuple(name, items) => format!("{}({})", name.as_deref().unwrap_or(""), join(items)),
            Node::Struct(name, fields) => format!(
                "{} {{ {} }}",
                name,
                fields
                    .iter()
                    .map(|(k, v)| format!("{}: {}", k, v.render(true)))
                    .collect::<Vec<_>>()
                    .join(", ")
            ),
        }
    }
}

fn is_json_number(text: &str) -> bool {
    let bytes = text.as_bytes();
    let mut i = 0;
    if bytes.first() == Some(&b'-') {
        i += 1;
    }
    let digits = |from: usize| bytes[from..].iter().take_while(|b| b.is_ascii_digit()).count();
    let whole = digits(i);
    if whole == 0 || (bytes[i] == b'0' && whole > 1) {
        return false;
    }
    i += whole;
    if bytes.get(i) == Some(&b'.') {
        let fraction = digits(i + 1);
        if fraction == 0 {
            return false;
        }
        i += 1 + fraction;
    }
    if matches!(bytes.get(i), Some(b'e') | Some(b'E')) {
        i += 1;
        if matches!(bytes.get(i), Some(b'+') | Some(b'-')) {
            i += 1;
        }
        let exponent = digits(i);
        if exponent == 0 {
            return false;
        }
        i += exponent;
    }
    i == bytes.len()
}

#[derive(PartialEq)]
enum Tok {
    Str(String),
    Chr(String),
    Num(String),
    Word(String),
    Open(char),
    Close(char),
    Comma,
    Colon,
    DotDot,
}

fn unescape(chars: &[char], at: usize) -> Option<(char, usize)> {
    let c = *chars.get(at)?;
    Some(match c {
        'n' => ('\n', 1),
        'r' => ('\r', 1),
        't' => ('\t', 1),
        '0' => ('\0', 1),
        '\\' | '"' | '\'' => (c, 1),
        'u' if chars.get(at + 1) == Some(&'{') => {
            let close = chars[at..].iter().position(|&x| x == '}')? + at;
            let hex: String = chars[at + 2..close].iter().collect();
            (char::from_u32(u32::from_str_radix(&hex, 16).ok()?)?, close - at + 1)
        }
        other => (other, 1),
    })
}

fn tokenize(text: &str) -> Option<Vec<Tok>> {
    let chars: Vec<char> = text.chars().collect();
    let mut i = 0;
    let mut out = Vec::new();
    while i < chars.len() {
        let c = chars[i];
        match c {
            c if c.is_whitespace() => i += 1,
            '"' => {
                i += 1;
                let mut s = String::new();
                loop {
                    match *chars.get(i)? {
                        '"' => {
                            i += 1;
                            break;
                        }
                        '\\' => {
                            let (ch, used) = unescape(&chars, i + 1)?;
                            s.push(ch);
                            i += 1 + used;
                        }
                        other => {
                            s.push(other);
                            i += 1;
                        }
                    }
                }
                out.push(Tok::Str(s));
            }
            '\'' => {
                i += 1;
                let ch = if *chars.get(i)? == '\\' {
                    let (ch, used) = unescape(&chars, i + 1)?;
                    i += 1 + used;
                    ch
                } else {
                    i += 1;
                    chars[i - 1]
                };
                if *chars.get(i)? != '\'' {
                    return None;
                }
                i += 1;
                out.push(Tok::Chr(ch.to_string()));
            }
            '[' | '{' | '(' => {
                out.push(Tok::Open(c));
                i += 1;
            }
            ']' | '}' | ')' => {
                out.push(Tok::Close(c));
                i += 1;
            }
            ',' => {
                out.push(Tok::Comma);
                i += 1;
            }
            ':' => {
                out.push(Tok::Colon);
                i += 1;
            }
            '.' if chars.get(i + 1) == Some(&'.') => {
                out.push(Tok::DotDot);
                while chars.get(i) == Some(&'.') {
                    i += 1;
                }
            }
            c if c == '-' || c.is_ascii_digit() => {
                let start = i;
                i += 1;
                while i < chars.len() {
                    let d = chars[i];
                    let exponent_sign = (d == '+' || d == '-') && matches!(chars[i - 1], 'e' | 'E');
                    if d.is_ascii_alphanumeric() || d == '.' || d == '_' || exponent_sign {
                        i += 1;
                    } else {
                        break;
                    }
                }
                let number: String = chars[start..i].iter().collect();
                out.push(Tok::Num(number));
            }
            c if c.is_alphabetic() || c == '_' => {
                let start = i;
                while i < chars.len() && (chars[i].is_alphanumeric() || chars[i] == '_') {
                    i += 1;
                }
                out.push(Tok::Word(chars[start..i].iter().collect()));
            }
            _ => return None,
        }
    }
    Some(out)
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
        if self.at < self.tokens.len() {
            self.at += 1;
            // Tokens are only ever read once, so take ownership by swapping in a filler.
            Some(std::mem::replace(&mut self.tokens[self.at - 1], Tok::Comma))
        } else {
            None
        }
    }

    fn value(&mut self) -> Option<Node> {
        self.depth += 1;
        if self.depth > MAX_DEPTH {
            return None;
        }
        let node = match self.next()? {
            Tok::Str(s) | Tok::Chr(s) => Node::Text(s),
            Tok::Num(n) => Node::Number(n),
            Tok::Word(w) => self.word(w)?,
            Tok::Open('[') => Node::List(self.items(']')?),
            Tok::Open('(') => Node::Tuple(None, self.items(')')?),
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

fn parse(text: &str) -> Option<Node> {
    let mut parser = Parser { tokens: tokenize(text)?, at: 0, depth: 0 };
    let node = parser.value()?;
    if parser.at == parser.tokens.len() {
        Some(node)
    } else {
        None
    }
}
""";
}
