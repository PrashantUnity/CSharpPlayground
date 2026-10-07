/**
 * The FryPDF studio's display API for JavaScript / Node.js, the same in a program that is run and in a notebook cell.
 *
 *     const { Display } = require('fry'); // or global Display
 *     const chart = Display.lineChart([3, 1, 4], { title: 'Sales', yAxis: { title: 'EUR' } });
 *     chart.onClick(e => console.log('clicked', e.target));
 *     chart.update({ title: 'Sales (updated)' });
 *     Display.wait();
 */

const fs = require('fs');
const util = require('util');
const net = require('net');

const CHART_MIME = 'application/vnd.fry.chart.v1+json';
const PLOT3D_MIME = 'application/vnd.fry.plot3d.v1+json';
const VISUALIZER_MIME = 'application/vnd.fry.visualizer.v1+json';
const TABLE_MIME = 'application/vnd.fry.table+json';
const DISPLAY_MARKER = '__FRY_DISPLAY__';

// ---------------------------------------------------------------------------------------------------------- channel
let _channel = null;

function setChannel(channel) {
    _channel = channel;
}

function channel() {
    if (_channel) return _channel;
    return _defaultChannel;
}

const _subscribers = new Map(); // displayId -> Map(event -> Set(callbacks))
let _eventSocket = null;
let _eventSocketConnecting = false;
const _pendingEvents = [];

function ensureEventSocket() {
    const addr = process.env.FRY_EVENTS;
    if (!addr || _eventSocket || _eventSocketConnecting) return;
    _eventSocketConnecting = true;
    const parts = addr.split(':');
    const host = parts[0];
    const port = parseInt(parts[1], 10);

    const client = net.createConnection({ host, port }, () => {
        _eventSocket = client;
        _eventSocketConnecting = false;
        const token = process.env.FRY_EVENTS_TOKEN || '';
        client.write(JSON.stringify({ type: 'hello', token }) + '\n');
        for (const [displayId, map] of _subscribers.entries()) {
            const events = Array.from(map.keys());
            if (events.length > 0) {
                emitRaw({ type: 'subscribe', display_id: displayId, events });
            }
        }
    });

    let buf = '';
    client.on('data', chunk => {
        buf += chunk.toString('utf8');
        let nl;
        while ((nl = buf.indexOf('\n')) >= 0) {
            const line = buf.slice(0, nl).trim();
            buf = buf.slice(nl + 1);
            if (line) {
                try {
                    postEvent(JSON.parse(line));
                } catch (e) {}
            }
        }
    });

    client.on('error', () => {});
    client.on('close', () => { _eventSocket = null; });
}

function postEvent(msg) {
    _pendingEvents.push(msg);
    runPendingEvents();
}

function runPendingEvents() {
    while (_pendingEvents.length > 0) {
        const msg = _pendingEvents.shift();
        const displayId = msg.display_id || (msg.data && msg.data.display_id);
        const eventData = msg.event || msg;
        const eventName = (eventData.event || 'click').toLowerCase();
        const map = _subscribers.get(displayId);
        if (map) {
            const cbs = map.get(eventName);
            if (cbs) {
                for (const cb of cbs) {
                    try {
                        if (_channel && _channel.runAs && msg.id) {
                            _channel.runAs(msg.id, () => cb(eventData));
                        } else {
                            cb(eventData);
                        }
                    } catch (e) {
                        console.error('[Display event error]:', e);
                    }
                }
            }
        }
    }
}

function emitRaw(msg) {
    try {
        process.stdout.write('\n' + DISPLAY_MARKER + ' ' + JSON.stringify(msg) + '\n');
    } catch (e) {}
}

const _defaultChannel = {
    send(msg) {
        emitRaw(msg);
    },
    sendSubscription(displayId, events, subscribe) {
        ensureEventSocket();
        emitRaw({ type: subscribe ? 'subscribe' : 'unsubscribe', display_id: displayId, events });
    },
    context() {
        return null;
    }
};

// --------------------------------------------------------------------------------------------------------- throttle
class Throttle {
    constructor(action, intervalMs = 33) {
        this.action = action;
        this.intervalMs = intervalMs;
        this.latest = null;
        this.timer = null;
        this.lastRun = 0;
    }

    submit(item) {
        this.latest = item;
        const now = Date.now();
        const elapsed = now - this.lastRun;
        if (elapsed >= this.intervalMs && !this.timer) {
            this._fire();
        } else if (!this.timer) {
            this.timer = setTimeout(() => {
                this.timer = null;
                this._fire();
            }, this.intervalMs - elapsed);
        }
    }

    _fire() {
        this.lastRun = Date.now();
        const item = this.latest;
        this.latest = null;
        if (item) this.action(item);
    }
}

// ---------------------------------------------------------------------------------------------------------- handles
class DisplayHandle {
    constructor(mime, spec, displayId) {
        this.mime = mime;
        this.spec = spec;
        this.displayId = displayId;
        this._closed = false;
        this._fry_shown = true;
        this._throttle = new Throttle(msg => channel().send(msg));
    }

