import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.*;
import java.util.concurrent.*;
import jdk.jshell.*;

/**
 * Java Notebook Kernel for FrySharp.
 * Speaks the line-delimited JSON Fry Kernel Protocol over stdin/stdout pipes.
 */
public class FryKernel {
    private static JShell jshell;
    private static volatile String currentRequestId = null;
    private static int executionCount = 0;
    private static volatile boolean interruptRequested = false;
    // Where each snippet of the running cell begins (1-based line of the cell), by JShell's snippet id, so that a stack frame
    // named "#3:1" can be put on the cell's own line.
    private static final Map<String, Integer> snippetStartLines = new HashMap<>();
    private static final PrintStream protocolOut = System.out;
    private static final SnippetOutputStream snippetOutStream = new SnippetOutputStream("stdout");
    private static final PrintStream snippetPs = new PrintStream(snippetOutStream, true, StandardCharsets.UTF_8);
    private static final SnippetOutputStream snippetErrStream = new SnippetOutputStream("stderr");
    private static final PrintStream snippetErrPs = new PrintStream(snippetErrStream, true, StandardCharsets.UTF_8);
    private static final BlockingQueue<String> messageQueue = new LinkedBlockingQueue<>();
    private static final Map<String, String> displayOwners = new ConcurrentHashMap<>();
    // The answers to input_request: a line of text, or empty for the end of input.
    private static final BlockingQueue<Optional<String>> inputReplies = new LinkedBlockingQueue<>();
    private static final KernelInput kernelInput = new KernelInput();

    /**
     * System.in of the running cell (Scanner, BufferedReader): when the code reads and nothing is waiting, the studio is
     * asked for a line (input_request) and the read waits for input_reply, as Python's input() does.
     */
    private static class KernelInput extends InputStream {
        private byte[] pending = new byte[0];
        private int position = 0;
        private boolean ended = false;

        @Override
        public synchronized int read() throws IOException {
            if (!fill()) return -1;
            return pending[position++] & 0xFF;
        }

        @Override
        public synchronized int read(byte[] buffer, int offset, int length) throws IOException {
            if (length == 0) return 0;
            if (!fill()) return -1;
            int count = Math.min(length, pending.length - position);
            System.arraycopy(pending, position, buffer, offset, count);
            position += count;
            return count;
        }

        @Override
        public synchronized int available() {
            return pending.length - position;
        }

        synchronized void startCell() {
            pending = new byte[0];
            position = 0;
            ended = false;
            inputReplies.clear();
        }

        private boolean fill() throws IOException {
            if (position < pending.length) return true;
            if (ended) return false;

            String id = currentRequestId;
            flushText();
            send(Map.of("type", "input_request", "id", id == null ? "" : id, "prompt", "", "password", false));
            try {
                Optional<String> reply = inputReplies.take();
                if (reply.isEmpty()) {
                    ended = true;
                    return false;
                }
                pending = (reply.get() + "\n").getBytes(StandardCharsets.UTF_8);
                position = 0;
                return true;
            } catch (InterruptedException e) {
                throw new InterruptedIOException();
            }
        }
    }

    // What the cell prints is sent in batches (8 KB, or 50 ms), in the order it was printed with what else it sends: a display, an
    // error, a question for input. Text is one stream per name (stdout, stderr); a change of name ends the batch.
    private static final Object textGate = new Object();
    private static final StringBuilder textBuffer = new StringBuilder();
    private static String textName = "stdout";
    private static String textOwner = null;

    private static void emitText(String name, String text) {
        if (text.isEmpty()) return;
        synchronized (textGate) {
            if (!name.equals(textName) || !Objects.equals(textOwner, currentRequestId)) flushTextLocked();
            textName = name;
            textOwner = currentRequestId;
            textBuffer.append(text);
            if (textBuffer.length() >= 8192) flushTextLocked();
        }
    }

    static void flushText() {
        synchronized (textGate) { flushTextLocked(); }
    }

    private static void flushTextLocked() {
        if (textBuffer.length() == 0) return;
        String id = textOwner != null ? textOwner : currentRequestId;
        String text = textBuffer.toString();
        textBuffer.setLength(0);
        if (id != null) sendNow(Map.of("type", "stream", "id", id, "name", textName, "text", text));
    }

