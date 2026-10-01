// Companion to public class Display for FryPDF C# Code Studio
import java.util.*;

/**
 * Interactive Visualizer API for FryPDF C# Code Studio (Java).
 * Provides fluent builders for trees, graphs, grids, island traversals, and custom vector canvases.
 */
public class Visualizer {

    public static class VisualizerBuilder {
        private final String kind;
        private final Object data;
        private String title;
        private boolean directed = true;

        public VisualizerBuilder(String kind, Object data) {
            this.kind = kind;
            this.data = data;
        }

        public VisualizerBuilder title(String title) {
            this.title = title;
            return this;
        }

        public VisualizerBuilder directed(boolean directed) {
            this.directed = directed;
            return this;
        }

        public Display.DisplayHandle show() {
            if ("tree".equalsIgnoreCase(kind)) {
                return Display.tree(data, title);
            } else if ("graph".equalsIgnoreCase(kind)) {
                return Display.graph(data, title);
            } else if ("grid".equalsIgnoreCase(kind)) {
                if (title != null && title.toLowerCase().contains("island")) {
                    return Display.islands(data, title);
                }
                return Display.matrix(data, title);
            } else if ("islands".equalsIgnoreCase(kind)) {
                return Display.islands(data, title);
            }
            return Display.matrix(data, title);
        }
    }

    public static class CanvasBuilder {
        private String title;
        private int width = 600;
        private int height = 300;
        private String background = null;
        private final List<Map<String, Object>> shapes = new ArrayList<>();

        public CanvasBuilder() {}

        public CanvasBuilder(String title, int width, int height) {
            this.title = title;
            this.width = width;
            this.height = height;
        }

        public CanvasBuilder title(String title) {
            this.title = title;
            return this;
        }

        public CanvasBuilder width(int w) {
            this.width = w;
            return this;
        }

        public CanvasBuilder height(int h) {
            this.height = h;
            return this;
        }

        public CanvasBuilder background(String bg) {
            this.background = bg;
            return this;
        }

        public CanvasBuilder addRect(double x, double y, double width, double height, String label, String fill, String stroke) {
            Map<String, Object> shape = new LinkedHashMap<>();
            shape.put("type", "rect");
            shape.put("x", x);
            shape.put("y", y);
            shape.put("width", width);
            shape.put("height", height);
            if (label != null) shape.put("label", label);
            if (fill != null) shape.put("fill", fill);
            if (stroke != null) shape.put("stroke", stroke);
            shapes.add(shape);
            return this;
        }

        public CanvasBuilder addRect(double x, double y, double width, double height) {
            return addRect(x, y, width, height, null, null, null);
        }

        public CanvasBuilder addArrow(double x1, double y1, double x2, double y2, String label, String color) {
            Map<String, Object> shape = new LinkedHashMap<>();
            shape.put("type", "arrow");
            shape.put("x1", x1);
            shape.put("y1", y1);
            shape.put("x2", x2);
            shape.put("y2", y2);
            if (label != null) shape.put("label", label);
            if (color != null) shape.put("stroke", color);
            shapes.add(shape);
            return this;
        }

        public CanvasBuilder addArrow(double x1, double y1, double x2, double y2) {
            return addArrow(x1, y1, x2, y2, null, null);
        }

        public CanvasBuilder addCircle(double cx, double cy, double radius, String label, String fill, String stroke) {
            Map<String, Object> shape = new LinkedHashMap<>();
            shape.put("type", "circle");
            shape.put("cx", cx);
            shape.put("cy", cy);
            shape.put("radius", radius);
            if (label != null) shape.put("label", label);
            if (fill != null) shape.put("fill", fill);
            if (stroke != null) shape.put("stroke", stroke);
            shapes.add(shape);
            return this;
        }

        public CanvasBuilder addLine(double x1, double y1, double x2, double y2, String color) {
            Map<String, Object> shape = new LinkedHashMap<>();
            shape.put("type", "line");
            shape.put("x1", x1);
            shape.put("y1", y1);
            shape.put("x2", x2);
            shape.put("y2", y2);
            if (color != null) shape.put("stroke", color);
            shapes.add(shape);
            return this;
        }

        public CanvasBuilder addText(double x, double y, String text, int fontSize, String color) {
            Map<String, Object> shape = new LinkedHashMap<>();
            shape.put("type", "text");
            shape.put("x", x);
            shape.put("y", y);
            shape.put("text", text);
            shape.put("fontSize", fontSize);
            if (color != null) shape.put("color", color);
            shapes.add(shape);
            return this;
        }

        public Display.DisplayHandle show() {
            Map<String, Object> canvasState = new LinkedHashMap<>();
            canvasState.put("width", width);
            canvasState.put("height", height);
            if (background != null) canvasState.put("background", background);
            canvasState.put("shapes", shapes);

            Map<String, Object> spec = new LinkedHashMap<>();
            spec.put("kind", "canvas");
            spec.put("state", Map.of("canvas", canvasState));
            if (title != null) spec.put("title", title);
            return Display.sendDisplay(Display.VISUALIZER_MIME, spec);
        }
    }

    public static VisualizerBuilder tree(Object root) {
        return new VisualizerBuilder("tree", root);
    }

    public static VisualizerBuilder graph(Object graph) {
        return new VisualizerBuilder("graph", graph);
    }

    public static VisualizerBuilder grid(Object grid) {
        return new VisualizerBuilder("grid", grid);
    }

    public static VisualizerBuilder islands(Object grid) {
        return new VisualizerBuilder("islands", grid);
    }

    public static CanvasBuilder canvas(String title, int width, int height) {
        return new CanvasBuilder(title, width, height);
    }

    public static CanvasBuilder canvas(String title) {
        return new CanvasBuilder(title, 600, 300);
    }

    public static CanvasBuilder canvas() {
        return new CanvasBuilder(null, 600, 300);
    }
}
