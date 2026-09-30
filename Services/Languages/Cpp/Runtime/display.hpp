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
#include <set>
#include <iomanip>
#include <thread>
#include <chrono>
#include <mutex>
#include <atomic>
#include <functional>
#include <memory>
#include <optional>
#include <cmath>
#include <algorithm>
#include <cstdlib>
#include <cstring>
#include <tuple>
#include <queue>

#ifdef _WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <winsock2.h>
#include <ws2tcpip.h>
#pragma comment(lib, "ws2_32.lib")
#else
#include <sys/socket.h>
#include <arpa/inet.h>
#include <unistd.h>
#include <netdb.h>
#endif

namespace fry {

inline const std::string CHART_MIME = "application/vnd.fry.chart.v1+json";
inline const std::string PLOT3D_MIME = "application/vnd.fry.plot3d.v1+json";
inline const std::string VISUALIZER_MIME = "application/vnd.fry.visualizer.v1+json";
inline const std::string TABLE_MIME = "application/vnd.fry.table+json";

// ── TreeNode & ListNode ───────────────────────────────────────────────────

template <typename T = int>
struct TreeNode {
    T val{};
    std::shared_ptr<TreeNode<T>> left{};
    std::shared_ptr<TreeNode<T>> right{};

    TreeNode() = default;
    TreeNode(T v) : val(v) {}
    TreeNode(T v, std::shared_ptr<TreeNode<T>> l, std::shared_ptr<TreeNode<T>> r = nullptr)
        : val(v), left(std::move(l)), right(std::move(r)) {}
};

template <typename T = int>
inline std::shared_ptr<TreeNode<T>> make_tree(T v, std::shared_ptr<TreeNode<T>> l = nullptr, std::shared_ptr<TreeNode<T>> r = nullptr) {
    return std::make_shared<TreeNode<T>>(v, std::move(l), std::move(r));
}

template <typename T = int>
struct ListNode {
    T val{};
    std::shared_ptr<ListNode<T>> next{};

    ListNode() = default;
    ListNode(T v) : val(v) {}
    ListNode(T v, std::shared_ptr<ListNode<T>> n) : val(v), next(std::move(n)) {}
};

template <typename T = int>
inline std::shared_ptr<ListNode<T>> make_list(T v, std::shared_ptr<ListNode<T>> n = nullptr) {
    return std::make_shared<ListNode<T>>(v, std::move(n));
}

// ── Event & Event Loop ────────────────────────────────────────────────────

struct Event {
    std::string kind;
    std::map<std::string, std::string> target;
    std::string raw;

