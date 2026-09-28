using System.Collections.Frozen;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Produces hover (Quick Info) documentation for C++ symbols directly from a curated static dictionary of the
/// standard library. No compiler or language server is required, so it works even before the user has installed one.
/// The symbol at the caret is looked up by the plain identifier it resolves to (sans template args, leading
/// qualifiers, or trailing punctuation) after stripping namespace prefixes like <c>std::</c>.
/// </summary>
public static class CppQuickInfoProvider
{
    /// <summary>A hover result for a single C++ symbol.</summary>
    /// <param name="Signature">The canonical C++ declaration, used in the top bold line of the tooltip.</param>
    /// <param name="Summary">One or two sentence description, shown in the body of the tooltip.</param>
    /// <param name="Header">The standard header that declares this symbol (e.g. <c>&lt;vector&gt;</c>), or null.</param>
    /// <param name="Since">The C++ standard that introduced this feature (e.g. <c>C++11</c>), or null.</param>
    public sealed record QuickInfo(string Signature, string Summary, string? Header = null, string? Since = null);

    private static readonly FrozenDictionary<string, QuickInfo> Entries = BuildEntries().ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Looks up the hover documentation for <paramref name="symbol"/> (the raw identifier at the caret).
    /// The lookup is tried first as-is, then after stripping a leading <c>std::</c> prefix, then lower-cased.
    /// Returns null when the symbol is unknown.
    /// </summary>
    public static QuickInfo? Lookup(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return null;

        // Exact match first (handles custom user names that happen to collide).
        if (Entries.TryGetValue(symbol, out var info)) return info;

        // Strip std:: prefix — users often write std::vector but the word under the caret is just "vector".
        var bare = symbol.StartsWith("std::", StringComparison.Ordinal) ? symbol[5..] : symbol;
        if (Entries.TryGetValue(bare, out info)) return info;

        // Case-insensitive fallback for keywords like NULL, TRUE, FALSE typed in caps.
        var lower = bare.ToLowerInvariant();
        return Entries.TryGetValue(lower, out info) ? info : null;
    }

    /// <summary>
    /// Extracts the word token surrounding <paramref name="position"/> within <paramref name="code"/>.
    /// Template argument lists, pointer/reference qualifiers, and namespace prefixes are stripped after
    /// extraction so the raw name can be fed directly to <see cref="Lookup"/>.
    /// </summary>
    public static string? ExtractSymbol(string code, int position)
    {
        if (string.IsNullOrEmpty(code) || position < 0 || position > code.Length) return null;
        if (position == code.Length) position--;

        // Walk left to find word start (C++ identifiers: letters, digits, underscores).
        int start = position;
        while (start > 0 && IsIdentChar(code[start - 1])) start--;

        // Walk right to find word end.
        int end = position;
        while (end < code.Length && IsIdentChar(code[end])) end++;

        if (start == end) return null;

        var word = code[start..end];

        // Include a leading std:: or fry:: qualifier so Lookup can strip it.
        if (start >= 5 && code[(start - 5)..start] == "std::") word = "std::" + word;
        else if (start >= 4 && code[(start - 4)..start] == "fry::") word = "fry::" + word;

        return word;
    }

    private static bool IsIdentChar(char c) =>
        char.IsLetterOrDigit(c) || c == '_';