    private static boolean couldBeMarker(String partial) {
        String marker = "__FRY_DISPLAY__ ";
        return partial.isEmpty() || marker.startsWith(partial) || partial.startsWith(marker);
    }

    /**
     * An output stream of the cell's code. Text goes on at once (a prompt printed without a newline is shown before the cell
     * waits for its answer); a line that is a display of Display (marker + JSON) is a protocol message in its place.
     */
    private static class SnippetOutputStream extends OutputStream {
        private final String name;
        private final ByteArrayOutputStream partial = new ByteArrayOutputStream();

        SnippetOutputStream(String name) {
            this.name = name;
        }

        @Override
        public synchronized void write(int b) {
            write(new byte[] { (byte) b }, 0, 1);
        }

        @Override
        public synchronized void write(byte[] b, int off, int len) {
            for (int i = off; i < off + len; i++) {
                if (b[i] == '\n') {
                    String line = partial.toString(StandardCharsets.UTF_8);
                    partial.reset();
                    line(line);
                } else {
                    partial.write(b[i]);
                }
            }
            // an unfinished line that can't be a display line is text now (a prompt); a character cut in half waits
            if (partial.size() > 0) {
                byte[] bytes = partial.toByteArray();
                int complete = completeUtf8Length(bytes);
                String head = new String(bytes, 0, complete, StandardCharsets.UTF_8);
                if (!(name.equals("stdout") && couldBeMarker(head)) && !head.isEmpty()) {
                    emitText(name, head);
                    partial.reset();
                    partial.write(bytes, complete, bytes.length - complete);
                }
            }
        }

        private void line(String line) {
            if (name.equals("stdout")) {
                String trimmed = line.trim();
                int at = line.indexOf("__FRY_DISPLAY__ ");
                if (at >= 0) {
                    String before = trimmed.startsWith("__FRY_DISPLAY__ ") ? "" : line.substring(0, at);
                    emitText(name, before);
                    flushText();
                    handleDisplayPayload(line.substring(at + "__FRY_DISPLAY__ ".length()).trim());
                    return;
                }
            }
            emitText(name, line + "\n");
        }

        @Override
        public synchronized void flush() {
        }

        @Override
        public synchronized void close() {
            if (partial.size() > 0) {
                emitText(name, partial.toString(StandardCharsets.UTF_8));
                partial.reset();
            }
        }
    }

    // How many bytes of the start of `bytes` are whole UTF-8 characters.
    private static int completeUtf8Length(byte[] bytes) {
        int end = bytes.length;
        int i = end - 1;
        int back = 0;
        while (i >= 0 && back < 4 && (bytes[i] & 0xC0) == 0x80) { i--; back++; }
        if (i < 0) return end;
        int lead = bytes[i] & 0xFF;
        int need = lead >= 0xF0 ? 4 : lead >= 0xE0 ? 3 : lead >= 0xC0 ? 2 : 1;
        return (end - i) < need ? i : end;
    }

    public static void handleDisplayPayload(String payload) {
        try {
            String trimmed = payload.trim();
            if (!trimmed.startsWith("{")) return;

            String displayId = null;
            int atTrans = trimmed.indexOf("\"display_id\"");
            if (atTrans >= 0) {
                int colon = trimmed.indexOf(':', atTrans);
                int q1 = trimmed.indexOf('"', colon);
                int q2 = trimmed.indexOf('"', q1 + 1);
                if (q1 >= 0 && q2 > q1) {
                    displayId = trimmed.substring(q1 + 1, q2);
                }
            }

            String targetCellId = currentRequestId;
            if (trimmed.contains("\"type\":\"display\"") && displayId != null && targetCellId != null) {
                displayOwners.put(displayId, targetCellId);
            } else if (displayId != null && targetCellId == null) {
                targetCellId = displayOwners.get(displayId);
            }

            String toSend = trimmed;
            if (targetCellId != null) {
                toSend = "{\"id\":\"" + targetCellId + "\"," + trimmed.substring(1);
            }
            flushText();
            synchronized (protocolOut) {
                protocolOut.println(toSend);
                protocolOut.flush();
            }
        } catch (Exception ex) {
            System.err.println("[FryKernel] error handling display: " + ex);
        }
    }