    std::optional<std::string> get(const std::string& key) const {
        auto it = target.find(key);
        if (it != target.end()) return it->second;
        return std::nullopt;
    }
};

namespace detail {

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

inline std::atomic<uint64_t> id_counter{1};

inline std::string new_id() {
    uint64_t n = id_counter.fetch_add(1);
    std::ostringstream ss;
    ss << "cpp_" << n << "_" << std::hex << (n * 7919);
    return ss.str();
}

inline std::once_flag socket_init_flag;
inline std::mutex listeners_mutex;
inline std::map<std::string, std::map<std::string, std::vector<std::function<void(const Event&)>>>> listeners_map;

inline void dispatch_event_line(const std::string& line) {
    auto id_pos = line.find("\"display_id\"");
    if (id_pos == std::string::npos) return;
    auto colon = line.find(':', id_pos);
    if (colon == std::string::npos) return;
    auto val_start = line.find('"', colon);
    if (val_start == std::string::npos) return;
    auto val_end = line.find('"', val_start + 1);
    if (val_end == std::string::npos) return;
    std::string disp_id = line.substr(val_start + 1, val_end - val_start - 1);

    std::string event_type = "click";
    auto ev_pos = line.find("\"event\"");
    if (ev_pos != std::string::npos) {
        auto ev_colon = line.find(':', ev_pos);
        if (ev_colon != std::string::npos) {
            auto ev_s = line.find('"', ev_colon);
            if (ev_s != std::string::npos) {
                auto ev_e = line.find('"', ev_s + 1);
                if (ev_e != std::string::npos) {
                    event_type = line.substr(ev_s + 1, ev_e - ev_s - 1);
                }
            }
        }
    }

    Event ev;
    ev.kind = event_type;
    ev.raw = line;

    auto idx_pos = line.find("\"index\"");
    if (idx_pos != std::string::npos) {
        auto idx_colon = line.find(':', idx_pos);
        if (idx_colon != std::string::npos) {
            size_t num_s = line.find_first_not_of(" \t\r\n\"", idx_colon + 1);
            if (num_s != std::string::npos) {
                size_t num_e = line.find_first_of(",}\" \t\r\n", num_s);
                if (num_e != std::string::npos) {
                    ev.target["index"] = line.substr(num_s, num_e - num_s);
                }
            }
        }
    }
    auto ser_pos = line.find("\"series\"");
    if (ser_pos != std::string::npos) {
        auto ser_colon = line.find(':', ser_pos);
        if (ser_colon != std::string::npos) {
            size_t num_s = line.find_first_not_of(" \t\r\n\"", ser_colon + 1);
            if (num_s != std::string::npos) {
                size_t num_e = line.find_first_of(",}\" \t\r\n", num_s);
                if (num_e != std::string::npos) {
                    ev.target["series"] = line.substr(num_s, num_e - num_s);
                }
            }
        }
    }

    std::vector<std::function<void(const Event&)>> cbs;
    {
        std::lock_guard<std::mutex> lock(listeners_mutex);
        auto it = listeners_map.find(disp_id);
        if (it != listeners_map.end()) {
            auto it2 = it->second.find(event_type);
            if (it2 != it->second.end()) {
                cbs = it2->second;
            }
            if (event_type != "click") {
                auto it3 = it->second.find("click");
                if (it3 != it->second.end() && cbs.empty()) {
                    cbs = it3->second;
                }
            }
        }
    }

    for (const auto& cb : cbs) {
        cb(ev);
    }
}

inline void ensure_event_socket() {
    std::call_once(socket_init_flag, [] {
        const char* addr_env = std::getenv("FRY_EVENTS");
        if (!addr_env || std::strlen(addr_env) == 0) return;
        const char* token_env = std::getenv("FRY_EVENTS_TOKEN");
        std::string token = token_env ? token_env : "";

        std::string addr_str(addr_env);
        auto colon = addr_str.find(':');
        std::string host = (colon != std::string::npos) ? addr_str.substr(0, colon) : "127.0.0.1";
        int port = (colon != std::string::npos) ? std::atoi(addr_str.substr(colon + 1).c_str()) : 0;
        if (port <= 0) return;

#ifdef _WIN32
        WSADATA wsa;
        if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) return;
        SOCKET sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        if (sock == INVALID_SOCKET) return;
#else
        int sock = socket(AF_INET, SOCK_STREAM, 0);
        if (sock < 0) return;
#endif

        sockaddr_in server_addr{};
        server_addr.sin_family = AF_INET;
        server_addr.sin_port = htons(static_cast<uint16_t>(port));
        if (inet_pton(AF_INET, host.c_str(), &server_addr.sin_addr) <= 0) {
#ifdef _WIN32
            closesocket(sock);
#else
            close(sock);
#endif
            return;
        }

        if (connect(sock, reinterpret_cast<sockaddr*>(&server_addr), sizeof(server_addr)) < 0) {
#ifdef _WIN32
            closesocket(sock);
#else
            close(sock);
#endif
            return;
        }

        std::string hello = "{\"type\":\"hello\",\"token\":" + escape_json(token) + "}\n";
        send(sock, hello.c_str(), static_cast<int>(hello.size()), 0);

        std::thread([sock]() mutable {
            std::string buffer;
            char recv_buf[1024];
            while (true) {
                int bytes = recv(sock, recv_buf, sizeof(recv_buf), 0);
                if (bytes <= 0) break;
                buffer.append(recv_buf, bytes);
                size_t nl;
                while ((nl = buffer.find('\n')) != std::string::npos) {
                    std::string line = buffer.substr(0, nl);
                    buffer.erase(0, nl + 1);
                    if (!line.empty()) {
                        dispatch_event_line(line);
                    }
                }
            }
#ifdef _WIN32
            closesocket(sock);
#else
            close(sock);
#endif
        }).detach();
    });
}

inline void emit_protocol(std::string_view line) {
    std::cout << line << std::endl;
}

inline void emit_update(const std::string& mime, const std::string& spec_json, const std::string& display_id) {
    std::string fallback = mime + ": visual (updated)";
    std::string json = "__FRY_DISPLAY__ {\"type\":\"update_display\",\"data\":{\"" + mime + "\":" +
                       spec_json + ",\"text/plain\":" + escape_json(fallback) +
                       "},\"metadata\":{},\"transient\":{\"display_id\":" + escape_json(display_id) + "}}";
    emit_protocol(json);
}

inline std::string replace_or_insert_title(const std::string& spec, std::string_view title) {
    auto title_pos = spec.find("\"title\":");
    if (title_pos != std::string::npos) {
        auto val_start = spec.find('"', title_pos + 8);
        if (val_start != std::string::npos) {
            auto val_end = spec.find('"', val_start + 1);
            while (val_end != std::string::npos && spec[val_end - 1] == '\\') {
                val_end = spec.find('"', val_end + 1);
            }
            if (val_end != std::string::npos) {
                std::string res = spec.substr(0, val_start);
                res += escape_json(title);
                res += spec.substr(val_end + 1);
                return res;
            }
        }
    }
    auto last_brace = spec.rfind('}');
    if (last_brace != std::string::npos) {
        std::string res = spec.substr(0, last_brace);
        if (res.find(':') != std::string::npos) {
            res += ",\"title\":";
        } else {
            res += "\"title\":";
        }
        res += escape_json(title);
        res += "}";
        return res;
    }
    return spec;
}

} // namespace detail

// ── DisplayHandle ─────────────────────────────────────────────────────────

class DisplayHandle {
public:
    std::string mime;
    std::string display_id;
    std::shared_ptr<std::mutex> spec_mutex;
    std::shared_ptr<std::string> spec_json;
    std::shared_ptr<std::mutex> time_mutex;
    std::shared_ptr<std::chrono::steady_clock::time_point> last_update;

    DisplayHandle() = default;

    DisplayHandle(std::string m, std::string id, std::string spec)
        : mime(std::move(m)),
          display_id(std::move(id)),
          spec_mutex(std::make_shared<std::mutex>()),
          spec_json(std::make_shared<std::string>(std::move(spec))),
          time_mutex(std::make_shared<std::mutex>()),
          last_update(std::make_shared<std::chrono::steady_clock::time_point>(std::chrono::steady_clock::now())) {}

