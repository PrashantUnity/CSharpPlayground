import java.io.*;
import java.util.*;
import jdk.jshell.*;

/**
 * Java Notebook Kernel for FryPDF C# Code Studio.
 * Speaks the line-delimited JSON Fry Kernel Protocol over stdin/stdout pipes.
 */
public class FryKernel {
    private static JShell jshell;
    private static String currentRequestId = null;
    private static int executionCount = 0;
    private static final PrintStream protocolOut = System.out;
    private static final ByteArrayOutputStream snippetOutput = new ByteArrayOutputStream();

    public static void main(String[] args) throws Exception {
        String version = System.getProperty("java.version");
        String javaHome = System.getProperty("java.home");
        String javaExe = javaHome + File.separator + "bin" + File.separator + "java";

        PrintStream snippetPs = new PrintStream(snippetOutput, true);
        jshell = JShell.builder().out(snippetPs).err(snippetPs).build();

        // Default imports
        evalSnippet("import java.util.*;");
        evalSnippet("import java.util.stream.*;");
        evalSnippet("import java.io.*;");

        // Display queue helper
        evalSnippet("""
        class FryDisplay {
            public static final List<Object> pending = new ArrayList<>();
            public static void show(Object obj) { pending.add(obj); }
        }
        class Display {
            public static <T> T dump(T obj) { FryDisplay.show(obj); return obj; }
            public static <T> T dump(String title, T obj) { FryDisplay.show(obj); return obj; }
            public static void table(Object obj) { FryDisplay.show(obj); }
            public static void table(String title, Object obj) { FryDisplay.show(obj); }
            public static void show(Object obj) { FryDisplay.show(obj); }
        }
        """);
        evalSnippet("void display(Object obj) { FryDisplay.show(obj); }");
        evalSnippet("void dump(Object obj) { FryDisplay.show(obj); }");

        // JSON serializer helper
        evalSnippet("""
        class __FryJson {
            public static String toJson(Object o) {
                if (o == null) return "null";
                if (o instanceof Number || o instanceof Boolean) return o.toString();
                if (o instanceof CharSequence) {
                    String s = o.toString();
                    StringBuilder sb = new StringBuilder("\\"");
                    for (int i = 0; i < s.length(); i++) {
                        char c = s.charAt(i);
                        if (c == '"') sb.append("\\\\\\\"");
                        else if (c == '\\\\') sb.append("\\\\\\\\");
                        else if (c == '\\n') sb.append("\\\\n");
                        else if (c == '\\r') sb.append("\\\\r");
                        else if (c == '\\t') sb.append("\\\\t");
                        else sb.append(c);
                    }
                    return sb.append('"').toString();
                }
                if (o instanceof int[] arr) return Arrays.toString(arr).replaceAll(" ", "");
                if (o instanceof long[] arr) return Arrays.toString(arr).replaceAll(" ", "");
                if (o instanceof double[] arr) return Arrays.toString(arr).replaceAll(" ", "");
                if (o instanceof boolean[] arr) return Arrays.toString(arr).replaceAll(" ", "");
                if (o instanceof Object[] arr) {
                    StringBuilder sb = new StringBuilder("[");
                    for (int i = 0; i < arr.length; i++) {
                        if (i > 0) sb.append(",");
                        sb.append(toJson(arr[i]));
                    }
                    return sb.append("]").toString();
                }
                if (o instanceof Collection<?> col) {
                    StringBuilder sb = new StringBuilder("[");
                    int i = 0;
                    for (Object item : col) {
                        if (i++ > 0) sb.append(",");
                        sb.append(toJson(item));
                    }
                    return sb.append("]").toString();
                }
                if (o instanceof Map<?,?> map) {
                    StringBuilder sb = new StringBuilder("{");
                    int i = 0;
                    for (Map.Entry<?,?> entry : map.entrySet()) {
                        if (i++ > 0) sb.append(",");
                        sb.append(toJson(String.valueOf(entry.getKey()))).append(":").append(toJson(entry.getValue()));
                    }
                    return sb.append("}").toString();
                }
                return toJson(String.valueOf(o));
            }
        }
        """);

        // Send ready message
        send(Map.of(
            "type", "ready",
            "language", "java",
            "version", version,
            "executable", javaExe
        ));

        BufferedReader reader = new BufferedReader(new InputStreamReader(System.in));
        String line;
        while ((line = reader.readLine()) != null) {
            line = line.trim();
            if (line.isEmpty()) continue;
            handleMessage(line);
        }
    }