    public static void main(String[] args) throws Exception {
        String version = System.getProperty("java.version");
        String javaHome = System.getProperty("java.home");
        String javaExe = javaHome + File.separator + "bin" + File.separator + "java";

        String kernelDir = null;
        for (int i = 0; i < args.length; i++) {
            if ("--kernel-dir".equals(args[i]) && i + 1 < args.length) {
                kernelDir = args[i + 1];
            } else if ("--cwd".equals(args[i]) && i + 1 < args.length) {
                try {
                    System.setProperty("user.dir", args[i + 1]);
                } catch (Exception ignored) {}
            }
        }

        jshell = JShell.builder().in(kernelInput).out(snippetPs).err(snippetErrPs).build();

        // Default imports
        evalSnippet("import java.util.*;");
        evalSnippet("import java.util.stream.*;");
        evalSnippet("import java.io.*;");
        evalSnippet("import java.nio.file.*;");

        // Load unified Display.java
        File displayFile = kernelDir != null ? new File(kernelDir, "Display.java") : new File("Display.java");
        if (!displayFile.exists()) {
            File cand = new File(System.getProperty("user.dir", "."), "Display.java");
            if (cand.exists()) displayFile = cand;
        }
        if (displayFile.exists()) {
            try {
                String displayCode = Files.readString(displayFile.toPath());
                evalSnippet(displayCode);
            } catch (Exception e) {
                System.err.println("[FryKernel] Failed loading Display.java: " + e);
            }
        } else {
            System.err.println("[FryKernel] Warning: Display.java not found at " + displayFile.getAbsolutePath());
        }

        // Its companion: Visualizer.grid(...), Visualizer.tree(...) and the other builders
        File visualizerFile = kernelDir != null ? new File(kernelDir, "Visualizer.java") : new File("Visualizer.java");
        if (visualizerFile.exists()) {
            try {
                evalSnippet(Files.readString(visualizerFile.toPath()));
            } catch (Exception e) {
                System.err.println("[FryKernel] Failed loading Visualizer.java: " + e);
            }
        }

        // The Object Explorer: what the last expression of a cell shows when it is an object (it needs Display loaded)
        File inspectorFile = kernelDir != null ? new File(kernelDir, "FryInspector.java") : new File("FryInspector.java");
        if (inspectorFile.exists()) {
            try {
                evalSnippet(Files.readString(inspectorFile.toPath()));
            } catch (Exception e) {
                System.err.println("[FryKernel] Failed loading FryInspector.java: " + e);
            }
        }

        evalSnippet("void display(Object obj) { Display.show(obj); }");
        evalSnippet("void dump(Object obj) { Display.show(obj); }");

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

        // Text a cell prints is sent at the latest 50 ms after it was printed
        Thread flusher = new Thread(() -> {
            while (true) {
                try { Thread.sleep(50); } catch (InterruptedException e) { return; }
                flushText();
            }
        }, "fry-text-flusher");
        flusher.setDaemon(true);
        flusher.start();

        // Background stdin reader: dispatches events immediately, queues execute/variables requests
        Thread readerThread = new Thread(() -> {
            try {
                BufferedReader reader = new BufferedReader(new InputStreamReader(System.in, StandardCharsets.UTF_8));
                String line;
                while ((line = reader.readLine()) != null) {
                    line = line.trim();
                    if (line.isEmpty()) continue;
                    Map<String, Object> msg = parseJsonMap(line);
                    String type = (String) msg.get("type");
                    if ("event".equals(type)) {
                        handleEvent(msg, line);
                    } else if ("input_reply".equals(type)) {
                        // Read while a cell runs, so not through the queue the running cell holds up.
                        Object value = msg.get("value");
                        inputReplies.put(value instanceof String text ? Optional.of(text) : Optional.empty());
                    } else if ("interrupt".equals(type)) {
                        // The cell that is running ends with an exception, and what the notebook declared stays. The main
                        // thread is busy with the cell, so this is read here and not through the queue.
                        interruptRequested = true;
                        try { jshell.stop(); } catch (Exception ignored) { }
                    } else if ("shutdown".equals(type)) {
                        System.exit(0);
                    } else {
                        messageQueue.put(line);
                    }
                }
            } catch (Exception ignored) {
            }
            // The studio closed its end (the notebook closed, or the program that started this one is gone): the kernel ends with
            // what it started, as the protocol says; nothing else would end it, and the JVM would outlive its owner.
            try { jshell.close(); } catch (Exception ignored) { }
            System.exit(0);
        }, "fry-stdin-reader");
        readerThread.setDaemon(true);
        readerThread.start();

        while (true) {
            String line = messageQueue.take();
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
        } else if ("add_search_path".equals(type)) {
            // A jar a package command just restored: the classes in it are usable from the next cell on, no restart.
            try {
                jshell.addToClasspath((String) msg.get("path"));
                send(Map.of("type", "reply", "id", id, "status", "ok"));
            } catch (Exception ex) {
                send(Map.of("type", "reply", "id", id, "status", "error", "message", String.valueOf(ex.getMessage())));
            }
        } else if ("shutdown".equals(type)) {
            System.exit(0);
        }
    }