    update(specOrOptions, options) {
        if (this._closed) throw new Error('This visual was closed: it cannot be updated.');
        let newSpec;
        if (typeof specOrOptions === 'function') {
            const clone = JSON.parse(JSON.stringify(this.spec));
            newSpec = specOrOptions(clone) || clone;
        } else if (specOrOptions && (specOrOptions.kind || specOrOptions.series || specOrOptions.state || specOrOptions.surface)) {
            newSpec = JSON.parse(JSON.stringify(specOrOptions));
        } else {
            newSpec = JSON.parse(JSON.stringify(this.spec));
            const opts = typeof specOrOptions === 'string'
                ? Object.assign({ title: specOrOptions }, options)
                : Object.assign({}, specOrOptions, options);
            applyOptions(newSpec, opts);
        }
        this.spec = newSpec;
        this._throttle.submit(buildMessage('update_display', this.mime, this.spec, this.displayId));
        return this;
    }

    on(event, fn) {
        const ev = event.toLowerCase();
        let map = _subscribers.get(this.displayId);
        if (!map) {
            map = new Map();
            _subscribers.set(this.displayId, map);
        }
        let set = map.get(ev);
        if (!set) {
            set = new Set();
            map.set(ev, set);
        }
        set.add(fn);
        channel().sendSubscription(this.displayId, [ev], true);
        return this;
    }

    onClick(fn) { return this.on('click', fn); }
    on_click(fn) { return this.on('click', fn); }
    onSelect(fn) { return this.on('select', fn); }
    on_select(fn) { return this.on('select', fn); }
    onStep(fn) { return this.on('step', fn); }
    on_step(fn) { return this.on('step', fn); }

    off(event, fn) {
        const ev = event.toLowerCase();
        const map = _subscribers.get(this.displayId);
        if (!map) return this;
        if (fn) {
            const set = map.get(ev);
            if (set) {
                set.delete(fn);
                if (set.size === 0) map.delete(ev);
            }
        } else {
            map.delete(ev);
        }
        channel().sendSubscription(this.displayId, [ev], false);
        return this;
    }

    close() {
        this._closed = true;
        _subscribers.delete(this.displayId);
    }
}

function newId() {
    return Math.random().toString(36).substring(2, 14);
}

function buildMessage(type, mime, spec, displayId) {
    const data = {};
    data[mime] = spec;
    data['text/plain'] = textFallback(mime, spec);
    return {
        type: type,
        data: data,
        metadata: {},
        transient: { display_id: displayId }
    };
}

function textFallback(mime, spec) {
    const kind = spec.kind || 'visual';
    const title = spec.title ? `: ${spec.title}` : '';
    if (mime.includes('chart')) return `${kind} chart${title}`;
    if (mime.includes('plot3d')) return `${kind} 3D plot${title}`;
    return `${kind} visualizer${title}`;
}

function sendDisplay(mime, spec) {
    const id = newId();
    const msg = buildMessage('display', mime, spec, id);
    channel().send(msg);
    return new DisplayHandle(mime, spec, id);
}

function applyOptions(spec, opts) {
    if (!opts) return;
    if (opts.title !== undefined) spec.title = opts.title;
    if (opts.subtitle !== undefined) spec.subtitle = opts.subtitle;
    if (opts.width !== undefined) spec.width = opts.width;
    if (opts.height !== undefined) spec.height = opts.height;
    if (opts.bins !== undefined) spec.bins = opts.bins;
    if (opts.color !== undefined) spec.color = opts.color;
    if (opts.showPoints !== undefined) spec.showPoints = opts.showPoints;
    if (opts.showCoordinates !== undefined) spec.showCoordinates = opts.showCoordinates;
    if (opts.showValues !== undefined) spec.showValues = opts.showValues;
    if (opts.cellSize !== undefined) spec.cellSize = opts.cellSize;
    if (opts.xTitle || opts.xAxis) {
        spec.xAxis = Object.assign({}, spec.xAxis, typeof opts.xTitle === 'string' ? { title: opts.xTitle } : opts.xAxis);
    }
    if (opts.yTitle || opts.yAxis) {
        spec.yAxis = Object.assign({}, spec.yAxis, typeof opts.yTitle === 'string' ? { title: opts.yTitle } : opts.yAxis);
    }
    if (opts.legend !== undefined) spec.legend = typeof opts.legend === 'boolean' ? { show: opts.legend } : opts.legend;

    // A second value axis, scales, suggested ranges, reversed axes, stacking, round charts, the legend's place.
    const axis = (name, key, value) => { if (value !== undefined) spec[name] = Object.assign({}, spec[name], { [key]: value }); };
    if (opts.y2Axis) spec.y2Axis = Object.assign({}, spec.y2Axis, opts.y2Axis);
    axis('y2Axis', 'title', opts.y2Title);
    axis('y2Axis', 'min', opts.y2Min);
    axis('y2Axis', 'max', opts.y2Max);
    axis('xAxis', 'scale', opts.xScale);
    axis('yAxis', 'scale', opts.yScale);
    axis('y2Axis', 'scale', opts.y2Scale);
    axis('xAxis', 'suggestedMin', opts.xSuggestedMin);
    axis('xAxis', 'suggestedMax', opts.xSuggestedMax);
    axis('yAxis', 'suggestedMin', opts.ySuggestedMin);
    axis('yAxis', 'suggestedMax', opts.ySuggestedMax);
    axis('xAxis', 'reverse', opts.reverseX);
    axis('yAxis', 'reverse', opts.reverseY);
    if (opts.legendPosition !== undefined) spec.legend = Object.assign({}, spec.legend, { position: opts.legendPosition });
    if (opts.orientation !== undefined) spec.orientation = opts.orientation;
    if (opts.horizontal) spec.orientation = 'horizontal';
    if (opts.stack !== undefined) spec.stack = opts.stack;
    if (opts.percent) spec.stack = 'percent';
    if (opts.startAngle !== undefined) spec.startAngle = opts.startAngle;
    if (opts.sweep !== undefined) spec.sweep = opts.sweep;
    if (opts.cutout !== undefined) spec.cutout = opts.cutout;
    if (opts.gauge) { spec.startAngle = -90; spec.sweep = 180; }
    if (opts.labels !== undefined && Array.isArray(spec.series)) {
        const names = Array.from(opts.labels).map(l => (l === null || l === undefined ? null : String(l)));
        for (const series of spec.series) {
            if (Array.isArray(series.y) && series.y.length === names.length) series.labels = names;
        }
    }
}