    private static void evalSnippet(String code) {
        SourceCodeAnalysis sca = jshell.sourceCodeAnalysis();
        String remaining = code;
        while (!remaining.trim().isEmpty()) {
            SourceCodeAnalysis.CompletionInfo info = sca.analyzeCompletion(remaining);
            if (info.source().trim().isEmpty()) break;
            jshell.eval(info.source());
            remaining = info.remaining();
        }
    }

    private static void handleMessage(String jsonLine) {
        Map<String, Object> msg = parseJsonMap(jsonLine);
        String type = (String) msg.get("type");
        String id = (String) msg.get("id");

        if ("execute".equals(type)) {
            handleExecute(id, (String) msg.get("code"), (String) msg.get("cell"));
        } else if ("variables".equals(type)) {
            handleVariables(id);
        } else if ("get_value".equals(type)) {
            handleGetValue(id, (String) msg.get("name"));
        } else if ("set_value".equals(type)) {
            handleSetValue(id, (String) msg.get("name"), (String) msg.get("json"));
        } else if ("shutdown".equals(type)) {
            System.exit(0);
        }
    }

    private static void handleExecute(String id, String code, String cell) {
        currentRequestId = id;
        executionCount++;
        snippetOutput.reset();

        SourceCodeAnalysis sca = jshell.sourceCodeAnalysis();
        String remaining = code;
        boolean hasError = false;
        String lastValue = null;

        while (!remaining.trim().isEmpty()) {
            SourceCodeAnalysis.CompletionInfo info = sca.analyzeCompletion(remaining);
            if (info.source().trim().isEmpty()) break;

            List<SnippetEvent> events = jshell.eval(info.source());
            for (SnippetEvent e : events) {
                if (e.status() == Snippet.Status.REJECTED) {
                    hasError = true;
                    StringBuilder diag = new StringBuilder();
                    jshell.diagnostics(e.snippet()).forEach(d -> {
                        diag.append(d.getMessage(null)).append("\n");
                    });
                    sendError(id, "CompileError", diag.toString(), cell);
                    break;
                }
                if (e.exception() != null) {
                    hasError = true;
                    sendError(id, e.exception().getClass().getSimpleName(), e.exception().getMessage(), cell);
                    break;
                }
                if (e.value() != null && !e.value().isEmpty()) {
                    lastValue = e.value();
                }
            }

            if (hasError) break;
            remaining = info.remaining();
        }

        // Check for rich displays queued by display(...)
        checkAndSendDisplays(id);

        // Flush any output captured
        String captured = snippetOutput.toString();
        if (!captured.isEmpty()) {
            sendStream(id, "stdout", captured);
        } else if (!hasError && lastValue != null) {
            sendStream(id, "stdout", lastValue + "\n");
        }

        if (hasError) {
            send(Map.of("type", "reply", "id", id, "status", "error"));
        } else {
            send(Map.of("type", "reply", "id", id, "status", "ok", "executionCount", executionCount));
        }

        currentRequestId = null;
    }

