import java.io.*;
import java.lang.reflect.*;
import java.net.*;
import java.nio.file.*;
import java.util.*;
import java.util.concurrent.*;
import java.util.function.Consumer;

/**
 * Interactive Display and Visual Dump API for FrySharp (Java).
 */
public class Display {
    public static final String DISPLAY_MARKER = "__FRY_DISPLAY__";
    public static final String CHART_MIME = "application/vnd.fry.chart.v1+json";
    public static final String PLOT3D_MIME = "application/vnd.fry.plot3d.v1+json";
    public static final String VISUALIZER_MIME = "application/vnd.fry.visualizer.v1+json";
    public static final String TABLE_MIME = "application/vnd.fry.table+json";

    public static class TreeNode {
        public Object val;
        public TreeNode left;
        public TreeNode right;
        public TreeNode(Object val) { this.val = val; }
        public TreeNode(Object val, TreeNode left, TreeNode right) {
            this.val = val;
            this.left = left;
            this.right = right;
        }
    }

    public static class ListNode {
        public Object val;
        public ListNode next;
        public ListNode(Object val) { this.val = val; }
        public ListNode(Object val, ListNode next) {
            this.val = val;
            this.next = next;
        }
    }

    private static final ConcurrentHashMap<String, DisplayHandle> activeHandles = new ConcurrentHashMap<>();
    private static Socket eventSocket = null;
    private static boolean socketConnecting = false;

    // ------------------------------------------------------------------------------------------------------- socket
    private static synchronized void ensureEventSocket() {
        String addr = System.getenv("FRY_EVENTS");
        if (addr == null || addr.isEmpty() || eventSocket != null || socketConnecting) return;
        socketConnecting = true;
        Thread t = new Thread(() -> {
            try {
                String[] parts = addr.split(":");
                String host = parts[0];
                int port = Integer.parseInt(parts[1]);
                Socket s = new Socket(host, port);
                synchronized (Display.class) {
                    eventSocket = s;
                    socketConnecting = false;
                }
                String token = System.getenv("FRY_EVENTS_TOKEN");
                if (token == null) token = "";
                OutputStream out = s.getOutputStream();
                out.write(("{\"type\":\"hello\",\"token\":\"" + escapeJson(token) + "\"}\n").getBytes("UTF-8"));
                out.flush();

                BufferedReader reader = new BufferedReader(new InputStreamReader(s.getInputStream(), "UTF-8"));
                String line;
                while ((line = reader.readLine()) != null) {
                    line = line.trim();
                    if (!line.isEmpty()) {
                        deliverEventLine(line);
                    }
                }
            } catch (Exception ignored) {
            } finally {
                synchronized (Display.class) {
                    eventSocket = null;
                    socketConnecting = false;
                }
            }
        }, "fry-event-socket");
        t.setDaemon(true);
        t.start();
    }

    public static void deliverEventLine(String line) {
        try {
            int atDisplay = line.indexOf("\"display_id\"");
            if (atDisplay < 0) return;
            int colon = line.indexOf(':', atDisplay);
            int q1 = line.indexOf('"', colon);
            int q2 = line.indexOf('"', q1 + 1);
            String displayId = line.substring(q1 + 1, q2);
            DisplayHandle handle = activeHandles.get(displayId);
            if (handle != null) {
                handle.dispatchRawEvent(line);
            }
        } catch (Exception ignored) {}
    }

    public interface Sink {
        void emit(String json);
    }
    private static volatile Sink customSink = null;
    public static void setSink(Sink sink) { customSink = sink; }

    public static Map<String, Object> map(Object... kvs) {
        Map<String, Object> m = new LinkedHashMap<>();
        for (int i = 0; i + 1 < kvs.length; i += 2) {
            m.put(String.valueOf(kvs[i]), kvs[i + 1]);
        }
        return m;
    }

    public static void emitRaw(String json) {
        if (customSink != null) {
            customSink.emit(json);
            return;
        }
        System.out.println(DISPLAY_MARKER + " " + json);
        System.out.flush();
    }

    // ------------------------------------------------------------------------------------------------------- handles
    public static class DisplayHandle {
        public final String mime;
        public volatile Map<String, Object> spec;
        public final String displayId;
        public final boolean _fry_shown = true;
        private final ConcurrentHashMap<String, List<Consumer<Map<String, Object>>>> listeners = new ConcurrentHashMap<>();
        private final ScheduledExecutorService throttleExecutor = Executors.newSingleThreadScheduledExecutor(r -> {
            Thread t = new Thread(r, "fry-throttle");
            t.setDaemon(true);
            return t;
        });
        private volatile Map<String, Object> pendingUpdate = null;
        private volatile ScheduledFuture<?> pendingTask = null;