    DisplayHandle& update(std::string_view title) {
        std::string new_spec;
        {
            std::lock_guard<std::mutex> lock(*spec_mutex);
            *spec_json = detail::replace_or_insert_title(*spec_json, title);
            new_spec = *spec_json;
        }
        auto now = std::chrono::steady_clock::now();
        std::unique_lock<std::mutex> tlock(*time_mutex);
        auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(now - *last_update).count();
        if (elapsed >= 33) {
            *last_update = now;
            tlock.unlock();
            detail::emit_update(mime, new_spec, display_id);
        } else {
            auto wait_ms = 33 - elapsed;
            auto m = mime;
            auto id = display_id;
            auto l_ptr = last_update;
            auto t_ptr = time_mutex;
            std::thread([m, new_spec, id, wait_ms, l_ptr, t_ptr]() {
                std::this_thread::sleep_for(std::chrono::milliseconds(wait_ms));
                {
                    std::lock_guard<std::mutex> lk(*t_ptr);
                    *l_ptr = std::chrono::steady_clock::now();
                }
                detail::emit_update(m, new_spec, id);
            }).detach();
        }
        return *this;
    }

    DisplayHandle& update_title(std::string_view title) { return update(title); }

    DisplayHandle& on(std::string_view event, std::function<void(const Event&)> cb) {
        detail::ensure_event_socket();
        std::string ev(event);
        for (auto& c : ev) c = static_cast<char>(std::tolower(c));
        {
            std::lock_guard<std::mutex> lock(detail::listeners_mutex);
            detail::listeners_map[display_id][ev].push_back(std::move(cb));
        }
        std::cout << "__FRY_DISPLAY__ {\"type\":\"subscribe\",\"display_id\":\""
                  << display_id << "\",\"events\":[\"" << ev << "\"]}" << std::endl;
        return *this;
    }

    DisplayHandle& on_click(std::function<void(const Event&)> cb) { return on("click", std::move(cb)); }
    DisplayHandle& on_select(std::function<void(const Event&)> cb) { return on("select", std::move(cb)); }
    DisplayHandle& on_step(std::function<void(const Event&)> cb) { return on("step", std::move(cb)); }

    DisplayHandle& off(std::string_view event) {
        std::string ev(event);
        for (auto& c : ev) c = static_cast<char>(std::tolower(c));
        {
            std::lock_guard<std::mutex> lock(detail::listeners_mutex);
            auto it = detail::listeners_map.find(display_id);
            if (it != detail::listeners_map.end()) {
                it->second.erase(ev);
            }
        }
        std::cout << "__FRY_DISPLAY__ {\"type\":\"unsubscribe\",\"display_id\":\""
                  << display_id << "\",\"events\":[\"" << ev << "\"]}" << std::endl;
        return *this;
    }

    void close() {
        std::lock_guard<std::mutex> lock(detail::listeners_mutex);
        detail::listeners_map.erase(display_id);
    }

    DisplayHandle& show() { return *this; }
    DisplayHandle& title(std::string_view t) { return update(t); }
    DisplayHandle& bins(int) { return *this; }
};

namespace detail {

inline DisplayHandle emit_display(const std::string& mime, const std::string& spec_json) {
    std::string id = new_id();
    std::string fallback = mime + ": visual";
    std::string json = "__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"" + mime + "\":" +
                       spec_json + ",\"text/plain\":" + escape_json(fallback) +
                       "},\"metadata\":{},\"transient\":{\"display_id\":" + escape_json(id) + "}}";
    emit_protocol(json);
    return DisplayHandle(mime, id, spec_json);
}

// ── Serialization Concepts & Helpers ─────────────────────────────────────

template <typename T>
concept StringLike = std::convertible_to<T, std::string_view>;

template <typename T>
concept PairLike = requires(T t) {
    t.first;
    t.second;
};

template <typename T>
concept HasNameAndValue = requires(T t) {
    t.name;
    t.value;
};

template <typename T>
concept Iterable = requires(T t) {
    std::begin(t);
    std::end(t);
} && !StringLike<T>;

template <typename T>
concept OptionalLike = requires(T t) {
    { t.has_value() } -> std::convertible_to<bool>;
    *t;
};

template <typename T>
constexpr bool is_num() {
    using U = std::decay_t<T>;
    return std::is_arithmetic_v<U> && !std::is_same_v<U, bool> && !std::is_same_v<U, char>;
}

template <typename T>
inline std::string to_json_array(const T& iter);

template <typename T>
inline std::string to_json_val(const T& val) {
    if constexpr (OptionalLike<T>) {
        if (!val.has_value()) return "null";
        return to_json_val(*val);
    } else if constexpr (std::is_same_v<T, bool>) {
        return val ? "true" : "false";
    } else if constexpr (std::is_null_pointer_v<T>) {
        return "null";
    } else if constexpr (std::is_floating_point_v<T>) {
        if (std::isnan(val) || std::isinf(val)) return "null";
        std::ostringstream ss;
        ss << std::setprecision(10) << val;
        return ss.str();
    } else if constexpr (is_num<T>()) {
        return std::to_string(val);
    } else if constexpr (StringLike<T>) {
        return escape_json(std::string_view(val));
    } else if constexpr (std::is_same_v<T, char>) {
        char buf[2] = { val, '\0' };
        return escape_json(buf);
    } else if constexpr (Iterable<T>) {
        return to_json_array(val);
    } else {
        std::ostringstream ss;
        ss << val;
        return escape_json(ss.str());
    }
}

template <typename T>
inline std::string to_json_array(const T& iter) {
    std::ostringstream ss;
    ss << "[";
    size_t i = 0;
    for (const auto& item : iter) {
        if (i++ > 0) ss << ",";
        ss << to_json_val(item);
    }
    ss << "]";
    return ss.str();
}

} // namespace detail

