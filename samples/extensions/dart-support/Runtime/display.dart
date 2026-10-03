import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';

const String _kChartMime = 'application/vnd.fry.chart.v1+json';
const String _kPlot3dMime = 'application/vnd.fry.plot3d.v1+json';
const String _kVisualizerMime = 'application/vnd.fry.visualizer.v1+json';
const String _kTableMime = 'application/vnd.fry.table+json';
const String _kDisplayMarker = '__FRY_DISPLAY__';

const List<String> _kPointerColors = [
  '#38bdf8', '#a855f7', '#f43f5e', '#10b981', '#eab308', '#06b6d4'
];

Socket? _eventSocket;
bool _eventSocketConnecting = false;
final Map<String, Map<String, List<Function>>> _subscribers = {};

void _ensureEventSocket() {
  final addr = Platform.environment['FRY_EVENTS'];
  if (addr == null || addr.isEmpty || _eventSocket != null || _eventSocketConnecting) return;
  _eventSocketConnecting = true;
  final parts = addr.split(':');
  if (parts.length != 2) {
    _eventSocketConnecting = false;
    return;
  }
  final host = parts[0];
  final port = int.tryParse(parts[1]) ?? 0;
  if (port == 0) {
    _eventSocketConnecting = false;
    return;
  }

  Socket.connect(host, port).then((socket) {
    _eventSocket = socket;
    _eventSocketConnecting = false;
    final token = Platform.environment['FRY_EVENTS_TOKEN'] ?? '';
    socket.writeln(jsonEncode({'type': 'hello', 'token': token}));

    for (final entry in _subscribers.entries) {
      final events = entry.value.keys.toList();
      if (events.isNotEmpty) {
        _emitRaw({'type': 'subscribe', 'display_id': entry.key, 'events': events});
      }
    }

    utf8.decoder.bind(socket).transform(const LineSplitter()).listen((line) {
      final trimmed = line.trim();
      if (trimmed.isEmpty) return;
      try {
        final msg = jsonDecode(trimmed);
        _dispatch(msg);
      } catch (_) {}
    }, onDone: () {
      _eventSocket = null;
    }, onError: (_) {
      _eventSocket = null;
    });
  }).catchError((_) {
    _eventSocketConnecting = false;
  });
}

void _dispatch(dynamic msg) {
  if (msg is! Map) return;
  final displayId = (msg['display_id'] ?? (msg['data'] is Map ? msg['data']['display_id'] : null))?.toString();
  if (displayId == null) return;
  final eventData = (msg['event'] is Map ? msg['event'] : msg) as Map;
  final eventName = (eventData['event'] ?? 'click').toString().toLowerCase();

  final map = _subscribers[displayId];
  if (map != null) {
    final cbs = map[eventName];
    if (cbs != null) {
      for (final cb in cbs) {
        try {
          if (cb is void Function(dynamic)) {
            cb(eventData);
          } else if (cb is void Function()) {
            cb();
          } else {
            Function.apply(cb, [eventData]);
          }
        } catch (e) {
          stderr.writeln('[Display event error]: $e');
        }
      }
    }
  }
}

void _emitRaw(Map<String, dynamic> msg) {
  stdout.writeln('$_kDisplayMarker ${jsonEncode(msg)}');
}

/// High-level display and interactive visualization engine for Dart in FrySharp.
class Display {
  static String _newId() =>
      Random().nextInt(0x7fffffff).toRadixString(36) +
      DateTime.now().millisecondsSinceEpoch.toRadixString(36);

  static DisplayHandle _send(String mime, Map<String, dynamic> spec) {
    final id = _newId();
    _emit('display', mime, spec, id);
    return DisplayHandle(mime, spec, id);
  }

  static void _emit(String type, String mime, Map<String, dynamic> spec, String displayId) {
    final kind = spec['kind'] ?? 'visual';
    final title = spec.containsKey('title') ? ': ${spec['title']}' : '';
    final fallback = '$kind visual$title';
    final payload = {
      'type': type,
      'data': {
        mime: spec,
        'text/plain': fallback,
      },
      'metadata': <String, dynamic>{},
      'transient': {
        'display_id': displayId,
      },
    };
    _emitRaw(payload);
  }

  // --------------------------------------------------------------------------
  // 2D Charts
  // --------------------------------------------------------------------------

  /// General chart builder (kind: 'line', 'bar', 'scatter', 'pie', 'area', 'donut', 'histogram').
  static DisplayHandle chart(dynamic data, [dynamic titleOrOpts, dynamic extra]) {
    final spec = _buildChartSpec(data, kind: 'line', titleOrOpts: titleOrOpts, extra: extra);
    return _send(_kChartMime, spec);
  }

