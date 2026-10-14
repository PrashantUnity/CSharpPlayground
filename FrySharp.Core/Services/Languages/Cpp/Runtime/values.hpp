#pragma once
// What a notebook cell does with a value: show it (text, a table, the Object Explorer), keep it for the next cell, share it with another
// language, ask the notebook for a line of input. Staged beside display.hpp as <fry/values.hpp>.
#ifndef FRY_VALUES_HPP_INCLUDED
#define FRY_VALUES_HPP_INCLUDED

#include <fry/display.hpp>

#include <array>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <deque>
#include <list>
#include <string>
#include <string_view>
#include <typeinfo>
#include <unordered_map>
#include <unordered_set>
#include <variant>
#include <limits>
#include <mutex>
#include <cerrno>

namespace fry {

// A struct the cell defines (or a type that wants to be shown with its members) names them by specialising this:
//   template <> struct fry::fields<Person> { static constexpr const char* names[] = {"name", "age"};
//                                            template <class F> static void each(Person& p, F&& f) { f(p.name); f(p.age); }
//                                            template <class F> static void each(const Person& p, F&& f) { f(p.name); f(p.age); } };
// The notebook writes this for the structs a cell defines, from their public data members.
template <class T>
struct fields;

namespace values {

template <class T>
concept registered = requires { fields<std::remove_cv_t<T>>::names; };

// ── The name of a type, as C++ writes it ──────────────────────────────────────────────────────────────────────────

template <class T>
constexpr const char* pretty_function() {
#if defined(_MSC_VER)
    return __FUNCSIG__;
#else
    return __PRETTY_FUNCTION__;
#endif
}

inline void replace_all(std::string& text, const std::string& from, const std::string& to) {
    for (std::size_t at = text.find(from); at != std::string::npos; at = text.find(from, at + to.size())) text.replace(at, from.size(), to);
}

template <class T>
inline std::string type_name() {
    std::string p = pretty_function<T>();
    std::string name;
#if defined(_MSC_VER)
    auto a = p.find("pretty_function<");
    auto b = p.rfind(">(void)");
    if (a != std::string::npos && b != std::string::npos) name = p.substr(a + 16, b - a - 16);
    for (const char* keyword : {"class ", "struct ", "enum ", "union "}) replace_all(name, keyword, "");
#else
    auto a = p.find("T = ");
    auto b = p.rfind(']');
    if (a != std::string::npos && b != std::string::npos && b > a) name = p.substr(a + 4, b - a - 4);
#endif
    if (name.empty()) name = typeid(T).name();
    replace_all(name, "std::__1::", "std::");
    replace_all(name, "std::__cxx11::", "std::");
    replace_all(name, "std::basic_string<char, std::char_traits<char>, std::allocator<char> >", "std::string");
    replace_all(name, "std::basic_string<char>", "std::string");
    replace_all(name, "std::basic_string<char,std::char_traits<char>,std::allocator<char> >", "std::string");
    return name;
}

// ── JSON: a value as a tree, written out and read back ────────────────────────────────────────────────────────────

struct Json {
    enum Kind { Null, Bool, Int, Float, String, Array, Object } kind = Null;
    bool b = false;
    long long i = 0;
    double d = 0;
    std::string s;
    std::vector<Json> items;        // an array's elements, an object's values
    std::vector<std::string> keys;  // an object's keys, alongside its values
    std::string type;               // an object that is a struct, pair or tuple: its type
    bool is_map = false;            // an object that is a map
    std::size_t total = 0;          // an array cut short for showing: how many it had
    bool cut = false;

    static Json make(Kind k) { Json j; j.kind = k; return j; }
    static Json text(std::string v) { Json j = make(String); j.s = std::move(v); return j; }
    static Json integer(long long v) { Json j = make(Int); j.i = v; return j; }
    static Json real(double v) { Json j = make(Float); j.d = v; return j; }
    static Json boolean(bool v) { Json j = make(Bool); j.b = v; return j; }
    void add(std::string key, Json value) { keys.push_back(std::move(key)); items.push_back(std::move(value)); }
    const Json* find(const std::string& key) const {
        for (std::size_t n = 0; n < keys.size(); ++n) if (keys[n] == key) return &items[n];
        return nullptr;
    }
    bool scalar() const { return kind != Array && kind != Object; }

