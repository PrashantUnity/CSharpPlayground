using System.IO;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// Provides zero-configuration runtime support for Java scripts in C# Code Studio,
/// generating the <c>Display</c> helper class so user scripts can call <c>Display.dump(data)</c>,
/// <c>Display.table(data)</c>, <c>Display.html(...)</c>, and <c>Display.image(...)</c> out of the box.
/// </summary>
public static class JavaDisplayRuntime
{
    public static async Task<IReadOnlyList<string>> EnsureSourceFilesAsync(string srcDir, string? package, CancellationToken ct = default)
    {
        var files = new List<string>();
        try
        {
            Directory.CreateDirectory(srcDir);

            // 1. Generate Display and Visualizer in the user's declared package (or default package if none)
            string userPkgDir = !string.IsNullOrWhiteSpace(package)
                ? Path.Combine(srcDir, package.Replace('.', Path.DirectorySeparatorChar))
                : srcDir;
            Directory.CreateDirectory(userPkgDir);

            var userDisplayPath = Path.Combine(userPkgDir, "Display.java");
            await File.WriteAllTextAsync(userDisplayPath, GenerateDisplayJavaSource(package), ct).ConfigureAwait(false);
            files.Add(userDisplayPath);

            var userVisualizerPath = Path.Combine(userPkgDir, "Visualizer.java");
            await File.WriteAllTextAsync(userVisualizerPath, GenerateVisualizerJavaSource(package), ct).ConfigureAwait(false);
            files.Add(userVisualizerPath);

            // 2. Also generate under package 'fry' so scripts can use `import fry.Display;` / `import fry.Visualizer;`
            if (!string.Equals(package, "fry", StringComparison.OrdinalIgnoreCase))
            {
                var fryDir = Path.Combine(srcDir, "fry");
                Directory.CreateDirectory(fryDir);
                var fryDisplayPath = Path.Combine(fryDir, "Display.java");
                await File.WriteAllTextAsync(fryDisplayPath, GenerateDisplayJavaSource("fry"), ct).ConfigureAwait(false);
                files.Add(fryDisplayPath);

                var fryVisualizerPath = Path.Combine(fryDir, "Visualizer.java");
                await File.WriteAllTextAsync(fryVisualizerPath, GenerateVisualizerJavaSource("fry"), ct).ConfigureAwait(false);
                files.Add(fryVisualizerPath);
            }

            // 3. Also generate under package 'com.frypdf.display' so scripts can use `import com.frypdf.display.Display;` / `import com.frypdf.display.Visualizer;`
            if (!string.Equals(package, "com.frypdf.display", StringComparison.OrdinalIgnoreCase))
            {
                var comDir = Path.Combine(srcDir, "com", "frypdf", "display");
                Directory.CreateDirectory(comDir);
                var comDisplayPath = Path.Combine(comDir, "Display.java");
                await File.WriteAllTextAsync(comDisplayPath, GenerateDisplayJavaSource("com.frypdf.display"), ct).ConfigureAwait(false);
                files.Add(comDisplayPath);

                var comVisualizerPath = Path.Combine(comDir, "Visualizer.java");
                await File.WriteAllTextAsync(comVisualizerPath, GenerateVisualizerJavaSource("com.frypdf.display"), ct).ConfigureAwait(false);
                files.Add(comVisualizerPath);
            }
        }
        catch
        {
            // Fallback gracefully if filesystem operations fail
        }

        return files;
    }

    private static string? _cachedDisplaySource;
    private static string? _cachedVisualizerSource;

    public static string GenerateDisplayJavaSource(string? package)
    {
        var pkgHeader = !string.IsNullOrWhiteSpace(package) ? $"package {package};\n\n" : string.Empty;
        return pkgHeader + GetBaseDisplaySource();
    }

    public static string GenerateVisualizerJavaSource(string? package)
    {
        var pkgHeader = !string.IsNullOrWhiteSpace(package) ? $"package {package};\n\n" : string.Empty;
        return pkgHeader + GetBaseVisualizerSource();
    }