// ── Basic Display Primitives (Preserved) ───────────────────────────────────

namespace display {

inline void html(std::string_view html_content) {
    std::string json = "__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"text/html\":" +
                       detail::escape_json(html_content) + "},\"metadata\":{}}";
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
            std::string json = "__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"" + mime + "\":\"" + b64 + "\"},\"metadata\":{}}";
            detail::emit_protocol(json);
            return;
        }
    }
    std::string mime = (format == "JPEG" || format == "jpeg" || format == "jpg") ? "image/jpeg" : "image/png";
    std::string json = "__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"" + mime + "\":\"" + str + "\"},\"metadata\":{}}";
    detail::emit_protocol(json);
}

inline void json(std::string_view json_content, std::string_view = "JSON") {
    std::string j = "__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"text/plain\":" +
                    detail::escape_json(json_content) + "},\"metadata\":{}}";
    detail::emit_protocol(j);
}

template <typename T>
inline void table(const T& val, std::string_view title = "") {
    std::string auto_title = title.empty() ? "Data" : std::string(title);
    std::string table_mime = "application/vnd.fry.table+json";

    if constexpr (detail::Iterable<T>) {
        using Elem = std::decay_t<decltype(*std::begin(val))>;

        if constexpr (detail::PairLike<Elem>) {
            using KeyType = std::decay_t<decltype(std::begin(val)->first)>;
            using ValType = std::decay_t<decltype(std::begin(val)->second)>;
            std::ostringstream ss;
            ss << "__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"" << table_mime << "\":{\"title\":"
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
            ss << "__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"" << table_mime << "\":{\"title\":"
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
            ss << "__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"" << table_mime << "\":{\"title\":"
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
        ss << "__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"" << table_mime << "\":{\"title\":"
           << detail::escape_json(auto_title)
           << ",\"columns\":[\"Value\"],\"numeric\":["
           << (detail::is_num<T>() ? "true" : "false") << "],\"rows\":[["
           << detail::to_json_val(val)
           << "]],\"totalRows\":1,\"totalColumns\":1}},\"metadata\":{}}";
        detail::emit_protocol(ss.str());
    }
}

template <typename T>
inline const T& dump(const T& val, std::string_view title = "") {
    table(val, title);
    return val;
}

} // namespace display

using display::dump;
using display::table;
using display::html;
using display::image;
using display::json;

// ── Canonical Visual API ──────────────────────────────────────────────────

// 1. Line Chart
template <typename T>
inline DisplayHandle line_chart(const T& data, std::string_view title = "") {
    std::ostringstream ss;
    ss << "{\"kind\":\"line\",\"series\":[{\"y\":" << detail::to_json_array(data) << "}]";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(CHART_MIME, ss.str());
}

// 2. Scatter Chart
template <typename T>
inline DisplayHandle scatter_chart(const T& data, std::string_view title = "") {
    std::ostringstream xs, ys;
    xs << "["; ys << "[";
    size_t i = 0;
    for (const auto& pt : data) {
        if (i++ > 0) { xs << ","; ys << ","; }
        if constexpr (detail::PairLike<decltype(pt)>) {
            xs << detail::to_json_val(pt.first);
            ys << detail::to_json_val(pt.second);
        } else {
            auto it = std::begin(pt);
            xs << detail::to_json_val(*it); ++it;
            ys << detail::to_json_val(*it);
        }
    }
    xs << "]"; ys << "]";

    std::ostringstream ss;
    ss << "{\"kind\":\"scatter\",\"series\":[{\"x\":" << xs.str() << ",\"y\":" << ys.str() << "}]";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(CHART_MIME, ss.str());
}

// 3. Bar Chart
template <typename T>
inline DisplayHandle bar_chart(const T& data, std::string_view title = "") {
    std::ostringstream y_ss;
    std::ostringstream l_ss;
    y_ss << "[";
    l_ss << "[";
    size_t i = 0;
    for (const auto& item : data) {
        if (i++ > 0) { y_ss << ","; l_ss << ","; }
        if constexpr (detail::PairLike<decltype(item)>) {
            l_ss << detail::escape_json(std::string_view(item.first));
            y_ss << detail::to_json_val(item.second);
        } else if constexpr (detail::HasNameAndValue<decltype(item)>) {
            l_ss << detail::escape_json(std::string_view(item.name));
            y_ss << detail::to_json_val(item.value);
        }
    }
    y_ss << "]";
    l_ss << "]";

    std::ostringstream ss;
    ss << "{\"kind\":\"bar\",\"series\":[{\"y\":" << y_ss.str() << ",\"labels\":" << l_ss.str() << "}]";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(CHART_MIME, ss.str());
}

// 4. Pie Chart
template <typename T>
inline DisplayHandle pie_chart(const T& data, std::string_view title = "") {
    std::ostringstream y_ss;
    std::ostringstream l_ss;
    y_ss << "[";
    l_ss << "[";
    size_t i = 0;
    for (const auto& item : data) {
        if (i++ > 0) { y_ss << ","; l_ss << ","; }
        if constexpr (detail::PairLike<decltype(item)>) {
            l_ss << detail::escape_json(std::string_view(item.first));
            y_ss << detail::to_json_val(item.second);
        } else if constexpr (detail::HasNameAndValue<decltype(item)>) {
            l_ss << detail::escape_json(std::string_view(item.name));
            y_ss << detail::to_json_val(item.value);
        }
    }
    y_ss << "]";
    l_ss << "]";

    std::ostringstream ss;
    ss << "{\"kind\":\"pie\",\"series\":[{\"y\":" << y_ss.str() << ",\"labels\":" << l_ss.str() << "}]";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(CHART_MIME, ss.str());
}