        public DisplayHandle(String mime, Map<String, Object> spec, String displayId) {
            this.mime = mime;
            this.spec = spec;
            this.displayId = displayId;
            activeHandles.put(displayId, this);
        }

        public DisplayHandle update(String title) {
            Map<String, Object> copy = new LinkedHashMap<>(spec);
            copy.put("title", title);
            return update(copy);
        }

        public DisplayHandle update(Map<String, Object> newSpec) {
            this.spec = new LinkedHashMap<>(newSpec);
            synchronized (this) {
                pendingUpdate = this.spec;
                if (pendingTask == null || pendingTask.isDone()) {
                    pendingTask = throttleExecutor.schedule(this::flushUpdate, 33, TimeUnit.MILLISECONDS);
                }
            }
            return this;
        }

        private void flushUpdate() {
            Map<String, Object> toSend;
            synchronized (this) {
                toSend = pendingUpdate;
                pendingUpdate = null;
                pendingTask = null;
            }
            if (toSend != null) {
                emitRaw(buildMessage("update_display", mime, toSend, displayId));
            }
        }

        public DisplayHandle on(String event, Consumer<Map<String, Object>> listener) {
            String ev = event.toLowerCase();
            listeners.computeIfAbsent(ev, k -> new CopyOnWriteArrayList<>()).add(listener);
            ensureEventSocket();
            emitRaw("{\"type\":\"subscribe\",\"display_id\":\"" + displayId + "\",\"events\":[\"" + ev + "\"]}");
            return this;
        }

        public DisplayHandle onClick(Consumer<Map<String, Object>> listener) { return on("click", listener); }
        public DisplayHandle onSelect(Consumer<Map<String, Object>> listener) { return on("select", listener); }
        public DisplayHandle onStep(Consumer<Map<String, Object>> listener) { return on("step", listener); }

        public DisplayHandle off(String event) {
            String ev = event.toLowerCase();
            listeners.remove(ev);
            emitRaw("{\"type\":\"unsubscribe\",\"display_id\":\"" + displayId + "\",\"events\":[\"" + ev + "\"]}");
            return this;
        }

        public void close() {
            activeHandles.remove(displayId);
            throttleExecutor.shutdown();
        }

        public void dispatchRawEvent(String line) {
            Object parsedObj = new JsonParser(line).parse();
            if (!(parsedObj instanceof Map<?, ?> rootMap)) return;
            Object evObj = rootMap.get("event");
            Map<String, Object> eventData = evObj instanceof Map ? (Map<String, Object>) evObj : (Map<String, Object>) rootMap;
            String kind = String.valueOf(eventData.getOrDefault("event", "click")).toLowerCase();
            List<Consumer<Map<String, Object>>> list = listeners.get(kind);
            if (list != null) {
                for (Consumer<Map<String, Object>> c : list) {
                    try {
                        c.accept(eventData);
                    } catch (Exception ex) {
                        System.err.println("[Display event error]: " + ex);
                    }
                }
            }
        }
    }

    private static String newId() {
        return UUID.randomUUID().toString().replace("-", "").substring(0, 12);
    }

    private static String buildMessage(String type, String mime, Map<String, Object> spec, String displayId) {
        String specJson = toJson(spec);
        String kind = String.valueOf(spec.getOrDefault("kind", "visual"));
        String title = spec.containsKey("title") ? ": " + spec.get("title") : "";
        String fallback = kind + " " + title;
        return "{\"type\":\"" + type + "\",\"data\":{\"" + mime + "\":" + specJson +
               ",\"text/plain\":\"" + escapeJson(fallback) + "\"},\"metadata\":{},\"transient\":{\"display_id\":\"" + displayId + "\"}}";
    }

    public static DisplayHandle sendDisplay(String mime, Map<String, Object> spec) {
        String id = newId();
        emitRaw(buildMessage("display", mime, spec, id));
        return new DisplayHandle(mime, spec, id);
    }

    public static <T> T dump(T obj) { return dump((String) null, obj); }
    public static <T> T dump(String title, T obj) { table(title, obj); return obj; }
    public static <T> T dump(T obj, String title) { return dump(title, obj); }
    public static <T> T show(T obj) { return dump(obj); }
    public static <T> T show(String title, T obj) { return dump(title, obj); }
    public static <T> T show(T obj, String title) { return dump(title, obj); }

    public static void table(Object obj) { table(null, obj); }
    public static void table(String title, Object obj) {
        emitRaw("{\"type\":\"display\",\"data\":{\"" + TABLE_MIME + "\":" + formatTable(title, obj) + "},\"metadata\":{}}");
    }