  /// Line chart helper.
  static DisplayHandle lineChart(dynamic data, [dynamic titleOrOpts, dynamic extra]) {
    final spec = _buildChartSpec(data, kind: 'line', titleOrOpts: titleOrOpts, extra: extra);
    return _send(_kChartMime, spec);
  }
  static DisplayHandle line_chart(dynamic data, [dynamic titleOrOpts, dynamic extra]) => lineChart(data, titleOrOpts, extra);

  /// Bar chart helper.
  static DisplayHandle barChart(dynamic data, [dynamic titleOrOpts, dynamic extra]) {
    final spec = _buildChartSpec(data, kind: 'bar', titleOrOpts: titleOrOpts, extra: extra);
    return _send(_kChartMime, spec);
  }
  static DisplayHandle bar_chart(dynamic data, [dynamic titleOrOpts, dynamic extra]) => barChart(data, titleOrOpts, extra);

  /// Scatter chart helper.
  static DisplayHandle scatterChart(dynamic data, [dynamic titleOrOpts, dynamic extra]) {
    final spec = _buildChartSpec(data, kind: 'scatter', titleOrOpts: titleOrOpts, extra: extra);
    return _send(_kChartMime, spec);
  }
  static DisplayHandle scatter_chart(dynamic data, [dynamic titleOrOpts, dynamic extra]) => scatterChart(data, titleOrOpts, extra);

  /// Pie chart helper.
  static DisplayHandle pieChart(dynamic data, [dynamic titleOrOpts, dynamic extra]) {
    final spec = _buildChartSpec(data, kind: 'pie', titleOrOpts: titleOrOpts, extra: extra);
    return _send(_kChartMime, spec);
  }
  static DisplayHandle pie_chart(dynamic data, [dynamic titleOrOpts, dynamic extra]) => pieChart(data, titleOrOpts, extra);

  /// Donut chart helper.
  static DisplayHandle donutChart(dynamic data, [dynamic titleOrOpts, dynamic extra]) {
    final spec = _buildChartSpec(data, kind: 'donut', titleOrOpts: titleOrOpts, extra: extra);
    return _send(_kChartMime, spec);
  }
  static DisplayHandle donut_chart(dynamic data, [dynamic titleOrOpts, dynamic extra]) => donutChart(data, titleOrOpts, extra);

  /// Area chart helper.
  static DisplayHandle areaChart(dynamic data, [dynamic titleOrOpts, dynamic extra]) {
    final spec = _buildChartSpec(data, kind: 'area', titleOrOpts: titleOrOpts, extra: extra);
    return _send(_kChartMime, spec);
  }
  static DisplayHandle area_chart(dynamic data, [dynamic titleOrOpts, dynamic extra]) => areaChart(data, titleOrOpts, extra);

  /// Histogram chart helper.
  static DisplayHandle histogram(dynamic data, [dynamic titleOrOpts, dynamic extra]) {
    final spec = _buildChartSpec(data, kind: 'histogram', titleOrOpts: titleOrOpts, extra: extra);
    return _send(_kChartMime, spec);
  }

  // --------------------------------------------------------------------------
  // 3D Visuals & Plots
  // --------------------------------------------------------------------------

  /// General 3D plot builder.
  static DisplayHandle chart3d(dynamic data, [dynamic titleOrOpts, dynamic extra]) =>
      plot3d(data, titleOrOpts, extra);

  /// Plot3D visual emitter.
  static DisplayHandle plot3d(dynamic data, [dynamic titleOrOpts, dynamic extra]) {
    if (data is num Function(num x, num y)) {
      return surface(data, titleOrOpts, extra);
    }
    if (data is List && data.isNotEmpty && data.first is List) {
      final firstRow = data.first as List;
      if (firstRow.isNotEmpty && firstRow.length == 3 && data.length > 5) {
        return scatter3d(data, titleOrOpts, extra);
      }
      return surface3d(data, titleOrOpts, extra);
    }
    final opts = _parseOpts(titleOrOpts, extra);
    final spec = <String, dynamic>{
      'kind': opts['kind'] ?? 'surface',
    };
    _applyOptions(spec, opts);
    return _send(_kPlot3dMime, spec);
  }

  /// 3D scatter plot of points [[x, y, z], ...].
  static DisplayHandle scatter3d(dynamic points, [dynamic titleOrOpts, dynamic extra]) {
    final opts = _parseOpts(titleOrOpts, extra);
    final x = <dynamic>[];
    final y = <dynamic>[];
    final z = <dynamic>[];
    if (points is List) {
      for (final p in points) {
        if (p is List && p.length >= 3) {
          x.add(p[0]);
          y.add(p[1]);
          z.add(p[2]);
        }
      }
    }
    final spec = {
      'kind': 'scatter',
      'series': [
        {'x': x, 'y': y, 'z': z}
      ],
    };
    _applyOptions(spec, opts);
    return _send(_kPlot3dMime, spec);
  }