// ----------------------------------------------------------------------------------------------------------- helpers
function number(v) {
    if (v === null || v === undefined || typeof v === 'boolean') return null;
    if (typeof v === 'number') return Number.isFinite(v) ? v : null;
    const n = Number(v);
    return Number.isFinite(n) ? n : null;
}

function cleanScalar(v) {
    if (v === null || v === undefined) return null;
    if (typeof v === 'number') return Number.isFinite(v) ? v : null;
    if (typeof v === 'boolean' || typeof v === 'string') return v;
    return String(v);
}

function parseOpts(titleOrOptions, options) {
    if (typeof titleOrOptions === 'string') {
        return Object.assign({ title: titleOrOptions }, options);
    }
    return Object.assign({}, titleOrOptions, options);
}

// ----------------------------------------------------------------------------------------------------------- specs
// What a series can be told, and the spec's name for it: { values: [...], dash: 'dashed', axis: 'right' }.
const SERIES_OPTIONS = {
    name: 'name', color: 'color', colors: 'colors', lineWidth: 'lineWidth', line_width: 'lineWidth', kind: 'kind', axis: 'axis', stack: 'stack',
    dash: 'dash', interpolation: 'interpolation', tension: 'tension', step: 'step', fill: 'fill', fillTo: 'fillTo', fill_to: 'fillTo',
    pointStyle: 'pointStyle', point_style: 'pointStyle', pointRadius: 'pointRadius', point_radius: 'pointRadius',
    colorSegments: 'colorSegments', color_segments: 'colorSegments', sizes: 'sizes', from: 'from', cornerRadius: 'cornerRadius',
    corner_radius: 'cornerRadius', ids: 'ids', labels: 'labels'
};
const KIND_ALIASES = { polar_area: 'polarArea', polararea: 'polarArea', polar: 'polarArea', doughnut: 'donut' };

function applySeriesOptions(series, options) {
    for (const [name, value] of Object.entries(options)) {
        if (name === 'values' || value === undefined || value === null) continue;
        if (!(name in SERIES_OPTIONS)) {
            throw new Error(`There is no series option '${name}': use one of ${Object.keys(SERIES_OPTIONS).filter(k => !k.includes('_')).sort().join(', ')}.`);
        }
        const key = SERIES_OPTIONS[name];
        series[key] = key === 'sizes' || key === 'from' ? Array.from(value).map(number) : value;
    }
    return series;
}

function bubbleItem(item) {
    if (Array.isArray(item) && item.length >= 3) return [number(item[0]), number(item[1]), number(item[2])];
    if (item && typeof item === 'object') {
        const valueKey = ['y', 'value', 'amount', 'count', 'total', 'score', 'revenue', 'sales', 'price', 'cost'].find(k => k in item);
        const sizeKey = ['size', 'r', 'radius', 'z', 'weight'].find(k => k in item);
        return [number(item.x), number(valueKey ? item[valueKey] : null), number(sizeKey ? item[sizeKey] : null)];
    }
    return [null, null, null];
}

function bubbleSeries(items, name) {
    const series = name === undefined ? {} : { name };
    const rows = Array.from(items).map(bubbleItem);
    series.x = rows.map(r => r[0]);
    series.y = rows.map(r => r[1]);
    series.sizes = rows.map(r => r[2]);
    return series;
}