    public static void html(String htmlContent) {
        emitRaw("{\"type\":\"display\",\"data\":{\"text/html\":\"" + escapeJson(htmlContent) + "\"},\"metadata\":{}}");
    }

    public static void markdown(String content) {
        emitRaw("{\"type\":\"display\",\"data\":{\"text/markdown\":\"" + escapeJson(content) + "\"},\"metadata\":{}}");
    }

    public static void image(byte[] bytes) { image(bytes, "PNG"); }
    public static void image(byte[] bytes, String format) {
        if (bytes == null) return;
        String b64 = Base64.getEncoder().encodeToString(bytes);
        String mime = "image/" + format.toLowerCase();
        emitRaw("{\"type\":\"display\",\"data\":{\"" + mime + "\":\"" + b64 + "\"},\"metadata\":{}}");
    }

    public static void image(String path) {
        try {
            File f = new File(path);
            if (f.exists()) {
                byte[] bytes = Files.readAllBytes(f.toPath());
                String ext = path.toLowerCase().endsWith(".jpg") || path.toLowerCase().endsWith(".jpeg") ? "jpeg" : "png";
                image(bytes, ext);
            }
        } catch (Exception ignored) {}
    }

    public static void json(Object obj) {
        emitRaw("{\"type\":\"display\",\"data\":{\"application/json\":" + toJson(obj) + "},\"metadata\":{}}");
    }

    // Charts
    public static DisplayHandle lineChart(Object data) { return lineChart(data, null); }
    public static DisplayHandle lineChart(Object data, String title) { return sendDisplay(CHART_MIME, chartSpec("line", data, title, null)); }

    public static DisplayHandle scatterChart(Object data) { return scatterChart(data, null); }
    public static DisplayHandle scatterChart(Object data, String title) { return sendDisplay(CHART_MIME, chartSpec("scatter", data, title, null)); }

    public static DisplayHandle barChart(Object data) { return barChart(data, null); }
    public static DisplayHandle barChart(Object data, String title) { return sendDisplay(CHART_MIME, chartSpec("bar", data, title, null)); }

    public static DisplayHandle chart(Object data) { return chart(data, null); }
    public static DisplayHandle chart(Object data, String title) { return sendDisplay(CHART_MIME, chartSpec("line", data, title, null)); }

    public static DisplayHandle pieChart(Object data) { return pieChart(data, null); }
    public static DisplayHandle pieChart(Object data, String title) { return sendDisplay(CHART_MIME, chartSpec("pie", data, title, null)); }

    public static DisplayHandle donutChart(Object data) { return donutChart(data, null); }
    public static DisplayHandle donutChart(Object data, String title) { return sendDisplay(CHART_MIME, chartSpec("donut", data, title, null)); }

    public static DisplayHandle histogram(Object data, String title, int bins) {
        Map<String, Object> extra = new LinkedHashMap<>();
        extra.put("bins", bins);
        return sendDisplay(CHART_MIME, chartSpec("histogram", data, title, extra));
    }
    public static DisplayHandle histogram(Object data, String title) { return histogram(data, title, 10); }

    // 3D
    public static DisplayHandle scatter3d(Object data) { return scatter3d(data, null); }
    public static DisplayHandle scatter3d(Object data, String title) { return sendDisplay(PLOT3D_MIME, plot3dSpec("scatter", data, title)); }

    public static DisplayHandle surface3d(Object data) { return surface3d(data, null); }
    public static DisplayHandle surface3d(Object data, String title) { return sendDisplay(PLOT3D_MIME, plot3dSpec("surface", data, title)); }

    public static DisplayHandle graph3d(Object data) { return graph3d(data, null); }
    public static DisplayHandle graph3d(Object data, String title) { return sendDisplay(PLOT3D_MIME, plot3dSpec("graph", data, title)); }

    public static DisplayHandle voxelBars(Object data) { return voxelBars(data, null); }
    public static DisplayHandle voxelBars(Object data, String title) { return sendDisplay(PLOT3D_MIME, plot3dSpec("voxelBar", data, title)); }

    @FunctionalInterface
    public interface DoubleBinaryFunction {
        double apply(double x, double y);
    }

    public static DisplayHandle plot3d(String title, DoubleBinaryFunction func, double minX, double maxX, double minY, double maxY, int res) {
        int n = Math.max(2, res);
        List<List<Object>> zGrid = new ArrayList<>();
        for (int r = 0; r < n; r++) {
            double y = minY + (maxY - minY) * r / (n - 1);
            List<Object> row = new ArrayList<>();
            for (int c = 0; c < n; c++) {
                double x = minX + (maxX - minX) * c / (n - 1);
                try {
                    row.add(func.apply(x, y));
                } catch (Exception e) {
                    row.add(null);
                }
            }
            zGrid.add(row);
        }
        Map<String, Object> surface = new LinkedHashMap<>();
        surface.put("x", Map.of("min", minX, "max", maxX));
        surface.put("y", Map.of("min", minY, "max", maxY));
        surface.put("z", zGrid);

        Map<String, Object> spec = new LinkedHashMap<>();
        spec.put("kind", "surface");
        spec.put("surface", surface);
        if (title != null) spec.put("title", title);
        return sendDisplay(PLOT3D_MIME, spec);
    }