  /// 3D surface plot from a 2D height grid [[z00, z01, ...], ...].
  static DisplayHandle surface3d(dynamic grid, [dynamic titleOrOpts, dynamic extra]) {
    final opts = _parseOpts(titleOrOpts, extra);
    int numRows = 0;
    int numCols = 0;
    if (grid is List) {
      numRows = grid.length;
      numCols = grid.isNotEmpty && grid.first is List ? (grid.first as List).length : 0;
    }
    final spec = {
      'kind': 'surface',
      'surface': {
        'x': {'min': 0, 'max': max(0, numCols - 1)},
        'y': {'min': 0, 'max': max(0, numRows - 1)},
        'z': grid,
      },
    };
    _applyOptions(spec, opts);
    return _send(_kPlot3dMime, spec);
  }

  /// 3D surface plot from a mathematical function z = f(x, y).
  static DisplayHandle surface(
    num Function(num x, num y) fn, [
    dynamic titleOrOpts,
    dynamic extra,
  ]) {
    final opts = _parseOpts(titleOrOpts, extra);
    final num xMin = opts['xMin'] ?? -5;
    final num xMax = opts['xMax'] ?? 5;
    final num yMin = opts['yMin'] ?? -5;
    final num yMax = opts['yMax'] ?? 5;
    final int resolution = opts['resolution'] ?? 25;
    final String colorMap = opts['colorMap'] ?? 'viridis';

    final dx = (xMax - xMin) / (resolution - 1);
    final dy = (yMax - yMin) / (resolution - 1);
    final zGrid = <List<num>>[];

    for (int r = 0; r < resolution; r++) {
      final y = yMin + r * dy;
      final row = <num>[];
      for (int c = 0; c < resolution; c++) {
        final x = xMin + c * dx;
        row.add(fn(x, y));
      }
      zGrid.add(row);
    }

    final spec = {
      'kind': 'surface',
      'colorMap': colorMap,
      'surface': {
        'x': {'min': xMin, 'max': xMax},
        'y': {'min': yMin, 'max': yMax},
        'z': zGrid,
      },
    };
    _applyOptions(spec, opts);
    return _send(_kPlot3dMime, spec);
  }

  /// Emits 3D surface plot from precomputed 2D height grid.
  static DisplayHandle surfaceGrid(List<List<num>> grid, [dynamic titleOrOpts, dynamic extra]) =>
      surface3d(grid, titleOrOpts, extra);

  /// 3D network graph plot from adjacency map.
  static DisplayHandle graph3d(dynamic data, [dynamic titleOrOpts, dynamic extra]) {
    final opts = _parseOpts(titleOrOpts, extra);
    final nodeSet = <String>{};
    final edges = <Map<String, dynamic>>[];
    if (data is Map) {
      for (final entry in data.entries) {
        final u = entry.key.toString();
        nodeSet.add(u);
        if (entry.value is List) {
          for (final v in entry.value) {
            final vs = v.toString();
            nodeSet.add(vs);
            edges.add({'from': u, 'to': vs});
          }
        }
      }
    }
    final nodes = nodeSet.map((n) => {'id': n}).toList();
    final spec = {
      'kind': 'graph',
      'graph': {
        'directed': true,
        'nodes': nodes,
        'edges': edges,
      },
    };
    _applyOptions(spec, opts);
    return _send(_kPlot3dMime, spec);
  }

  /// 3D trajectory plot.
  static DisplayHandle trajectory3d(dynamic points, [dynamic titleOrOpts, dynamic extra]) {
    final opts = _parseOpts(titleOrOpts, extra);
    final x = <dynamic>[];
    final y = <dynamic>[];
    final z = <dynamic>[];
    if (points is List) {
      for (final p in points) {
        if (p is List && p.length >= 3) {
          x.add(p[0]);
          y.add(p[1]);
          z.add(p[2]);
        }
      }
    }
    final spec = {
      'kind': 'trajectory',
      'series': [
        {'x': x, 'y': y, 'z': z}
      ],
    };
    _applyOptions(spec, opts);
    return _send(_kPlot3dMime, spec);
  }

  /// 3D voxel bar plot.
  static DisplayHandle voxelBar3d(dynamic data, [dynamic titleOrOpts, dynamic extra]) {
    final opts = _parseOpts(titleOrOpts, extra);
    final spec = <String, dynamic>{'kind': 'voxelBar', 'data': data};
    _applyOptions(spec, opts);
    return _send(_kPlot3dMime, spec);
  }
  static DisplayHandle voxel_bar3d(dynamic data, [dynamic titleOrOpts, dynamic extra]) => voxelBar3d(data, titleOrOpts, extra);

  // --------------------------------------------------------------------------
  // Data Structure Visualizers (callable directly on Display or Visualizer)
  // --------------------------------------------------------------------------