function chartSpec(kind, data, titleOrOptions, options) {
    const opts = parseOpts(titleOrOptions, options);
    let series = [];
    const wanted = kind || opts.kind || opts.type || opts.chartType || 'line';
    const spec = { kind: KIND_ALIASES[String(wanted).toLowerCase()] || wanted, series: [] };

    if (spec.kind === 'bubble') {
        if (Array.isArray(data)) spec.series.push(bubbleSeries(data));
        else if (data && typeof data === 'object') for (const [name, items] of Object.entries(data)) spec.series.push(bubbleSeries(items, name));
        applyOptions(spec, opts);
        return spec;
    }

    if (Array.isArray(data)) {
        if (data.length > 0 && Array.isArray(data[0])) {
            // [x, y] pairs
            const x = [];
            const y = [];
            for (const p of data) {
                x.push(number(p[0]));
                y.push(number(p[1]));
            }
            series.push({ x, y });
        } else if (data.length > 0 && typeof data[0] === 'object' && data[0] !== null) {
            // Records
            const keys = Object.keys(data[0]);
            const valNames = ['y', 'value', 'amount', 'count', 'total', 'score', 'revenue', 'sales', 'price', 'cost'];
            const placeNames = ['x', 'key', 'label', 'name', 'title', 'category', 'region', 'country', 'item'];
            const valKey = keys.find(k => valNames.includes(k.toLowerCase())) || keys.find(k => typeof data[0][k] === 'number') || keys[1] || keys[0];
            const placeKey = keys.find(k => placeNames.includes(k.toLowerCase())) || keys.find(k => k !== valKey) || keys[0];
            const y = data.map(r => number(r[valKey]));
            const labels = data.map(r => String(r[placeKey]));
            series.push({ y, labels });
        } else if (spec.kind === 'histogram') {
            series.push({ values: data.map(number) });
        } else {
            series.push({ y: data.map(number) });
        }
    } else if (typeof data === 'object' && data !== null) {
        const entries = Object.entries(data);
        const isValues = v => Array.isArray(v) || (v !== null && typeof v === 'object' && Array.isArray(v.values));
        if (entries.length > 0 && isValues(entries[0][1])) {
            // Series map: each name has its values, or { values, ...how it is drawn }
            for (const [name, v] of entries) {
                const arr = Array.isArray(v) ? v : v.values;
                const one = { name, y: arr.map(number) };
                series.push(Array.isArray(v) ? one : applySeriesOptions(one, v));
            }
        } else {
            // Labels map
            const labels = [];
            const y = [];
            for (const [k, v] of entries) {
                labels.push(k);
                y.push(number(v));
            }
            series.push({ y, labels });
        }
    }

    spec.series = series;
    applyOptions(spec, opts);
    return spec;
}

function plot3dSpec(kind, data, titleOrOptions, options) {
    const opts = parseOpts(titleOrOptions, options);
    const spec = { kind: kind || opts.kind || 'scatter' };

    if (spec.kind === 'scatter' || spec.kind === 'trajectory') {
        const x = [];
        const y = [];
        const z = [];
        if (Array.isArray(data)) {
            for (const p of data) {
                x.push(number(p[0] ?? p.x));
                y.push(number(p[1] ?? p.y));
                z.push(number(p[2] ?? p.z));
            }
        }
        spec.series = [{ x, y, z }];
    } else if (spec.kind === 'surface') {
        const rows = data.length;
        const cols = rows > 0 ? data[0].length : 0;
        spec.surface = {
            x: { min: 0, max: cols - 1 },
            y: { min: 0, max: rows - 1 },
            z: data.map(r => Array.from(r).map(number))
        };
    } else if (spec.kind === 'graph') {
        const nodeSet = new Set();
        const edges = [];
        if (typeof data === 'object' && data !== null) {
            for (const [u, vs] of Object.entries(data)) {
                nodeSet.add(u);
                for (const v of vs) {
                    nodeSet.add(v);
                    edges.push({ from: u, to: v });
                }
            }
        }
        const nodes = Array.from(nodeSet).map(id => ({ id }));
        spec.graph = { directed: true, nodes, edges };
    }

    applyOptions(spec, opts);
    return spec;
}

function matrixSpec(grid, titleOrOptions, options) {
    const opts = parseOpts(titleOrOptions, options);
    const values = Array.from(grid).map(row => Array.from(row).map(cleanScalar));
    const spec = { kind: 'matrix', state: { grid: { values } } };
    applyOptions(spec, opts);
    return spec;
}

function islandsSpec(grid, titleOrOptions, options) {
    const opts = parseOpts(titleOrOptions, options);
    const values = Array.from(grid).map(row => Array.from(row).map(cleanScalar));
    const spec = { kind: 'islands', state: { grid: { values } } };
    applyOptions(spec, opts);
    return spec;
}

