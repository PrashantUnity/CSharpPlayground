using System.IO;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Provides zero-configuration runtime support for C++ scripts and algorithms in C# Code Studio,
/// staging <c>&lt;fry/display.hpp&gt;</c> and <c>"display.hpp"</c> into the compiler include path.
/// User scripts can call <c>fry::display::table(...)</c>, <c>fry::dump(...)</c>, <c>fry::display::html(...)</c>,
/// and <c>fry::display::image(...)</c> out of the box with interactive Results (.DUMP) deck rendering.
/// </summary>
public static class CppDisplayRuntime
{
    public static async Task<string> EnsureIncludeDirectoryAsync(string buildDir, CancellationToken ct = default)
    {
        var includeDir = Path.Combine(buildDir, "include");
        var fryIncludeDir = Path.Combine(includeDir, "fry");

        try
        {
            Directory.CreateDirectory(fryIncludeDir);

            var header1 = Path.Combine(fryIncludeDir, "display.hpp");
            var header2 = Path.Combine(includeDir, "display.hpp");

            await File.WriteAllTextAsync(header1, DisplayHppContent, ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(header2, DisplayHppContent, ct).ConfigureAwait(false);
        }
        catch
        {
            // Gracefully ignore filesystem contention
        }

        return includeDir;
    }

    public const string DisplayHppContent = """
        #pragma once
        #include <iostream>
        #include <string>
        #include <string_view>
        #include <vector>
        #include <sstream>
        #include <fstream>
        #include <type_traits>
        #include <concepts>
        #include <utility>
        #include <map>
        #include <iomanip>

        namespace fry {
        namespace display {
        namespace detail {

        inline std::string base64_encode(const unsigned char* data, size_t len) {
            static const char* chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
            std::string out;
            out.reserve(((len + 2) / 3) * 4);
            for (size_t i = 0; i < len; i += 3) {
                unsigned int val = (data[i] << 16) |
                                   ((i + 1 < len ? data[i + 1] : 0) << 8) |
                                   (i + 2 < len ? data[i + 2] : 0);
                out.push_back(chars[(val >> 18) & 0x3F]);
                out.push_back(chars[(val >> 12) & 0x3F]);
                out.push_back(i + 1 < len ? chars[(val >> 6) & 0x3F] : '=');
                out.push_back(i + 2 < len ? chars[val & 0x3F] : '=');
            }
            return out;
        }

        inline std::string escape_json(std::string_view s) {
            std::string out;
            out.reserve(s.size() + 16);
            out.push_back('"');
            for (char c : s) {
                switch (c) {
                    case '"':  out.append("\\\""); break;
                    case '\\': out.append("\\\\"); break;
                    case '\b': out.append("\\b"); break;
                    case '\f': out.append("\\f"); break;
                    case '\n': out.append("\\n"); break;
                    case '\r': out.append("\\r"); break;
                    case '\t': out.append("\\t"); break;
                    default:
                        if (static_cast<unsigned char>(c) < 0x20) {
                            std::ostringstream ss;
                            ss << "\\u" << std::hex << std::setw(4) << std::setfill('0') << static_cast<int>(c);
                            out.append(ss.str());
                        } else {
                            out.push_back(c);
                        }
                        break;
                }
            }
            out.push_back('"');
            return out;
        }

        template <typename T>
        concept StringLike = std::convertible_to<T, std::string_view>;

        template <typename T>
        concept PairLike = requires(T t) {
            t.first;
            t.second;
        };

        template <typename T>
        concept Iterable = requires(T t) {
            std::begin(t);
            std::end(t);
        } && !StringLike<T>;

        template <typename T>
        constexpr bool is_num() {
            return std::is_arithmetic_v<T> && !std::is_same_v<T, bool> && !std::is_same_v<T, char>;
        }

        template <typename T>
        inline std::string to_json_val(const T& val) {
            if constexpr (std::is_same_v<T, bool>) {
                return val ? "true" : "false";
            } else if constexpr (std::is_null_pointer_v<T>) {
                return "null";
            } else if constexpr (is_num<T>()) {
                return std::to_string(val);
            } else if constexpr (StringLike<T>) {
                return escape_json(std::string_view(val));
            } else if constexpr (std::is_same_v<T, char>) {
                char buf[2] = { val, '\0' };
                return escape_json(buf);
            } else {
                std::ostringstream ss;
                ss << val;
                return escape_json(ss.str());
            }
        }

        inline void emit_protocol(std::string_view json_bundle) {
            std::cout << "__FRY_DISPLAY__ " << json_bundle << std::endl;
        }

        } // namespace detail

        inline void emit(std::string_view json_bundle) {
            detail::emit_protocol(json_bundle);
        }

        inline void html(std::string_view html_content) {
            std::string json = "{\"type\":\"display\",\"data\":{\"text/html\":" + detail::escape_json(html_content) + "},\"metadata\":{}}";
            detail::emit_protocol(json);
        }

        inline void image(std::string_view base64_or_path, std::string_view format = "PNG") {
            std::string str(base64_or_path);
            if (str.rfind("data:image", 0) == 0) {
                auto comma = str.find(',');
                if (comma != std::string::npos) str = str.substr(comma + 1);
            }
            std::ifstream file(str, std::ios::binary | std::ios::ate);
            if (file.is_open()) {
                auto size = file.tellg();
                file.seekg(0, std::ios::beg);
                std::vector<unsigned char> bytes(size);
                if (file.read(reinterpret_cast<char*>(bytes.data()), size)) {
                    std::string b64 = detail::base64_encode(bytes.data(), bytes.size());
                    std::string mime = (format == "JPEG" || format == "jpeg" || format == "jpg") ? "image/jpeg" : "image/png";
                    std::string json = "{\"type\":\"display\",\"data\":{\"" + mime + "\":\"" + b64 + "\"},\"metadata\":{}}";
                    detail::emit_protocol(json);
                    return;
                }
            }
            std::string mime = (format == "JPEG" || format == "jpeg" || format == "jpg") ? "image/jpeg" : "image/png";
            std::string json = "{\"type\":\"display\",\"data\":{\"" + mime + "\":\"" + str + "\"},\"metadata\":{}}";
            detail::emit_protocol(json);
        }

        inline void json(std::string_view json_content, std::string_view = "JSON") {
            std::string j = "{\"type\":\"display\",\"data\":{\"text/plain\":" + detail::escape_json(json_content) + "},\"metadata\":{}}";
            detail::emit_protocol(j);
        }

        template <typename T>
        inline const T& dump(const T& val, std::string_view title = "");

        template <typename T>
        inline void table(const T& val, std::string_view title = "");

        template <typename T>
        inline void table(const T& val, std::string_view title) {
            std::string auto_title = title.empty() ? "Data" : std::string(title);
            std::string table_mime = "application/vnd.fry.table+json";

            if constexpr (detail::Iterable<T>) {
                using Elem = std::decay_t<decltype(*std::begin(val))>;

                if constexpr (detail::PairLike<Elem>) {
                    using KeyType = std::decay_t<decltype(std::begin(val)->first)>;
                    using ValType = std::decay_t<decltype(std::begin(val)->second)>;
                    std::ostringstream ss;
                    ss << "{\"type\":\"display\",\"data\":{\"" << table_mime << "\":{\"title\":"
                       << detail::escape_json(auto_title)
                       << ",\"columns\":[\"Key\",\"Value\"],\"numeric\":["
                       << (detail::is_num<KeyType>() ? "true" : "false") << ","
                       << (detail::is_num<ValType>() ? "true" : "false") << "],\"rows\":[";
                    size_t count = 0;
                    for (const auto& item : val) {
                        if (count++ > 0) ss << ",";
                        ss << "[" << detail::to_json_val(item.first) << "," << detail::to_json_val(item.second) << "]";
                    }
                    ss << "],\"totalRows\":" << count << ",\"totalColumns\":2}},\"metadata\":{}}";
                    detail::emit_protocol(ss.str());
                    return;
                } else if constexpr (detail::Iterable<Elem>) {
                    using CellType = std::decay_t<decltype(*std::begin(*std::begin(val)))>;
                    size_t max_cols = 0;
                    size_t rows_count = 0;
                    for (const auto& row : val) {
                        rows_count++;
                        size_t col_count = 0;
                        for (const auto& c : row) { (void)c; col_count++; }
                        if (col_count > max_cols) max_cols = col_count;
                    }
                    std::ostringstream ss;
                    ss << "{\"type\":\"display\",\"data\":{\"" << table_mime << "\":{\"title\":"
                       << detail::escape_json(auto_title)
                       << ",\"columns\":[";
                    for (size_t c = 0; c < max_cols; ++c) {
                        if (c > 0) ss << ",";
                        ss << "\"[" << c << "]\"";
                    }
                    ss << "],\"numeric\":[";
                    for (size_t c = 0; c < max_cols; ++c) {
                        if (c > 0) ss << ",";
                        ss << (detail::is_num<CellType>() ? "true" : "false");
                    }
                    ss << "],\"rows\":[";
                    size_t r = 0;
                    for (const auto& row : val) {
                        if (r++ > 0) ss << ",";
                        ss << "[";
                        size_t c = 0;
                        for (const auto& cell : row) {
                            if (c++ > 0) ss << ",";
                            ss << detail::to_json_val(cell);
                        }
                        while (c < max_cols) {
                            if (c++ > 0) ss << ",";
                            ss << "null";
                        }
                        ss << "]";
                    }
                    ss << "],\"totalRows\":" << rows_count << ",\"totalColumns\":" << max_cols << "}},\"metadata\":{}}";
                    detail::emit_protocol(ss.str());
                    return;
                } else {
                    size_t count = 0;
                    for (const auto& x : val) { (void)x; count++; }
                    std::ostringstream ss;
                    ss << "{\"type\":\"display\",\"data\":{\"" << table_mime << "\":{\"title\":"
                       << detail::escape_json(auto_title)
                       << ",\"columns\":[\"Index\",\"Value\"],\"numeric\":[true,"
                       << (detail::is_num<Elem>() ? "true" : "false") << "],\"rows\":[";
                    size_t i = 0;
                    for (const auto& item : val) {
                        if (i > 0) ss << ",";
                        ss << "[" << i << "," << detail::to_json_val(item) << "]";
                        i++;
                    }
                    ss << "],\"totalRows\":" << count << ",\"totalColumns\":2}},\"metadata\":{}}";
                    detail::emit_protocol(ss.str());
                    return;
                }
            } else {
                std::ostringstream ss;
                ss << "{\"type\":\"display\",\"data\":{\"" << table_mime << "\":{\"title\":"
                   << detail::escape_json(auto_title)
                   << ",\"columns\":[\"Value\"],\"numeric\":["
                   << (detail::is_num<T>() ? "true" : "false") << "],\"rows\":[["
                   << detail::to_json_val(val)
                   << "]],\"totalRows\":1,\"totalColumns\":1}},\"metadata\":{}}";
                detail::emit_protocol(ss.str());
            }
        }

        template <typename T>
        inline const T& dump(const T& val, std::string_view title) {
            table(val, title);
            return val;
        }

        } // namespace display

        using display::dump;
        using display::table;
        using display::html;
        using display::image;
        using display::json;

        } // namespace fry

        class Display {
        public:
            template <typename T>
            static const T& dump(const T& val, std::string_view title = "") {
                return fry::display::dump(val, title);
            }
            template <typename T>
            static void table(const T& val, std::string_view title = "") {
                fry::display::table(val, title);
            }
            static void html(std::string_view html_content) {
                fry::display::html(html_content);
            }
            static void image(std::string_view base64_or_path, std::string_view format = "PNG") {
                fry::display::image(base64_or_path, format);
            }
            static void json(std::string_view json_content, std::string_view title = "JSON") {
                fry::display::json(json_content, title);
            }
        };
        """;
}