  /// 2D Matrix / Grid visualizer.
  static DisplayHandle matrix(dynamic grid, [dynamic titleOrOpts, dynamic extra]) =>
      Visualizer.matrix(grid, titleOrOpts, extra);

  /// Islands matrix search visualizer.
  static DisplayHandle islands(dynamic grid, [dynamic titleOrOpts, dynamic extra]) =>
      Visualizer.islands(grid, titleOrOpts, extra);

  /// 1D Array with movable named pointers.
  static DisplayHandle array(dynamic values, [dynamic pointersOrTitle, dynamic titleOrExtra]) =>
      Visualizer.array(values, pointersOrTitle, titleOrExtra);

  /// Binary / N-ary tree visualizer.
  static DisplayHandle tree(dynamic root, [dynamic titleOrOpts, dynamic extra]) =>
      Visualizer.tree(root, titleOrOpts, extra);

  /// Graph visualizer from adjacency map.
  static DisplayHandle graph(dynamic adj, [dynamic titleOrOpts, dynamic extra]) =>
      Visualizer.graph(adj, titleOrOpts, extra);

  /// Linked list visualizer.
  static DisplayHandle linkedList(dynamic head, [dynamic titleOrOpts, dynamic extra]) =>
      Visualizer.linkedList(head, titleOrOpts, extra);
  static DisplayHandle linked_list(dynamic head, [dynamic titleOrOpts, dynamic extra]) =>
      Visualizer.linkedList(head, titleOrOpts, extra);

  /// Bar array visualizer (for sorting algorithms).
  static DisplayHandle bars(dynamic values, [dynamic titleOrOpts, dynamic extra]) =>
      Visualizer.bars(values, titleOrOpts, extra);

  // --------------------------------------------------------------------------
  // Tables & Dumps
  // --------------------------------------------------------------------------

  /// Emits interactive sortable data table.
  static dynamic table(dynamic data, [dynamic title]) {
    final titleStr = title is String ? title : (title is Map ? title['title']?.toString() : null);
    final tableSpec = _buildTableSpec(data, title: titleStr);
    stdout.writeln('$_kDisplayMarker ${jsonEncode({
      'type': 'display',
      'data': {
        _kTableMime: tableSpec,
        'text/plain': 'Table: ${titleStr ?? "Data"} (${tableSpec["totalRows"]} rows)',
      },
      'metadata': <String, dynamic>{},
    })}');
    return data;
  }

  /// Inspects and dumps variable as rich table.
  static dynamic dump(dynamic data, [dynamic title]) {
    table(data, title);
    return data;
  }
  static dynamic show(dynamic data, [dynamic title]) => dump(data, title);

  /// Emits raw HTML chunk.
  static void html(String htmlContent) {
    stdout.writeln('$_kDisplayMarker ${jsonEncode({
      'type': 'display',
      'data': {
        'text/html': htmlContent,
        'text/plain': htmlContent,
      },
      'metadata': <String, dynamic>{},
    })}');
  }

  /// Emits markdown text chunk.
  static void markdown(String markdownContent) {
    stdout.writeln('$_kDisplayMarker ${jsonEncode({
      'type': 'display',
      'data': {
        'text/markdown': markdownContent,
        'text/plain': markdownContent,
      },
      'metadata': <String, dynamic>{},
    })}');
  }

  /// Emits image chunk (base64 or file path).
  static void image(dynamic data, [String format = 'PNG']) {
    if (data == null) return;
    String b64 = '';
    final mime = format.toUpperCase() == 'JPEG' || format.toUpperCase() == 'JPG'
        ? 'image/jpeg'
        : 'image/png';
    if (data is List<int>) {
      b64 = base64Encode(data);
    } else if (data is String) {
      final file = File(data);
      if (file.existsSync()) {
        b64 = base64Encode(file.readAsBytesSync());
      } else {
        b64 = data.startsWith('data:image') ? (data.split(',').length > 1 ? data.split(',')[1] : data) : data;
      }
    }
    stdout.writeln('$_kDisplayMarker ${jsonEncode({
      'type': 'display',
      'data': {
        mime: b64,
        'text/plain': '[Image: $format]',
      },
      'metadata': <String, dynamic>{},
    })}');
  }

  /// Emits formatted JSON chunk.
  static void json(dynamic data) {
    stdout.writeln('$_kDisplayMarker ${jsonEncode({
      'type': 'display',
      'data': {
        'application/json': jsonEncode(data),
        'text/plain': const JsonEncoder.withIndent('  ').convert(data),
      },
      'metadata': <String, dynamic>{},
    })}');
  }

  /// Shares a variable with other cells and kernels via Fry notebook protocol.
  static void share(String name, dynamic value) {
    stdout.writeln('__FRY_SHARE__ ${jsonEncode({'name': name, 'json': jsonEncode(value)})}');
  }

  /// Keeps the program or cell active to receive loopback events (e.g. click events).
  static Future<void> wait([int seconds = 10]) async {
    await Future.delayed(Duration(seconds: seconds));
  }
}