    public static DisplayHandle plot3d(DoubleBinaryFunction func, double minX, double maxX, double minY, double maxY, int res, String title) {
        return plot3d(title, func, minX, maxX, minY, maxY, res);
    }

    public static DisplayHandle plot3d(Object data, String title) { return surface3d(data, title); }
    public static DisplayHandle plot3d(Object data) { return surface3d(data, null); }


    // Visualizers
    public static DisplayHandle matrix(Object grid) { return matrix(grid, null); }
    public static DisplayHandle matrix(Object grid, String title) { return sendDisplay(VISUALIZER_MIME, matrixSpec(grid, title)); }

    public static DisplayHandle islands(Object grid) { return islands(grid, null); }
    public static DisplayHandle islands(Object grid, String title) { return sendDisplay(VISUALIZER_MIME, islandsSpec(grid, title)); }

    public static DisplayHandle array(Object values, Object pointers, String title) {
        return sendDisplay(VISUALIZER_MIME, arraySpec(values, pointers, title));
    }

    public static DisplayHandle tree(Object root) { return tree(root, null); }
    public static DisplayHandle tree(Object root, String title) { return sendDisplay(VISUALIZER_MIME, treeSpec(root, title)); }

    public static DisplayHandle graph(Object graph) { return graph(graph, null); }
    public static DisplayHandle graph(Object graph, String title) { return sendDisplay(VISUALIZER_MIME, graphSpec(graph, title)); }

    public static DisplayHandle linkedList(Object head) { return linkedList(head, null); }
    public static DisplayHandle linkedList(Object head, String title) { return sendDisplay(VISUALIZER_MIME, linkedListSpec(head, title)); }

    public static DisplayHandle bars(Object values) { return bars(values, null); }
    public static DisplayHandle bars(Object values, String title) { return sendDisplay(VISUALIZER_MIME, barsSpec(values, title)); }

    public static void wait(double seconds) {
        ensureEventSocket();
        long end = System.currentTimeMillis() + (long)(seconds * 1000);
        while (System.currentTimeMillis() < end) {
            try {
                Thread.sleep(50);
            } catch (InterruptedException e) {
                break;
            }
        }
    }
    public static void wait(int seconds) { wait((double) seconds); }
    public static void waitFor(double seconds) { wait(seconds); }
    public static void waitLoop() { wait(3600.0); }
    public static void processEvents() {
        // Events are dispatched on receipt by reader thread
    }

    // ------------------------------------------------------------------------------------------------------ specs
    private static Map<String, Object> chartSpec(String kind, Object data, String title, Map<String, Object> extra) {
        Map<String, Object> spec = new LinkedHashMap<>();
        spec.put("kind", kind);
        if (extra != null) spec.putAll(extra);
        List<Map<String, Object>> series = new ArrayList<>();

        List<?> list = toList(data);
        if (list != null) {
            List<?> firstPair = toList(!list.isEmpty() ? list.get(0) : null);
            if (firstPair != null && firstPair.size() >= 2) {
                List<Object> x = new ArrayList<>();
                List<Object> y = new ArrayList<>();
                for (Object item : list) {
                    List<?> p = toList(item);
                    if (p != null && p.size() >= 2) {
                        x.add(toNum(p.get(0)));
                        y.add(toNum(p.get(1)));
                    }
                }
                Map<String, Object> s = new LinkedHashMap<>();
                s.put("x", x);
                s.put("y", y);
                series.add(s);
            } else if (!list.isEmpty() && list.get(0) instanceof Map<?, ?> record) {
                List<Object> y = new ArrayList<>();
                List<String> labels = new ArrayList<>();
                String valKey = findKey(record, "value", "y", "amount", "count", "total");
                String labelKey = findKey(record, "name", "x", "label", "key", "title");
                for (Object item : list) {
                    Map<?, ?> r = (Map<?, ?>) item;
                    y.add(toNum(r.get(valKey)));
                    labels.add(String.valueOf(r.get(labelKey)));
                }
                Map<String, Object> s = new LinkedHashMap<>();
                s.put("y", y);
                s.put("labels", labels);
                series.add(s);
            } else if ("histogram".equals(kind)) {
                List<Object> values = new ArrayList<>();
                for (Object item : list) values.add(toNum(item));
                Map<String, Object> s = new LinkedHashMap<>();
                s.put("values", values);
                series.add(s);
            } else {
                List<Object> y = new ArrayList<>();
                for (Object item : list) y.add(toNum(item));
                Map<String, Object> s = new LinkedHashMap<>();
                s.put("y", y);
                series.add(s);
            }
        } else if (data instanceof Map<?, ?> map) {
            boolean isMultiSeries = false;
            for (Object v : map.values()) {
                if (toList(v) != null) { isMultiSeries = true; break; }
            }
            if (isMultiSeries) {
                for (Map.Entry<?, ?> e : map.entrySet()) {
                    List<?> yVals = toList(e.getValue());
                    List<Object> y = new ArrayList<>();
                    if (yVals != null) for (Object item : yVals) y.add(toNum(item));
                    Map<String, Object> s = new LinkedHashMap<>();
                    s.put("name", String.valueOf(e.getKey()));
                    s.put("y", y);
                    series.add(s);
                }
            } else {
                List<String> labels = new ArrayList<>();
                List<Object> y = new ArrayList<>();
                for (Map.Entry<?, ?> e : map.entrySet()) {
                    labels.add(String.valueOf(e.getKey()));
                    y.add(toNum(e.getValue()));
                }
                Map<String, Object> s = new LinkedHashMap<>();
                s.put("y", y);
                s.put("labels", labels);
                series.add(s);
            }
        }

        spec.put("series", series);
        if (title != null) spec.put("title", title);
        return spec;
    }