    private static string GetBaseVisualizerSource()
    {
        if (_cachedVisualizerSource != null) return _cachedVisualizerSource;
        try
        {
            using var stream = typeof(JavaDisplayRuntime).Assembly.GetManifestResourceStream("JavaRuntime.Visualizer.java");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                return _cachedVisualizerSource = reader.ReadToEnd();
            }
        }
        catch { }

        var devPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Services", "Languages", "Java", "Runtime", "Visualizer.java");
        if (File.Exists(devPath))
        {
            try { return _cachedVisualizerSource = File.ReadAllText(devPath); } catch { }
        }

        return VisualizerJavaFallback;
    }

    private const string VisualizerJavaFallback = "// Companion to public class Display for FryPDF C# Code Studio\npublic class Visualizer { }";

    private static string GetBaseDisplaySource()
    {
        if (_cachedDisplaySource != null) return _cachedDisplaySource;
        try
        {
            using var stream = typeof(JavaDisplayRuntime).Assembly.GetManifestResourceStream("JavaRuntime.Display.java");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                _cachedDisplaySource = reader.ReadToEnd();
                return _cachedDisplaySource;
            }
        }
        catch { }

        var devPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Services", "Languages", "Java", "Runtime", "Display.java");
        if (File.Exists(devPath))
        {
            try { return _cachedDisplaySource = File.ReadAllText(devPath); } catch { }
        }

        return DisplayJavaTemplate;
    }

    private const string DisplayJavaTemplate = """
import java.io.*;
import java.lang.reflect.*;
import java.util.*;

/**
 * Interactive Display and Visual Dump API for FryPDF C# Code Studio.
 * Allows Java scripts to emit interactive tables, charts, HTML, and images
 * directly to the studio's Results (.Dump) deck.
 */
public class Display {
    private static final String MARKER = "__FRY_DISPLAY__ ";
    private static final String TABLE_MIME = "application/vnd.fry.table+json";

    public static <T> T dump(T obj) {
        return dump((String) null, obj);
    }

    public static <T> T dump(String title, T obj) {
        table(title, obj);
        return obj;
    }

    public static <T> T dump(T obj, String title) {
        return dump(title, obj);
    }

    public static void table(Object obj) {
        table(null, obj);
    }

    public static void table(String title, Object obj) {
        try {
            String bundleJson = formatTableBundle(title, obj);
            emit(bundleJson);
        } catch (Throwable t) {
            System.err.println("[Display] table error: " + t.getMessage());
        }
    }

    public static void show(Object obj) {
        dump(obj);
    }

    public static void show(String title, Object obj) {
        dump(title, obj);
    }

    public static void html(String htmlContent) {
        if (htmlContent == null) htmlContent = "";
        String json = "{\"type\":\"display\",\"data\":{\"text/html\":" + quote(htmlContent) + "},\"metadata\":{}}";
        emit(json);
    }

    public static void image(byte[] imageBytes) {
        image(imageBytes, "PNG");
    }

    public static void image(byte[] imageBytes, String format) {
        if (imageBytes == null || imageBytes.length == 0) return;
        String b64 = Base64.getEncoder().encodeToString(imageBytes);
        String mime = (format != null && format.equalsIgnoreCase("JPEG")) ? "image/jpeg" : "image/png";
        String json = "{\"type\":\"display\",\"data\":{\"" + mime + "\":\"" + b64 + "\"},\"metadata\":{}}";
        emit(json);
    }

    public static void image(String base64OrPath) {
        if (base64OrPath == null || base64OrPath.trim().isEmpty()) return;
        base64OrPath = base64OrPath.trim();
        if (base64OrPath.startsWith("data:image")) {
            int comma = base64OrPath.indexOf(',');
            if (comma >= 0) base64OrPath = base64OrPath.substring(comma + 1);
        }
        File file = new File(base64OrPath);
        if (file.exists() && file.isFile()) {
            try {
                byte[] bytes = new byte[(int) file.length()];
                try (FileInputStream fis = new FileInputStream(file)) {
                    int read = fis.read(bytes);
                    if (read > 0) {
                        String ext = file.getName().toLowerCase();
                        String fmt = (ext.endsWith(".jpg") || ext.endsWith(".jpeg")) ? "JPEG" : "PNG";
                        image(bytes, fmt);
                        return;
                    }
                }
            } catch (Throwable ignored) {}
        }
        String json = "{\"type\":\"display\",\"data\":{\"image/png\":\"" + base64OrPath + "\"},\"metadata\":{}}";
        emit(json);
    }

    public static void json(String jsonContent) {
        json(null, jsonContent);
    }

    public static void json(String title, String jsonContent) {
        if (jsonContent == null) jsonContent = "null";
        String displayTitle = (title != null && !title.isEmpty()) ? title : "JSON";
        String bundle = "{\"type\":\"display\",\"data\":{\"text/plain\":" + quote(jsonContent) + "},\"metadata\":{}}";
        emit(bundle);
    }

    private static void emit(String protocolJson) {
        System.out.println(MARKER + protocolJson);
        System.out.flush();
    }

    private static String formatTableBundle(String title, Object obj) {
        if (obj == null) {
            String t = title != null ? title : "null";
            return "{\"type\":\"display\",\"data\":{\"" + TABLE_MIME + "\":{\"title\":" + quote(t) + ",\"columns\":[\"Value\"],\"numeric\":[false],\"rows\":[[\"null\"]],\"totalRows\":1,\"totalColumns\":1}},\"metadata\":{}}";
        }

        Class<?> clazz = obj.getClass();

        // 1. Primitive or Object array
        if (clazz.isArray()) {
            int len = Array.getLength(obj);
            Class<?> comp = clazz.getComponentType();
            String autoTitle = title != null ? title : (comp.getSimpleName() + "[" + len + "]");

            // 2D Array
            if (comp.isArray()) {
                int maxCols = 0;
                for (int i = 0; i < len; i++) {
                    Object rowObj = Array.get(obj, i);
                    if (rowObj != null) maxCols = Math.max(maxCols, Array.getLength(rowObj));
                }
                StringBuilder cols = new StringBuilder("[");
                StringBuilder num = new StringBuilder("[");
                for (int c = 0; c < maxCols; c++) {
                    if (c > 0) { cols.append(","); num.append(","); }
                    cols.append("\"[").append(c).append("]\"");
                    num.append("false");
                }
                cols.append("]"); num.append("]");

                StringBuilder rows = new StringBuilder("[");
                for (int r = 0; r < len; r++) {
                    if (r > 0) rows.append(",");
                    rows.append("[");
                    Object rowObj = Array.get(obj, r);
                    int rLen = rowObj != null ? Array.getLength(rowObj) : 0;
                    for (int c = 0; c < maxCols; c++) {
                        if (c > 0) rows.append(",");
                        if (c < rLen) rows.append(toJsonValue(Array.get(rowObj, c)));
                        else rows.append("null");
                    }
                    rows.append("]");
                }
                rows.append("]");
                return buildTableJson(autoTitle, cols.toString(), num.toString(), rows.toString(), len, maxCols);
            }

            // 1D Array
            boolean isNum = isNumeric(comp);
            StringBuilder rows = new StringBuilder("[");
            for (int i = 0; i < len; i++) {
                if (i > 0) rows.append(",");
                rows.append("[").append(i).append(",").append(toJsonValue(Array.get(obj, i))).append("]");
            }
            rows.append("]");
            String cols = "[\"Index\",\"Value\"]";
            String num = "[true," + isNum + "]";
            return buildTableJson(autoTitle, cols, num, rows.toString(), len, 2);
        }

        // 2. Collection
        if (obj instanceof Collection<?>) {
            List<?> list = new ArrayList<>((Collection<?>) obj);
            int len = list.size();
            String autoTitle = title != null ? title : ("Collection[" + len + "]");
            if (len == 0) {
                return buildTableJson(autoTitle, "[\"Value\"]", "[false]", "[]", 0, 1);
            }

            Object first = list.get(0);
            if (first instanceof Map<?, ?>) {
                Map<?, ?> map0 = (Map<?, ?>) first;
                List<String> colNames = new ArrayList<>();
                for (Object k : map0.keySet()) colNames.add(String.valueOf(k));
                StringBuilder cols = new StringBuilder("[");
                StringBuilder num = new StringBuilder("[");
                for (int i = 0; i < colNames.size(); i++) {
                    if (i > 0) { cols.append(","); num.append(","); }
                    cols.append(quote(colNames.get(i)));
                    num.append("false");
                }
                cols.append("]"); num.append("]");

                StringBuilder rows = new StringBuilder("[");
                for (int r = 0; r < len; r++) {
                    if (r > 0) rows.append(",");
                    rows.append("[");
                    Map<?, ?> curMap = (Map<?, ?>) list.get(r);
                    for (int c = 0; c < colNames.size(); c++) {
                        if (c > 0) rows.append(",");
                        rows.append(toJsonValue(curMap != null ? curMap.get(colNames.get(c)) : null));
                    }
                    rows.append("]");
                }
                rows.append("]");
                return buildTableJson(autoTitle, cols.toString(), num.toString(), rows.toString(), len, colNames.size());
            }

            // Scalars
            if (first == null || isScalar(first.getClass())) {
                StringBuilder rows = new StringBuilder("[");
                for (int i = 0; i < len; i++) {
                    if (i > 0) rows.append(",");
                    rows.append("[").append(i).append(",").append(toJsonValue(list.get(i))).append("]");
                }
                rows.append("]");
                return buildTableJson(autoTitle, "[\"Index\",\"Value\"]", "[true,false]", rows.toString(), len, 2);
            }

            // POJO / Record / JavaBean via getters
            List<Method> getters = getPublicGetters(first.getClass());
            if (!getters.isEmpty()) {
                StringBuilder cols = new StringBuilder("[");
                StringBuilder num = new StringBuilder("[");
                for (int i = 0; i < getters.size(); i++) {
                    if (i > 0) { cols.append(","); num.append(","); }
                    cols.append(quote(getterToPropName(getters.get(i).getName())));
                    num.append(isNumeric(getters.get(i).getReturnType()));
                }
                cols.append("]"); num.append("]");

                StringBuilder rows = new StringBuilder("[");
                for (int r = 0; r < len; r++) {
                    if (r > 0) rows.append(",");
                    rows.append("[");
                    Object item = list.get(r);
                    for (int c = 0; c < getters.size(); c++) {
                        if (c > 0) rows.append(",");
                        Object val = null;
                        try { if (item != null) val = getters.get(c).invoke(item); } catch (Throwable ignored) {}
                        rows.append(toJsonValue(val));
                    }
                    rows.append("]");
                }
                rows.append("]");
                return buildTableJson(autoTitle, cols.toString(), num.toString(), rows.toString(), len, getters.size());
            }

            // Fallback 1D list
            StringBuilder rows = new StringBuilder("[");
            for (int i = 0; i < len; i++) {
                if (i > 0) rows.append(",");
                rows.append("[").append(i).append(",").append(toJsonValue(list.get(i))).append("]");
            }
            rows.append("]");
            return buildTableJson(autoTitle, "[\"Index\",\"Value\"]", "[true,false]", rows.toString(), len, 2);
        }

        // 3. Map
        if (obj instanceof Map<?, ?>) {
            Map<?, ?> map = (Map<?, ?>) obj;
            int len = map.size();
            String autoTitle = title != null ? title : ("Map[" + len + "]");
            StringBuilder rows = new StringBuilder("[");
            int i = 0;
            for (Map.Entry<?, ?> entry : map.entrySet()) {
                if (i++ > 0) rows.append(",");
                rows.append("[").append(toJsonValue(entry.getKey())).append(",").append(toJsonValue(entry.getValue())).append("]");
            }
            rows.append("]");
            return buildTableJson(autoTitle, "[\"Key\",\"Value\"]", "[false,false]", rows.toString(), len, 2);
        }

        // 4. Single Object inspection
        String autoTitle = title != null ? title : clazz.getSimpleName();
        List<Method> getters = getPublicGetters(clazz);
        if (!getters.isEmpty()) {
            StringBuilder rows = new StringBuilder("[");
            int r = 0;
            for (Method g : getters) {
                if (r++ > 0) rows.append(",");
                Object val = null;
                try { val = g.invoke(obj); } catch (Throwable ignored) {}
                rows.append("[").append(quote(getterToPropName(g.getName()))).append(",").append(toJsonValue(val)).append("]");
            }
            rows.append("]");
            return buildTableJson(autoTitle, "[\"Property\",\"Value\"]", "[false,false]", rows.toString(), getters.size(), 2);
        }

        // 5. Scalar
        return buildTableJson(autoTitle, "[\"Value\"]", "[false]", "[[" + toJsonValue(obj) + "]]", 1, 1);
    }

    private static String buildTableJson(String title, String colsJson, String numJson, String rowsJson, int totalRows, int totalCols) {
        return "{\"type\":\"display\",\"data\":{\"" + TABLE_MIME + "\":{\"title\":" + quote(title) + ",\"columns\":" + colsJson + ",\"numeric\":" + numJson + ",\"rows\":" + rowsJson + ",\"totalRows\":" + totalRows + ",\"totalColumns\":" + totalCols + "}},\"metadata\":{}}";
    }

    private static boolean isNumeric(Class<?> c) {
        return c == int.class || c == long.class || c == double.class || c == float.class ||
               c == short.class || c == byte.class || Number.class.isAssignableFrom(c);
    }

    private static boolean isScalar(Class<?> c) {
        return isNumeric(c) || c == boolean.class || c == Boolean.class ||
               c == char.class || c == Character.class || CharSequence.class.isAssignableFrom(c);
    }

    private static List<Method> getPublicGetters(Class<?> clazz) {
        List<Method> list = new ArrayList<>();
        for (Method m : clazz.getMethods()) {
            if (m.getParameterCount() != 0 || m.getReturnType() == void.class) continue;
            if (m.getDeclaringClass() == Object.class) continue;
            String name = m.getName();
            if ((name.startsWith("get") && name.length() > 3) || (name.startsWith("is") && name.length() > 2)) {
                list.add(m);
            }
        }
        return list;
    }

    private static String getterToPropName(String getterName) {
        int prefix = getterName.startsWith("get") ? 3 : 2;
        String prop = getterName.substring(prefix);
        if (prop.isEmpty()) return getterName;
        return Character.toLowerCase(prop.charAt(0)) + prop.substring(1);
    }

    private static String toJsonValue(Object o) {
        if (o == null) return "null";
        if (o instanceof Number || o instanceof Boolean) return o.toString();
        if (o instanceof CharSequence || o instanceof Character) return quote(o.toString());
        return quote(String.valueOf(o));
    }

    private static String quote(String s) {
        if (s == null) return "\"\"";
        StringBuilder sb = new StringBuilder("\"");
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (c == '"') sb.append("\\\"");
            else if (c == '\\') sb.append("\\\\");
            else if (c == '\n') sb.append("\\n");
            else if (c == '\r') sb.append("\\r");
            else if (c == '\t') sb.append("\\t");
            else sb.append(c);
        }
        return sb.append('"').toString();
    }
}
""";
}