    // For a cell that was handed a value from another language (an object, a mixed list): read it by key or position.
    const Json& operator[](const std::string& key) const { static const Json none; const Json* found = find(key); return found ? *found : none; }
    const Json& operator[](const char* key) const { return (*this)[std::string(key)]; }
    const Json& operator[](std::size_t index) const { static const Json none; return index < items.size() ? items[index] : none; }
    std::size_t size() const { return items.size(); }
    bool is_null() const { return kind == Null; }
    std::string as_string() const { return kind == String ? s : kind == Bool ? (b ? "true" : "false") : kind == Int ? std::to_string(i) : kind == Float ? number_text_of(d) : kind == Null ? std::string() : std::string("<json>"); }
    long long as_int() const { return kind == Int ? i : kind == Float ? static_cast<long long>(d) : kind == Bool ? (b ? 1 : 0) : 0; }
    double as_double() const { return kind == Float ? d : kind == Int ? static_cast<double>(i) : 0.0; }
    bool as_bool() const { return kind == Bool ? b : kind == Int ? i != 0 : false; }

private:
    static std::string number_text_of(double v) {
        char buffer[40];
        std::snprintf(buffer, sizeof buffer, "%.15g", v);
        return buffer;
    }
};

}  // namespace values

// A value handed over from another language that has no C++ type of its own (an object, a mixed list).
using Json = values::Json;

namespace values {

inline std::string number_text(double v) {
    char buffer[40];
    std::snprintf(buffer, sizeof buffer, "%.15g", v);
    std::string out = buffer;
    if (out.find_first_of(".eEn") == std::string::npos) out += ".0";
    return out;
}

inline void write_json(const Json& j, std::string& out) {
    switch (j.kind) {
        case Json::Null: out += "null"; break;
        case Json::Bool: out += j.b ? "true" : "false"; break;
        case Json::Int: out += std::to_string(j.i); break;
        case Json::Float: out += number_text(j.d); break;
        case Json::String: out += detail::escape_json(j.s); break;
        case Json::Array:
            out += '[';
            for (std::size_t n = 0; n < j.items.size(); ++n) {
                if (n > 0) out += ',';
                write_json(j.items[n], out);
            }
            out += ']';
            break;
        case Json::Object:
            out += '{';
            for (std::size_t n = 0; n < j.items.size(); ++n) {
                if (n > 0) out += ',';
                out += detail::escape_json(j.keys[n]);
                out += ':';
                write_json(j.items[n], out);
            }
            out += '}';
            break;
    }
}

inline std::string dump_json(const Json& j) {
    std::string out;
    write_json(j, out);
    return out;
}

class JsonParser {
public:
    explicit JsonParser(std::string_view text) : text_(text) {}

    bool parse(Json& out) {
        skip();
        if (!value(out, 0)) return false;
        skip();
        return pos_ == text_.size();
    }

private:
    std::string_view text_;
    std::size_t pos_ = 0;

    void skip() { while (pos_ < text_.size() && (text_[pos_] == ' ' || text_[pos_] == '\t' || text_[pos_] == '\n' || text_[pos_] == '\r')) ++pos_; }
    bool literal(const char* word) {
        std::size_t n = std::char_traits<char>::length(word);
        if (text_.compare(pos_, n, word) != 0) return false;
        pos_ += n;
        return true;
    }

    bool value(Json& out, int depth) {
        if (depth > 200 || pos_ >= text_.size()) return false;
        char c = text_[pos_];
        if (c == '{') return object(out, depth);
        if (c == '[') return array(out, depth);
        if (c == '"') { out = Json::make(Json::String); return string(out.s); }
        if (c == 't') { out = Json::boolean(true); return literal("true"); }
        if (c == 'f') { out = Json::boolean(false); return literal("false"); }
        if (c == 'n') { out = Json::make(Json::Null); return literal("null"); }
        return number(out);
    }

    bool number(Json& out) {
        std::size_t start = pos_;
        bool real = false;
        if (pos_ < text_.size() && (text_[pos_] == '-' || text_[pos_] == '+')) ++pos_;
        while (pos_ < text_.size() && ((text_[pos_] >= '0' && text_[pos_] <= '9') || text_[pos_] == '.' || text_[pos_] == 'e' || text_[pos_] == 'E' || text_[pos_] == '-' || text_[pos_] == '+')) {
            if (text_[pos_] == '.' || text_[pos_] == 'e' || text_[pos_] == 'E') real = true;
            ++pos_;
        }
        if (pos_ == start) return false;
        std::string token(text_.substr(start, pos_ - start));
        char* end = nullptr;
        if (!real) {
            errno = 0;
            long long v = std::strtoll(token.c_str(), &end, 10);
            if (errno == 0 && end && *end == '\0') { out = Json::integer(v); return true; }
        }
        double d = std::strtod(token.c_str(), &end);
        if (!end || *end != '\0') return false;
        out = Json::real(d);
        return true;
    }