function arraySpec(values, pointers, titleOrOptions, options) {
    const opts = parseOpts(titleOrOptions, options);
    const cleanValues = (typeof values === 'string' ? Array.from(values) : Array.from(values)).map(cleanScalar);
    const ptrs = [];
    if (pointers && typeof pointers === 'object') {
        for (const [name, at] of Object.entries(pointers)) {
            ptrs.push({ name: String(name), at: (typeof at === 'number' ? at : String(at)) });
        }
    }
    const spec = {
        kind: 'arrayPointers',
        state: { array: { values: cleanValues } },
        pointers: ptrs.length > 0 ? ptrs : undefined
    };
    applyOptions(spec, opts);
    return spec;
}

function treeSpec(root, titleOrOptions, options) {
    const opts = parseOpts(titleOrOptions, options);
    let treeState;
    if (Array.isArray(root)) {
        // Level order array
        const values = root.map(v => (v === null || v === undefined || String(v).toLowerCase() === 'null' ? null : String(v)));
        if (!values.length || values[0] === null) {
            treeState = { root: 'node_1', nodes: [] };
        } else {
            let count = 1;
            const rootNode = { id: 'node_1', value: values[0] };
            const queue = [rootNode];
            const childrenMap = {};
            let idx = 1;
            while (queue.length > 0 && idx < values.length) {
                const cur = queue.shift();
                for (const side of ['left', 'right']) {
                    if (idx < values.length) {
                        if (values[idx] !== null) {
                            count++;
                            const child = { id: `node_${count}`, value: values[idx] };
                            cur[side] = child.id;
                            if (!childrenMap[cur.id]) childrenMap[cur.id] = [];
                            childrenMap[cur.id].push(child);
                            queue.push(child);
                        }
                        idx++;
                    }
                }
            }
            const listed = [];
            function walk(n) {
                listed.push(n);
                const k = childrenMap[n.id] || [];
                for (const c of k) walk(c);
            }
            walk(rootNode);
            treeState = { root: 'node_1', nodes: listed };
        }
    } else {
        // Object node
        const nodes = [];
        let count = 0;
        function parseNode(n) {
            if (!n) return null;
            count++;
            const id = `node_${count}`;
            const val = n.val ?? n.Val ?? n.value ?? n.Value ?? n.data ?? n.Data ?? n.key ?? n.Key ?? n;
            const specNode = { id, value: String(val) };
            nodes.push(specNode);
            const left = n.left ?? n.Left;
            const right = n.right ?? n.Right;
            if (left || right) {
                if (left) specNode.left = parseNode(left);
                if (right) specNode.right = parseNode(right);
            } else if (n.children || n.Children) {
                const kids = (n.children || n.Children).map(parseNode).filter(Boolean);
                if (kids.length) specNode.children = kids;
            }
            return id;
        }
        parseNode(root);
        treeState = { root: 'node_1', nodes };
    }

    const spec = { kind: 'tree', state: { tree: treeState } };
    applyOptions(spec, opts);
    return spec;
}

function graphSpec(graph, titleOrOptions, options) {
    const opts = parseOpts(titleOrOptions, options);
    const nodeSet = new Set();
    const edges = [];
    if (typeof graph === 'object' && graph !== null) {
        for (const [u, vs] of Object.entries(graph)) {
            nodeSet.add(u);
            if (Array.isArray(vs)) {
                for (const v of vs) {
                    nodeSet.add(v);
                    edges.push({ from: u, to: v });
                }
            }
        }
    }
    const nodes = Array.from(nodeSet).map(id => ({ id }));
    const spec = { kind: 'graph', state: { graph: { directed: true, nodes, edges } } };
    applyOptions(spec, opts);
    return spec;
}

function linkedListSpec(head, titleOrOptions, options) {
    const opts = parseOpts(titleOrOptions, options);
    const nodes = [];
    let cur = head;
    let idx = 0;
    while (cur && idx < 500) {
        const id = `n${idx}`;
        const val = cur.val ?? cur.Val ?? cur.value ?? cur.Value ?? cur.data ?? cur.Data ?? cur;
        if (nodes.length > 0) {
            nodes[nodes.length - 1].next = id;
        }
        nodes.push({ id, value: String(val) });
        cur = cur.next ?? cur.Next;
        idx++;
    }
    const spec = { kind: 'linkedList', state: { linkedList: { nodes } } };
    applyOptions(spec, opts);
    return spec;
}

function barsSpec(values, titleOrOptions, options) {
    const opts = parseOpts(titleOrOptions, options);
    const clean = Array.from(values).map(number);
    const spec = { kind: 'bars', state: { bars: { values: clean } } };
    applyOptions(spec, opts);
    return spec;
}