    private static void checkAndSendDisplays(String id) {
        try {
            var sizeEv = jshell.eval("FryDisplay.pending.size()");
            if (!sizeEv.isEmpty() && sizeEv.get(0).value() != null) {
                int count = Integer.parseInt(sizeEv.get(0).value());
                for (int i = 0; i < count; i++) {
                    var itemEv = jshell.eval("__FryJson.toJson(FryDisplay.pending.get(" + i + "))");
                    if (!itemEv.isEmpty() && itemEv.get(0).value() != null) {
                        String raw = unquote(itemEv.get(0).value());
                        Object parsed = new JsonParser(raw).parse();
                        Map<String, Object> bundle = new LinkedHashMap<>();
                        if (parsed instanceof List<?> list && !list.isEmpty() && list.get(0) instanceof Map<?,?> firstRow) {
                            List<String> cols = new ArrayList<>();
                            for (Object k : ((Map<?,?>) firstRow).keySet()) cols.add(String.valueOf(k));
                            List<List<Object>> rows = new ArrayList<>();
                            for (Object r : list) {
                                if (r instanceof Map<?,?> rowMap) {
                                    List<Object> row = new ArrayList<>();
                                    for (String c : cols) row.add(rowMap.get(c));
                                    rows.add(row);
                                }
                            }
                            bundle.put("application/vnd.fry.table+json", Map.of(
                                "title", "Table",
                                "columns", cols,
                                "numeric", cols.stream().map(c -> false).toList(),
                                "rows", rows,
                                "totalRows", rows.size(),
                                "totalColumns", cols.size()
                            ));
                        } else if (parsed instanceof List<?> list && !list.isEmpty()) {
                            List<List<Object>> rows = new ArrayList<>();
                            int idx = 0;
                            for (Object item : list) {
                                rows.add(List.of(idx++, item != null ? item : "null"));
                            }
                            bundle.put("application/vnd.fry.table+json", Map.of(
                                "title", "List[" + list.size() + "]",
                                "columns", List.of("Index", "Value"),
                                "numeric", List.of(true, false),
                                "rows", rows,
                                "totalRows", rows.size(),
                                "totalColumns", 2
                            ));
                        }
                        bundle.put("text/plain", raw);
                        send(Map.of("type", "display", "id", id, "data", bundle, "metadata", Map.of()));
                    }
                }
                jshell.eval("FryDisplay.pending.clear();");
            }
        } catch (Exception ignored) {}
    }

    private static void handleVariables(String id) {
        List<Map<String, Object>> vars = new ArrayList<>();
        for (VarSnippet v : jshell.variables().toList()) {
            if (v.name().startsWith("__") || v.name().startsWith("FryDisplay") || v.name().startsWith("$")) continue;
            vars.add(Map.of(
                "name", v.name(),
                "type", v.typeName(),
                "value", jshell.varValue(v),
                "kind", "variable"
            ));
        }
        send(Map.of("type", "reply", "id", id, "status", "ok", "variables", vars));
    }

    private static void handleGetValue(String id, String name) {
        try {
            var events = jshell.eval("__FryJson.toJson(" + name + ")");
            if (!events.isEmpty() && events.get(0).status() == Snippet.Status.VALID && events.get(0).value() != null) {
                String raw = unquote(events.get(0).value());
                send(Map.of("type", "reply", "id", id, "status", "ok", "json", raw));
                return;
            }
        } catch (Exception ignored) {}

        send(Map.of("type", "reply", "id", id, "status", "error", "message", "Java has no variable named '" + name + "'."));
    }

    private static void handleSetValue(String id, String name, String json) {
        try {
            Object obj = new JsonParser(json).parse();
            String literal = toJavaLiteral(obj);
            String snippet = "var " + name + " = " + literal + ";";
            var events = jshell.eval(snippet);
            for (SnippetEvent e : events) {
                if (e.status() == Snippet.Status.REJECTED) {
                    send(Map.of("type", "reply", "id", id, "status", "error", "message", "Could not set Java variable: " + name));
                    return;
                }
            }
            send(Map.of("type", "reply", "id", id, "status", "ok"));
        } catch (Exception e) {
            send(Map.of("type", "reply", "id", id, "status", "error", "message", e.getMessage() != null ? e.getMessage() : "Error parsing JSON"));
        }
    }

    private static String unquote(String raw) {
        if (raw.startsWith("\"") && raw.endsWith("\"")) {
            Object parsed = new JsonParser(raw).parse();
            if (parsed instanceof String s) return s;
        }
        return raw;
    }

    private static void sendStream(String id, String name, String text) {
        send(Map.of("type", "stream", "id", id, "name", name, "text", text));
    }

    private static void sendError(String id, String ename, String evalue, String cell) {
        send(Map.of("type", "error", "id", id, "ename", ename, "evalue", evalue, "traceback", evalue, "line", 1));
    }