    static void utf8(std::string& out, unsigned code) {
        if (code < 0x80) out += static_cast<char>(code);
        else if (code < 0x800) { out += static_cast<char>(0xC0 | (code >> 6)); out += static_cast<char>(0x80 | (code & 0x3F)); }
        else if (code < 0x10000) { out += static_cast<char>(0xE0 | (code >> 12)); out += static_cast<char>(0x80 | ((code >> 6) & 0x3F)); out += static_cast<char>(0x80 | (code & 0x3F)); }
        else { out += static_cast<char>(0xF0 | (code >> 18)); out += static_cast<char>(0x80 | ((code >> 12) & 0x3F)); out += static_cast<char>(0x80 | ((code >> 6) & 0x3F)); out += static_cast<char>(0x80 | (code & 0x3F)); }
    }

    bool hex4(unsigned& code) {
        if (pos_ + 4 > text_.size()) return false;
        code = 0;
        for (int n = 0; n < 4; ++n) {
            char h = text_[pos_++];
            code <<= 4;
            if (h >= '0' && h <= '9') code |= static_cast<unsigned>(h - '0');
            else if (h >= 'a' && h <= 'f') code |= static_cast<unsigned>(h - 'a' + 10);
            else if (h >= 'A' && h <= 'F') code |= static_cast<unsigned>(h - 'A' + 10);
            else return false;
        }
        return true;
    }

    bool string(std::string& out) {
        ++pos_;  // the opening quote
        while (pos_ < text_.size()) {
            char c = text_[pos_++];
            if (c == '"') return true;
            if (c != '\\') { out += c; continue; }
            if (pos_ >= text_.size()) return false;
            char e = text_[pos_++];
            switch (e) {
                case 'n': out += '\n'; break;
                case 't': out += '\t'; break;
                case 'r': out += '\r'; break;
                case 'b': out += '\b'; break;
                case 'f': out += '\f'; break;
                case 'u': {
                    unsigned code = 0;
                    if (!hex4(code)) return false;
                    if (code >= 0xD800 && code < 0xDC00 && pos_ + 1 < text_.size() && text_[pos_] == '\\' && text_[pos_ + 1] == 'u') {
                        pos_ += 2;
                        unsigned low = 0;
                        if (!hex4(low)) return false;
                        code = 0x10000 + ((code - 0xD800) << 10) + (low - 0xDC00);
                    }
                    utf8(out, code);
                    break;
                }
                default: out += e; break;
            }
        }
        return false;
    }

    bool array(Json& out, int depth) {
        out = Json::make(Json::Array);
        ++pos_;
        skip();
        if (pos_ < text_.size() && text_[pos_] == ']') { ++pos_; return true; }
        while (true) {
            skip();
            Json item;
            if (!value(item, depth + 1)) return false;
            out.items.push_back(std::move(item));
            skip();
            if (pos_ >= text_.size()) return false;
            if (text_[pos_] == ',') { ++pos_; continue; }
            if (text_[pos_] == ']') { ++pos_; return true; }
            return false;
        }
    }