/// Specialized data-structure visualizer for algorithms, trees, grids, and graphs.
class Visualizer {
  static DisplayHandle _send(Map<String, dynamic> spec) {
    final id = Display._newId();
    Display._emit('display', _kVisualizerMime, spec, id);
    return DisplayHandle(_kVisualizerMime, spec, id);
  }

  /// 2D Matrix / Grid visualizer.
  static DisplayHandle matrix(dynamic grid, [dynamic titleOrOpts, dynamic extra]) {
    final opts = _parseOpts(titleOrOpts, extra);
    final values = <List<dynamic>>[];
    final cells = <Map<String, dynamic>>[];
    if (grid is List) {
      for (int r = 0; r < grid.length; r++) {
        final row = grid[r] as List;
        final rowVals = <dynamic>[];
        for (int c = 0; c < row.length; c++) {
          final v = row[c];
          rowVals.add(v);
          final numVal = v is num ? v : (num.tryParse(v.toString()) ?? 0);
          cells.add({
            'row': r,
            'col': c,
            'terrain': numVal > 0 ? 'land' : 'water',
          });
        }
        values.add(rowVals);
      }
    }
    final spec = {
      'kind': 'matrix',
      'state': {
        'grid': {
          'values': values,
          'cells': cells,
          'inferTerrain': false,
        },
      },
      'showCoordinates': true,
      'showValues': true,
      'cellSize': 38,
      'fitOnOpen': true,
    };
    _applyOptions(spec, opts);
    return _send(spec);
  }

  /// Alias for matrix.
  static DisplayHandle grid(dynamic grid, [dynamic titleOrOpts, dynamic extra]) =>
      matrix(grid, titleOrOpts, extra);

  /// Islands matrix search visualizer.
  static DisplayHandle islands(dynamic grid, [dynamic titleOrOpts, dynamic extra]) {
    final opts = _parseOpts(titleOrOpts, extra);
    final values = <List<dynamic>>[];
    final cells = <Map<String, dynamic>>[];
    if (grid is List) {
      for (int r = 0; r < grid.length; r++) {
        final row = grid[r] as List;
        final rowVals = <dynamic>[];
        for (int c = 0; c < row.length; c++) {
          final v = row[c];
          rowVals.add(v);
          final numVal = v is num ? v : (num.tryParse(v.toString()) ?? 0);
          cells.add({
            'row': r,
            'col': c,
            'terrain': numVal > 0 ? 'land' : 'water',
          });
        }
        values.add(rowVals);
      }
    }
    final spec = {
      'kind': 'islands',
      'state': {
        'grid': {
          'values': values,
          'cells': cells,
          'inferTerrain': false,
        },
      },
      'showCoordinates': true,
      'showValues': true,
      'cellSize': 38,
      'fitOnOpen': true,
    };
    _applyOptions(spec, opts);
    return _send(spec);
  }

  /// 1D Array with movable named pointers.
  static DisplayHandle array(dynamic values, [dynamic pointersOrTitle, dynamic titleOrExtra]) {
    Map<String, dynamic>? pointers;
    dynamic titleOrOpts = pointersOrTitle;
    dynamic extra = titleOrExtra;

    if (pointersOrTitle is Map) {
      final isPointerMap = pointersOrTitle.values.every((v) => v is num || int.tryParse(v.toString()) != null);
      if (isPointerMap) {
        pointers = Map<String, dynamic>.from(pointersOrTitle);
        titleOrOpts = titleOrExtra;
        extra = null;
      }
    }

    final opts = _parseOpts(titleOrOpts, extra);
    final cleanValues = (values is String ? values.split('') : (values as List)).toList();
    final ptrList = <Map<String, dynamic>>[];
    if (pointers != null) {
      int pi = 0;
      for (final e in pointers.entries) {
        ptrList.add({
          'name': e.key.toString(),
          'at': e.value is num ? e.value : (int.tryParse(e.value.toString()) ?? 0),
          'color': _kPointerColors[pi % _kPointerColors.length],
        });
        pi++;
      }
    }
    final spec = {
      'kind': 'arrayPointers',
      'state': {
        'array': {'values': cleanValues},
      },
      if (ptrList.isNotEmpty) 'pointers': ptrList,
      'showCoordinates': true,
      'showValues': true,
      'cellSize': 38,
      'fitOnOpen': true,
    };
    _applyOptions(spec, opts);
    return _send(spec);
  }