    private static void handleEvent(Map<String, Object> msg, String jsonLine) {
        String id = (String) msg.get("id");
        currentRequestId = id;
        try {
            String escaped = escapeJavaString(jsonLine);
            jshell.eval("Display.deliverEventLine(\"" + escaped + "\");");
            snippetPs.flush();
        } catch (Exception ex) {
            System.err.println("[FryKernel] handleEvent error: " + ex);
        } finally {
            currentRequestId = null;
        }
    }

    private static String escapeJavaString(String s) {
        if (s == null) return "";
        return s.replace("\\", "\\\\")
                .replace("\"", "\\\"")
                .replace("\n", "\\n")
                .replace("\r", "\\r")
                .replace("\t", "\\t");
    }

    // The class a cell declares with public static void main(String[]), or null.
    private static String mainClassOf(String code) {
        java.util.regex.Matcher main = java.util.regex.Pattern.compile("public\\s+static\\s+void\\s+main\\s*\\(\\s*String").matcher(code);
        if (!main.find()) return null;
        // The public class holds main (a class nested before it, like a Node, does not).
        String before = code.substring(0, main.start());
        java.util.regex.Matcher top = java.util.regex.Pattern.compile("\\bpublic\\s+(?:final\\s+|abstract\\s+)*class\\s+(\\w+)").matcher(before);
        if (top.find()) return top.group(1);
        java.util.regex.Matcher declared = java.util.regex.Pattern.compile("\\b(?:class|record|enum)\\s+(\\w+)").matcher(before);
        String name = null;
        while (declared.find()) name = declared.group(1);
        return name;
    }

    // JShell reports the value of a declaration (`int x = 7;`) and of an assignment too; a cell shows the value of an expression
    // (`x + 1`, `x`, a call), as in every other language. (An expression is a temporary variable to JShell.)
    private static boolean isValueExpression(Snippet snippet) {
        Snippet.SubKind kind = snippet.subKind();
        return kind == Snippet.SubKind.TEMP_VAR_EXPRESSION_SUBKIND
            || kind == Snippet.SubKind.VAR_VALUE_SUBKIND
            || kind == Snippet.SubKind.OTHER_EXPRESSION_SUBKIND;
    }

    private static int lineAt(String text, int offset) {
        int line = 1;
        for (int i = 0; i < Math.min(offset, text.length()); i++) if (text.charAt(i) == '\n') line++;
        return line;
    }

    private static int columnAt(String text, int offset) {
        int clamped = Math.max(0, Math.min(offset, text.length()));
        return clamped - (text.lastIndexOf('\n', clamped - 1) + 1) + 1;
    }

    // The compile errors of a rejected snippet, on the cell's own lines and columns: the positions JShell gives are in the
    // text of the snippet it was handed, which begins at chunkStart of the cell.
    private static List<Object> diagnosticsOf(Snippet snippet, String code, int chunkStart) {
        List<Object> list = new ArrayList<>();
        jshell.diagnostics(snippet).forEach(d -> {
            long start = d.getStartPosition() >= 0 ? d.getStartPosition() : d.getPosition();
            long end = d.getEndPosition() >= start ? d.getEndPosition() : start;
            int from = chunkStart + (int) Math.max(0, start);
            int to = chunkStart + (int) Math.max(0, end);
            Map<String, Object> item = new LinkedHashMap<>();
            item.put("severity", d.isError() ? "error" : "warning");
            item.put("code", d.getCode());
            item.put("message", d.getMessage(null).trim());
            item.put("line", lineAt(code, from));
            item.put("column", columnAt(code, from));
            item.put("endLine", lineAt(code, to));
            item.put("endColumn", Math.max(columnAt(code, to), columnAt(code, from) + 1));
            list.add(item);
        });
        return list;
    }

