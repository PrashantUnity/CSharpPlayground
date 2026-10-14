import java.lang.reflect.Array;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.lang.reflect.Modifier;
import java.lang.reflect.RecordComponent;
import java.util.ArrayList;
import java.util.Collection;
import java.util.Comparator;
import java.util.IdentityHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

/**
 * The Object Explorer for Java: what the last expression of a notebook cell shows when it is an object, the way the
 * studio shows a C# value (ObjectInspectorBuilder): a scalar stays text, a short collection of scalars is one line
 * ("[ 1, 34, 45 ]"), anything else is a tree of its public fields and getters (a record's components, a map's entries,
 * a collection's items), each nested object a node that opens. Sent as application/vnd.fry.inspector+json.
 * Loaded into the kernel's JShell after Display, which carries the message.
 */
class FryInspector {
    static final String MIME = "application/vnd.fry.inspector+json";
    static final int MAX_DEPTH = 3;     // levels of objects below the one shown, as the C# inspector
    static final int MAX_ITEMS = 100;   // items of a collection or map listed
    static final int MAX_INLINE = 50;   // scalars of a collection shown on one line

    /**
     * Shows the value. Returns false when it is for the caller to print as text: null, a scalar, or an object
     * with nothing to list. A chart, plot or visualizer a cell ends with (Display's handle) is already on screen,
     * so it is taken as shown, as in every other language.
     */
    static boolean show(Object value) {
        if (value == null || isScalar(value)) return false;
        if (isShown(value)) return true;

        String inline = inlineScalars(value);
        if (inline != null) {
            System.out.println(inline);
            return true;
        }

        // a collection of records, beans or maps is the studio's table, as in Python and F#
        try {
            if (Display.showTableIfTabular(value)) return true;
        } catch (Throwable ignored) {
            // then it is listed as a tree
        }

        List<String> rows;
        try {
            IdentityHashMap<Object, Boolean> path = new IdentityHashMap<>();
            path.put(value, Boolean.TRUE);
            rows = rows(value, 0, path);
        } catch (Throwable t) {
            return false;
        }
        if (rows.isEmpty()) return false;

        String text;
        try {
            text = String.valueOf(value);
        } catch (Throwable t) {
            text = titleOf(value.getClass());
        }
        Display.emitRaw("{\"type\":\"display\",\"data\":{\"" + MIME + "\":" + node(value, rows, false) + ",\"text/plain\":" + quote(plain(text)) + "},\"metadata\":{}}");
        return true;
    }

    // ------------------------------------------------------------------------------------------------------- values

    static boolean isScalar(Object v) {
        return v != null && Display.isScalarValue(v);
    }

    // Display's handles carry _fry_shown: a value that was shown when it was made.
    static boolean isShown(Object value) {
        try {
            return value.getClass().getField("_fry_shown").getBoolean(value);
        } catch (Throwable t) {
            return false;
        }
    }

    static String format(Object v) {
        if (v instanceof Double d) {
            if (d.isNaN()) return "NaN";
            if (d.isInfinite()) return d > 0 ? "∞" : "-∞";
        }
        if (v instanceof Float f) {
            if (f.isNaN()) return "NaN";
            if (f.isInfinite()) return f > 0 ? "∞" : "-∞";
        }
        return String.valueOf(v);
    }

    /** One line for an array or collection whose items are all scalars (or null); null when it has anything else. */
    static String inlineScalars(Object obj) {
        List<Object> items = new ArrayList<>();
        boolean more = false;
        if (obj.getClass().isArray()) {
            int length = Array.getLength(obj);
            for (int i = 0; i < length; i++) {
                if (i > MAX_INLINE) { more = true; break; }
                items.add(Array.get(obj, i));
            }
        } else if (obj instanceof Collection<?> collection) {
            int i = 0;
            for (Object item : collection) {
                if (i++ > MAX_INLINE) { more = true; break; }
                items.add(item);
            }
        } else {
            return null;
        }

        StringBuilder sb = new StringBuilder("[ ");
        for (int i = 0; i < items.size(); i++) {
            Object item = items.get(i);
            if (i > 0) sb.append(", ");
            if (item == null) sb.append("<null>");
            else if (isScalar(item)) sb.append(format(item));
            else return null;
        }
        if (more) sb.append(items.isEmpty() ? "..." : ", ...");
        return sb.append(" ]").toString();
    }

    // ------------------------------------------------------------------------------------------------------- tree

    static String titleOf(Class<?> type) {
        if (type.isArray()) return titleOf(type.getComponentType()) + "[]";
        // The JDK's own hidden classes (List.of makes a "List12") are named for what they are.
        if (type.getName().startsWith("java.") && !Modifier.isPublic(type.getModifiers())) {
            for (Class<?> kind : new Class<?>[] { List.class, java.util.Set.class, Map.class, Collection.class }) {
                if (kind.isAssignableFrom(type)) return kind.getSimpleName();
            }
        }
        String name = type.getSimpleName();
        return name.isEmpty() ? type.getName() : name;
    }

    // A JShell class is REPL.$JShell$27$People: the part before its own name says nothing.
    static String plain(String text) {
        return text.replaceAll("REPL\\.\\$JShell\\$\\d+\\$", "");
    }

    static String node(Object obj, List<String> rows, boolean circular) {
        return "{\"title\":" + quote(titleOf(obj.getClass())) + (circular ? ",\"circular\":true" : "")
            + ",\"properties\":[" + String.join(",", rows) + "]}";
    }