  /// Binary / N-ary tree visualizer. Supports lists (level-order) or objects with val/left/right.
  static DisplayHandle tree(dynamic root, [dynamic titleOrOpts, dynamic extra]) {
    final opts = _parseOpts(titleOrOpts, extra);
    Map<String, dynamic> treeState;

    if (root is List) {
      if (root.isEmpty || root.first == null) {
        treeState = {'root': '', 'nodes': []};
      } else {
        int count = 1;
        final rootNode = {'id': 'node_1', 'value': root.first.toString()};
        final queue = <Map<String, dynamic>>[rootNode];
        final childrenMap = <String, List<Map<String, dynamic>>>{};
        int idx = 1;
        while (queue.isNotEmpty && idx < root.length) {
          final cur = queue.removeAt(0);
          for (final side in ['left', 'right']) {
            if (idx < root.length) {
              final val = root[idx];
              if (val != null) {
                count++;
                final child = {'id': 'node_$count', 'value': val.toString()};
                cur[side] = child['id'];
                childrenMap.putIfAbsent(cur['id'] as String, () => []).add(child);
                queue.add(child);
              }
              idx++;
            }
          }
        }
        final listed = <Map<String, dynamic>>[];
        void walk(Map<String, dynamic> n) {
          listed.add(n);
          final kids = childrenMap[n['id']];
          if (kids != null) {
            for (final k in kids) walk(k);
          }
        }
        walk(rootNode);
        treeState = {'root': 'node_1', 'nodes': listed};
      }
    } else {
      final nodes = <Map<String, dynamic>>[];
      int count = 0;
      String? parseNode(dynamic n) {
        if (n == null) return null;
        count++;
        final id = 'node_$count';
        String val = '';
        dynamic left;
        dynamic right;
        try {
          val = (n.val ?? n.value ?? n.data ?? n).toString();
          left = n.left;
          right = n.right;
        } catch (_) {
          val = n.toString();
        }
        final specNode = <String, dynamic>{'id': id, 'value': val};
        nodes.add(specNode);
        if (left != null) {
          final lId = parseNode(left);
          if (lId != null) specNode['left'] = lId;
        }
        if (right != null) {
          final rId = parseNode(right);
          if (rId != null) specNode['right'] = rId;
        }
        return id;
      }
      parseNode(root);
      treeState = {'root': 'node_1', 'nodes': nodes};
    }

    final spec = {
      'kind': 'tree',
      'state': {
        'tree': treeState,
      },
      'showCoordinates': true,
      'showValues': true,
      'cellSize': 38,
      'fitOnOpen': true,
    };
    _applyOptions(spec, opts);
    return _send(spec);
  }

  /// Graph visualizer from adjacency map.
  static DisplayHandle graph(dynamic adj, [dynamic titleOrOpts, dynamic extra]) {
    final opts = _parseOpts(titleOrOpts, extra);
    final nodeSet = <String>{};
    final edges = <Map<String, dynamic>>[];
    if (adj is Map) {
      for (final entry in adj.entries) {
        final u = entry.key.toString();
        nodeSet.add(u);
        if (entry.value is List) {
          for (final v in entry.value) {
            final vs = v.toString();
            nodeSet.add(vs);
            edges.add({'from': u, 'to': vs});
          }
        }
      }
    }
    final nodes = nodeSet.map((n) => {'id': n}).toList();
    final spec = {
      'kind': 'graph',
      'state': {
        'graph': {
          'directed': true,
          'nodes': nodes,
          'edges': edges,
        },
      },
      'showCoordinates': true,
      'showValues': true,
      'cellSize': 38,
      'fitOnOpen': true,
    };
    _applyOptions(spec, opts);
    return _send(spec);
  }

  /// Linked list visualizer.
  static DisplayHandle linkedList(dynamic head, [dynamic titleOrOpts, dynamic extra]) {
    final opts = _parseOpts(titleOrOpts, extra);
    final nodes = <Map<String, dynamic>>[];
    dynamic cur = head;
    int idx = 0;
    while (cur != null && idx < 500) {
      final id = 'n$idx';
      String val = '';
      dynamic next;
      try {
        val = (cur.val ?? cur.value ?? cur.data ?? cur).toString();
        next = cur.next;
      } catch (_) {
        val = cur.toString();
      }
      if (nodes.isNotEmpty) {
        nodes.last['next'] = id;
      }
      nodes.add({'id': id, 'value': val});
      cur = next;
      idx++;
    }
    final spec = {
      'kind': 'linkedList',
      'state': {
        'linkedList': {
          'nodes': nodes,
          'markCycle': false,
        },
      },
      'showCoordinates': true,
      'showValues': true,
      'cellSize': 38,
      'fitOnOpen': true,
    };
    _applyOptions(spec, opts);
    return _send(spec);
  }
  static DisplayHandle linked_list(dynamic head, [dynamic titleOrOpts, dynamic extra]) =>
      linkedList(head, titleOrOpts, extra);