    private static void send(Map<String, Object> map) {
        StringBuilder sb = new StringBuilder("{");
        int i = 0;
        for (Map.Entry<String, Object> e : map.entrySet()) {
            if (i++ > 0) sb.append(",");
            sb.append("\"").append(e.getKey()).append("\":").append(formatJsonValue(e.getValue()));
        }
        sb.append("}\n");
        protocolOut.print(sb.toString());
        protocolOut.flush();
    }

    private static String formatJsonValue(Object o) {
        if (o == null) return "null";
        if (o instanceof Number || o instanceof Boolean) return o.toString();
        if (o instanceof String s) {
            return "\"" + s.replace("\\", "\\\\").replace("\"", "\\\"").replace("\n", "\\n").replace("\r", "\\r").replace("\t", "\\t") + "\"";
        }
        if (o instanceof List<?> list) {
            StringBuilder sb = new StringBuilder("[");
            for (int i = 0; i < list.size(); i++) {
                if (i > 0) sb.append(",");
                sb.append(formatJsonValue(list.get(i)));
            }
            return sb.append("]").toString();
        }
        if (o instanceof Map<?,?> map) {
            StringBuilder sb = new StringBuilder("{");
            int i = 0;
            for (Map.Entry<?,?> e : map.entrySet()) {
                if (i++ > 0) sb.append(",");
                sb.append("\"").append(e.getKey()).append("\":").append(formatJsonValue(e.getValue()));
            }
            return sb.append("}").toString();
        }
        return "\"" + o.toString() + "\"";
    }

    private static String toJavaLiteral(Object obj) {
        if (obj == null) return "null";
        if (obj instanceof Boolean || obj instanceof Number) return obj.toString();
        if (obj instanceof String s) {
            return "\"" + s.replace("\\", "\\\\").replace("\"", "\\\"").replace("\n", "\\n").replace("\r", "\\r").replace("\t", "\\t") + "\"";
        }
        if (obj instanceof List<?> list) {
            if (list.isEmpty()) return "new java.util.ArrayList<>()";
            if (list.stream().allMatch(x -> x instanceof Integer)) {
                return "new int[]{ " + list.stream().map(Object::toString).reduce((a, b) -> a + ", " + b).orElse("") + " }";
            }
            if (list.stream().allMatch(x -> x instanceof Number)) {
                return "new double[]{ " + list.stream().map(x -> ((Number)x).doubleValue() + "").reduce((a, b) -> a + ", " + b).orElse("") + " }";
            }
            if (list.stream().allMatch(x -> x instanceof String)) {
                return "new String[]{ " + list.stream().map(FryKernel::toJavaLiteral).reduce((a, b) -> a + ", " + b).orElse("") + " }";
            }
            return "java.util.List.of(" + list.stream().map(FryKernel::toJavaLiteral).reduce((a, b) -> a + ", " + b).orElse("") + ")";
        }
        if (obj instanceof Map<?, ?> map) {
            if (map.isEmpty()) return "new java.util.LinkedHashMap<String, Object>()";
            if (map.size() <= 10) {
                StringBuilder sb = new StringBuilder("java.util.Map.of(");
                int i = 0;
                for (Map.Entry<?, ?> e : map.entrySet()) {
                    if (i++ > 0) sb.append(", ");
                    sb.append(toJavaLiteral(String.valueOf(e.getKey()))).append(", ").append(toJavaLiteral(e.getValue()));
                }
                return sb.append(")").toString();
            } else {
                StringBuilder sb = new StringBuilder("java.util.Map.ofEntries(");
                int i = 0;
                for (Map.Entry<?, ?> e : map.entrySet()) {
                    if (i++ > 0) sb.append(", ");
                    sb.append("java.util.Map.entry(").append(toJavaLiteral(String.valueOf(e.getKey()))).append(", ").append(toJavaLiteral(e.getValue())).append(")");
                }
                return sb.append(")").toString();
            }
        }
        return "null";
    }