// ----------------------------------------------------------------------------------------------------------- table
function formatTableBundle(obj, title) {
    if (obj === null || obj === undefined) {
        return {
            [TABLE_MIME]: {
                title: title || 'null',
                columns: ['Value'],
                numeric: [false],
                rows: [['null']],
                totalRows: 1,
                totalColumns: 1
            }
        };
    }
    if (Array.isArray(obj)) {
        if (obj.length === 0) {
            return {
                [TABLE_MIME]: {
                    title: title || 'Array[0]',
                    columns: ['Value'],
                    numeric: [false],
                    rows: [],
                    totalRows: 0,
                    totalColumns: 1
                }
            };
        }
        const first = obj[0];
        if (typeof first === 'object' && first !== null && !Array.isArray(first)) {
            const cols = Object.keys(first);
            const rows = obj.map(r => cols.map(c => cleanCell(r ? r[c] : null)));
            const numeric = cols.map(c => obj.every(r => r && (r[c] === null || typeof r[c] === 'number')));
            return {
                [TABLE_MIME]: {
                    title: title || `Array[${obj.length}]`,
                    columns: cols,
                    numeric,
                    rows,
                    totalRows: rows.length,
                    totalColumns: cols.length
                }
            };
        }
        if (Array.isArray(first)) {
            const maxCols = Math.max(...obj.map(r => Array.isArray(r) ? r.length : 1));
            const cols = Array.from({ length: maxCols }, (_, i) => `[${i}]`);
            const rows = obj.map(r => Array.isArray(r) ? Array.from({ length: maxCols }, (_, i) => cleanCell(r[i])) : [cleanCell(r)]);
            return {
                [TABLE_MIME]: {
                    title: title || `Array[${obj.length}]`,
                    columns: cols,
                    numeric: cols.map(() => false),
                    rows,
                    totalRows: rows.length,
                    totalColumns: maxCols
                }
            };
        }
        const rows = obj.map((v, i) => [i, cleanCell(v)]);
        const isNum = obj.every(v => v === null || typeof v === 'number');
        return {
            [TABLE_MIME]: {
                title: title || `Array[${obj.length}]`,
                columns: ['Index', 'Value'],
                numeric: [true, isNum],
                rows,
                totalRows: rows.length,
                totalColumns: 2
            }
        };
    }
    if (typeof obj === 'object') {
        const rows = Object.entries(obj).map(([k, v]) => [k, cleanCell(v)]);
        return {
            [TABLE_MIME]: {
                title: title || `${obj.constructor ? obj.constructor.name : 'Object'}[${rows.length}]`,
                columns: ['Key', 'Value'],
                numeric: [false, false],
                rows,
                totalRows: rows.length,
                totalColumns: 2
            }
        };
    }
    return {
        [TABLE_MIME]: {
            title: title || typeof obj,
            columns: ['Value'],
            numeric: [typeof obj === 'number'],
            rows: [[cleanCell(obj)]],
            totalRows: 1,
            totalColumns: 1
        }
    };
}

function cleanCell(v) {
    if (v === null || v === undefined) return null;
    if (typeof v === 'number') return Number.isFinite(v) ? v : null;
    if (typeof v === 'boolean' || typeof v === 'string') return v;
    return util.inspect(v, { depth: 1 });
}

// ---------------------------------------------------------------------------------------------------------- Display
class Display {
    static dump(obj, title) { return Display.table(obj, title); }
    static show(obj, options) { return Display.dump(obj, options); }

    static table(obj, title) {
        const bundle = formatTableBundle(obj, title);
        channel().send({ type: 'display', data: bundle, metadata: {} });
        return obj;
    }

    static html(content) {
        channel().send({ type: 'display', data: { 'text/html': String(content) }, metadata: {} });
    }

    static markdown(content) {
        channel().send({ type: 'display', data: { 'text/markdown': String(content) }, metadata: {} });
    }

    static image(data, format = 'PNG') {
        if (!data) return;
        let b64 = '';
        const mime = format.toUpperCase() === 'JPEG' || format.toUpperCase() === 'JPG' ? 'image/jpeg' : 'image/png';
        if (Buffer.isBuffer(data)) {
            b64 = data.toString('base64');
        } else if (typeof data === 'string') {
            if (fs.existsSync(data)) {
                b64 = fs.readFileSync(data).toString('base64');
            } else {
                b64 = data.startsWith('data:image') ? data.split(',')[1] || data : data;
            }
        }
        channel().send({ type: 'display', data: { [mime]: b64 }, metadata: {} });
    }

    static json(data) {
        channel().send({ type: 'display', data: { 'application/json': JSON.stringify(data), 'text/plain': util.inspect(data) }, metadata: {} });
    }