  /// Bar array visualizer.
  static DisplayHandle bars(dynamic values, [dynamic titleOrOpts, dynamic extra]) {
    final opts = _parseOpts(titleOrOpts, extra);
    final clean = <num>[];
    num minVal = 0;
    num maxVal = 0;
    if (values is List) {
      for (final v in values) {
        final n = v is num ? v : (num.tryParse(v.toString()) ?? 0);
        clean.add(n);
        if (n < minVal) minVal = n;
        if (n > maxVal) maxVal = n;
      }
    }
    final spec = {
      'kind': 'bars',
      'state': {
        'bars': {
          'values': clean,
          'min': minVal,
          'max': maxVal,
        },
      },
      'showCoordinates': true,
      'showValues': true,
      'cellSize': 38,
      'fitOnOpen': true,
    };
    _applyOptions(spec, opts);
    return _send(spec);
  }
}

/// Handle to an active on-screen visual with in-place live updating and event capabilities.
class DisplayHandle {
  final String mime;
  Map<String, dynamic> spec;
  final String displayId;
  bool _closed = false;

  DisplayHandle(this.mime, this.spec, this.displayId);

  DisplayHandle update(dynamic specOrTitle) {
    if (_closed) throw StateError('This visual was closed: it cannot be updated.');
    if (specOrTitle is String) {
      spec['title'] = specOrTitle;
    } else if (specOrTitle is Map<String, dynamic>) {
      if (specOrTitle.containsKey('title') && specOrTitle.length == 1) {
        spec['title'] = specOrTitle['title'];
      } else {
        spec = Map<String, dynamic>.from(specOrTitle);
      }
    } else if (specOrTitle is Map) {
      if (specOrTitle.containsKey('title') && specOrTitle.length == 1) {
        spec['title'] = specOrTitle['title']?.toString();
      } else {
        spec = Map<String, dynamic>.from(specOrTitle);
      }
    }
    Display._emit('update_display', mime, spec, displayId);
    return this;
  }

  DisplayHandle on(String event, Function callback) {
    final ev = event.toLowerCase();
    _subscribers.putIfAbsent(displayId, () => {}).putIfAbsent(ev, () => []).add(callback);
    _ensureEventSocket();
    _emitRaw({'type': 'subscribe', 'display_id': displayId, 'events': [ev]});
    return this;
  }

  DisplayHandle onClick(Function callback) => on('click', callback);
  DisplayHandle on_click(Function callback) => on('click', callback);
  DisplayHandle onSelect(Function callback) => on('select', callback);
  DisplayHandle on_select(Function callback) => on('select', callback);
  DisplayHandle onStep(Function callback) => on('step', callback);
  DisplayHandle on_step(Function callback) => on('step', callback);

  DisplayHandle off(String event, [Function? callback]) {
    final ev = event.toLowerCase();
    final map = _subscribers[displayId];
    if (map != null) {
      if (callback != null) {
        map[ev]?.remove(callback);
      } else {
        map.remove(ev);
      }
    }
    _emitRaw({'type': 'unsubscribe', 'display_id': displayId, 'events': [ev]});
    return this;
  }

  void close() {
    _closed = true;
    _subscribers.remove(displayId);
  }
}

// ----------------------------------------------------------------------------
// Internal Spec Parsing Helpers
// ----------------------------------------------------------------------------

Map<String, dynamic> _parseOpts(dynamic titleOrOpts, dynamic extra) {
  final opts = <String, dynamic>{};
  if (titleOrOpts is String) {
    opts['title'] = titleOrOpts;
  } else if (titleOrOpts is Map) {
    for (final e in titleOrOpts.entries) {
      opts[e.key.toString()] = e.value;
    }
  } else if (titleOrOpts is num) {
    opts['bins'] = titleOrOpts.toInt();
  }

  if (extra is String) {
    opts['title'] = extra;
  } else if (extra is Map) {
    for (final e in extra.entries) {
      opts[e.key.toString()] = e.value;
    }
  } else if (extra is num) {
    opts['bins'] = extra.toInt();
  }
  return opts;
}

void _applyOptions(Map<String, dynamic> spec, Map<String, dynamic> opts) {
  if (opts.containsKey('title')) spec['title'] = opts['title'];
  if (opts.containsKey('subtitle')) spec['subtitle'] = opts['subtitle'];
  if (opts.containsKey('bins')) spec['bins'] = opts['bins'];
  if (opts.containsKey('width')) spec['width'] = opts['width'];
  if (opts.containsKey('height')) spec['height'] = opts['height'];
  if (opts.containsKey('color')) spec['color'] = opts['color'];
  if (opts.containsKey('colorMap')) spec['colorMap'] = opts['colorMap'];
  if (opts.containsKey('showCoordinates')) spec['showCoordinates'] = opts['showCoordinates'];
  if (opts.containsKey('showValues')) spec['showValues'] = opts['showValues'];
  if (opts.containsKey('cellSize')) spec['cellSize'] = opts['cellSize'];
  if (opts.containsKey('xAxis')) spec['xAxis'] = opts['xAxis'];
  if (opts.containsKey('yAxis')) spec['yAxis'] = opts['yAxis'];
}