    private static void sendCompileError(String id, Snippet snippet, String code, int chunkStart, int startLine) {
        List<Object> diagnostics = diagnosticsOf(snippet, code, chunkStart);
        StringBuilder text = new StringBuilder();
        String first = "";
        int line = startLine;
        for (Object o : diagnostics) {
            Map<?, ?> d = (Map<?, ?>) o;
            if (first.isEmpty()) { first = String.valueOf(d.get("message")); line = (Integer) d.get("line"); }
            text.append("(").append(d.get("line")).append(",").append(d.get("column")).append("): ")
                .append(d.get("severity")).append(" ").append(d.get("code")).append(": ").append(d.get("message")).append("\n");
        }
        send(sendErrorFields(id, "CompileError", first.isEmpty() ? "compilation failed" : first, text.toString(), line, diagnostics));
    }

    // An exception of the cell's code: what kind it was, what it said, and the line of the cell it went through: the innermost
    // frame in a snippet this cell declared or ran (JShell names a frame "#<snippet id>:<line>").
    private static void sendRuntimeError(String id, JShellException exception, int snippetStartLine) {
        String kind = exception instanceof EvalException eval ? eval.getExceptionClassName() : exception.getClass().getName();
        String shortKind = kind.substring(kind.lastIndexOf('.') + 1);
        int line = snippetStartLine;
        StringBuilder trace = new StringBuilder(kind).append(exception.getMessage() != null ? ": " + exception.getMessage() : "").append("\n");
        boolean found = false;
        for (StackTraceElement frame : exception.getStackTrace()) {
            String file = frame.getFileName();
            if (file == null || !file.startsWith("#")) continue;
            Integer start = snippetStartLines.get(file.substring(1));
            if (start == null) continue;
            int at = start + Math.max(0, frame.getLineNumber() - 1);
            trace.append("    at ").append(frame.getMethodName().isEmpty() ? "<cell>" : frame.getMethodName()).append(" (line ").append(at).append(")\n");
            if (!found) { line = at; found = true; }
        }
        send(sendErrorFields(id, shortKind, exception.getMessage() == null ? "" : exception.getMessage(), trace.toString(), line, null));
    }

