using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>What a name in <see cref="RustStandardLibrary"/> is, so completion can pick an icon and hover can label it.</summary>
public enum RustSymbolKind
{
    Keyword,
    Macro,
    Type,
    Trait,
    Function,
    Method,
    Module,
    Constant,
    Variant
}

/// <summary>A standard-library or language item shown in completion and hover.</summary>
/// <param name="Name">What is typed, e.g. <c>push</c>, <c>println!</c>, <c>HashMap</c>.</param>
/// <param name="Signature">How it looks in Rust, e.g. <c>fn push(&amp;mut self, value: T)</c>.</param>
/// <param name="Summary">One sentence about it.</param>
/// <param name="Owner">Where it lives: a type, trait or module, e.g. <c>Vec</c>, <c>std::collections</c>.</param>
public sealed record RustSymbol(string Name, string Signature, string Summary, RustSymbolKind Kind, string? Owner = null)
{
    public CompletionItemKind CompletionKind => Kind switch
    {
        RustSymbolKind.Keyword => CompletionItemKind.Keyword,
        RustSymbolKind.Macro => CompletionItemKind.Snippet,
        RustSymbolKind.Type => CompletionItemKind.Struct,
        RustSymbolKind.Trait => CompletionItemKind.Interface,
        RustSymbolKind.Function => CompletionItemKind.Method,
        RustSymbolKind.Method => CompletionItemKind.Method,
        RustSymbolKind.Module => CompletionItemKind.Namespace,
        RustSymbolKind.Constant => CompletionItemKind.Field,
        RustSymbolKind.Variant => CompletionItemKind.Enum,
        _ => CompletionItemKind.Variable
    };
}

/// <summary>
/// The parts of Rust and its standard library the editor knows without a language server: keywords, macros, the everyday
/// types and traits, iterator, string, collection and Option/Result methods, module contents, and associated functions.
/// </summary>
public static class RustStandardLibrary
{
    private static RustSymbol Kw(string name, string signature, string summary) => new(name, signature, summary, RustSymbolKind.Keyword);
    private static RustSymbol Macro(string name, string signature, string summary) => new(name, signature, summary, RustSymbolKind.Macro, "std");
    private static RustSymbol Ty(string name, string signature, string summary, string? owner = null) => new(name, signature, summary, RustSymbolKind.Type, owner);
    private static RustSymbol Tr(string name, string signature, string summary, string? owner = null) => new(name, signature, summary, RustSymbolKind.Trait, owner);
    private static RustSymbol Fn(string name, string signature, string summary, string owner) => new(name, signature, summary, RustSymbolKind.Function, owner);
    private static RustSymbol Me(string name, string signature, string summary, string owner) => new(name, signature, summary, RustSymbolKind.Method, owner);
    private static RustSymbol Mod(string name, string summary, string owner) => new(name, $"mod {name}", summary, RustSymbolKind.Module, owner);
    private static RustSymbol Const(string name, string signature, string summary, string owner) => new(name, signature, summary, RustSymbolKind.Constant, owner);

    public static IReadOnlyList<RustSymbol> Keywords { get; } =
    [
        Kw("as", "expr as Type", "Converts a value to another primitive type, or renames an import."),
        Kw("async", "async fn / async { }", "Makes a function or block return a future."),
        Kw("await", "future.await", "Waits for a future to finish."),
        Kw("break", "break 'label value", "Leaves a loop, optionally with a value."),
        Kw("const", "const NAME: Type = value;", "Declares a compile-time constant, or a const fn."),
        Kw("continue", "continue 'label", "Skips to the next iteration of a loop."),
        Kw("crate", "crate::path", "The root of the current crate."),
        Kw("dyn", "dyn Trait", "A trait object: a value of any type that implements the trait, called through a vtable."),
        Kw("else", "if cond { } else { }", "The branch taken when an if condition is false."),
        Kw("enum", "enum Name { A, B(T) }", "Declares a type that is one of several variants."),
        Kw("extern", "extern \"C\" { }", "Links to code outside Rust, or declares a foreign ABI."),
        Kw("false", "false", "The boolean false."),
        Kw("fn", "fn name(params) -> Type { }", "Declares a function."),
        Kw("for", "for pattern in iterator { }", "Loops over an iterator; also part of impl Trait for Type."),
        Kw("if", "if cond { }", "Runs a block when a condition is true; if let matches a pattern."),
        Kw("impl", "impl Type { } / impl Trait for Type { }", "Adds methods to a type, or implements a trait for it."),
        Kw("in", "for x in iter", "Separates the pattern from the iterator in a for loop."),
        Kw("let", "let pattern = value;", "Binds a value to a name (immutable unless mut)."),
        Kw("loop", "loop { }", "Repeats forever until break; break can return a value."),
        Kw("match", "match value { pattern => expr, }", "Compares a value against patterns; the arms must cover every case."),
        Kw("mod", "mod name { } / mod name;", "Declares a module, inline or in another file."),
        Kw("move", "move |x| ...", "Makes a closure take ownership of the variables it uses."),
        Kw("mut", "let mut x / &mut x", "Allows a binding to change, or borrows a value mutably."),
        Kw("pub", "pub fn / pub(crate)", "Makes an item visible outside its module."),
        Kw("ref", "ref x", "Binds by reference inside a pattern."),
        Kw("return", "return value;", "Returns from the function."),
        Kw("self", "self / &self / &mut self", "The value a method is called on, or the current module."),
        Kw("Self", "Self", "The type an impl or trait is for."),
        Kw("static", "static NAME: Type = value;", "A single value that lives for the whole program; or the 'static lifetime."),
        Kw("struct", "struct Name { field: T }", "Declares a type made of named fields (or a tuple struct)."),
        Kw("super", "super::path", "The parent module."),
        Kw("trait", "trait Name { fn method(&self); }", "Declares shared behaviour types can implement."),
        Kw("true", "true", "The boolean true."),
        Kw("type", "type Alias = Type;", "Names another type, or an associated type in a trait."),
        Kw("unsafe", "unsafe { }", "Allows operations the compiler can't check (raw pointers, FFI)."),
        Kw("use", "use path::Item;", "Brings names into scope."),
        Kw("where", "where T: Trait", "Adds bounds on generic parameters."),
        Kw("while", "while cond { }", "Repeats while a condition is true; while let matches a pattern."),
    ];