    private static Map<String, Object> plot3dSpec(String kind, Object data, String title) {
        Map<String, Object> spec = new LinkedHashMap<>();
        spec.put("kind", kind);
        if ("scatter".equals(kind)) {
            List<Object> x = new ArrayList<>();
            List<Object> y = new ArrayList<>();
            List<Object> z = new ArrayList<>();
            List<?> list = toList(data);
            if (list != null) {
                for (Object item : list) {
                    List<?> p = toList(item);
                    if (p != null && p.size() >= 3) {
                        x.add(toNum(p.get(0)));
                        y.add(toNum(p.get(1)));
                        z.add(toNum(p.get(2)));
                    }
                }
            }
            Map<String, Object> s = new LinkedHashMap<>();
            s.put("x", x);
            s.put("y", y);
            s.put("z", z);
            spec.put("series", List.of(s));
        } else if ("surface".equals(kind)) {
            List<?> rows = toList(data);
            int numRows = rows != null ? rows.size() : 0;
            int numCols = 0;
            List<List<Object>> zGrid = new ArrayList<>();
            if (rows != null) {
                for (Object r : rows) {
                    List<?> cols = toList(r);
                    if (cols != null) {
                        numCols = Math.max(numCols, cols.size());
                        List<Object> rowVals = new ArrayList<>();
                        for (Object cell : cols) rowVals.add(toNum(cell));
                        zGrid.add(rowVals);
                    }
                }
            }
            Map<String, Object> surface = new LinkedHashMap<>();
            surface.put("x", Map.of("min", 0, "max", Math.max(0, numCols - 1)));
            surface.put("y", Map.of("min", 0, "max", Math.max(0, numRows - 1)));
            surface.put("z", zGrid);
            spec.put("surface", surface);
        } else if ("voxelBar".equals(kind)) {
            List<List<Object>> rows = to2DList(data);
            List<Object> x = new ArrayList<>();
            List<Object> y = new ArrayList<>();
            List<Object> z = new ArrayList<>();
            List<String> labels = new ArrayList<>();
            for (int r = 0; r < rows.size(); r++) {
                List<Object> cols = rows.get(r);
                for (int c = 0; c < cols.size(); c++) {
                    Object cell = cols.get(c);
                    Object numObj = toNum(cell);
                    if (numObj instanceof Number n) {
                        double num = n.doubleValue();
                        x.add(r);
                        y.add(c);
                        z.add(num);
                        labels.add("[" + r + "," + c + "]=" + num);
                    }
                }
            }
            Map<String, Object> s = new LinkedHashMap<>();
            s.put("x", x);
            s.put("y", y);
            s.put("z", z);
            s.put("labels", labels);
            spec.put("series", List.of(s));
        } else if ("graph".equals(kind)) {
            Set<String> nodeSet = new LinkedHashSet<>();
            List<Map<String, Object>> edges = new ArrayList<>();
            if (data instanceof Map<?, ?> map) {
                for (Map.Entry<?, ?> e : map.entrySet()) {
                    String u = String.valueOf(e.getKey());
                    nodeSet.add(u);
                    List<?> targets = toList(e.getValue());
                    if (targets != null) {
                        for (Object v : targets) {
                            String vs = String.valueOf(v);
                            nodeSet.add(vs);
                            Map<String, Object> edge = new LinkedHashMap<>();
                            edge.put("from", u);
                            edge.put("to", vs);
                            edges.add(edge);
                        }
                    }
                }
            }
            List<Map<String, Object>> nodes = new ArrayList<>();
            for (String n : nodeSet) {
                Map<String, Object> node = new LinkedHashMap<>();
                node.put("id", n);
                nodes.add(node);
            }
            Map<String, Object> graph = new LinkedHashMap<>();
            graph.put("directed", true);
            graph.put("nodes", nodes);
            graph.put("edges", edges);
            spec.put("graph", graph);
        }
        if (title != null) spec.put("title", title);
        return spec;
    }