    private static void handleExecute(String id, String code, String cell) {
        currentRequestId = id;
        executionCount++;
        interruptRequested = false;
        snippetStartLines.clear();
        kernelInput.startCell();

        // A program written for a script (imports of the Display package, a class with main) runs here too: Display and
        // Visualizer are already loaded, and a class that declares main has it called once it is declared.
        String mainClass = mainClassOf(code);
        // (the line is emptied, not removed, and spaces and tabs only: the lines after it keep their numbers)
        code = code.replaceAll("(?m)^[ \\t]*import\\s+(com\\.frypdf\\.display|fry)\\.[\\w*]+\\s*;[ \\t]*$", "");

        SourceCodeAnalysis sca = jshell.sourceCodeAnalysis();
        String remaining = code;
        boolean hasError = false;
        String lastValue = null;
        Snippet lastSnippet = null;

        while (!remaining.trim().isEmpty()) {
            SourceCodeAnalysis.CompletionInfo info = sca.analyzeCompletion(remaining);
            if (info.source().trim().isEmpty()) break;

            int chunkStart = code.length() - remaining.length();
            int leading = 0;
            while (leading < info.source().length() && Character.isWhitespace(info.source().charAt(leading))) leading++;
            int startLine = lineAt(code, chunkStart + leading);

            List<SnippetEvent> events = jshell.eval(info.source());
            for (SnippetEvent e : events) {
                snippetStartLines.put(e.snippet().id(), startLine);
                if (e.status() == Snippet.Status.REJECTED) {
                    hasError = true;
                    sendCompileError(id, e.snippet(), code, chunkStart, startLine);
                    break;
                }
                if (e.exception() != null) {
                    hasError = true;
                    if (interruptRequested) break;
                    sendRuntimeError(id, e.exception(), startLine);
                    break;
                }
                // The value of an expression is shown, as in the other languages; a declaration (`int x = 7;`) is not an
                // expression, though JShell reports its value too.
                if (e.value() != null && !e.value().isEmpty() && isValueExpression(e.snippet())) {
                    lastValue = e.value();
                    lastSnippet = e.snippet();
                }
            }

            if (hasError) break;
            remaining = info.remaining();
        }

        if (!hasError && mainClass != null) {
            for (SnippetEvent e : jshell.eval(mainClass + ".main(new String[0]);")) {
                if (e.status() == Snippet.Status.REJECTED) {
                    hasError = true;
                    sendCompileError(id, e.snippet(), mainClass + ".main(new String[0]);", 0, 1);
                    break;
                }
                if (e.exception() != null) {
                    hasError = true;
                    if (interruptRequested) break;
                    snippetStartLines.put(e.snippet().id(), 1);
                    sendRuntimeError(id, e.exception(), 1);
                    break;
                }
            }
        }

        snippetPs.flush();
        snippetErrPs.flush();
        flushText();

        // The last expression: an object opens in the Object Explorer, as a C# value does; a scalar, null, or an object
        // with nothing to list is JShell's own text.
        if (!hasError && !interruptRequested && lastValue != null && !showAsObject(lastSnippet)) {
            sendStream(id, "stdout", lastValue + "\n");
        }

        // (a stopped cell can end without an exception: JShell just returns)
        if (interruptRequested) {
            send(Map.of("type", "reply", "id", id, "status", "interrupted"));
        } else if (hasError) {
            send(Map.of("type", "reply", "id", id, "status", "error"));
        } else {
            send(Map.of("type", "reply", "id", id, "status", "ok", "executionCount", executionCount));
        }

        currentRequestId = null;
    }

    // True when the value of this snippet (the temporary JShell makes for an expression, or a variable named on its own)
    // was shown by FryInspector, which does not for a scalar, null, or an object with nothing to list.
    private static boolean showAsObject(Snippet snippet) {
        String name = null;
        if (snippet instanceof VarSnippet v && v.subKind() == Snippet.SubKind.TEMP_VAR_EXPRESSION_SUBKIND) {
            name = v.name();
        } else if (snippet instanceof ExpressionSnippet x && x.subKind() == Snippet.SubKind.VAR_VALUE_SUBKIND) {
            name = x.name();
        }
        if (name == null) return false;

        try {
            List<SnippetEvent> events = jshell.eval("FryInspector.show(" + name + ");");
            snippetPs.flush();
            return !events.isEmpty() && events.get(0).status() == Snippet.Status.VALID && "true".equals(events.get(0).value());
        } catch (Exception ex) {
            return false;
        }
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
        sendNow(Map.of("type", "stream", "id", id, "name", name, "text", text));
    }

    // An `error` message: what failed, the text to show, the failing line of the cell, and (for a compile error) the
    // diagnostics with their columns.
    private static Map<String, Object> sendErrorFields(String id, String ename, String evalue, String traceback, int line, List<Object> diagnostics) {
        Map<String, Object> message = new LinkedHashMap<>();
        message.put("type", "error");
        message.put("id", id);
        message.put("ename", ename);
        message.put("evalue", evalue);
        message.put("traceback", traceback);
        message.put("line", line);
        if (diagnostics != null && !diagnostics.isEmpty()) message.put("diagnostics", diagnostics);
        return message;
    }

    // A message of the protocol: after the text the cell printed before it.
    private static void send(Map<String, Object> map) {
        if (!"stream".equals(map.get("type"))) flushText();
        sendNow(map);
    }

    private static void sendNow(Map<String, Object> map) {
        StringBuilder sb = new StringBuilder("{");
        int i = 0;
        for (Map.Entry<String, Object> e : map.entrySet()) {
            if (i++ > 0) sb.append(",");
            sb.append("\"").append(e.getKey()).append("\":").append(formatJsonValue(e.getValue()));
        }
        sb.append("}\n");
        synchronized (protocolOut) {
            protocolOut.print(sb.toString());
            protocolOut.flush();
        }
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