    public static IReadOnlyList<RustSymbol> Macros { get; } =
    [
        Macro("println!", "println!(\"{}\", value)", "Prints to standard output with a newline."),
        Macro("print!", "print!(\"{}\", value)", "Prints to standard output without a newline."),
        Macro("eprintln!", "eprintln!(\"{}\", value)", "Prints to standard error with a newline."),
        Macro("eprint!", "eprint!(\"{}\", value)", "Prints to standard error without a newline."),
        Macro("format!", "format!(\"{}\", value) -> String", "Builds a String from a format string."),
        Macro("write!", "write!(dest, \"{}\", value) -> Result", "Writes formatted text to a Write or fmt::Write destination."),
        Macro("writeln!", "writeln!(dest, \"{}\", value) -> Result", "Like write!, with a newline."),
        Macro("vec!", "vec![a, b, c] / vec![value; n]", "Builds a Vec from elements, or n copies of a value."),
        Macro("panic!", "panic!(\"message\")", "Stops the thread with a message (exit code 101 for the main thread)."),
        Macro("assert!", "assert!(cond, \"message\")", "Panics if the condition is false."),
        Macro("assert_eq!", "assert_eq!(left, right)", "Panics, printing both values, if they are not equal."),
        Macro("assert_ne!", "assert_ne!(left, right)", "Panics if the values are equal."),
        Macro("debug_assert!", "debug_assert!(cond)", "Like assert!, but only checked in debug builds."),
        Macro("dbg!", "dbg!(expr)", "Prints the expression, its value and its place to standard error, then returns the value."),
        Macro("todo!", "todo!()", "Marks code you haven't written yet; panics if reached."),
        Macro("unimplemented!", "unimplemented!()", "Marks code that isn't implemented; panics if reached."),
        Macro("unreachable!", "unreachable!()", "Marks code that should never run; panics if reached."),
        Macro("matches!", "matches!(value, pattern)", "True when the value matches the pattern."),
        Macro("include_str!", "include_str!(\"file\")", "Embeds a file's text at compile time."),
        Macro("env!", "env!(\"NAME\")", "Reads an environment variable at compile time."),
        Macro("concat!", "concat!(a, b)", "Joins literals into one at compile time."),
        Macro("stringify!", "stringify!(tokens)", "Turns tokens into a string literal."),
        Macro("line!", "line!()", "The current line number."),
        Macro("file!", "file!()", "The current file name."),
        Macro("cfg!", "cfg!(condition)", "Evaluates a configuration condition to a bool."),
        Macro("macro_rules!", "macro_rules! name { (pattern) => { body }; }", "Defines a declarative macro."),
    ];

    public static IReadOnlyList<RustSymbol> Types { get; } =
    [
        Ty("String", "struct String", "A growable, owned UTF-8 string."),
        Ty("str", "str", "A string slice; usually seen as &str."),
        Ty("Vec", "struct Vec<T>", "A growable array on the heap."),
        Ty("Option", "enum Option<T> { Some(T), None }", "A value that may be absent."),
        Ty("Result", "enum Result<T, E> { Ok(T), Err(E) }", "Success with a value, or failure with an error."),
        Ty("Box", "struct Box<T>", "An owned value on the heap."),
        Ty("Rc", "struct Rc<T>", "A reference-counted pointer for single-threaded sharing."),
        Ty("Arc", "struct Arc<T>", "An atomically reference-counted pointer for sharing across threads."),
        Ty("Cell", "struct Cell<T>", "A mutable memory location for Copy values, through a shared reference."),
        Ty("RefCell", "struct RefCell<T>", "Interior mutability checked at run time (borrow / borrow_mut)."),
        Ty("Mutex", "struct Mutex<T>", "A lock that guards a value shared between threads."),
        Ty("RwLock", "struct RwLock<T>", "A lock allowing many readers or one writer."),
        Ty("HashMap", "struct HashMap<K, V>", "A hash map: key to value, no order.", "std::collections"),
        Ty("HashSet", "struct HashSet<T>", "A hash set of unique values, no order.", "std::collections"),
        Ty("BTreeMap", "struct BTreeMap<K, V>", "A map kept sorted by key.", "std::collections"),
        Ty("BTreeSet", "struct BTreeSet<T>", "A set kept sorted.", "std::collections"),
        Ty("VecDeque", "struct VecDeque<T>", "A double-ended queue (ring buffer).", "std::collections"),
        Ty("BinaryHeap", "struct BinaryHeap<T>", "A max-heap priority queue.", "std::collections"),
        Ty("Cow", "enum Cow<'a, B>", "Clone-on-write: borrowed until it has to change.", "std::borrow"),
        Ty("PathBuf", "struct PathBuf", "An owned file system path.", "std::path"),
        Ty("Duration", "struct Duration", "A span of time.", "std::time"),
        Ty("Instant", "struct Instant", "A point in time from a monotonic clock, for measuring elapsed time.", "std::time"),
        Ty("Ordering", "enum Ordering { Less, Equal, Greater }", "The result of comparing two values.", "std::cmp"),
        Ty("i8", "i8", "8-bit signed integer."), Ty("i16", "i16", "16-bit signed integer."), Ty("i32", "i32", "32-bit signed integer (the default integer type)."),
        Ty("i64", "i64", "64-bit signed integer."), Ty("i128", "i128", "128-bit signed integer."), Ty("isize", "isize", "Pointer-sized signed integer."),
        Ty("u8", "u8", "8-bit unsigned integer (a byte)."), Ty("u16", "u16", "16-bit unsigned integer."), Ty("u32", "u32", "32-bit unsigned integer."),
        Ty("u64", "u64", "64-bit unsigned integer."), Ty("u128", "u128", "128-bit unsigned integer."), Ty("usize", "usize", "Pointer-sized unsigned integer; used for indexes and lengths."),
        Ty("f32", "f32", "32-bit floating point."), Ty("f64", "f64", "64-bit floating point (the default float type)."),
        Ty("bool", "bool", "true or false."), Ty("char", "char", "A Unicode scalar value (4 bytes)."),
    ];