    private static Map<String, Object> matrixSpec(Object grid, String title) {
        Map<String, Object> spec = new LinkedHashMap<>();
        spec.put("kind", "matrix");
        spec.put("state", Map.of("grid", Map.of("values", to2DList(grid))));
        if (title != null) spec.put("title", title);
        return spec;
    }

    private static Map<String, Object> islandsSpec(Object grid, String title) {
        Map<String, Object> spec = new LinkedHashMap<>();
        spec.put("kind", "islands");
        spec.put("state", Map.of("grid", Map.of("values", to2DList(grid))));
        if (title != null) spec.put("title", title);
        return spec;
    }

    private static Map<String, Object> arraySpec(Object values, Object pointers, String title) {
        Map<String, Object> spec = new LinkedHashMap<>();
        spec.put("kind", "arrayPointers");
        List<Object> cleanValues = new ArrayList<>();
        List<?> list = toList(values);
        if (list != null) for (Object v : list) cleanValues.add(cleanScalar(v));
        spec.put("state", Map.of("array", Map.of("values", cleanValues)));

        if (pointers instanceof Map<?, ?> map && !map.isEmpty()) {
            List<Map<String, Object>> ptrList = new ArrayList<>();
            for (Map.Entry<?, ?> e : map.entrySet()) {
                Map<String, Object> p = new LinkedHashMap<>();
                p.put("name", String.valueOf(e.getKey()));
                p.put("at", e.getValue() instanceof Number ? ((Number)e.getValue()).intValue() : String.valueOf(e.getValue()));
                ptrList.add(p);
            }
            spec.put("pointers", ptrList);
        }
        if (title != null) spec.put("title", title);
        return spec;
    }

    private static Map<String, Object> treeSpec(Object root, String title) {
        Map<String, Object> spec = new LinkedHashMap<>();
        spec.put("kind", "tree");
        List<?> list = toList(root);
        Map<String, Object> treeState = new LinkedHashMap<>();
        treeState.put("root", "node_1");
        List<Map<String, Object>> nodes = new ArrayList<>();

        if (list != null) {
            // Level order array
            if (!list.isEmpty() && list.get(0) != null) {
                int count = 1;
                Map<String, Object> rootNode = new LinkedHashMap<>();
                rootNode.put("id", "node_1");
                rootNode.put("value", String.valueOf(list.get(0)));
                Queue<Map<String, Object>> q = new LinkedList<>();
                q.add(rootNode);
                Map<String, List<Map<String, Object>>> childrenMap = new LinkedHashMap<>();
                int idx = 1;
                while (!q.isEmpty() && idx < list.size()) {
                    Map<String, Object> cur = q.poll();
                    for (String side : new String[]{"left", "right"}) {
                        if (idx < list.size()) {
                            Object val = list.get(idx);
                            if (val != null) {
                                count++;
                                Map<String, Object> child = new LinkedHashMap<>();
                                child.put("id", "node_" + count);
                                child.put("value", String.valueOf(val));
                                cur.put(side, child.get("id"));
                                childrenMap.computeIfAbsent((String) cur.get("id"), k -> new ArrayList<>()).add(child);
                                q.add(child);
                            }
                            idx++;
                        }
                    }
                }
                walkTree(rootNode, childrenMap, nodes);
            }
        } else if (root != null) {
            int[] counter = new int[]{0};
            parseTreeNode(root, counter, nodes);
        }

        treeState.put("nodes", nodes);
        spec.put("state", Map.of("tree", treeState));
        if (title != null) spec.put("title", title);
        return spec;
    }

    private static void walkTree(Map<String, Object> n, Map<String, List<Map<String, Object>>> childrenMap, List<Map<String, Object>> nodes) {
        nodes.add(n);
        List<Map<String, Object>> kids = childrenMap.get(n.get("id"));
        if (kids != null) {
            for (Map<String, Object> c : kids) walkTree(c, childrenMap, nodes);
        }
    }