Map<String, dynamic> _buildChartSpec(dynamic data, {String kind = 'line', dynamic titleOrOpts, dynamic extra}) {
  final opts = _parseOpts(titleOrOpts, extra);
  final actualKind = opts['kind'] ?? kind;
  final spec = <String, dynamic>{'kind': actualKind};
  final seriesList = <Map<String, dynamic>>[];

  if (actualKind == 'histogram') {
    final values = <dynamic>[];
    if (data is List) {
      for (final v in data) {
        if (v == null) {
          values.add(null);
        } else if (v is num) {
          values.add(v);
        } else {
          values.add(num.tryParse(v.toString()) ?? 0);
        }
      }
    }
    seriesList.add({'values': values});
  } else if (data is List) {
    if (data.isNotEmpty && data.first is List) {
      final x = <dynamic>[];
      final y = <dynamic>[];
      for (final item in data) {
        if (item is List && item.length >= 2) {
          x.add(item[0]);
          y.add(item[1]);
        }
      }
      seriesList.add({'x': x, 'y': y});
    } else if (data.isNotEmpty && data.first is Map) {
      final y = <dynamic>[];
      final labels = <String>[];
      for (final item in data) {
        final m = item as Map;
        dynamic valKey;
        dynamic labelKey;
        for (final k in m.keys) {
          final s = k.toString().toLowerCase();
          if (valKey == null && ['value', 'y', 'price', 'count', 'total', 'score'].contains(s)) {
            valKey = k;
          }
          if (labelKey == null && ['name', 'label', 'x', 'title', 'key', 'item'].contains(s)) {
            labelKey = k;
          }
        }
        valKey ??= m.keys.isNotEmpty ? m.keys.last : 'value';
        labelKey ??= m.keys.isNotEmpty ? m.keys.first : 'name';

        final valObj = m[valKey];
        y.add(valObj is num ? valObj : (valObj == null ? null : num.tryParse(valObj.toString()) ?? 0));
        labels.add((m[labelKey] ?? '').toString());
      }
      seriesList.add({'y': y, 'labels': labels});
    } else {
      final y = <dynamic>[];
      for (final item in data) {
        if (item == null) {
          y.add(null);
        } else if (item is num) {
          y.add(item);
        } else {
          y.add(num.tryParse(item.toString()) ?? 0);
        }
      }
      seriesList.add({'y': y});
    }
  } else if (data is Map) {
    final isMulti = data.values.isNotEmpty && data.values.first is List;
    if (isMulti) {
      for (final entry in data.entries) {
        final yVals = (entry.value as List).map((e) {
          if (e == null) return null;
          if (e is num) return e;
          return num.tryParse(e.toString()) ?? 0;
        }).toList();
        seriesList.add({'name': entry.key.toString(), 'y': yVals});
      }
    } else {
      final labels = <String>[];
      final y = <dynamic>[];
      for (final entry in data.entries) {
        labels.add(entry.key.toString());
        final val = entry.value;
        if (val == null) {
          y.add(null);
        } else if (val is num) {
          y.add(val);
        } else {
          y.add(num.tryParse(val.toString()) ?? 0);
        }
      }
      seriesList.add({'y': y, 'labels': labels});
    }
  }

  spec['series'] = seriesList;
  _applyOptions(spec, opts);
  return spec;
}

Map<String, dynamic> _buildTableSpec(dynamic data, {String? title}) {
  final columns = <String>[];
  final numeric = <bool>[];
  final rows = <List<dynamic>>[];

  if (data is List) {
    if (data.isNotEmpty && data.first is Map) {
      final firstMap = data.first as Map;
      for (final k in firstMap.keys) {
        columns.add(k.toString());
        final val = firstMap[k];
        numeric.add(val is num);
      }
      for (final item in data) {
        if (item is Map) {
          final row = <dynamic>[];
          for (final col in columns) {
            row.add(item[col]);
          }
          rows.add(row);
        }
      }
    } else {
      columns.add('Value');
      numeric.add(data.isNotEmpty && data.first is num);
      for (final item in data) {
        rows.add([item]);
      }
    }
  } else if (data is Map) {
    columns.addAll(['Key', 'Value']);
    numeric.addAll([false, data.values.isNotEmpty && data.values.first is num]);
    for (final e in data.entries) {
      rows.add([e.key.toString(), e.value]);
    }
  } else {
    columns.add('Value');
    numeric.add(data is num);
    rows.add([data]);
  }

  return {
    'title': title ?? 'Data Table',
    'columns': columns,
    'numeric': numeric,
    'rows': rows,
    'totalRows': rows.length,
    'totalColumns': columns.length,
  };
}