    public static IReadOnlyList<RustSymbol> Traits { get; } =
    [
        Tr("Clone", "trait Clone", "Makes an explicit duplicate with .clone()."),
        Tr("Copy", "trait Copy", "Marks a type that is copied instead of moved."),
        Tr("Debug", "trait Debug", "Formatting for programmers: {:?} and {:#?}.", "std::fmt"),
        Tr("Display", "trait Display", "Formatting for people: {}.", "std::fmt"),
        Tr("Default", "trait Default", "A default value: Type::default()."),
        Tr("PartialEq", "trait PartialEq", "Equality with == and !=."),
        Tr("Eq", "trait Eq", "Equality that is reflexive (no NaN-like values)."),
        Tr("PartialOrd", "trait PartialOrd", "Ordering with <, >, <= and >=."),
        Tr("Ord", "trait Ord", "A total order: cmp, max, min, sorting."),
        Tr("Hash", "trait Hash", "Lets a value be hashed, e.g. as a HashMap key.", "std::hash"),
        Tr("Iterator", "trait Iterator { type Item; fn next(&mut self) -> Option<Self::Item>; }", "A sequence of values, with map, filter, collect and more."),
        Tr("IntoIterator", "trait IntoIterator", "Anything a for loop can loop over."),
        Tr("From", "trait From<T>", "Converts from another type: Type::from(value)."),
        Tr("Into", "trait Into<T>", "Converts into another type: value.into()."),
        Tr("TryFrom", "trait TryFrom<T>", "A conversion that can fail.", "std::convert"),
        Tr("AsRef", "trait AsRef<T>", "A cheap reference-to-reference conversion."),
        Tr("Drop", "trait Drop", "Code that runs when a value goes out of scope."),
        Tr("Deref", "trait Deref", "Lets a smart pointer act like the value it holds.", "std::ops"),
        Tr("Fn", "trait Fn(Args) -> R", "A closure or function callable by shared reference."),
        Tr("FnMut", "trait FnMut(Args) -> R", "A closure that may change what it captured."),
        Tr("FnOnce", "trait FnOnce(Args) -> R", "A closure that can be called once (it may consume what it captured)."),
        Tr("Send", "trait Send", "Safe to move to another thread."),
        Tr("Sync", "trait Sync", "Safe to share between threads by reference."),
        Tr("Sized", "trait Sized", "A type whose size is known at compile time."),
        Tr("ToString", "trait ToString", "Converts to a String: value.to_string()."),
        Tr("FromStr", "trait FromStr", "Parses from a string: \"5\".parse::<i32>().", "std::str"),
    ];