    // ─────────────────────────────────────────────────────────────────────
    // Curated reference dictionary — sorted by category for maintainability.
    // ─────────────────────────────────────────────────────────────────────
    private static Dictionary<string, QuickInfo> BuildEntries()
    {
        var d = new Dictionary<string, QuickInfo>(StringComparer.Ordinal);

        // ── Control flow keywords ─────────────────────────────────────────
        d["if"] = new("if (condition) { ... }", "Conditional branch. Executes the body when condition is truthy.");
        d["else"] = new("else { ... }", "Alternative branch of an if statement. Runs when the condition is false.");
        d["for"] = new("for (init; condition; increment) { ... }", "General-purpose loop. Executes init once, then repeats body while condition holds.");
        d["while"] = new("while (condition) { ... }", "Loop that repeats its body while condition holds.");
        d["do"] = new("do { ... } while (condition);", "Loop that always executes its body at least once, then repeats while condition holds.");
        d["switch"] = new("switch (expr) { case v: ... }", "Multi-branch dispatch on an integral or enum expression.");
        d["case"] = new("case value:", "Label inside a switch statement matching a specific constant.");
        d["break"] = new("break;", "Exits the nearest enclosing loop or switch statement.");
        d["continue"] = new("continue;", "Skips the rest of the current loop iteration and tests the condition again.");
        d["return"] = new("return expression;", "Exits the current function, optionally returning a value to the caller.");
        d["goto"] = new("goto label;", "Unconditional jump to the named label. Avoid in modern C++.");
        d["try"] = new("try { ... } catch (Type e) { ... }", "Marks a block for exception handling.");
        d["catch"] = new("catch (ExceptionType e) { ... }", "Handles exceptions thrown in an associated try block.");
        d["throw"] = new("throw expression;", "Raises an exception. The exception unwinds the stack until it's caught.");

        // ── Storage / class keywords ──────────────────────────────────────
        d["class"] = new("class Name { public: ... };", "Defines a user-defined type with data and member functions. Members default to private.");
        d["struct"] = new("struct Name { ... };", "Like class but members default to public. Idiomatic for plain data aggregates.");
        d["union"] = new("union Name { ... };", "All members share the same memory region. Size equals the largest member.");
        d["enum"] = new("enum Name { A, B, C };", "Defines a set of named integral constants. Prefer enum class for scoped enums.");
        d["namespace"] = new("namespace Name { ... }", "Groups declarations to avoid name collisions.");
        d["using"] = new("using Name = Type;", "Type alias (preferred over typedef). Also introduces namespace members into current scope.");
        d["template"] = new("template<typename T>", "Parameterises a class or function over one or more types or values.");
        d["typename"] = new("typename T", "Introduces a type parameter in a template or disambiguates a dependent type name.");
        d["concept"] = new("concept Name = constraints;", "A named set of requirements on template parameters.", null, "C++20");
        d["requires"] = new("requires (expr)", "Attaches a constraint to a template or verifies that an expression is valid.", null, "C++20");
        d["constexpr"] = new("constexpr Type name = expr;", "Evaluated at compile time when possible. Implies const.", null, "C++11");
        d["consteval"] = new("consteval ReturnType name(...);", "Always evaluated at compile time. Never produces runtime code.", null, "C++20");
        d["constinit"] = new("constinit Type name = expr;", "Ensures a variable has static/thread-local storage duration and is constant-initialised.", null, "C++20");
        d["static"] = new("static", "Inside a class: one copy shared by all instances. At namespace/file scope: internal linkage.");
        d["inline"] = new("inline", "Suggests inlining and allows multiple definitions across translation units.");
        d["virtual"] = new("virtual ReturnType method();", "Marks a method as dynamically dispatchable through a base-class pointer.");
        d["override"] = new("ReturnType method() override;", "Verifies that the method overrides a virtual method in a base class.", null, "C++11");
        d["final"] = new("class Name final { ... };", "Prevents further inheritance of a class or overriding of a virtual method.", null, "C++11");
        d["explicit"] = new("explicit Constructor(...);", "Prevents implicit conversions from this constructor or conversion operator.");
        d["friend"] = new("friend class Name;", "Grants another class or function access to private and protected members.");
        d["mutable"] = new("mutable Type member;", "Allows modification of this member even inside const member functions.");
        d["volatile"] = new("volatile Type name;", "Prevents the compiler from caching the value; typically used for hardware registers.");
        d["const"] = new("const Type name;", "The value may not be modified after initialisation.");
        d["public"] = new("public:", "Access specifier: members are accessible from anywhere.");
        d["protected"] = new("protected:", "Access specifier: members are accessible from the class and its subclasses.");
        d["private"] = new("private:", "Access specifier: members are only accessible from within the class itself.");
        d["operator"] = new("ReturnType operator+(const T& rhs);", "Defines an overloaded operator for a user-defined type.");
        d["new"] = new("new Type(args)", "Allocates memory on the heap and constructs an object. Remember to delete or use smart pointers.");
        d["delete"] = new("delete pointer;", "Frees heap memory allocated by new and runs the destructor.");
        d["sizeof"] = new("sizeof(type_or_expr)", "Yields the size in bytes of a type or expression at compile time.");
        d["alignof"] = new("alignof(Type)", "Yields the required memory alignment of a type in bytes.", null, "C++11");
        d["decltype"] = new("decltype(expression)", "Deduces the type of an expression without evaluating it.", null, "C++11");
        d["typeid"] = new("typeid(type_or_expr)", "Returns a std::type_info object describing the type at run time.");
        d["noexcept"] = new("noexcept(expr)", "Specifies that a function does not throw, or tests whether an expression can throw.", null, "C++11");
        d["static_cast"] = new("static_cast<Type>(expr)", "Performs a safe, checked compile-time cast.");
        d["dynamic_cast"] = new("dynamic_cast<Type*>(ptr)", "Performs a safe run-time downcast; returns nullptr on failure for pointer casts.");
        d["const_cast"] = new("const_cast<Type>(expr)", "Removes or adds const/volatile qualifiers.");
        d["reinterpret_cast"] = new("reinterpret_cast<Type>(expr)", "Low-level bit-pattern reinterpretation. Use sparingly.");
        d["nullptr"] = new("nullptr", "Null pointer constant; type-safe replacement for NULL and 0.", null, "C++11");
        d["true"] = new("true", "Boolean literal representing logical truth.");
        d["false"] = new("false", "Boolean literal representing logical falsity.");
        d["this"] = new("this", "Pointer to the object on which a non-static member function is being called.");
        d["co_await"] = new("co_await expr", "Suspends a coroutine until the awaitable completes.", null, "C++20");
        d["co_yield"] = new("co_yield value", "Suspends a coroutine and yields a value to the caller.", null, "C++20");
        d["co_return"] = new("co_return value;", "Completes a coroutine, optionally returning a final value.", null, "C++20");
        d["export"] = new("export module Name;", "Exports a named module or its declarations.", null, "C++20");
        d["import"] = new("import Name;", "Imports a named module.", null, "C++20");
        d["module"] = new("module Name;", "Declares a named module unit.", null, "C++20");

        // ── Built-in types ────────────────────────────────────────────────
        d["void"] = new("void", "Represents an absence of type; used as a return type for functions that return nothing.");
        d["bool"] = new("bool", "Boolean type. Values: true or false.");
        d["char"] = new("char", "Character type, typically 1 byte. Signedness is implementation-defined.");
        d["char8_t"] = new("char8_t", "UTF-8 character type.", null, "C++20");
        d["char16_t"] = new("char16_t", "UTF-16 character type.", null, "C++11");
        d["char32_t"] = new("char32_t", "UTF-32 character type.", null, "C++11");
        d["wchar_t"] = new("wchar_t", "Wide character type. Size is implementation-defined (2 or 4 bytes).");
        d["short"] = new("short", "Typically 16-bit signed integer.");
        d["int"] = new("int", "Signed integer, at least 16 bits (usually 32 bits).");
        d["long"] = new("long", "Signed integer, at least 32 bits.");
        d["float"] = new("float", "Single-precision IEEE 754 floating point, 32 bits.");
        d["double"] = new("double", "Double-precision IEEE 754 floating point, 64 bits.");
        d["signed"] = new("signed", "Explicitly signed integral modifier.");
        d["unsigned"] = new("unsigned", "Unsigned integral modifier; cannot hold negative values.");
        d["auto"] = new("auto", "Type is deduced from the initialiser expression. Prefer over verbose type names.", null, "C++11");
        d["size_t"] = new("size_t", "Unsigned integer type large enough to represent any object size in bytes.", "<cstddef>");
        d["int8_t"] = new("int8_t", "Exactly 8-bit signed integer.", "<cstdint>", "C++11");
        d["int16_t"] = new("int16_t", "Exactly 16-bit signed integer.", "<cstdint>", "C++11");
        d["int32_t"] = new("int32_t", "Exactly 32-bit signed integer.", "<cstdint>", "C++11");
        d["int64_t"] = new("int64_t", "Exactly 64-bit signed integer.", "<cstdint>", "C++11");
        d["uint8_t"] = new("uint8_t", "Exactly 8-bit unsigned integer.", "<cstdint>", "C++11");
        d["uint16_t"] = new("uint16_t", "Exactly 16-bit unsigned integer.", "<cstdint>", "C++11");
        d["uint32_t"] = new("uint32_t", "Exactly 32-bit unsigned integer.", "<cstdint>", "C++11");
        d["uint64_t"] = new("uint64_t", "Exactly 64-bit unsigned integer.", "<cstdint>", "C++11");

        // ── Containers ────────────────────────────────────────────────────
        d["vector"] = new("std::vector<T>", "Dynamic array. Random access O(1), amortised O(1) push_back. Elements contiguous in memory.", "<vector>", "C++98");
        d["string"] = new("std::string", "Owning sequence of char with O(1) amortised append and many manipulation methods.", "<string>", "C++98");
        d["string_view"] = new("std::string_view", "Non-owning view into a character sequence. Zero-copy; does not own the data.", "<string_view>", "C++17");
        d["array"] = new("std::array<T, N>", "Fixed-size array with the interface of std::vector. Size must be a compile-time constant.", "<array>", "C++11");
        d["deque"] = new("std::deque<T>", "Double-ended queue. O(1) push/pop at both ends; slightly slower random access than vector.", "<deque>", "C++98");
        d["list"] = new("std::list<T>", "Doubly-linked list. O(1) insert/erase anywhere; no random access.", "<list>", "C++98");
        d["forward_list"] = new("std::forward_list<T>", "Singly-linked list. Lower overhead than list; O(1) insert after a known iterator.", "<forward_list>", "C++11");
        d["map"] = new("std::map<K, V>", "Ordered key-value store (red-black tree). O(log n) lookup, insertion and deletion.", "<map>", "C++98");
        d["multimap"] = new("std::multimap<K, V>", "Like map but allows duplicate keys.", "<map>", "C++98");
        d["set"] = new("std::set<T>", "Ordered unique-key container (red-black tree). O(log n) operations.", "<set>", "C++98");
        d["multiset"] = new("std::multiset<T>", "Like set but allows duplicate keys.", "<set>", "C++98");
        d["unordered_map"] = new("std::unordered_map<K, V>", "Hash table key-value store. Average O(1) lookup, insertion and deletion.", "<unordered_map>", "C++11");
        d["unordered_set"] = new("std::unordered_set<T>", "Hash table of unique keys. Average O(1) operations.", "<unordered_set>", "C++11");
        d["unordered_multimap"] = new("std::unordered_multimap<K, V>", "Hash map that allows duplicate keys.", "<unordered_map>", "C++11");
        d["unordered_multiset"] = new("std::unordered_multiset<T>", "Hash set that allows duplicate keys.", "<unordered_set>", "C++11");
        d["stack"] = new("std::stack<T>", "LIFO adapter over deque (or vector/list). push(), pop(), top().", "<stack>", "C++98");
        d["queue"] = new("std::queue<T>", "FIFO adapter over deque. push(), pop(), front(), back().", "<queue>", "C++98");
        d["priority_queue"] = new("std::priority_queue<T>", "Max-heap by default. push(), pop(), top(). Pass greater<T> for a min-heap.", "<queue>", "C++98");
        d["pair"] = new("std::pair<T1, T2>", "Holds two heterogeneous values (.first, .second). make_pair() is a convenient factory.", "<utility>", "C++98");
        d["tuple"] = new("std::tuple<Types...>", "Holds a fixed number of heterogeneous values. Access with std::get<N>(t).", "<tuple>", "C++11");
        d["span"] = new("std::span<T, Extent>", "Non-owning view over a contiguous sequence of elements. Like string_view but for any type.", "<span>", "C++20");
        d["bitset"] = new("std::bitset<N>", "Fixed-size set of bits. O(1) bit operations with compact storage.", "<bitset>", "C++98");

        // ── Smart pointers ────────────────────────────────────────────────
        d["unique_ptr"] = new("std::unique_ptr<T>", "Sole-ownership smart pointer. Automatically deletes the managed object when destroyed.", "<memory>", "C++11");
        d["shared_ptr"] = new("std::shared_ptr<T>", "Reference-counted shared ownership. The object is deleted when the last shared_ptr is destroyed.", "<memory>", "C++11");
        d["weak_ptr"] = new("std::weak_ptr<T>", "Non-owning observer of a shared_ptr-managed object. Must lock() before use.", "<memory>", "C++11");
        d["make_unique"] = new("std::make_unique<T>(args...)", "Constructs a T and wraps it in unique_ptr. Prefer over new for exception safety.", "<memory>", "C++14");
        d["make_shared"] = new("std::make_shared<T>(args...)", "Constructs a T and wraps it in shared_ptr in a single allocation.", "<memory>", "C++11");

        // ── Optional / variant / any ──────────────────────────────────────
        d["optional"] = new("std::optional<T>", "Holds a value or nothing (nullopt). Avoids sentinel values and pointer semantics.", "<optional>", "C++17");
        d["variant"] = new("std::variant<Types...>", "Type-safe discriminated union. Holds exactly one of the listed types at a time.", "<variant>", "C++17");
        d["any"] = new("std::any", "Type-erased container for a single value of any type.", "<any>", "C++17");
        d["nullopt"] = new("std::nullopt", "Represents an empty optional.", "<optional>", "C++17");
        d["monostate"] = new("std::monostate", "Unit type usable as the first alternative of a variant to make it default-constructible.", "<variant>", "C++17");

        // ── I/O streams ───────────────────────────────────────────────────
        d["cout"] = new("std::cout", "Standard output stream. Use << to write to the console.", "<iostream>");
        d["cin"] = new("std::cin", "Standard input stream. Use >> to read from the console.", "<iostream>");
        d["cerr"] = new("std::cerr", "Standard error stream, unbuffered. Use for diagnostic output.", "<iostream>");
        d["clog"] = new("std::clog", "Standard error stream, buffered. Use for logging.", "<iostream>");
        d["endl"] = new("std::endl", "Flushes the output buffer and inserts a newline. Prefer '\\n' when flushing isn't needed.", "<ostream>");
        d["flush"] = new("std::flush", "Flushes the output buffer without inserting a newline.", "<ostream>");
        d["setw"] = new("std::setw(n)", "Sets the field width for the next output operation.", "<iomanip>");
        d["setprecision"] = new("std::setprecision(n)", "Sets the precision of floating-point output.", "<iomanip>");
        d["fixed"] = new("std::fixed", "Output floating-point numbers in fixed-point notation.", "<iomanip>");
        d["scientific"] = new("std::scientific", "Output floating-point numbers in scientific notation.", "<iomanip>");
        d["hex"] = new("std::hex", "Output integers in hexadecimal.", "<iomanip>");
        d["dec"] = new("std::dec", "Output integers in decimal (default).", "<iomanip>");
        d["oct"] = new("std::oct", "Output integers in octal.", "<iomanip>");
        d["ostringstream"] = new("std::ostringstream", "In-memory output stream. Call .str() to get the accumulated string.", "<sstream>");
        d["istringstream"] = new("std::istringstream", "In-memory input stream for parsing strings.", "<sstream>");
        d["stringstream"] = new("std::stringstream", "In-memory bidirectional string stream.", "<sstream>");
        d["ofstream"] = new("std::ofstream", "Output file stream.", "<fstream>");
        d["ifstream"] = new("std::ifstream", "Input file stream.", "<fstream>");
        d["fstream"] = new("std::fstream", "Bidirectional file stream.", "<fstream>");

        // ── Algorithms ────────────────────────────────────────────────────
        d["sort"] = new("std::sort(first, last, comp)", "Sorts [first, last) in-place. O(n log n). Unstable; use stable_sort when order of equals matters.", "<algorithm>");
        d["stable_sort"] = new("std::stable_sort(first, last, comp)", "Like sort but preserves the relative order of equal elements.", "<algorithm>");
        d["find"] = new("std::find(first, last, value)", "Returns an iterator to the first element equal to value, or last if not found.", "<algorithm>");
        d["find_if"] = new("std::find_if(first, last, pred)", "Returns an iterator to the first element for which pred returns true.", "<algorithm>");
        d["count"] = new("std::count(first, last, value)", "Counts how many elements in [first, last) equal value.", "<algorithm>");
        d["count_if"] = new("std::count_if(first, last, pred)", "Counts elements for which pred returns true.", "<algorithm>");
        d["for_each"] = new("std::for_each(first, last, f)", "Applies f to every element in [first, last).", "<algorithm>");
        d["transform"] = new("std::transform(first, last, out, f)", "Applies f to every element and writes results to out.", "<algorithm>");
        d["copy"] = new("std::copy(first, last, out)", "Copies elements from [first, last) to the range starting at out.", "<algorithm>");
        d["fill"] = new("std::fill(first, last, value)", "Assigns value to every element in [first, last).", "<algorithm>");
        d["replace"] = new("std::replace(first, last, old_val, new_val)", "Replaces every occurrence of old_val with new_val.", "<algorithm>");
        d["remove"] = new("std::remove(first, last, value)", "Moves elements not equal to value to the front; returns new logical end (erase-remove idiom).", "<algorithm>");
        d["remove_if"] = new("std::remove_if(first, last, pred)", "Like remove but removes elements satisfying pred.", "<algorithm>");
        d["unique"] = new("std::unique(first, last)", "Removes consecutive duplicate elements; returns new logical end.", "<algorithm>");
        d["reverse"] = new("std::reverse(first, last)", "Reverses the order of elements in [first, last) in-place.", "<algorithm>");
        d["rotate"] = new("std::rotate(first, n_first, last)", "Rotates elements so that n_first becomes the new first element.", "<algorithm>");
        d["partition"] = new("std::partition(first, last, pred)", "Reorders so elements satisfying pred come before those that don't.", "<algorithm>");
        d["accumulate"] = new("std::accumulate(first, last, init, op)", "Computes the left-fold of the range using op (default +).", "<numeric>");
        d["reduce"] = new("std::reduce(first, last, init, op)", "Like accumulate but can run in parallel; order of operations is unspecified.", "<numeric>", "C++17");
        d["iota"] = new("std::iota(first, last, value)", "Fills [first, last) with value, value+1, value+2, …", "<numeric>", "C++11");
        d["max"] = new("std::max(a, b, comp)", "Returns the larger of a and b. Three-argument form uses comp.", "<algorithm>");
        d["min"] = new("std::min(a, b, comp)", "Returns the smaller of a and b.", "<algorithm>");
        d["clamp"] = new("std::clamp(v, lo, hi)", "Returns v clamped to the interval [lo, hi].", "<algorithm>", "C++17");
        d["max_element"] = new("std::max_element(first, last)", "Returns an iterator to the maximum element in [first, last).", "<algorithm>");
        d["min_element"] = new("std::min_element(first, last)", "Returns an iterator to the minimum element.", "<algorithm>");
        d["lower_bound"] = new("std::lower_bound(first, last, value)", "Binary search: first position where value could be inserted to keep sort order.", "<algorithm>");
        d["upper_bound"] = new("std::upper_bound(first, last, value)", "Binary search: first position after all elements equal to value.", "<algorithm>");
        d["binary_search"] = new("std::binary_search(first, last, value)", "Returns true if value is in the sorted range [first, last).", "<algorithm>");
        d["next_permutation"] = new("std::next_permutation(first, last)", "Transforms [first, last) into the next lexicographic permutation. Returns false when wrapped.", "<algorithm>");
        d["prev_permutation"] = new("std::prev_permutation(first, last)", "Transforms into the previous permutation.", "<algorithm>");
        d["merge"] = new("std::merge(f1, l1, f2, l2, out)", "Merges two sorted ranges into a sorted output range.", "<algorithm>");
        d["inplace_merge"] = new("std::inplace_merge(first, middle, last)", "Merges two consecutive sorted ranges in-place.", "<algorithm>");
        d["set_union"] = new("std::set_union(f1, l1, f2, l2, out)", "Computes the union of two sorted ranges.", "<algorithm>");
        d["set_intersection"] = new("std::set_intersection(f1, l1, f2, l2, out)", "Computes the intersection of two sorted ranges.", "<algorithm>");
        d["set_difference"] = new("std::set_difference(f1, l1, f2, l2, out)", "Computes elements in range 1 not in range 2.", "<algorithm>");
        d["is_sorted"] = new("std::is_sorted(first, last)", "Returns true if [first, last) is in non-descending order.", "<algorithm>", "C++11");
        d["ranges::sort"] = new("std::ranges::sort(range, comp)", "C++20 ranges version of sort. Accepts a range directly (no first/last pair).", "<algorithm>", "C++20");

        // ── Iterators ─────────────────────────────────────────────────────
        d["begin"] = new("std::begin(container)", "Returns an iterator to the first element.", "<iterator>", "C++11");
        d["end"] = new("std::end(container)", "Returns an iterator one past the last element.", "<iterator>", "C++11");
        d["rbegin"] = new("std::rbegin(container)", "Returns a reverse iterator to the last element.", "<iterator>", "C++14");
        d["rend"] = new("std::rend(container)", "Returns a reverse iterator before the first element.", "<iterator>", "C++14");
        d["next"] = new("std::next(it, n)", "Returns an iterator advanced n positions from it (default n=1).", "<iterator>", "C++11");
        d["prev"] = new("std::prev(it, n)", "Returns an iterator retreated n positions from it (default n=1).", "<iterator>", "C++11");
        d["distance"] = new("std::distance(first, last)", "Returns the number of steps from first to last.", "<iterator>");
        d["back_inserter"] = new("std::back_inserter(container)", "Returns an output iterator that calls push_back() on the container.", "<iterator>");
        d["front_inserter"] = new("std::front_inserter(container)", "Returns an output iterator that calls push_front() on the container.", "<iterator>");
        d["inserter"] = new("std::inserter(container, it)", "Returns an output iterator that calls insert(it, value) on the container.", "<iterator>");

        // ── Threading ─────────────────────────────────────────────────────
        d["thread"] = new("std::thread t(func, args...)", "Represents a thread of execution. join() waits for it; detach() releases ownership.", "<thread>", "C++11");
        d["mutex"] = new("std::mutex", "Mutual exclusion primitive. lock() and unlock() protect shared data.", "<mutex>", "C++11");
        d["lock_guard"] = new("std::lock_guard<Mutex> lk(m)", "RAII wrapper that locks a mutex on construction and unlocks on destruction.", "<mutex>", "C++11");
        d["unique_lock"] = new("std::unique_lock<Mutex> lk(m)", "Like lock_guard but supports deferred, timed and recursive locking.", "<mutex>", "C++11");
        d["condition_variable"] = new("std::condition_variable", "Used with unique_lock to block until a condition holds.", "<condition_variable>", "C++11");
        d["atomic"] = new("std::atomic<T>", "Atomic operations on T without locks. Use for simple counters and flags.", "<atomic>", "C++11");
        d["future"] = new("std::future<T>", "Represents a value that will be available asynchronously.", "<future>", "C++11");
        d["promise"] = new("std::promise<T>", "Producer side of the future/promise pair. Call set_value() to fulfil the future.", "<future>", "C++11");
        d["async"] = new("std::async(policy, func, args...)", "Runs func asynchronously and returns a future for its result.", "<future>", "C++11");

        // ── Type traits ───────────────────────────────────────────────────
        d["is_same"] = new("std::is_same<T, U>::value", "True if T and U are the same type.", "<type_traits>", "C++11");
        d["is_arithmetic"] = new("std::is_arithmetic<T>::value", "True if T is an integral or floating-point type.", "<type_traits>", "C++11");
        d["is_integral"] = new("std::is_integral<T>::value", "True if T is an integer type.", "<type_traits>", "C++11");
        d["is_floating_point"] = new("std::is_floating_point<T>::value", "True if T is float, double or long double.", "<type_traits>", "C++11");
        d["is_pointer"] = new("std::is_pointer<T>::value", "True if T is a pointer type.", "<type_traits>", "C++11");
        d["is_reference"] = new("std::is_reference<T>::value", "True if T is an lvalue or rvalue reference.", "<type_traits>", "C++11");
        d["enable_if"] = new("std::enable_if<condition, T>", "SFINAE utility: defines type T only when condition is true.", "<type_traits>", "C++11");
        d["decay"] = new("std::decay<T>", "Applies array-to-pointer, function-to-pointer and cv-qualifier removal.", "<type_traits>", "C++11");
        d["remove_cv"] = new("std::remove_cv<T>", "Removes const and volatile from T.", "<type_traits>", "C++11");
        d["conditional"] = new("std::conditional<B, T, F>", "Selects T when B is true, F otherwise.", "<type_traits>", "C++11");
        d["invoke_result"] = new("std::invoke_result<F, Args...>", "Deduces the return type of invoking F with Args…", "<type_traits>", "C++17");

        // ── String utilities ──────────────────────────────────────────────
        d["stoi"] = new("std::stoi(str, pos, base)", "Converts a string to int. Throws std::invalid_argument on failure.", "<string>");
        d["stol"] = new("std::stol(str, pos, base)", "Converts a string to long.", "<string>");
        d["stoll"] = new("std::stoll(str, pos, base)", "Converts a string to long long.", "<string>");
        d["stof"] = new("std::stof(str, pos)", "Converts a string to float.", "<string>");
        d["stod"] = new("std::stod(str, pos)", "Converts a string to double.", "<string>");
        d["to_string"] = new("std::to_string(value)", "Converts an arithmetic value to std::string.", "<string>", "C++11");
        d["getline"] = new("std::getline(stream, str, delim)", "Reads until delim (default '\\n') from stream into str.", "<string>");

        // ── Math ──────────────────────────────────────────────────────────
        d["abs"] = new("std::abs(x)", "Absolute value. Works for integral and floating-point types.", "<cmath> / <cstdlib>");
        d["pow"] = new("std::pow(base, exp)", "base raised to the power exp.", "<cmath>");
        d["sqrt"] = new("std::sqrt(x)", "Square root of x.", "<cmath>");
        d["ceil"] = new("std::ceil(x)", "Smallest integer not less than x.", "<cmath>");
        d["floor"] = new("std::floor(x)", "Largest integer not greater than x.", "<cmath>");
        d["round"] = new("std::round(x)", "Rounds x to the nearest integer, halfway away from zero.", "<cmath>", "C++11");
        d["log"] = new("std::log(x)", "Natural logarithm of x.", "<cmath>");
        d["log2"] = new("std::log2(x)", "Base-2 logarithm of x.", "<cmath>", "C++11");
        d["log10"] = new("std::log10(x)", "Base-10 logarithm of x.", "<cmath>");
        d["exp"] = new("std::exp(x)", "e raised to the power x.", "<cmath>");
        d["fabs"] = new("std::fabs(x)", "Absolute value for floating-point types.", "<cmath>");
        d["fmod"] = new("std::fmod(x, y)", "Floating-point remainder of x/y.", "<cmath>");
        d["hypot"] = new("std::hypot(x, y)", "sqrt(x² + y²) without overflow/underflow.", "<cmath>", "C++11");
        d["gcd"] = new("std::gcd(a, b)", "Greatest common divisor of a and b.", "<numeric>", "C++17");
        d["lcm"] = new("std::lcm(a, b)", "Least common multiple of a and b.", "<numeric>", "C++17");

        // ── Random ────────────────────────────────────────────────────────
        d["random_device"] = new("std::random_device", "Non-deterministic random number engine (seed source).", "<random>", "C++11");
        d["mt19937"] = new("std::mt19937", "Mersenne Twister engine, seeded with a 32-bit value. Fast and high quality.", "<random>", "C++11");
        d["mt19937_64"] = new("std::mt19937_64", "64-bit Mersenne Twister engine.", "<random>", "C++11");
        d["uniform_int_distribution"] = new("std::uniform_int_distribution<T>(a, b)", "Produces uniformly distributed integers in [a, b].", "<random>", "C++11");
        d["uniform_real_distribution"] = new("std::uniform_real_distribution<T>(a, b)", "Produces uniformly distributed reals in [a, b).", "<random>", "C++11");
        d["shuffle"] = new("std::shuffle(first, last, rng)", "Randomly permutes [first, last) using rng as the random engine.", "<algorithm>", "C++11");

        // ── C++ 20 ranges & views ─────────────────────────────────────────
        d["views::filter"] = new("std::views::filter(pred)", "Range adaptor that filters elements satisfying pred.", "<ranges>", "C++20");
        d["views::transform"] = new("std::views::transform(f)", "Range adaptor that lazily applies f to each element.", "<ranges>", "C++20");
        d["views::take"] = new("std::views::take(n)", "Range adaptor that takes the first n elements.", "<ranges>", "C++20");
        d["views::drop"] = new("std::views::drop(n)", "Range adaptor that skips the first n elements.", "<ranges>", "C++20");
        d["views::reverse"] = new("std::views::reverse", "Range adaptor that reverses element order.", "<ranges>", "C++20");
        d["views::iota"] = new("std::views::iota(start, end)", "Lazy range of integers [start, end).", "<ranges>", "C++20");

        // ── Preprocessor / compiler keywords ─────────────────────────────
        d["include"] = new("#include <header>", "Textually includes a header file. Use angle brackets for system headers, quotes for local.");
        d["define"] = new("#define NAME value", "Defines a preprocessor macro.");
        d["ifdef"] = new("#ifdef NAME", "Conditionally compiles the following block if NAME is defined.");
        d["ifndef"] = new("#ifndef NAME", "Conditionally compiles the following block if NAME is not defined.");
        d["endif"] = new("#endif", "Closes a conditional compilation block.");
        d["pragma"] = new("#pragma directive", "Implementation-defined compiler instruction. Common: #pragma once prevents multiple inclusion.");
        d["pragma once"] = new("#pragma once", "Ensures the header file is only included once per compilation unit. Widely supported.", null);

        // ── Common fry:: display runtime ──────────────────────────────────
        d["fry::dump"] = new("fry::dump(value, title = \"\")", "Displays value in the Results (.DUMP) panel as an interactive table. Returns the value for chaining.", "<fry/display.hpp>");
        d["fry::table"] = new("fry::table(container, title = \"\")", "Renders a container or nested container as an interactive table in the Results panel.", "<fry/display.hpp>");
        d["fry::html"] = new("fry::html(html_string)", "Renders arbitrary HTML in the Results panel.", "<fry/display.hpp>");
        d["fry::image"] = new("fry::image(path_or_base64, format = \"PNG\")", "Displays an image (file path or base64 data) in the Results panel.", "<fry/display.hpp>");
        d["dump"] = new("fry::dump(value, title = \"\")", "Displays value in the Results (.DUMP) panel. Include <fry/display.hpp>.", "<fry/display.hpp>");
        d["table"] = new("fry::table(container, title = \"\")", "Renders a container as an interactive table. Include <fry/display.hpp>.", "<fry/display.hpp>");

        return d;
    }
}