    static chart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec(null, data, titleOrOpts, opts)); }
    static lineChart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('line', data, titleOrOpts, opts)); }
    static line_chart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('line', data, titleOrOpts, opts)); }
    static areaChart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('area', data, titleOrOpts, opts)); }
    static area_chart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('area', data, titleOrOpts, opts)); }
    static barChart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('bar', data, titleOrOpts, opts)); }
    static bar_chart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('bar', data, titleOrOpts, opts)); }
    static scatterChart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('scatter', data, titleOrOpts, opts)); }
    static scatter_chart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('scatter', data, titleOrOpts, opts)); }
    static pieChart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('pie', data, titleOrOpts, opts)); }
    static pie_chart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('pie', data, titleOrOpts, opts)); }
    static donutChart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('donut', data, titleOrOpts, opts)); }
    static donut_chart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('donut', data, titleOrOpts, opts)); }
    static histogram(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('histogram', data, titleOrOpts, opts)); }
    static bubbleChart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('bubble', data, titleOrOpts, opts)); }
    static bubble_chart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('bubble', data, titleOrOpts, opts)); }
    static radarChart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('radar', data, titleOrOpts, opts)); }
    static radar_chart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('radar', data, titleOrOpts, opts)); }
    static polarAreaChart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('polarArea', data, titleOrOpts, opts)); }
    static polar_area_chart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('polarArea', data, titleOrOpts, opts)); }
    static stackedBarChart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('bar', data, titleOrOpts, Object.assign({ stack: 'stacked' }, opts))); }
    static stacked_bar_chart(data, titleOrOpts, opts) { return Display.stackedBarChart(data, titleOrOpts, opts); }
    static horizontalBarChart(data, titleOrOpts, opts) { return sendDisplay(CHART_MIME, chartSpec('bar', data, titleOrOpts, Object.assign({ horizontal: true }, opts))); }
    static horizontal_bar_chart(data, titleOrOpts, opts) { return Display.horizontalBarChart(data, titleOrOpts, opts); }

    static plot3d(data, titleOrOpts, opts) { return sendDisplay(PLOT3D_MIME, plot3dSpec('scatter', data, titleOrOpts, opts)); }
    static scatter3d(data, titleOrOpts, opts) { return sendDisplay(PLOT3D_MIME, plot3dSpec('scatter', data, titleOrOpts, opts)); }
    static trajectory3d(data, titleOrOpts, opts) { return sendDisplay(PLOT3D_MIME, plot3dSpec('trajectory', data, titleOrOpts, opts)); }
    static surface3d(data, titleOrOpts, opts) { return sendDisplay(PLOT3D_MIME, plot3dSpec('surface', data, titleOrOpts, opts)); }
    static graph3d(data, titleOrOpts, opts) { return sendDisplay(PLOT3D_MIME, plot3dSpec('graph', data, titleOrOpts, opts)); }
    static voxelBar3d(data, titleOrOpts, opts) { return sendDisplay(PLOT3D_MIME, plot3dSpec('voxelBar', data, titleOrOpts, opts)); }
    static voxel_bar3d(data, titleOrOpts, opts) { return sendDisplay(PLOT3D_MIME, plot3dSpec('voxelBar', data, titleOrOpts, opts)); }

    static matrix(grid, titleOrOpts, opts) { return sendDisplay(VISUALIZER_MIME, matrixSpec(grid, titleOrOpts, opts)); }
    static islands(grid, titleOrOpts, opts) { return sendDisplay(VISUALIZER_MIME, islandsSpec(grid, titleOrOpts, opts)); }
    static tree(root, titleOrOpts, opts) { return sendDisplay(VISUALIZER_MIME, treeSpec(root, titleOrOpts, opts)); }
    static graph(graph, titleOrOpts, opts) { return sendDisplay(VISUALIZER_MIME, graphSpec(graph, titleOrOpts, opts)); }
    static linkedList(head, titleOrOpts, opts) { return sendDisplay(VISUALIZER_MIME, linkedListSpec(head, titleOrOpts, opts)); }
    static linked_list(head, titleOrOpts, opts) { return sendDisplay(VISUALIZER_MIME, linkedListSpec(head, titleOrOpts, opts)); }
    static array(values, pointers, titleOrOpts, opts) { return sendDisplay(VISUALIZER_MIME, arraySpec(values, pointers, titleOrOpts, opts)); }
    static bars(values, titleOrOpts, opts) { return sendDisplay(VISUALIZER_MIME, barsSpec(values, titleOrOpts, opts)); }

    static canvas(title, width, height) { return new CanvasVisualizer(title, width, height); }
    static surface3dFunc(title, fn, minX = -3, maxX = 3, minY = -3, maxY = 3, resolution = 30) {
        const n = Math.max(2, resolution);
        const zGrid = [];
        for (let r = 0; r < n; r++) {
            const y = minY + (maxY - minY) * r / (n - 1);
            const row = [];
            for (let c = 0; c < n; c++) {
                const x = minX + (maxX - minX) * c / (n - 1);
                row.push(fn(x, y));
            }
            zGrid.push(row);
        }
        const spec = {
            kind: "surface",
            surface: {
                x: { min: minX, max: maxX },
                y: { min: minY, max: maxY },
                z: zGrid
            }
        };
        if (title) spec.title = title;
        return sendDisplay(PLOT3D_MIME, spec);
    }

    static processEvents() {
        runPendingEvents();
        return new Promise(r => setImmediate(r));
    }

    static wait(seconds = 3600) {
        ensureEventSocket();
        runPendingEvents();
        return new Promise(resolve => setTimeout(resolve, seconds * 1000));
    }
}