    public static IReadOnlyList<RustSymbol> Methods { get; } =
    [
        // Collections and slices
        Me("len", "fn len(&self) -> usize", "How many elements (or bytes, for a string) it holds.", "Vec, String, slice, HashMap"),
        Me("is_empty", "fn is_empty(&self) -> bool", "True when it holds nothing.", "Vec, String, slice, HashMap"),
        Me("push", "fn push(&mut self, value: T)", "Adds an element at the end.", "Vec"),
        Me("pop", "fn pop(&mut self) -> Option<T>", "Removes and returns the last element.", "Vec"),
        Me("insert", "fn insert(&mut self, index: usize, value: T)", "Inserts at a position (HashMap/HashSet: adds a key).", "Vec, HashMap, HashSet"),
        Me("remove", "fn remove(&mut self, index: usize) -> T", "Removes and returns an element (HashMap: by key).", "Vec, HashMap, String"),
        Me("get", "fn get(&self, index: usize) -> Option<&T>", "The element at a position or key, or None.", "Vec, slice, HashMap"),
        Me("get_mut", "fn get_mut(&mut self, index: usize) -> Option<&mut T>", "A mutable reference to an element, or None.", "Vec, slice, HashMap"),
        Me("contains", "fn contains(&self, x: &T) -> bool", "Whether it holds the value (a str holds the pattern).", "Vec, slice, HashSet, str"),
        Me("contains_key", "fn contains_key(&self, key: &K) -> bool", "Whether the map has the key.", "HashMap, BTreeMap"),
        Me("clear", "fn clear(&mut self)", "Removes everything.", "Vec, String, HashMap"),
        Me("extend", "fn extend<I: IntoIterator<Item = T>>(&mut self, iter: I)", "Adds every element of an iterator.", "Vec, String, HashMap"),
        Me("truncate", "fn truncate(&mut self, len: usize)", "Shortens to at most len elements.", "Vec, String"),
        Me("retain", "fn retain<F: FnMut(&T) -> bool>(&mut self, f: F)", "Keeps only the elements the closure accepts.", "Vec, HashMap, HashSet"),
        Me("drain", "fn drain<R: RangeBounds<usize>>(&mut self, range: R)", "Removes a range and yields it.", "Vec, String"),
        Me("dedup", "fn dedup(&mut self)", "Removes consecutive repeats.", "Vec"),
        Me("swap", "fn swap(&mut self, a: usize, b: usize)", "Swaps two elements.", "Vec, slice"),
        Me("reverse", "fn reverse(&mut self)", "Reverses the order in place.", "Vec, slice"),
        Me("sort", "fn sort(&mut self)", "Sorts in place (stable).", "Vec, slice"),
        Me("sort_by", "fn sort_by<F: FnMut(&T, &T) -> Ordering>(&mut self, compare: F)", "Sorts with a comparison closure.", "Vec, slice"),
        Me("sort_by_key", "fn sort_by_key<K: Ord, F: FnMut(&T) -> K>(&mut self, f: F)", "Sorts by a key.", "Vec, slice"),
        Me("sort_unstable", "fn sort_unstable(&mut self)", "Sorts in place, faster, not stable.", "Vec, slice"),
        Me("binary_search", "fn binary_search(&self, x: &T) -> Result<usize, usize>", "Finds a value in a sorted slice.", "Vec, slice"),
        Me("first", "fn first(&self) -> Option<&T>", "The first element.", "Vec, slice"),
        Me("last", "fn last(&self) -> Option<&T>", "The last element.", "Vec, slice"),
        Me("windows", "fn windows(&self, size: usize) -> Windows<T>", "Overlapping sub-slices of a size.", "Vec, slice"),
        Me("chunks", "fn chunks(&self, size: usize) -> Chunks<T>", "Non-overlapping sub-slices of a size.", "Vec, slice"),
        Me("split_at", "fn split_at(&self, mid: usize) -> (&[T], &[T])", "Splits into two slices at an index.", "slice, str"),
        Me("join", "fn join(&self, sep: &str) -> String", "Joins strings with a separator.", "Vec<String>, slice"),
        Me("concat", "fn concat(&self) -> T", "Joins slices or strings end to end.", "slice"),
        Me("entry", "fn entry(&mut self, key: K) -> Entry<K, V>", "Looks up a key to insert or update in one step.", "HashMap, BTreeMap"),
        Me("or_insert", "fn or_insert(self, default: V) -> &mut V", "The entry's value, inserting default if missing.", "Entry"),
        Me("or_insert_with", "fn or_insert_with<F: FnOnce() -> V>(self, default: F) -> &mut V", "The entry's value, inserting default() if missing.", "Entry"),
        Me("or_default", "fn or_default(self) -> &mut V", "The entry's value, inserting the default if missing.", "Entry"),
        Me("keys", "fn keys(&self) -> Keys<K, V>", "Iterates over the keys.", "HashMap, BTreeMap"),
        Me("values", "fn values(&self) -> Values<K, V>", "Iterates over the values.", "HashMap, BTreeMap"),
        Me("values_mut", "fn values_mut(&mut self) -> ValuesMut<K, V>", "Iterates over the values mutably.", "HashMap, BTreeMap"),
        Me("push_back", "fn push_back(&mut self, value: T)", "Adds at the back.", "VecDeque"),
        Me("push_front", "fn push_front(&mut self, value: T)", "Adds at the front.", "VecDeque"),
        Me("pop_front", "fn pop_front(&mut self) -> Option<T>", "Removes from the front.", "VecDeque"),
        Me("pop_back", "fn pop_back(&mut self) -> Option<T>", "Removes from the back.", "VecDeque"),
        Me("peek", "fn peek(&self) -> Option<&T>", "The largest element without removing it.", "BinaryHeap"),

        // Iterators
        Me("iter", "fn iter(&self) -> Iter<T>", "Iterates over references.", "Vec, slice, HashMap"),
        Me("iter_mut", "fn iter_mut(&mut self) -> IterMut<T>", "Iterates over mutable references.", "Vec, slice, HashMap"),
        Me("into_iter", "fn into_iter(self) -> IntoIter<T>", "Iterates by value, consuming the collection.", "Vec, HashMap, Option"),
        Me("map", "fn map<B, F: FnMut(Self::Item) -> B>(self, f: F) -> Map<Self, F>", "Transforms each item.", "Iterator, Option, Result"),
        Me("filter", "fn filter<P: FnMut(&Self::Item) -> bool>(self, predicate: P) -> Filter<Self, P>", "Keeps the items the closure accepts.", "Iterator"),
        Me("filter_map", "fn filter_map<B, F: FnMut(Self::Item) -> Option<B>>(self, f: F)", "Maps and drops the None results.", "Iterator"),
        Me("flat_map", "fn flat_map<U: IntoIterator, F: FnMut(Self::Item) -> U>(self, f: F)", "Maps each item to an iterator and flattens.", "Iterator"),
        Me("flatten", "fn flatten(self)", "Flattens nested iterators (or Option/Result).", "Iterator, Option"),
        Me("enumerate", "fn enumerate(self) -> Enumerate<Self>", "Pairs each item with its index.", "Iterator"),
        Me("zip", "fn zip<U: IntoIterator>(self, other: U) -> Zip<Self, U::IntoIter>", "Pairs items of two iterators.", "Iterator"),
        Me("chain", "fn chain<U: IntoIterator>(self, other: U) -> Chain<Self, U::IntoIter>", "Follows this iterator with another.", "Iterator"),
        Me("rev", "fn rev(self) -> Rev<Self>", "Iterates backwards.", "Iterator"),
        Me("take", "fn take(self, n: usize) -> Take<Self>", "Yields at most n items (Option: moves the value out).", "Iterator, Option"),
        Me("skip", "fn skip(self, n: usize) -> Skip<Self>", "Skips the first n items.", "Iterator"),
        Me("step_by", "fn step_by(self, step: usize) -> StepBy<Self>", "Yields every step-th item.", "Iterator"),
        Me("take_while", "fn take_while<P: FnMut(&Self::Item) -> bool>(self, predicate: P)", "Yields items while the closure holds.", "Iterator"),
        Me("skip_while", "fn skip_while<P: FnMut(&Self::Item) -> bool>(self, predicate: P)", "Skips items while the closure holds.", "Iterator"),
        Me("peekable", "fn peekable(self) -> Peekable<Self>", "Lets you look at the next item without consuming it.", "Iterator"),
        Me("cloned", "fn cloned<'a, T: 'a + Clone>(self) -> Cloned<Self>", "Clones each referenced item.", "Iterator, Option"),
        Me("copied", "fn copied<'a, T: 'a + Copy>(self) -> Copied<Self>", "Copies each referenced item.", "Iterator, Option"),
        Me("collect", "fn collect<B: FromIterator<Self::Item>>(self) -> B", "Gathers the items into a collection (Vec, String, HashMap, Result...).", "Iterator"),
        Me("fold", "fn fold<B, F: FnMut(B, Self::Item) -> B>(self, init: B, f: F) -> B", "Combines all items into one value.", "Iterator"),
        Me("sum", "fn sum<S: Sum<Self::Item>>(self) -> S", "Adds the items.", "Iterator"),
        Me("product", "fn product<P: Product<Self::Item>>(self) -> P", "Multiplies the items.", "Iterator"),
        Me("count", "fn count(self) -> usize", "How many items.", "Iterator"),
        Me("any", "fn any<F: FnMut(Self::Item) -> bool>(&mut self, f: F) -> bool", "True if any item satisfies the closure.", "Iterator"),
        Me("all", "fn all<F: FnMut(Self::Item) -> bool>(&mut self, f: F) -> bool", "True if every item satisfies the closure.", "Iterator"),
        Me("find", "fn find<P: FnMut(&Self::Item) -> bool>(&mut self, predicate: P) -> Option<Self::Item>", "The first item the closure accepts (str: position of a pattern).", "Iterator, str"),
        Me("position", "fn position<P: FnMut(Self::Item) -> bool>(&mut self, predicate: P) -> Option<usize>", "The index of the first item the closure accepts.", "Iterator"),
        Me("min", "fn min(self) -> Option<Self::Item>", "The smallest item (on numbers: the smaller of two).", "Iterator, Ord"),
        Me("max", "fn max(self) -> Option<Self::Item>", "The largest item (on numbers: the larger of two).", "Iterator, Ord"),
        Me("min_by_key", "fn min_by_key<B: Ord, F: FnMut(&Self::Item) -> B>(self, f: F) -> Option<Self::Item>", "The item with the smallest key.", "Iterator"),
        Me("max_by_key", "fn max_by_key<B: Ord, F: FnMut(&Self::Item) -> B>(self, f: F) -> Option<Self::Item>", "The item with the largest key.", "Iterator"),
        Me("next", "fn next(&mut self) -> Option<Self::Item>", "The next item, or None when done.", "Iterator"),
        Me("last", "fn last(self) -> Option<Self::Item>", "The last item.", "Iterator"),
        Me("nth", "fn nth(&mut self, n: usize) -> Option<Self::Item>", "The n-th item (from 0).", "Iterator"),
        Me("for_each", "fn for_each<F: FnMut(Self::Item)>(self, f: F)", "Runs a closure on each item.", "Iterator"),

        // Strings
        Me("to_string", "fn to_string(&self) -> String", "Converts to a String.", "ToString"),
        Me("to_owned", "fn to_owned(&self) -> Self::Owned", "Makes an owned copy (&str becomes String).", "ToOwned"),
        Me("to_lowercase", "fn to_lowercase(&self) -> String", "Lower-cases the text.", "str"),
        Me("to_uppercase", "fn to_uppercase(&self) -> String", "Upper-cases the text.", "str"),
        Me("push_str", "fn push_str(&mut self, string: &str)", "Appends a string slice.", "String"),
        Me("as_str", "fn as_str(&self) -> &str", "Borrows the String as a &str.", "String"),
        Me("as_bytes", "fn as_bytes(&self) -> &[u8]", "The UTF-8 bytes.", "str, String"),
        Me("chars", "fn chars(&self) -> Chars", "Iterates over the characters.", "str"),
        Me("bytes", "fn bytes(&self) -> Bytes", "Iterates over the bytes.", "str"),
        Me("lines", "fn lines(&self) -> Lines", "Iterates over the lines.", "str"),
        Me("split", "fn split<P: Pattern>(&self, pat: P) -> Split<P>", "Splits at each match of a pattern.", "str, slice"),
        Me("split_whitespace", "fn split_whitespace(&self) -> SplitWhitespace", "Splits on whitespace.", "str"),
        Me("trim", "fn trim(&self) -> &str", "Removes whitespace at both ends.", "str"),
        Me("starts_with", "fn starts_with<P: Pattern>(&self, pat: P) -> bool", "Whether it begins with the pattern.", "str, slice"),
        Me("ends_with", "fn ends_with<P: Pattern>(&self, pat: P) -> bool", "Whether it ends with the pattern.", "str, slice"),
        Me("replace", "fn replace<P: Pattern>(&self, from: P, to: &str) -> String", "Replaces every match (Option/mem: see std::mem::replace).", "str"),
        Me("parse", "fn parse<F: FromStr>(&self) -> Result<F, F::Err>", "Parses the text into a value: s.parse::<i32>().", "str"),
        Me("repeat", "fn repeat(&self, n: usize) -> String", "Repeats the text n times.", "str"),

        // Option and Result
        Me("unwrap", "fn unwrap(self) -> T", "The value, or panics if None/Err.", "Option, Result"),
        Me("expect", "fn expect(self, msg: &str) -> T", "The value, or panics with your message.", "Option, Result"),
        Me("unwrap_or", "fn unwrap_or(self, default: T) -> T", "The value, or a default.", "Option, Result"),
        Me("unwrap_or_else", "fn unwrap_or_else<F: FnOnce() -> T>(self, f: F) -> T", "The value, or the result of a closure.", "Option, Result"),
        Me("unwrap_or_default", "fn unwrap_or_default(self) -> T", "The value, or the type's default.", "Option, Result"),
        Me("is_some", "fn is_some(&self) -> bool", "True when it holds a value.", "Option"),
        Me("is_none", "fn is_none(&self) -> bool", "True when it holds nothing.", "Option"),
        Me("is_ok", "fn is_ok(&self) -> bool", "True when it is Ok.", "Result"),
        Me("is_err", "fn is_err(&self) -> bool", "True when it is Err.", "Result"),
        Me("ok", "fn ok(self) -> Option<T>", "Turns Ok(v) into Some(v) and Err into None.", "Result"),
        Me("ok_or", "fn ok_or<E>(self, err: E) -> Result<T, E>", "Turns Some(v) into Ok(v) and None into Err(err).", "Option"),
        Me("map_err", "fn map_err<F, O: FnOnce(E) -> F>(self, op: O) -> Result<T, F>", "Transforms the error.", "Result"),
        Me("and_then", "fn and_then<U, F: FnOnce(T) -> Option<U>>(self, f: F) -> Option<U>", "Chains a step that may fail.", "Option, Result"),
        Me("or_else", "fn or_else<F: FnOnce() -> Option<T>>(self, f: F) -> Option<T>", "Tries an alternative when empty.", "Option, Result"),
        Me("as_ref", "fn as_ref(&self) -> Option<&T>", "Borrows the inside.", "Option, Result"),
        Me("as_mut", "fn as_mut(&mut self) -> Option<&mut T>", "Mutably borrows the inside.", "Option, Result"),

        // Pointers, borrowing, general
        Me("clone", "fn clone(&self) -> Self", "Makes an explicit duplicate.", "Clone"),
        Me("borrow", "fn borrow(&self) -> Ref<T>", "Borrows the RefCell's value immutably (checked at run time).", "RefCell"),
        Me("borrow_mut", "fn borrow_mut(&self) -> RefMut<T>", "Borrows the RefCell's value mutably (checked at run time).", "RefCell"),
        Me("lock", "fn lock(&self) -> LockResult<MutexGuard<T>>", "Waits for the mutex and returns a guard.", "Mutex"),

        // Numbers
        Me("abs", "fn abs(self) -> Self", "Absolute value.", "i32, f64, ..."),
        Me("pow", "fn pow(self, exp: u32) -> Self", "Raises to an integer power.", "i32, u64, ..."),
        Me("powi", "fn powi(self, n: i32) -> f64", "Raises a float to an integer power.", "f64"),
        Me("sqrt", "fn sqrt(self) -> f64", "Square root.", "f64"),
        Me("floor", "fn floor(self) -> f64", "Rounds down.", "f64"),
        Me("ceil", "fn ceil(self) -> f64", "Rounds up.", "f64"),
        Me("round", "fn round(self) -> f64", "Rounds to the nearest integer.", "f64"),
        Me("clamp", "fn clamp(self, min: Self, max: Self) -> Self", "Limits to a range.", "Ord, f64"),
        Me("checked_add", "fn checked_add(self, rhs: Self) -> Option<Self>", "Adds, or None on overflow.", "integers"),
        Me("checked_sub", "fn checked_sub(self, rhs: Self) -> Option<Self>", "Subtracts, or None on overflow.", "integers"),
        Me("checked_mul", "fn checked_mul(self, rhs: Self) -> Option<Self>", "Multiplies, or None on overflow.", "integers"),
        Me("wrapping_add", "fn wrapping_add(self, rhs: Self) -> Self", "Adds, wrapping around on overflow.", "integers"),
        Me("saturating_sub", "fn saturating_sub(self, rhs: Self) -> Self", "Subtracts, stopping at the minimum.", "integers"),
        Me("rem_euclid", "fn rem_euclid(self, rhs: Self) -> Self", "The remainder that is never negative.", "integers"),
    ];