    private static String parseTreeNode(Object node, int[] counter, List<Map<String, Object>> nodes) {
        if (node == null) return null;
        counter[0]++;
        String id = "node_" + counter[0];
        Object val = getField(node, "val", "value", "data", "key");
        Map<String, Object> n = new LinkedHashMap<>();
        n.put("id", id);
        n.put("value", String.valueOf(val != null ? val : node));
        nodes.add(n);

        Object left = getField(node, "left");
        Object right = getField(node, "right");
        if (left != null) n.put("left", parseTreeNode(left, counter, nodes));
        if (right != null) n.put("right", parseTreeNode(right, counter, nodes));
        return id;
    }

    private static Map<String, Object> graphSpec(Object graph, String title) {
        Map<String, Object> spec = new LinkedHashMap<>();
        spec.put("kind", "graph");
        Set<String> nodeSet = new LinkedHashSet<>();
        List<Map<String, Object>> edges = new ArrayList<>();
        if (graph instanceof Map<?, ?> map) {
            for (Map.Entry<?, ?> e : map.entrySet()) {
                String u = String.valueOf(e.getKey());
                nodeSet.add(u);
                List<?> targets = toList(e.getValue());
                if (targets != null) {
                    for (Object v : targets) {
                        String vs = String.valueOf(v);
                        nodeSet.add(vs);
                        Map<String, Object> edge = new LinkedHashMap<>();
                        edge.put("from", u);
                        edge.put("to", vs);
                        edges.add(edge);
                    }
                }
            }
        }
        List<Map<String, Object>> nodes = new ArrayList<>();
        for (String n : nodeSet) {
            Map<String, Object> node = new LinkedHashMap<>();
            node.put("id", n);
            nodes.add(node);
        }
        Map<String, Object> state = new LinkedHashMap<>();
        state.put("directed", true);
        state.put("nodes", nodes);
        state.put("edges", edges);
        spec.put("state", Map.of("graph", state));
        if (title != null) spec.put("title", title);
        return spec;
    }

    private static Map<String, Object> linkedListSpec(Object head, String title) {
        Map<String, Object> spec = new LinkedHashMap<>();
        spec.put("kind", "linkedList");
        List<Map<String, Object>> nodes = new ArrayList<>();
        Object cur = head;
        int idx = 0;
        while (cur != null && idx < 500) {
            String id = "n" + idx;
            Object val = getField(cur, "val", "value", "data");
            Map<String, Object> n = new LinkedHashMap<>();
            n.put("id", id);
            n.put("value", String.valueOf(val != null ? val : cur));
            if (!nodes.isEmpty()) {
                nodes.get(nodes.size() - 1).put("next", id);
            }
            nodes.add(n);
            cur = getField(cur, "next");
            idx++;
        }
        spec.put("state", Map.of("linkedList", Map.of("nodes", nodes)));
        if (title != null) spec.put("title", title);
        return spec;
    }

    private static Map<String, Object> barsSpec(Object values, String title) {
        Map<String, Object> spec = new LinkedHashMap<>();
        spec.put("kind", "bars");
        List<Object> clean = new ArrayList<>();
        List<?> list = toList(values);
        if (list != null) for (Object v : list) clean.add(toNum(v));
        spec.put("state", Map.of("bars", Map.of("values", clean)));
        if (title != null) spec.put("title", title);
        return spec;
    }

    // ------------------------------------------------------------------------------------------------ serialization
    private static Object getField(Object obj, String... names) {
        if (obj == null) return null;
        for (String name : names) {
            try {
                Field f = obj.getClass().getField(name);
                return f.get(obj);
            } catch (Exception ignored) {}
            try {
                Field f = obj.getClass().getDeclaredField(name);
                f.setAccessible(true);
                return f.get(obj);
            } catch (Exception ignored) {}
            try {
                Method m = obj.getClass().getMethod("get" + Character.toUpperCase(name.charAt(0)) + name.substring(1));
                return m.invoke(obj);
            } catch (Exception ignored) {}
        }
        return null;
    }

    private static String findKey(Map<?, ?> map, String... names) {
        for (String name : names) {
            for (Object k : map.keySet()) {
                if (name.equalsIgnoreCase(String.valueOf(k))) return String.valueOf(k);
            }
        }
        return String.valueOf(map.keySet().iterator().next());
    }

    private static List<List<Object>> to2DList(Object grid) {
        List<List<Object>> rows = new ArrayList<>();
        List<?> list = toList(grid);
        if (list != null) {
            for (Object r : list) {
                List<?> cols = toList(r);
                if (cols != null) {
                    List<Object> row = new ArrayList<>();
                    for (Object c : cols) row.add(cleanScalar(c));
                    rows.add(row);
                }
            }
        }
        return rows;
    }