// 5. General Chart (Multi-series or records)
template <typename T>
inline DisplayHandle chart(const T& data, std::string_view title = "") {
    if constexpr (detail::Iterable<T>) {
        using Elem = std::decay_t<decltype(*std::begin(data))>;
        if constexpr (detail::PairLike<Elem>) {
            using ValType = std::decay_t<decltype(std::begin(data)->second)>;
            if constexpr (detail::Iterable<ValType>) {
                // Multi-series line chart: map of name -> values
                std::ostringstream ss;
                ss << "{\"kind\":\"line\",\"series\":[";
                size_t i = 0;
                for (const auto& s : data) {
                    if (i++ > 0) ss << ",";
                    ss << "{\"name\":" << detail::escape_json(std::string_view(s.first))
                       << ",\"y\":" << detail::to_json_array(s.second) << "}";
                }
                ss << "]";
                if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
                ss << "}";
                return detail::emit_display(CHART_MIME, ss.str());
            }
        }
    }

    // Otherwise records with labels + y:
    std::ostringstream y_ss;
    std::ostringstream l_ss;
    y_ss << "[";
    l_ss << "[";
    size_t i = 0;
    for (const auto& item : data) {
        if (i++ > 0) { y_ss << ","; l_ss << ","; }
        if constexpr (detail::PairLike<decltype(item)>) {
            l_ss << detail::escape_json(std::string_view(item.first));
            y_ss << detail::to_json_val(item.second);
        } else if constexpr (detail::HasNameAndValue<decltype(item)>) {
            l_ss << detail::escape_json(std::string_view(item.name));
            y_ss << detail::to_json_val(item.value);
        }
    }
    y_ss << "]";
    l_ss << "]";

    std::ostringstream ss;
    ss << "{\"kind\":\"line\",\"series\":[{\"y\":" << y_ss.str() << ",\"labels\":" << l_ss.str() << "}]";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(CHART_MIME, ss.str());
}

// 6. Histogram
template <typename T>
inline DisplayHandle histogram(const T& data, std::string_view title = "", int bins = 0) {
    std::ostringstream ss;
    ss << "{\"kind\":\"histogram\"";
    if (bins > 0) ss << ",\"bins\":" << bins;
    ss << ",\"series\":[{\"values\":" << detail::to_json_array(data) << "}]";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(CHART_MIME, ss.str());
}

// 7. Scatter 3D
template <typename T>
inline DisplayHandle scatter3d(const T& data, std::string_view title = "") {
    std::ostringstream xs, ys, zs;
    xs << "["; ys << "["; zs << "[";
    size_t i = 0;
    for (const auto& pt : data) {
        if (i++ > 0) { xs << ","; ys << ","; zs << ","; }
        if constexpr (requires { std::get<0>(pt); }) {
            xs << detail::to_json_val(std::get<0>(pt));
            ys << detail::to_json_val(std::get<1>(pt));
            zs << detail::to_json_val(std::get<2>(pt));
        } else {
            auto it = std::begin(pt);
            xs << detail::to_json_val(*it); ++it;
            ys << detail::to_json_val(*it); ++it;
            zs << detail::to_json_val(*it);
        }
    }
    xs << "]"; ys << "]"; zs << "]";

    std::ostringstream ss;
    ss << "{\"kind\":\"scatter\",\"series\":[{\"x\":" << xs.str() << ",\"y\":" << ys.str() << ",\"z\":" << zs.str() << "}]";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(PLOT3D_MIME, ss.str());
}

// 8. Surface 3D
template <typename T>
inline DisplayHandle surface3d(const T& data, std::string_view title = "") {
    size_t rows = 0;
    size_t cols = 0;
    std::ostringstream z_ss;
    z_ss << "[";
    for (const auto& row : data) {
        if (rows++ > 0) z_ss << ",";
        z_ss << detail::to_json_array(row);
        size_t c = 0;
        for (const auto& cell : row) { (void)cell; c++; }
        if (c > cols) cols = c;
    }
    z_ss << "]";

    double max_x = (cols > 1) ? static_cast<double>(cols - 1) : 1.0;
    double max_y = (rows > 1) ? static_cast<double>(rows - 1) : 1.0;

    std::ostringstream ss;
    ss << "{\"kind\":\"surface\",\"surface\":{\"x\":{\"min\":0,\"max\":" << max_x
       << "},\"y\":{\"min\":0,\"max\":" << max_y << "},\"z\":" << z_ss.str() << "}";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(PLOT3D_MIME, ss.str());
}