class CanvasVisualizer {
    constructor(title = "", width = 600, height = 300) {
        this.title = title;
        this.width = width;
        this.height = height;
        this.shapes = [];
    }

    addRect(x, y, width, height, label = "", fill = "", stroke = "") {
        const shape = { type: "rect", x, y, width, height };
        if (label) shape.label = label;
        if (fill) shape.fill = fill;
        if (stroke) shape.stroke = stroke;
        this.shapes.push(shape);
        return this;
    }

    add_rect(x, y, width, height, label = "", fill = "", stroke = "") {
        return this.addRect(x, y, width, height, label, fill, stroke);
    }

    addArrow(x1, y1, x2, y2, label = "", stroke = "") {
        const shape = { type: "arrow", x1, y1, x2, y2 };
        if (label) shape.label = label;
        if (stroke) shape.stroke = stroke;
        this.shapes.push(shape);
        return this;
    }

    add_arrow(x1, y1, x2, y2, label = "", stroke = "") {
        return this.addArrow(x1, y1, x2, y2, label, stroke);
    }

    addCircle(cx, cy, radius, label = "", fill = "", stroke = "") {
        const shape = { type: "circle", cx, cy, radius };
        if (label) shape.label = label;
        if (fill) shape.fill = fill;
        if (stroke) shape.stroke = stroke;
        this.shapes.push(shape);
        return this;
    }

    add_circle(cx, cy, radius, label = "", fill = "", stroke = "") {
        return this.addCircle(cx, cy, radius, label, fill, stroke);
    }

    addLine(x1, y1, x2, y2, stroke = "") {
        const shape = { type: "line", x1, y1, x2, y2 };
        if (stroke) shape.stroke = stroke;
        this.shapes.push(shape);
        return this;
    }

    add_line(x1, y1, x2, y2, stroke = "") {
        return this.addLine(x1, y1, x2, y2, stroke);
    }

    addText(x, y, text, fontSize = 14, color = "") {
        const shape = { type: "text", x, y, text, fontSize };
        if (color) shape.color = color;
        this.shapes.push(shape);
        return this;
    }

    add_text(x, y, text, fontSize = 14, color = "") {
        return this.addText(x, y, text, fontSize, color);
    }

    show() {
        const spec = {
            kind: "canvas",
            state: {
                canvas: {
                    width: this.width,
                    height: this.height,
                    shapes: this.shapes
                }
            }
        };
        if (this.title) spec.title = this.title;
        return sendDisplay(VISUALIZER_MIME, spec);
    }
}

function dump(obj, title) { return Display.dump(obj, title); }
function display(...objects) { for (const o of objects) Display.dump(o); }

module.exports = {
    Display,
    DisplayHandle,
    dump,
    display,
    setChannel,
    postEvent,
    runPendingEvents,
    table: Display.table,
    html: Display.html,
    markdown: Display.markdown,
    image: Display.image,
    json: Display.json,
    chart: Display.chart,
    lineChart: Display.lineChart,
    line_chart: Display.line_chart,
    areaChart: Display.areaChart,
    area_chart: Display.area_chart,
    barChart: Display.barChart,
    bar_chart: Display.bar_chart,
    scatterChart: Display.scatterChart,
    scatter_chart: Display.scatter_chart,
    pieChart: Display.pieChart,
    pie_chart: Display.pie_chart,
    donutChart: Display.donutChart,
    donut_chart: Display.donut_chart,
    histogram: Display.histogram,
    bubbleChart: Display.bubbleChart,
    bubble_chart: Display.bubble_chart,
    radarChart: Display.radarChart,
    radar_chart: Display.radar_chart,
    polarAreaChart: Display.polarAreaChart,
    polar_area_chart: Display.polar_area_chart,
    stackedBarChart: Display.stackedBarChart,
    stacked_bar_chart: Display.stacked_bar_chart,
    horizontalBarChart: Display.horizontalBarChart,
    horizontal_bar_chart: Display.horizontal_bar_chart,
    plot3d: Display.plot3d,
    scatter3d: Display.scatter3d,
    trajectory3d: Display.trajectory3d,
    surface3d: Display.surface3d,
    graph3d: Display.graph3d,
    voxelBar3d: Display.voxelBar3d,
    voxel_bar3d: Display.voxel_bar3d,
    matrix: Display.matrix,
    islands: Display.islands,
    tree: Display.tree,
    graph: Display.graph,
    linkedList: Display.linkedList,
    linked_list: Display.linked_list,
    array: Display.array,
    bars: Display.bars,
    CanvasVisualizer,
    canvas: Display.canvas,
    processEvents: Display.processEvents,
    wait: Display.wait
};