    bool object(Json& out, int depth) {
        out = Json::make(Json::Object);
        ++pos_;
        skip();
        if (pos_ < text_.size() && text_[pos_] == '}') { ++pos_; return true; }
        while (true) {
            skip();
            if (pos_ >= text_.size() || text_[pos_] != '"') return false;
            std::string key;
            if (!string(key)) return false;
            skip();
            if (pos_ >= text_.size() || text_[pos_] != ':') return false;
            ++pos_;
            skip();
            Json item;
            if (!value(item, depth + 1)) return false;
            out.add(std::move(key), std::move(item));
            skip();
            if (pos_ >= text_.size()) return false;
            if (text_[pos_] == ',') { ++pos_; continue; }
            if (text_[pos_] == '}') { ++pos_; return true; }
            return false;
        }
    }
};

inline bool parse_json(std::string_view text, Json& out) { return JsonParser(text).parse(out); }

// ── Kinds of type ────────────────────────────────────────────────────────────────────────────────────────────────────

template <class T> struct is_optional : std::false_type {};
template <class T> struct is_optional<std::optional<T>> : std::true_type {};
template <class T> struct is_pair : std::false_type {};
template <class A, class B> struct is_pair<std::pair<A, B>> : std::true_type {};
template <class T> struct is_tuple : std::false_type {};
template <class... A> struct is_tuple<std::tuple<A...>> : std::true_type {};
template <class T> struct is_smart_pointer : std::false_type {};
template <class T, class D> struct is_smart_pointer<std::unique_ptr<T, D>> : std::true_type {};
template <class T> struct is_smart_pointer<std::shared_ptr<T>> : std::true_type {};
template <class T> struct is_std_array : std::false_type {};
template <class T, std::size_t N> struct is_std_array<std::array<T, N>> : std::true_type {};

template <class T>
concept string_like = std::is_convertible_v<const T&, std::string_view> && !std::is_pointer_v<T> && !std::is_array_v<T>;

template <class T>
concept c_string = std::is_same_v<std::decay_t<T>, const char*> || std::is_same_v<std::decay_t<T>, char*>;

template <class T>
concept map_like = requires { typename T::key_type; typename T::mapped_type; } && requires(const T& t) { std::begin(t); std::end(t); };

template <class T>
concept sequence_like = requires(const T& t) { std::begin(t); std::end(t); } && !string_like<T> && !map_like<T>;

// What a variable can be read back into: a container that clears and takes elements (not a C array).
template <class T>
concept container_like = sequence_like<T> && !std::is_array_v<T> && requires { typename T::value_type; } && requires(T& t) { t.clear(); };

template <class T>
concept appendable = requires(T& t, typename T::value_type v) { t.push_back(v); };

template <class T>
concept insertable = requires(T& t, typename T::value_type v) { t.insert(v); };

// A value that can be shown as a tree (as much as it can: what has no way to be shown becomes text) and a value that can also be
// read back from one (what a variable needs to be kept for the next cell).
template <class T>
constexpr bool persistable() {
    using U = std::remove_cv_t<std::remove_reference_t<T>>;
    if constexpr (std::is_same_v<U, Json> || std::is_same_v<U, bool> || std::is_arithmetic_v<U> || std::is_enum_v<U>) return true;
    else if constexpr (std::is_same_v<U, std::string>) return true;
    else if constexpr (is_optional<U>::value) return persistable<typename U::value_type>() && std::is_default_constructible_v<typename U::value_type>;
    else if constexpr (is_pair<U>::value) return persistable<typename U::first_type>() && persistable<typename U::second_type>() && std::is_default_constructible_v<U>;
    else if constexpr (is_tuple<U>::value) return std::is_default_constructible_v<U>;  // refined where it is used
    else if constexpr (is_std_array<U>::value) return persistable<typename U::value_type>();
    else if constexpr (map_like<U>) {
        using K = typename U::key_type;
        return (std::is_same_v<K, std::string> || std::is_arithmetic_v<K>) && persistable<typename U::mapped_type>() && std::is_default_constructible_v<typename U::mapped_type>;
    }
    else if constexpr (container_like<U>) return (appendable<U> || insertable<U>) && persistable<typename U::value_type>() && std::is_default_constructible_v<typename U::value_type>;
    else if constexpr (registered<U>) return std::is_default_constructible_v<U>;
    else return false;
}

// ── A value as a tree ────────────────────────────────────────────────────────────────────────────────────────────────────

template <class T>
inline Json to_tree(const T& value, std::size_t limit, int depth = 0);

inline std::size_t strnlen_portable(const char* text, std::size_t most) {
    std::size_t n = 0;
    while (n < most && text[n] != '\0') ++n;
    return n;
}

template <class T>
inline std::string key_text(const T& key) {
    if constexpr (std::is_arithmetic_v<T> && !std::is_same_v<T, bool>) {
        if constexpr (std::is_floating_point_v<T>) return number_text(static_cast<double>(key));
        else return std::to_string(key);
    } else if constexpr (std::is_same_v<T, bool>) return key ? "true" : "false";
    else if constexpr (string_like<T>) return std::string(std::string_view(key));
    else return "?";
}

template <class T>
inline Json to_tree(const T& value, std::size_t limit, int depth) {
    using U = std::remove_cv_t<T>;
    if (depth > 8) return Json::text("…");
    if constexpr (std::is_same_v<U, Json>) return value;
    else if constexpr (std::is_same_v<U, bool>) return Json::boolean(value);
    else if constexpr (std::is_same_v<U, char> || std::is_same_v<U, wchar_t> || std::is_same_v<U, char16_t> || std::is_same_v<U, char32_t>) {
        if constexpr (std::is_same_v<U, char>) return Json::text(std::string(1, value));
        else return Json::integer(static_cast<long long>(value));
    }
    else if constexpr (std::is_floating_point_v<U>) {
        double d = static_cast<double>(value);
        return std::isfinite(d) ? Json::real(d) : Json::make(Json::Null);
    }
    else if constexpr (std::is_integral_v<U>) return Json::integer(static_cast<long long>(value));
    else if constexpr (std::is_enum_v<U>) return Json::integer(static_cast<long long>(value));
    else if constexpr (std::is_null_pointer_v<U>) return Json::make(Json::Null);
    else if constexpr (c_string<U>) return value == nullptr ? Json::make(Json::Null) : Json::text(std::string(value));
    else if constexpr (std::is_array_v<U> && std::is_same_v<std::remove_cv_t<std::remove_extent_t<U>>, char>) return Json::text(std::string(value, strnlen_portable(value, std::extent_v<U>)));
    else if constexpr (string_like<U>) return Json::text(std::string(std::string_view(value)));
    else if constexpr (is_optional<U>::value) return value.has_value() ? to_tree(*value, limit, depth + 1) : Json::make(Json::Null);
    else if constexpr (is_smart_pointer<U>::value) return value ? to_tree(*value, limit, depth + 1) : Json::make(Json::Null);
    else if constexpr (std::is_pointer_v<U>) {
        if (value == nullptr) return Json::make(Json::Null);
        if constexpr (std::is_void_v<std::remove_pointer_t<U>> || std::is_function_v<std::remove_pointer_t<U>>) return Json::text("<pointer>");
        else return to_tree(*value, limit, depth + 1);
    }
    else if constexpr (is_pair<U>::value) {
        Json o = Json::make(Json::Object);
        o.type = type_name<U>();
        o.add("first", to_tree(value.first, limit, depth + 1));
        o.add("second", to_tree(value.second, limit, depth + 1));
        return o;
    }
    else if constexpr (is_tuple<U>::value) {
        Json o = Json::make(Json::Object);
        o.type = type_name<U>();
        std::size_t n = 0;
        std::apply([&](const auto&... element) { ((o.add(std::to_string(n++), to_tree(element, limit, depth + 1))), ...); }, value);
        return o;
    }
    else if constexpr (registered<U>) {
        Json o = Json::make(Json::Object);
        o.type = type_name<U>();
        std::size_t n = 0;
        fields<U>::each(value, [&](const auto& member) { o.add(fields<U>::names[n++], to_tree(member, limit, depth + 1)); });
        return o;
    }
    else if constexpr (map_like<U>) {
        Json o = Json::make(Json::Object);
        o.is_map = true;
        std::size_t n = 0;
        for (const auto& entry : value) {
            if (n++ >= limit) { o.cut = true; break; }
            o.add(key_text(entry.first), to_tree(entry.second, limit, depth + 1));
        }
        o.total = std::size(value);
        return o;
    }
    else if constexpr (sequence_like<U>) {
        Json a = Json::make(Json::Array);
        std::size_t n = 0;
        for (const auto& item : value) {
            if (n++ >= limit) { a.cut = true; break; }
            a.items.push_back(to_tree(item, limit, depth + 1));
        }
        a.total = static_cast<std::size_t>(std::distance(std::begin(value), std::end(value)));
        return a;
    }
    else if constexpr (requires(std::ostream& os, const U& v) { os << v; }) {
        std::ostringstream text;
        text << value;
        return Json::text(text.str());
    }
    else return Json::text("<" + type_name<U>() + ">");
}

// ── A value read back from a tree ────────────────────────────────────────────────────────────────────────────────────────

template <class T>
inline bool from_tree(T& out, const Json& j);

template <class T>
inline bool key_from_text(T& key, const std::string& text) {
    if constexpr (std::is_same_v<T, std::string>) { key = text; return true; }
    else if constexpr (std::is_same_v<T, bool>) { key = text == "true"; return text == "true" || text == "false"; }
    else if constexpr (std::is_floating_point_v<T>) { char* end = nullptr; key = static_cast<T>(std::strtod(text.c_str(), &end)); return end && *end == '\0'; }
    else if constexpr (std::is_arithmetic_v<T>) { char* end = nullptr; key = static_cast<T>(std::strtoll(text.c_str(), &end, 10)); return end && *end == '\0'; }
    else return false;
}

template <class T>
inline bool from_tree(T& out, const Json& j) {
    using U = std::remove_cv_t<T>;
    if constexpr (std::is_same_v<U, Json>) { out = j; return true; }
    else if constexpr (std::is_same_v<U, bool>) { if (j.kind != Json::Bool) return false; out = j.b; return true; }
    else if constexpr (std::is_same_v<U, char>) { if (j.kind != Json::String || j.s.size() != 1) return false; out = j.s[0]; return true; }
    else if constexpr (std::is_floating_point_v<U>) {
        if (j.kind == Json::Null) { out = std::numeric_limits<U>::quiet_NaN(); return true; }
        if (j.kind == Json::Float) { out = static_cast<U>(j.d); return true; }
        if (j.kind == Json::Int) { out = static_cast<U>(j.i); return true; }
        return false;
    }
    else if constexpr (std::is_integral_v<U>) {
        if (j.kind == Json::Int) { out = static_cast<U>(j.i); return true; }
        if (j.kind == Json::Float) { out = static_cast<U>(j.d); return true; }
        return false;
    }
    else if constexpr (std::is_enum_v<U>) { if (j.kind != Json::Int) return false; out = static_cast<U>(j.i); return true; }
    else if constexpr (std::is_same_v<U, std::string>) { if (j.kind != Json::String) return false; out = j.s; return true; }
    else if constexpr (is_optional<U>::value) {
        if (j.kind == Json::Null) { out.reset(); return true; }
        typename U::value_type v{};
        if (!from_tree(v, j)) return false;
        out = std::move(v);
        return true;
    }
    else if constexpr (is_pair<U>::value) {
        if (j.kind != Json::Object) return false;
        auto* first = j.find("first");
        auto* second = j.find("second");
        return first && second && from_tree(out.first, *first) && from_tree(out.second, *second);
    }
    else if constexpr (is_tuple<U>::value) {
        if (j.kind != Json::Object) return false;
        std::size_t n = 0;
        bool ok = true;
        std::apply([&](auto&... element) { ((ok = ok && [&] { auto* item = j.find(std::to_string(n++)); return item && from_tree(element, *item); }()), ...); }, out);
        return ok;
    }
    else if constexpr (is_std_array<U>::value) {
        if (j.kind != Json::Array || j.items.size() != out.size()) return false;
        for (std::size_t n = 0; n < out.size(); ++n) if (!from_tree(out[n], j.items[n])) return false;
        return true;
    }
    else if constexpr (registered<U>) {
        if (j.kind != Json::Object) return false;
        std::size_t n = 0;
        bool ok = true;
        fields<U>::each(out, [&](auto& member) { auto* item = j.find(fields<U>::names[n++]); ok = ok && item && from_tree(member, *item); });
        return ok;
    }
    else if constexpr (map_like<U>) {
        if (j.kind != Json::Object) return false;
        out.clear();
        for (std::size_t n = 0; n < j.items.size(); ++n) {
            typename U::key_type key{};
            typename U::mapped_type mapped{};
            if (!key_from_text(key, j.keys[n]) || !from_tree(mapped, j.items[n])) return false;
            out.emplace(std::move(key), std::move(mapped));
        }
        return true;
    }
    else if constexpr (container_like<U>) {
        if (j.kind != Json::Array) return false;
        out.clear();
        for (const auto& item : j.items) {
            typename U::value_type element{};
            if (!from_tree(element, item)) return false;
            if constexpr (appendable<U>) out.push_back(std::move(element));
            else out.insert(std::move(element));
        }
        return true;
    }
    else return false;
}

// ── Showing a value ──────────────────────────────────────────────────────────────────────────────────────────────────────

constexpr std::size_t max_inline = 50;
constexpr std::size_t max_table_rows = 1000;
constexpr std::size_t max_inspect_items = 100;
constexpr int max_inspect_depth = 3;

inline std::string scalar_text(const Json& j, bool quote) {
    switch (j.kind) {
        case Json::Null: return "null";
        case Json::Bool: return j.b ? "true" : "false";
        case Json::Int: return std::to_string(j.i);
        case Json::Float: { char buffer[40]; std::snprintf(buffer, sizeof buffer, "%.15g", j.d); return buffer; }
        case Json::String: return quote ? detail::escape_json(j.s) : j.s;
        default: return dump_json(j);
    }
}

inline void emit_mime(const std::string& data, const std::string& plain_fallback = "") {
    std::string line = "__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{" + data;
    if (!plain_fallback.empty()) line += ",\"text/plain\":" + detail::escape_json(plain_fallback);
    line += "},\"metadata\":{}}";
    detail::emit_protocol(line);
}

inline void emit_table(const std::string& title, const std::vector<std::string>& columns, const std::vector<std::vector<Json>>& rows, std::size_t total) {
    std::string cols = "[";
    std::string numeric = "[";
    for (std::size_t c = 0; c < columns.size(); ++c) {
        if (c > 0) { cols += ','; numeric += ','; }
        cols += detail::escape_json(columns[c]);
        bool seen = false, all = true;
        for (const auto& row : rows) {
            const Json& cell = row[c];
            if (cell.kind == Json::Int || cell.kind == Json::Float) seen = true;
            else if (cell.kind != Json::Null) all = false;
        }
        numeric += (seen && all) ? "true" : "false";
    }
    cols += ']';
    numeric += ']';
    std::string body = "[";
    for (std::size_t r = 0; r < rows.size(); ++r) {
        if (r > 0) body += ',';
        body += '[';
        for (std::size_t c = 0; c < rows[r].size(); ++c) {
            if (c > 0) body += ',';
            const Json& cell = rows[r][c];
            // a cell is one value: a number, a bool or text; anything bigger is its text
            if (cell.scalar()) write_json(cell, body);
            else body += detail::escape_json(dump_json(cell));
        }
        body += ']';
    }
    body += ']';
    emit_mime("\"" + TABLE_MIME + "\":{\"title\":" + detail::escape_json(title) + ",\"columns\":" + cols + ",\"numeric\":" + numeric + ",\"rows\":" + body +
              ",\"totalRows\":" + std::to_string(total) + ",\"totalColumns\":" + std::to_string(columns.size()) + "}");
}

inline std::string inspector_json(const Json& j, int depth, const std::string& title);

inline std::string inspector_property(const std::string& name, const Json& value, int depth) {
    std::string out = "{\"name\":" + detail::escape_json(name);
    if (value.scalar()) {
        out += ",\"value\":" + detail::escape_json(scalar_text(value, false)) + ",\"isNull\":" + (value.kind == Json::Null ? "true" : "false") +
               ",\"isString\":" + (value.kind == Json::String ? "true" : "false");
    } else if (depth >= max_inspect_depth) {
        out += ",\"isNull\":false,\"isString\":false,\"child\":{\"title\":" + detail::escape_json(value.kind == Json::Array ? "list" : value.type.empty() ? "map" : value.type) + ",\"properties\":[]}";
    } else {
        out += ",\"isNull\":false,\"isString\":false,\"child\":" + inspector_json(value, depth + 1, "");
    }
    return out + "}";
}

inline std::string inspector_json(const Json& j, int depth, const std::string& title) {
    std::string name = !title.empty() ? title : !j.type.empty() ? j.type : j.kind == Json::Array ? "list" : "map";
    std::string out = "{\"title\":" + detail::escape_json(name) + ",\"properties\":[";
    std::size_t shown = 0;
    for (std::size_t n = 0; n < j.items.size() && shown < max_inspect_items; ++n, ++shown) {
        if (shown > 0) out += ',';
        out += inspector_property(j.kind == Json::Array ? "[" + std::to_string(n) + "]" : j.keys[n], j.items[n], depth);
    }
    if (j.items.size() > max_inspect_items || j.cut) {
        if (shown > 0) out += ',';
        out += "{\"name\":\"…\",\"value\":" + detail::escape_json("(more than " + std::to_string(max_inspect_items) + " shown)") + ",\"isNull\":false,\"isString\":false}";
    }
    return out + "]}";
}

inline void show_tree(const Json& j, const std::string& title) {
    if (j.scalar()) {
        std::cout << scalar_text(j, true) << std::endl;
        return;
    }
    if (j.kind == Json::Array) {
        if (j.items.empty() && !j.cut) { std::cout << "[ ]" << std::endl; return; }
        bool all_scalar = true, all_records = !j.items.empty();
        for (const auto& item : j.items) {
            if (!item.scalar()) all_scalar = false;
            if (item.kind != Json::Object) all_records = false;
        }
        if (all_scalar && !j.cut && j.items.size() <= max_inline) {
            std::string line = "[ ";
            for (std::size_t n = 0; n < j.items.size(); ++n) {
                if (n > 0) line += ", ";
                line += scalar_text(j.items[n], false);
            }
            std::cout << line << " ]" << std::endl;
            return;
        }
        std::size_t total = j.total ? j.total : j.items.size();
        std::vector<std::vector<Json>> rows;
        if (all_records) {
            std::vector<std::string> columns;
            for (const auto& record : j.items) for (const auto& key : record.keys) if (std::find(columns.begin(), columns.end(), key) == columns.end()) columns.push_back(key);
            for (const auto& record : j.items) {
                std::vector<Json> row;
                for (const auto& column : columns) { const Json* cell = record.find(column); row.push_back(cell ? *cell : Json()); }
                rows.push_back(std::move(row));
            }
            emit_table(title, columns, rows, total);
        } else {
            for (std::size_t n = 0; n < j.items.size(); ++n) rows.push_back({Json::integer(static_cast<long long>(n)), j.items[n]});
            emit_table(title, {"index", "value"}, rows, total);
        }
        return;
    }
    // an object
    if (j.is_map) {
        if (j.items.empty()) { std::cout << "{ }" << std::endl; return; }
        std::vector<std::vector<Json>> rows;
        for (std::size_t n = 0; n < j.items.size(); ++n) rows.push_back({Json::text(j.keys[n]), j.items[n]});
        emit_table(title, {"key", "value"}, rows, j.total ? j.total : j.items.size());
        return;
    }
    emit_mime("\"application/vnd.fry.inspector+json\":" + inspector_json(j, 0, ""), dump_json(j));
}

}  // namespace values

// Shows a value as a notebook cell's last expression is shown: a number or text as text, a short list on one line, a list of
// structs or maps as a table, a struct as the Object Explorer.
template <class T>
inline void show(const T& value) {
    values::show_tree(values::to_tree(value, values::max_table_rows), "");
}

inline void markdown(std::string_view text) {
    values::emit_mime("\"text/markdown\":" + detail::escape_json(text), std::string(text));
}

// ── Keeping variables for the next cell ──────────────────────────────────────────────────────────────────────────────────

namespace values {

inline std::mutex& persist_mutex() { static std::mutex m; return m; }

inline const std::unordered_map<std::string, Json>& restored() {
    static const std::unordered_map<std::string, Json> all = [] {
        std::unordered_map<std::string, Json> map;
        const char* path = std::getenv("FRY_VARS_IN");
        if (!path) return map;
        std::ifstream file(path);
        std::string line;
        while (std::getline(file, line)) {
            Json entry;
            if (!parse_json(line, entry) || entry.kind != Json::Object) continue;
            const Json* name = entry.find("name");
            const Json* value = entry.find("json");
            if (name && value && name->kind == Json::String) map.emplace(name->s, *value);
        }
        return map;
    }();
    return all;
}

}  // namespace values

// A variable the cells before made: its value (a cell's start gives these back; it isn't called by hand).
template <class T>
inline T restore(const char* name) {
    T value{};
    auto& all = values::restored();
    auto found = all.find(name);
    if (found != all.end()) values::from_tree(value, found->second);
    return value;
}

template <class T>
inline void restore_into(T& target, const char* name) {
    auto& all = values::restored();
    auto found = all.find(name);
    if (found != all.end()) values::from_tree(target, found->second);
}

// Records a variable the cell made, for the next cell: its name, its type as C++ writes it, and its value as JSON. A value that
// wouldn't come back the same (a pointer, an array, a type the notebook doesn't know the members of) is recorded as skipped, with why.
template <class T>
inline void persist(const char* name, const T& value) {
    const char* path = std::getenv("FRY_VARS_OUT");
    if (!path) return;
    using U = std::remove_cv_t<std::remove_reference_t<T>>;
    std::string type = values::type_name<U>();
    std::string line = "{\"name\":" + detail::escape_json(name) + ",\"type\":" + detail::escape_json(type);
    std::string skipped;
    bool quiet = false;
    if constexpr (values::persistable<U>()) {
        values::Json tree = values::to_tree(value, static_cast<std::size_t>(-1));
        std::string text = values::dump_json(tree);
        values::Json parsed;
        U back{};
        if (values::parse_json(text, parsed) && values::from_tree(back, parsed) && values::dump_json(values::to_tree(back, static_cast<std::size_t>(-1))) == text) {
            line += ",\"json\":" + text;
        } else {
            skipped = "it doesn't come back the same when it is saved";
        }
    } else {
        skipped = "its type (" + type + ") isn't one that can be saved: numbers, text, lists, maps, optionals and structs of those can";
        quiet = type.find("lambda") != std::string::npos || type.find("(unnamed") != std::string::npos || type.find("$_") != std::string::npos;
    }
    if (!skipped.empty()) line += ",\"skipped\":" + detail::escape_json(skipped) + (quiet ? ",\"quiet\":true" : "");
    line += "}\n";
    std::lock_guard<std::mutex> lock(values::persist_mutex());
    std::ofstream file(path, std::ios::app | std::ios::binary);
    file << line;
}

// Offers a value to the cells of other languages (#!share --from cpp name): it goes as JSON.
template <class T>
inline void share(const char* name, const T& value) {
    using U = std::remove_cv_t<std::remove_reference_t<T>>;
    if constexpr (values::persistable<U>()) {
        std::string json = values::dump_json(values::to_tree(value, static_cast<std::size_t>(-1)));
        detail::emit_protocol("__FRY_SHARE__ {\"name\":" + detail::escape_json(name) + ",\"json\":" + detail::escape_json(json) + "}");
    } else {
        std::cout << "⚠️ " << name << " can't be shared: " << values::type_name<U>() << " isn't a type that can be sent as JSON" << std::endl;
    }
}

// ── Input ────────────────────────────────────────────────────────────────────────────────────────────────────────────────

namespace values {

inline std::optional<std::string> ask(std::string_view prompt, bool secret) {
    static bool ended = false;
    if (ended) return std::nullopt;
    if (std::getenv("FRY_INPUT_REQUESTS")) {
        detail::emit_protocol("__FRY_INPUT__ {\"prompt\":" + detail::escape_json(prompt) + ",\"password\":" + (secret ? "true" : "false") + "}");
    } else {
        std::cout << prompt << std::flush;
    }
    std::string line;
    if (!std::getline(std::cin, line)) {
        ended = true;
        return std::nullopt;
    }
    if (!line.empty() && line.back() == '\r') line.pop_back();
    return line;
}

}  // namespace values

// Asks for a line of input and returns it, without its line break; nothing is the end of input. In a notebook cell the notebook shows
// the prompt and the user answers there (as for input() in Python); a plain std::cin read can't ask, and sees the end of input at
// once. In a program run on its own the prompt is printed and the line is read from the terminal.
inline std::optional<std::string> input(std::string_view prompt = "") { return values::ask(prompt, false); }

// input() for a secret: what is typed is not shown.
inline std::optional<std::string> password(std::string_view prompt = "") { return values::ask(prompt, true); }

}  // namespace fry

#endif  // FRY_VALUES_HPP_INCLUDED