// 9. Graph 3D
template <typename T>
inline DisplayHandle graph3d(const T& data, std::string_view title = "") {
    std::set<std::string> nodes_set;
    std::vector<std::pair<std::string, std::string>> edges;

    for (const auto& entry : data) {
        std::string src(entry.first);
        nodes_set.insert(src);
        for (const auto& dst_item : entry.second) {
            std::string dst(dst_item);
            nodes_set.insert(dst);
            edges.emplace_back(src, dst);
        }
    }

    std::ostringstream ss;
    ss << "{\"kind\":\"graph\",\"graph\":{\"directed\":true,\"nodes\":[";
    size_t ni = 0;
    for (const auto& n : nodes_set) {
        if (ni++ > 0) ss << ",";
        ss << "{\"id\":" << detail::escape_json(n) << "}";
    }
    ss << "],\"edges\":[";
    size_t ei = 0;
    for (const auto& e : edges) {
        if (ei++ > 0) ss << ",";
        ss << "{\"from\":" << detail::escape_json(e.first) << ",\"to\":" << detail::escape_json(e.second) << "}";
    }
    ss << "]}";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(PLOT3D_MIME, ss.str());
}

// 10. Matrix
template <typename T>
inline DisplayHandle matrix(const T& data, std::string_view title = "") {
    std::ostringstream val_ss;
    std::ostringstream cell_ss;
    val_ss << "[";
    cell_ss << "[";
    size_t r = 0;
    size_t cell_count = 0;
    for (const auto& row : data) {
        if (r > 0) val_ss << ",";
        val_ss << "[";
        size_t c = 0;
        for (const auto& val : row) {
            if (c > 0) val_ss << ",";
            val_ss << detail::to_json_val(val);
            if (cell_count++ > 0) cell_ss << ",";
            int v = 0;
            if constexpr (detail::is_num<decltype(val)>()) v = static_cast<int>(val);
            std::string terrain = (v > 0) ? "land" : "water";
            cell_ss << "{\"row\":" << r << ",\"col\":" << c << ",\"terrain\":\"" << terrain << "\"}";
            c++;
        }
        val_ss << "]";
        r++;
    }
    val_ss << "]";
    cell_ss << "]";

    std::ostringstream ss;
    ss << "{\"kind\":\"matrix\",\"state\":{\"grid\":{\"values\":" << val_ss.str()
       << ",\"cells\":" << cell_ss.str() << ",\"inferTerrain\":false}}"
       << ",\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(VISUALIZER_MIME, ss.str());
}

// 11. Islands
template <typename T>
inline DisplayHandle islands(const T& data, std::string_view title = "") {
    std::ostringstream val_ss;
    std::ostringstream cell_ss;
    val_ss << "[";
    cell_ss << "[";
    size_t r = 0;
    size_t cell_count = 0;
    for (const auto& row : data) {
        if (r > 0) val_ss << ",";
        val_ss << "[";
        size_t c = 0;
        for (const auto& val : row) {
            if (c > 0) val_ss << ",";
            val_ss << detail::to_json_val(val);
            if (cell_count++ > 0) cell_ss << ",";
            int v = 0;
            if constexpr (detail::is_num<decltype(val)>()) v = static_cast<int>(val);
            std::string terrain = (v > 0) ? "land" : "water";
            cell_ss << "{\"row\":" << r << ",\"col\":" << c << ",\"terrain\":\"" << terrain << "\"}";
            c++;
        }
        val_ss << "]";
        r++;
    }
    val_ss << "]";
    cell_ss << "]";

    std::ostringstream ss;
    ss << "{\"kind\":\"islands\",\"state\":{\"grid\":{\"values\":" << val_ss.str()
       << ",\"cells\":" << cell_ss.str() << ",\"inferTerrain\":false}}"
       << ",\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(VISUALIZER_MIME, ss.str());
}

// 12. Array with Pointers
template <typename T, typename P>
inline DisplayHandle array(const T& values, const P& pointers, std::string_view title = "") {
    static const std::vector<std::string> colors = {
        "#38bdf8", "#a855f7", "#f43f5e", "#10b981", "#eab308", "#06b6d4"
    };

    std::ostringstream pt_ss;
    pt_ss << "[";
    size_t pi = 0;
    for (const auto& ptr : pointers) {
        if (pi > 0) pt_ss << ",";
        std::string name;
        int at = 0;
        if constexpr (detail::PairLike<decltype(ptr)>) {
            name = std::string(ptr.first);
            at = static_cast<int>(ptr.second);
        }
        std::string color = colors[pi % colors.size()];
        pt_ss << "{\"name\":" << detail::escape_json(name)
              << ",\"at\":" << at
              << ",\"color\":\"" << color << "\"}";
        pi++;
    }
    pt_ss << "]";

    std::ostringstream ss;
    ss << "{\"kind\":\"arrayPointers\",\"state\":{\"array\":{\"values\":"
       << detail::to_json_array(values) << "}},\"pointers\":" << pt_ss.str()
       << ",\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(VISUALIZER_MIME, ss.str());
}

// 13. Tree (Pointer-based or Level-order vector)
namespace detail {

struct InternalNode {
    std::string id;
    std::string value;
    std::string left;
    std::string right;
};

template <typename T>
inline void collect_tree_preorder(
    const std::shared_ptr<TreeNode<T>>& node,
    std::string_view id,
    int& counter,
    std::vector<InternalNode>& nodes)
{
    if (!node) return;
    InternalNode in;
    in.id = std::string(id);
    std::ostringstream ss;
    ss << node->val;
    in.value = ss.str();

    std::string left_id;
    if (node->left) {
        left_id = "node_" + std::to_string(++counter);
        in.left = left_id;
    }
    std::string right_id;
    if (node->right) {
        right_id = "node_" + std::to_string(++counter);
        in.right = right_id;
    }
    nodes.push_back(in);

    if (node->left) collect_tree_preorder(node->left, left_id, counter, nodes);
    if (node->right) collect_tree_preorder(node->right, right_id, counter, nodes);
}

} // namespace detail