    public static IReadOnlyList<RustSymbol> Functions { get; } =
    [
        Fn("std::mem::swap", "fn swap<T>(x: &mut T, y: &mut T)", "Swaps two values.", "std::mem"),
        Fn("std::mem::replace", "fn replace<T>(dest: &mut T, src: T) -> T", "Puts a value in place and returns the old one.", "std::mem"),
        Fn("std::mem::take", "fn take<T: Default>(dest: &mut T) -> T", "Takes the value out, leaving the default.", "std::mem"),
        Fn("std::mem::drop", "fn drop<T>(_x: T)", "Drops a value now.", "std::mem"),
        Fn("std::process::exit", "fn exit(code: i32) -> !", "Ends the program with an exit code.", "std::process"),
        Fn("std::env::args", "fn args() -> Args", "The command-line arguments.", "std::env"),
        Fn("std::env::var", "fn var<K: AsRef<OsStr>>(key: K) -> Result<String, VarError>", "Reads an environment variable.", "std::env"),
        Fn("std::fs::read_to_string", "fn read_to_string<P: AsRef<Path>>(path: P) -> io::Result<String>", "Reads a whole file as text.", "std::fs"),
        Fn("std::fs::write", "fn write<P: AsRef<Path>, C: AsRef<[u8]>>(path: P, contents: C) -> io::Result<()>", "Writes bytes or text to a file.", "std::fs"),
        Fn("std::io::stdin", "fn stdin() -> Stdin", "The standard input; stdin().read_line(&mut s) reads a line.", "std::io"),
        Fn("std::io::stdout", "fn stdout() -> Stdout", "The standard output.", "std::io"),
        Fn("std::thread::spawn", "fn spawn<F, T>(f: F) -> JoinHandle<T>", "Runs a closure on a new thread.", "std::thread"),
        Fn("std::thread::sleep", "fn sleep(dur: Duration)", "Pauses the thread.", "std::thread"),
        Fn("std::cmp::max", "fn max<T: Ord>(v1: T, v2: T) -> T", "The larger of two values.", "std::cmp"),
        Fn("std::cmp::min", "fn min<T: Ord>(v1: T, v2: T) -> T", "The smaller of two values.", "std::cmp"),
        Fn("String::new", "fn new() -> String", "An empty String.", "String"),
        Fn("String::from", "fn from(s: &str) -> String", "A String from a &str (or char).", "String"),
        Fn("String::with_capacity", "fn with_capacity(capacity: usize) -> String", "An empty String with room reserved.", "String"),
        Fn("Vec::new", "fn new() -> Vec<T>", "An empty Vec.", "Vec"),
        Fn("Vec::with_capacity", "fn with_capacity(capacity: usize) -> Vec<T>", "An empty Vec with room reserved.", "Vec"),
        Fn("HashMap::new", "fn new() -> HashMap<K, V>", "An empty HashMap.", "HashMap"),
        Fn("HashSet::new", "fn new() -> HashSet<T>", "An empty HashSet.", "HashSet"),
        Fn("BTreeMap::new", "fn new() -> BTreeMap<K, V>", "An empty BTreeMap.", "BTreeMap"),
        Fn("VecDeque::new", "fn new() -> VecDeque<T>", "An empty VecDeque.", "VecDeque"),
        Fn("Box::new", "fn new(x: T) -> Box<T>", "Puts a value on the heap.", "Box"),
        Fn("Rc::new", "fn new(value: T) -> Rc<T>", "A new reference-counted pointer.", "Rc"),
        Fn("Rc::clone", "fn clone(this: &Rc<T>) -> Rc<T>", "A new pointer to the same value (cheap).", "Rc"),
        Fn("Rc::strong_count", "fn strong_count(this: &Rc<T>) -> usize", "How many pointers share the value.", "Rc"),
        Fn("Arc::new", "fn new(data: T) -> Arc<T>", "A new atomically reference-counted pointer.", "Arc"),
        Fn("Arc::clone", "fn clone(this: &Arc<T>) -> Arc<T>", "A new pointer to the same value (cheap).", "Arc"),
        Fn("Mutex::new", "fn new(t: T) -> Mutex<T>", "A new mutex around a value.", "Mutex"),
        Fn("RefCell::new", "fn new(value: T) -> RefCell<T>", "A new RefCell around a value.", "RefCell"),
        Fn("Instant::now", "fn now() -> Instant", "The current instant.", "Instant"),
        Fn("Duration::from_secs", "fn from_secs(secs: u64) -> Duration", "A duration of whole seconds.", "Duration"),
        Fn("Duration::from_millis", "fn from_millis(millis: u64) -> Duration", "A duration of milliseconds.", "Duration"),
        Fn("Default::default", "fn default() -> Self", "The default value of the expected type.", "Default"),
        Fn("char::from_u32", "fn from_u32(i: u32) -> Option<char>", "A char from a code point, if valid.", "char"),
        Fn("char::from_digit", "fn from_digit(num: u32, radix: u32) -> Option<char>", "A digit character, if valid.", "char"),
        Const("i32::MAX", "const MAX: i32", "The largest i32.", "i32"),
        Const("i32::MIN", "const MIN: i32", "The smallest i32.", "i32"),
        Const("i64::MAX", "const MAX: i64", "The largest i64.", "i64"),
        Const("u32::MAX", "const MAX: u32", "The largest u32.", "u32"),
        Const("u64::MAX", "const MAX: u64", "The largest u64.", "u64"),
        Const("usize::MAX", "const MAX: usize", "The largest usize.", "usize"),
        Const("f64::MAX", "const MAX: f64", "The largest finite f64.", "f64"),
        Const("f64::EPSILON", "const EPSILON: f64", "The gap between 1.0 and the next f64.", "f64"),
        Const("f64::INFINITY", "const INFINITY: f64", "Positive infinity.", "f64"),
        Const("f64::NAN", "const NAN: f64", "Not a number.", "f64"),
    ];