    private static List<?> toList(Object o) {
        if (o == null) return null;
        if (o instanceof List<?>) return (List<?>) o;
        if (o instanceof Collection<?>) return new ArrayList<>((Collection<?>) o);
        if (o instanceof Object[]) return Arrays.asList((Object[]) o);
        if (o instanceof int[] arr) {
            List<Integer> l = new ArrayList<>(arr.length);
            for (int v : arr) l.add(v);
            return l;
        }
        if (o instanceof double[] arr) {
            List<Double> l = new ArrayList<>(arr.length);
            for (double v : arr) l.add(v);
            return l;
        }
        if (o instanceof long[] arr) {
            List<Long> l = new ArrayList<>(arr.length);
            for (long v : arr) l.add(v);
            return l;
        }
        return null;
    }

    private static Object toNum(Object v) {
        if (v == null || v instanceof Boolean) return null;
        if (v instanceof Number n) {
            double d = n.doubleValue();
            if (Double.isNaN(d) || Double.isInfinite(d)) return null;
            if (v instanceof Integer || v instanceof Long) return v;
            return d;
        }
        try {
            double d = Double.parseDouble(String.valueOf(v));
            return Double.isNaN(d) || Double.isInfinite(d) ? null : d;
        } catch (Exception e) {
            return null;
        }
    }

    private static Object cleanScalar(Object v) {
        if (v == null) return null;
        if (v instanceof Number || v instanceof Boolean || v instanceof String) return v;
        return String.valueOf(v);
    }

    public static String toJson(Object o) {
        if (o == null) return "null";
        if (o instanceof Number n) {
            double d = n.doubleValue();
            if (Double.isNaN(d) || Double.isInfinite(d)) return "null";
            return o.toString();
        }
        if (o instanceof Boolean) return o.toString();
        if (o instanceof CharSequence) return "\"" + escapeJson(o.toString()) + "\"";
        List<?> list = toList(o);
        if (list != null) {
            StringBuilder sb = new StringBuilder("[");
            for (int i = 0; i < list.size(); i++) {
                if (i > 0) sb.append(",");
                sb.append(toJson(list.get(i)));
            }
            return sb.append("]").toString();
        }
        if (o instanceof Map<?, ?> map) {
            StringBuilder sb = new StringBuilder("{");
            int i = 0;
            for (Map.Entry<?, ?> e : map.entrySet()) {
                if (i++ > 0) sb.append(",");
                sb.append("\"").append(escapeJson(String.valueOf(e.getKey()))).append("\":").append(toJson(e.getValue()));
            }
            return sb.append("}").toString();
        }
        return "\"" + escapeJson(String.valueOf(o)) + "\"";
    }

    public static String escapeJson(String s) {
        if (s == null) return "";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (c == '"') sb.append("\\\"");
            else if (c == '\\') sb.append("\\\\");
            else if (c == '\b') sb.append("\\b");
            else if (c == '\f') sb.append("\\f");
            else if (c == '\n') sb.append("\\n");
            else if (c == '\r') sb.append("\\r");
            else if (c == '\t') sb.append("\\t");
            else if (c < 32) sb.append(String.format("\\u%04x", (int) c));
            else sb.append(c);
        }
        return sb.toString();
    }

    private static String formatTable(String title, Object obj) {
        Map<String, Object> table = new LinkedHashMap<>();
        table.put("title", title != null ? title : "Table");
        List<String> cols = new ArrayList<>();
        List<Boolean> numeric = new ArrayList<>();
        List<List<Object>> rows = new ArrayList<>();
        List<?> list = toList(obj);
        if (list != null && !list.isEmpty() && list.get(0) instanceof Map<?, ?> first) {
            for (Object k : first.keySet()) {
                cols.add(String.valueOf(k));
                numeric.add(first.get(k) instanceof Number);
            }
            for (Object item : list) {
                if (item instanceof Map<?, ?> r) {
                    List<Object> row = new ArrayList<>();
                    for (String c : cols) row.add(r.get(c));
                    rows.add(row);
                }
            }
        } else {
            cols.add("Value");
            numeric.add(false);
            if (list != null) {
                for (Object item : list) rows.add(List.of(String.valueOf(item)));
            } else {
                rows.add(List.of(String.valueOf(obj)));
            }
        }
        table.put("columns", cols);
        table.put("numeric", numeric);
        table.put("rows", rows);
        table.put("totalRows", rows.size());
        table.put("totalColumns", cols.size());
        return toJson(table);
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
            try {
                long val = Long.parseLong(numStr);
                if (val >= Integer.MIN_VALUE && val <= Integer.MAX_VALUE) return (int) val;
                return val;
            } catch (Exception e) {
                return 0;
            }
        }
    }
}