template <typename T>
inline DisplayHandle tree(const std::shared_ptr<TreeNode<T>>& root, std::string_view title = "") {
    std::vector<detail::InternalNode> nodes;
    int counter = 1;
    if (root) {
        detail::collect_tree_preorder(root, "node_1", counter, nodes);
    }

    std::ostringstream ss;
    ss << "{\"kind\":\"tree\",\"state\":{\"tree\":{\"root\":\"node_1\",\"nodes\":[";
    for (size_t i = 0; i < nodes.size(); ++i) {
        if (i > 0) ss << ",";
        const auto& n = nodes[i];
        ss << "{\"id\":\"" << n.id << "\",\"value\":" << detail::escape_json(n.value);
        if (!n.left.empty()) ss << ",\"left\":\"" << n.left << "\"";
        if (!n.right.empty()) ss << ",\"right\":\"" << n.right << "\"";
        ss << "}";
    }
    ss << "]}},\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(VISUALIZER_MIME, ss.str());
}

template <typename T>
inline DisplayHandle tree(const TreeNode<T>& root, std::string_view title = "") {
    auto sp = std::make_shared<TreeNode<T>>(root);
    return tree(sp, title);
}

// Level-order array overload
template <typename T>
requires detail::Iterable<T>
inline DisplayHandle tree(const T& level_order, std::string_view title = "") {
    using Elem = std::decay_t<decltype(*std::begin(level_order))>;
    std::vector<std::optional<std::string>> items;
    for (const auto& item : level_order) {
        if constexpr (detail::OptionalLike<Elem>) {
            if (item.has_value()) {
                std::ostringstream ss;
                ss << *item;
                items.push_back(ss.str());
            } else {
                items.push_back(std::nullopt);
            }
        } else {
            std::ostringstream ss;
            ss << item;
            items.push_back(ss.str());
        }
    }

    if (items.empty() || !items[0].has_value()) {
        std::string spec = "{\"kind\":\"tree\",\"state\":{\"tree\":{\"root\":\"\",\"nodes\":[]}},\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true";
        if (!title.empty()) spec += ",\"title\":" + detail::escape_json(title);
        spec += "}";
        return detail::emit_display(VISUALIZER_MIME, spec);
    }

    // Build binary tree from level-order items
    struct BNode {
        std::string val;
        std::shared_ptr<BNode> left;
        std::shared_ptr<BNode> right;
        BNode(std::string v) : val(std::move(v)) {}
    };

    auto root = std::make_shared<BNode>(*items[0]);
    std::queue<std::shared_ptr<BNode>> q;
    q.push(root);
    size_t idx = 1;
    while (!q.empty() && idx < items.size()) {
        auto curr = q.front();
        q.pop();

        if (idx < items.size()) {
            if (items[idx].has_value()) {
                curr->left = std::make_shared<BNode>(*items[idx]);
                q.push(curr->left);
            }
            idx++;
        }
        if (idx < items.size()) {
            if (items[idx].has_value()) {
                curr->right = std::make_shared<BNode>(*items[idx]);
                q.push(curr->right);
            }
            idx++;
        }
    }

    // Collect preorder
    std::vector<detail::InternalNode> nodes;
    int counter = 1;
    std::function<void(const std::shared_ptr<BNode>&, std::string_view)> dfs =
        [&](const std::shared_ptr<BNode>& node, std::string_view id) {
            if (!node) return;
            detail::InternalNode in;
            in.id = std::string(id);
            in.value = node->val;
            std::string lid, rid;
            if (node->left) {
                lid = "node_" + std::to_string(++counter);
                in.left = lid;
            }
            if (node->right) {
                rid = "node_" + std::to_string(++counter);
                in.right = rid;
            }
            nodes.push_back(in);
            if (node->left) dfs(node->left, lid);
            if (node->right) dfs(node->right, rid);
        };

    dfs(root, "node_1");

    std::ostringstream ss;
    ss << "{\"kind\":\"tree\",\"state\":{\"tree\":{\"root\":\"node_1\",\"nodes\":[";
    for (size_t i = 0; i < nodes.size(); ++i) {
        if (i > 0) ss << ",";
        const auto& n = nodes[i];
        ss << "{\"id\":\"" << n.id << "\",\"value\":" << detail::escape_json(n.value);
        if (!n.left.empty()) ss << ",\"left\":\"" << n.left << "\"";
        if (!n.right.empty()) ss << ",\"right\":\"" << n.right << "\"";
        ss << "}";
    }
    ss << "]}},\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(VISUALIZER_MIME, ss.str());
}

// 14. Graph Visualizer
template <typename T>
inline DisplayHandle graph(const T& data, std::string_view title = "") {
    std::set<std::string> nodes_set;
    std::vector<std::pair<std::string, std::string>> edges;

    for (const auto& entry : data) {
        std::string src(entry.first);
        nodes_set.insert(src);
        for (const auto& dst_item : entry.second) {
            std::string dst(dst_item);
            nodes_set.insert(dst);
            edges.emplace_back(src, dst);
        }
    }

    std::ostringstream ss;
    ss << "{\"kind\":\"graph\",\"state\":{\"graph\":{\"directed\":true,\"nodes\":[";
    size_t ni = 0;
    for (const auto& n : nodes_set) {
        if (ni++ > 0) ss << ",";
        ss << "{\"id\":" << detail::escape_json(n) << "}";
    }
    ss << "],\"edges\":[";
    size_t ei = 0;
    for (const auto& e : edges) {
        if (ei++ > 0) ss << ",";
        ss << "{\"from\":" << detail::escape_json(e.first) << ",\"to\":" << detail::escape_json(e.second) << "}";
    }
    ss << "]}},\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(VISUALIZER_MIME, ss.str());
}