    /// <summary>The names a <c>std::</c> path continues with, keyed by the path typed so far (<c>std</c>, <c>std::collections</c>, <c>String</c>…).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<RustSymbol>> Paths { get; } = BuildPaths();

    private static Dictionary<string, IReadOnlyList<RustSymbol>> BuildPaths()
    {
        // A function is listed under its full path (std::mem::swap); inside std::mem:: it is just swap.
        RustSymbol Member(string owner, string name) =>
            Functions.FirstOrDefault(f => f.Name == $"{owner}::{name}") is { } function ? function with { Name = name }
            : Types.FirstOrDefault(t => t.Name == name) ?? Traits.FirstOrDefault(t => t.Name == name)
            ?? new RustSymbol(name, name, string.Empty, RustSymbolKind.Function, owner);

        IReadOnlyList<RustSymbol> Names(string owner, params string[] names) => names.Select(n => Member(owner, n)).ToArray();
        IReadOnlyList<RustSymbol> Modules(string owner, params (string Name, string Summary)[] modules) => modules.Select(m => Mod(m.Name, m.Summary, owner)).ToArray();

        var paths = new Dictionary<string, IReadOnlyList<RustSymbol>>(StringComparer.Ordinal)
        {
            ["std"] = Modules("std",
                ("collections", "HashMap, HashSet, BTreeMap, VecDeque and more."), ("fmt", "Formatting: Display, Debug, Formatter."),
                ("io", "Input and output: stdin, stdout, Read, Write."), ("fs", "The file system."), ("env", "Environment variables and arguments."),
                ("thread", "Threads."), ("sync", "Arc, Mutex, RwLock, channels, atomics."), ("time", "Duration and Instant."),
                ("cmp", "Ordering, min, max, Reverse."), ("mem", "swap, replace, take, drop."), ("process", "exit and child processes."),
                ("iter", "Iterator helpers: repeat, once, from_fn, successors."), ("rc", "Rc and Weak."), ("cell", "Cell and RefCell."),
                ("path", "Path and PathBuf."), ("str", "String slices and FromStr."), ("string", "String and ToString."), ("vec", "Vec."),
                ("error", "The Error trait."), ("ops", "Operator traits: Add, Index, Deref, Range."), ("hash", "Hash and Hasher."),
                ("convert", "From, Into, TryFrom, AsRef."), ("borrow", "Borrow and Cow."), ("num", "Number types and parsing errors."),
                ("f64", "f64 constants."), ("i32", "i32 constants."), ("char", "char helpers."), ("net", "TCP and UDP networking.")),
            ["std::collections"] = Names("std::collections", "HashMap", "HashSet", "BTreeMap", "BTreeSet", "VecDeque", "BinaryHeap"),
            ["std::io"] = new[]
            {
                Member("std::io", "stdin"), Member("std::io", "stdout"),
                Tr("Read", "trait Read", "Reads bytes from a source.", "std::io"), Tr("Write", "trait Write", "Writes bytes to a sink.", "std::io"),
                Tr("BufRead", "trait BufRead", "Buffered reading: lines(), read_line().", "std::io"),
                Ty("BufReader", "struct BufReader<R>", "Buffers a reader.", "std::io"), Ty("BufWriter", "struct BufWriter<W>", "Buffers a writer.", "std::io"),
                Ty("Result", "type Result<T> = Result<T, io::Error>", "A Result with io::Error.", "std::io")
            },
            ["std::fs"] = new[] { Member("std::fs", "read_to_string"), Member("std::fs", "write"), Ty("File", "struct File", "An open file.", "std::fs") },
            ["std::thread"] = new[] { Member("std::thread", "spawn"), Member("std::thread", "sleep"), Ty("JoinHandle", "struct JoinHandle<T>", "Waits for a thread to finish.", "std::thread") },
            ["std::sync"] = new[]
            {
                Member("std::sync", "Arc"), Member("std::sync", "Mutex"), Member("std::sync", "RwLock"),
                Mod("mpsc", "Channels between threads.", "std::sync"), Mod("atomic", "Atomic integers and booleans.", "std::sync")
            },
            ["std::time"] = Names("std::time", "Duration", "Instant"),
            ["std::cmp"] = new[] { Member("std::cmp", "max"), Member("std::cmp", "min"), Member("std::cmp", "Ordering"), Ty("Reverse", "struct Reverse<T>", "Reverses an ordering (for min-heaps).", "std::cmp") },
            ["std::mem"] = Names("std::mem", "swap", "replace", "take", "drop"),
            ["std::env"] = Names("std::env", "args", "var"),
            ["std::process"] = new[] { Member("std::process", "exit"), Ty("Command", "struct Command", "Runs another program.", "std::process") },
            ["std::rc"] = new[] { Member("std::rc", "Rc"), Ty("Weak", "struct Weak<T>", "A non-owning pointer to an Rc value.", "std::rc") },
            ["std::cell"] = Names("std::cell", "Cell", "RefCell"),
            ["std::fmt"] = new[]
            {
                Member("std::fmt", "Display"), Member("std::fmt", "Debug"),
                Ty("Formatter", "struct Formatter", "Where a fmt implementation writes.", "std::fmt"), Ty("Result", "type Result = Result<(), fmt::Error>", "The result of formatting.", "std::fmt")
            },
            ["std::iter"] = new[]
            {
                Fn("repeat", "fn repeat<T: Clone>(elt: T) -> Repeat<T>", "Repeats a value forever.", "std::iter"), Fn("once", "fn once<T>(value: T) -> Once<T>", "An iterator of one value.", "std::iter"),
                Fn("from_fn", "fn from_fn<T, F: FnMut() -> Option<T>>(f: F) -> FromFn<F>", "An iterator from a closure.", "std::iter"),
                Fn("successors", "fn successors<T, F>(first: Option<T>, succ: F) -> Successors<T, F>", "Each value from the one before.", "std::iter")
            },
        };

        foreach (var type in new[] { "String", "Vec", "HashMap", "HashSet", "BTreeMap", "VecDeque", "Box", "Rc", "Arc", "Mutex", "RefCell", "Instant", "Duration", "char", "Default" })
        {
            var members = Functions.Where(f => f.Owner == type).Select(f => f with { Name = f.Name[(type.Length + 2)..] }).ToArray();
            if (members.Length > 0) paths[type] = members;
        }

        foreach (var number in new[] { "i32", "i64", "u32", "u64", "usize", "f64" })
        {
            var constants = Functions.Where(f => f.Owner == number && f.Kind == RustSymbolKind.Constant).Select(f => f with { Name = f.Name[(number.Length + 2)..] }).ToList();
            if (constants.Count > 0) paths[number] = constants;
        }

        paths["Option"] = new[] { new RustSymbol("Some", "Some(value)", "An Option that holds a value.", RustSymbolKind.Variant, "Option"), new RustSymbol("None", "None", "An Option that holds nothing.", RustSymbolKind.Variant, "Option") };
        paths["Result"] = new[] { new RustSymbol("Ok", "Ok(value)", "A successful Result.", RustSymbolKind.Variant, "Result"), new RustSymbol("Err", "Err(error)", "A failed Result.", RustSymbolKind.Variant, "Result") };
        paths["Ordering"] = new[]
        {
            new RustSymbol("Less", "Ordering::Less", "The left value is smaller.", RustSymbolKind.Variant, "Ordering"),
            new RustSymbol("Equal", "Ordering::Equal", "The values are equal.", RustSymbolKind.Variant, "Ordering"),
            new RustSymbol("Greater", "Ordering::Greater", "The left value is larger.", RustSymbolKind.Variant, "Ordering")
        };
        return paths;
    }

    /// <summary>Traits <c>#[derive(…)]</c> can implement.</summary>
    public static IReadOnlyList<string> Derivable { get; } = ["Debug", "Clone", "Copy", "PartialEq", "Eq", "PartialOrd", "Ord", "Hash", "Default"];
}