    static String row(String name, Object value, int depth, IdentityHashMap<Object, Boolean> path) {
        String key = "{\"name\":" + quote(name);
        if (value == null) return key + ",\"value\":\"<null>\",\"isNull\":true}";
        if (isScalar(value)) {
            boolean text = value instanceof CharSequence || value instanceof Character;
            return key + ",\"value\":" + quote(format(value)) + ",\"isString\":" + text + "}";
        }
        String inline = inlineScalars(value);
        if (inline != null) return key + ",\"value\":" + quote(inline) + "}";
        if (depth > MAX_DEPTH) return key + ",\"child\":" + node(value, List.of(), false) + "}";

        // A reference back to an object above is listed again (flagged), down to the depth limit, as the desktop's C# inspector does.
        boolean circular = path.containsKey(value);
        path.put(value, Boolean.TRUE);
        try {
            return key + ",\"child\":" + node(value, rows(value, depth, path), circular) + "}";
        } finally {
            if (!circular) path.remove(value);
        }
    }

    static List<String> rows(Object obj, int depth, IdentityHashMap<Object, Boolean> path) {
        List<String> rows = new ArrayList<>();
        int next = depth + 1;

        if (obj instanceof Map<?, ?> map) {
            int i = 0;
            for (Map.Entry<?, ?> entry : map.entrySet()) {
                if (i++ >= MAX_ITEMS) { rows.add(truncated(i - 1)); break; }
                rows.add(row(String.valueOf(entry.getKey()), entry.getValue(), next, path));
            }
        } else if (obj instanceof Iterable<?> items) {
            int i = 0;
            for (Object item : items) {
                if (i >= MAX_ITEMS) { rows.add(truncated(i)); break; }
                rows.add(row("[" + i++ + "]", item, next, path));
            }
        } else if (obj.getClass().isArray()) {
            int length = Array.getLength(obj);
            for (int i = 0; i < length; i++) {
                if (i >= MAX_ITEMS) { rows.add(truncated(i)); break; }
                rows.add(row("[" + i + "]", Array.get(obj, i), next, path));
            }
        } else {
            members(obj, next, path, rows);
        }
        return rows;
    }

    static String truncated(int shown) {
        return "{\"name\":\"...\",\"value\":\"(" + shown + "+ items truncated)\"}";
    }

    // A record's components; otherwise the public fields, then the public getters (getX(), isX()) that no field covers.
    static void members(Object obj, int depth, IdentityHashMap<Object, Boolean> path, List<String> rows) {
        Class<?> type = obj.getClass();

        if (type.isRecord()) {
            for (RecordComponent component : type.getRecordComponents()) {
                rows.add(read(component.getName(), () -> {
                    Method accessor = component.getAccessor();
                    accessor.setAccessible(true);
                    return accessor.invoke(obj);
                }, depth, path));
            }
            return;
        }

        List<String> names = new ArrayList<>();
        for (Field field : type.getFields()) {
            if (Modifier.isStatic(field.getModifiers()) || field.isSynthetic()) continue;
            names.add(field.getName());
            rows.add(read(field.getName(), () -> field.get(obj), depth, path));
        }

        List<Method> getters = new ArrayList<>();
        for (Method method : type.getMethods()) {
            if (isGetter(method)) getters.add(method);
        }
        getters.sort(Comparator.comparing(FryInspector::propertyName));
        for (Method getter : getters) {
            String name = propertyName(getter);
            if (names.contains(name)) continue;
            names.add(name);
            rows.add(read(name, () -> {
                getter.setAccessible(true);
                return getter.invoke(obj);
            }, depth, path));
        }
    }

    interface Read { Object get() throws Exception; }

    static String read(String name, Read read, int depth, IdentityHashMap<Object, Boolean> path) {
        Object value;
        try {
            value = read.get();
        } catch (Throwable t) {
            Throwable cause = t.getCause() != null ? t.getCause() : t;
            return "{\"name\":" + quote(name) + ",\"value\":" + quote("<Error: " + cause.getMessage() + ">") + "}";
        }
        return row(name, value, depth, path);
    }

    static boolean isGetter(Method m) {
        if (Modifier.isStatic(m.getModifiers()) || m.isSynthetic() || m.isBridge()) return false;
        if (m.getParameterCount() != 0 || m.getDeclaringClass() == Object.class) return false;
        Class<?> returns = m.getReturnType();
        if (returns == void.class) return false;
        String name = m.getName();
        if (name.equals("getClass") || name.equals("hashCode") || name.equals("toString")) return false;
        return (name.startsWith("get") && name.length() > 3)
            || (name.startsWith("is") && name.length() > 2 && (returns == boolean.class || returns == Boolean.class));
    }

    static String propertyName(Method m) {
        String name = m.getName();
        String rest = name.startsWith("get") ? name.substring(3) : name.substring(2);
        return rest.length() > 1 && Character.isUpperCase(rest.charAt(1)) ? rest : Character.toLowerCase(rest.charAt(0)) + rest.substring(1);
    }

    // ------------------------------------------------------------------------------------------------------- json

    static String quote(String s) {
        StringBuilder sb = new StringBuilder("\"");
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (c == '"') sb.append("\\\"");
            else if (c == '\\') sb.append("\\\\");
            else if (c == '\n') sb.append("\\n");
            else if (c == '\r') sb.append("\\r");
            else if (c == '\t') sb.append("\\t");
            else if (c < 32) sb.append(String.format("\\u%04x", (int) c));
            else sb.append(c);
        }
        return sb.append('"').toString();
    }
}