// 15. Linked List
template <typename T>
inline DisplayHandle linked_list(const std::shared_ptr<ListNode<T>>& head, std::string_view title = "") {
    std::ostringstream n_ss;
    n_ss << "[";
    auto curr = head;
    size_t i = 0;
    while (curr) {
        if (i > 0) n_ss << ",";
        std::string id = "n" + std::to_string(i);
        std::ostringstream val_ss;
        val_ss << curr->val;
        n_ss << "{\"id\":\"" << id << "\",\"value\":" << detail::escape_json(val_ss.str());
        if (curr->next) {
            n_ss << ",\"next\":\"n" << (i + 1) << "\"";
        }
        n_ss << "}";
        curr = curr->next;
        i++;
    }
    n_ss << "]";

    std::ostringstream ss;
    ss << "{\"kind\":\"linkedList\",\"state\":{\"linkedList\":{\"nodes\":" << n_ss.str()
       << ",\"markCycle\":false}},\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(VISUALIZER_MIME, ss.str());
}

template <typename T>
inline DisplayHandle linked_list(const ListNode<T>& head, std::string_view title = "") {
    auto sp = std::make_shared<ListNode<T>>(head);
    return linked_list(sp, title);
}

// 16. Bars Visualizer
template <typename T>
inline DisplayHandle bars(const T& data, std::string_view title = "") {
    int min_val = 0;
    int max_val = 0;
    bool first = true;
    for (const auto& item : data) {
        int v = static_cast<int>(item);
        if (first) {
            min_val = v;
            max_val = v;
            first = false;
        } else {
            if (v < min_val) min_val = v;
            if (v > max_val) max_val = v;
        }
    }
    if (min_val > 0) min_val = 0;

    std::ostringstream ss;
    ss << "{\"kind\":\"bars\",\"state\":{\"bars\":{\"values\":" << detail::to_json_array(data)
       << ",\"min\":" << min_val << ",\"max\":" << max_val
       << "}},\"showCoordinates\":true,\"showValues\":true,\"cellSize\":38,\"fitOnOpen\":true";
    if (!title.empty()) ss << ",\"title\":" << detail::escape_json(title);
    ss << "}";
    return detail::emit_display(VISUALIZER_MIME, ss.str());
}

// ── Timing & Process Events ───────────────────────────────────────────────

inline void wait(double seconds) {
    detail::ensure_event_socket();
    auto end = std::chrono::steady_clock::now() + std::chrono::duration<double>(seconds);
    while (std::chrono::steady_clock::now() < end) {
        std::this_thread::sleep_for(std::chrono::milliseconds(25));
    }
}

inline void process_events() {}

} // namespace fry

// ── Backward Compatible Display Class ──────────────────────────────────────

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

    template <typename T>
    static fry::DisplayHandle line_chart(const T& data, std::string_view title = "") {
        return fry::line_chart(data, title);
    }
    template <typename T>
    static fry::DisplayHandle scatter_chart(const T& data, std::string_view title = "") {
        return fry::scatter_chart(data, title);
    }
    template <typename T>
    static fry::DisplayHandle bar_chart(const T& data, std::string_view title = "") {
        return fry::bar_chart(data, title);
    }
    template <typename T>
    static fry::DisplayHandle chart(const T& data, std::string_view title = "") {
        return fry::chart(data, title);
    }
    template <typename T>
    static fry::DisplayHandle histogram(const T& data, std::string_view title = "", int bins = 0) {
        return fry::histogram(data, title, bins);
    }
    template <typename T>
    static fry::DisplayHandle pie_chart(const T& data, std::string_view title = "") {
        return fry::pie_chart(data, title);
    }
    template <typename T>
    static fry::DisplayHandle scatter3d(const T& data, std::string_view title = "") {
        return fry::scatter3d(data, title);
    }
    template <typename T>
    static fry::DisplayHandle surface3d(const T& data, std::string_view title = "") {
        return fry::surface3d(data, title);
    }
    template <typename T>
    static fry::DisplayHandle graph3d(const T& data, std::string_view title = "") {
        return fry::graph3d(data, title);
    }
    template <typename T>
    static fry::DisplayHandle matrix(const T& data, std::string_view title = "") {
        return fry::matrix(data, title);
    }
    template <typename T>
    static fry::DisplayHandle islands(const T& data, std::string_view title = "") {
        return fry::islands(data, title);
    }
    template <typename T, typename P>
    static fry::DisplayHandle array(const T& values, const P& pointers, std::string_view title = "") {
        return fry::array(values, pointers, title);
    }
    template <typename T>
    static fry::DisplayHandle tree(const T& root, std::string_view title = "") {
        return fry::tree(root, title);
    }
    template <typename T>
    static fry::DisplayHandle graph(const T& data, std::string_view title = "") {
        return fry::graph(data, title);
    }
    template <typename T>
    static fry::DisplayHandle linked_list(const T& head, std::string_view title = "") {
        return fry::linked_list(head, title);
    }
    template <typename T>
    static fry::DisplayHandle bars(const T& data, std::string_view title = "") {
        return fry::bars(data, title);
    }
};