    private static Map<String, Object> parseJsonMap(String json) {
        Object o = new JsonParser(json).parse();
        if (o instanceof Map<?,?> m) {
            Map<String, Object> res = new LinkedHashMap<>();
            for (Map.Entry<?,?> e : m.entrySet()) res.put(String.valueOf(e.getKey()), e.getValue());
            return res;
        }
        return Collections.emptyMap();
    }

    public static class JsonParser {
        private final String src;
        private int pos = 0;

        public JsonParser(String src) { this.src = src; }

        public Object parse() {
            skipWhitespace();
            if (pos >= src.length()) return null;
            char c = src.charAt(pos);
            if (c == 'n') { pos += 4; return null; }
            if (c == 't') { pos += 4; return Boolean.TRUE; }
            if (c == 'f') { pos += 5; return Boolean.FALSE; }
            if (c == '"') return parseString();
            if (c == '[') return parseArray();
            if (c == '{') return parseObject();
            return parseNumber();
        }

        private void skipWhitespace() {
            while (pos < src.length() && Character.isWhitespace(src.charAt(pos))) pos++;
        }

        private String parseString() {
            pos++;
            StringBuilder sb = new StringBuilder();
            while (pos < src.length()) {
                char c = src.charAt(pos++);
                if (c == '"') return sb.toString();
                if (c == '\\' && pos < src.length()) {
                    char esc = src.charAt(pos++);
                    if (esc == '"') sb.append('"');
                    else if (esc == '\\') sb.append('\\');
                    else if (esc == '/') sb.append('/');
                    else if (esc == 'b') sb.append('\b');
                    else if (esc == 'f') sb.append('\f');
                    else if (esc == 'n') sb.append('\n');
                    else if (esc == 'r') sb.append('\r');
                    else if (esc == 't') sb.append('\t');
                    else if (esc == 'u' && pos + 4 <= src.length()) {
                        String hex = src.substring(pos, pos + 4);
                        pos += 4;
                        try {
                            sb.append((char) Integer.parseInt(hex, 16));
                        } catch (NumberFormatException ignored) {
                            sb.append("\\u").append(hex);
                        }
                    } else {
                        sb.append(esc);
                    }
                } else {
                    sb.append(c);
                }
            }
            return sb.toString();
        }

        private List<Object> parseArray() {
            pos++;
            List<Object> list = new ArrayList<>();
            skipWhitespace();
            if (pos < src.length() && src.charAt(pos) == ']') { pos++; return list; }
            while (pos < src.length()) {
                list.add(parse());
                skipWhitespace();
                if (pos < src.length() && src.charAt(pos) == ',') { pos++; }
                else if (pos < src.length() && src.charAt(pos) == ']') { pos++; break; }
            }
            return list;
        }

        private Map<String, Object> parseObject() {
            pos++;
            Map<String, Object> map = new LinkedHashMap<>();
            skipWhitespace();
            if (pos < src.length() && src.charAt(pos) == '}') { pos++; return map; }
            while (pos < src.length()) {
                skipWhitespace();
                String key = parseString();
                skipWhitespace();
                if (pos < src.length() && src.charAt(pos) == ':') pos++;
                Object val = parse();
                map.put(key, val);
                skipWhitespace();
                if (pos < src.length() && src.charAt(pos) == ',') { pos++; }
                else if (pos < src.length() && src.charAt(pos) == '}') { pos++; break; }
            }
            return map;
        }

        private Number parseNumber() {
            int start = pos;
            if (pos < src.length() && (src.charAt(pos) == '-' || src.charAt(pos) == '+')) pos++;
            boolean isFloat = false;
            while (pos < src.length() && (Character.isDigit(src.charAt(pos)) || src.charAt(pos) == '.' || src.charAt(pos) == 'e' || src.charAt(pos) == 'E')) {
                if (src.charAt(pos) == '.' || src.charAt(pos) == 'e' || src.charAt(pos) == 'E') isFloat = true;
                pos++;
            }
            String numStr = src.substring(start, pos);
            if (isFloat) return Double.parseDouble(numStr);
            long val = Long.parseLong(numStr);
            if (val >= Integer.MIN_VALUE && val <= Integer.MAX_VALUE) return (int) val;
            return val;
        }
    }
}
