/* XFL CSMS - engineer performance charts (Chart.js 4).
   Each <canvas data-chart='{...}'> describes one chart as data: type (bar, line, radar, doughnut),
   labels, datasets [{label, data, color | colors, fill, dashed, axis}], and unit ("%", "min", "" ...).
   Colours of text and grid lines come from the page's theme variables, and every chart is drawn
   again when the light / dark theme changes (event "xfl-theme" from xfl-theme.js). */
(function () {
    'use strict';
    var PALETTE = {
        blue: '#3b82f6', green: '#22a06b', amber: '#f59e0b', red: '#e5484d', purple: '#8b5cf6',
        teal: '#14b8a6', pink: '#ec4899', slate: '#94a3b8', orange: '#f97316', gold: '#eab308'
    };
    var charts = [];

    function color(c) { return PALETTE[c] || c || PALETTE.blue; }
    function alpha(c, a) {
        var hex = color(c).replace('#', '');
        var n = parseInt(hex, 16);
        return 'rgba(' + (n >> 16 & 255) + ',' + (n >> 8 & 255) + ',' + (n & 255) + ',' + a + ')';
    }
    function cssVar(name, fallback) {
        var v = getComputedStyle(document.documentElement).getPropertyValue(name);
        return v && v.trim() ? v.trim() : fallback;
    }
    function duration(m) {
        m = Math.round(m);
        if (m < 60) { return m + 'm'; }
        if (m < 1440) { return Math.floor(m / 60) + 'h' + (m % 60 ? ' ' + (m % 60) + 'm' : ''); }
        var h = Math.floor((m % 1440) / 60);
        return Math.floor(m / 1440) + 'd' + (h ? ' ' + h + 'h' : '');
    }
    function fmt(v, unit) {
        if (v === null || v === undefined) { return '-'; }
        if (unit === '%') { return Math.round(v) + '%'; }
        if (unit === 'min') { return duration(v); }
        if (unit === 'pts') { return Math.round(v) + ' pts'; }
        return (Math.round(v * 100) / 100) + (unit || '');
    }

    function build(canvas) {
        var cfg;
        try { cfg = JSON.parse(canvas.getAttribute('data-chart')); } catch (e) { return; }
        var text = cssVar('--xfl-muted', '#5b6b7d');
        var grid = cssVar('--xfl-line', '#e3e8ef');
        var surface = cssVar('--xfl-surface', '#ffffff');
        var type = cfg.type || 'bar';
        var unit = cfg.unit || '';

        var datasets = (cfg.datasets || []).map(function (d) {
            var ds = { label: d.label, data: d.data, spanGaps: true };
            if (d.axis) { ds.yAxisID = d.axis; }
            if (d.stack) { ds.stack = d.stack; }
            if (type === 'line') {
                ds.borderColor = color(d.color);
                ds.backgroundColor = alpha(d.color, 0.14);
                ds.pointBackgroundColor = color(d.color);
                ds.fill = !!d.fill;
                ds.tension = 0.35;
                ds.pointRadius = 4;
                ds.borderWidth = 2;
                if (d.dashed) { ds.borderDash = [6, 4]; ds.pointRadius = 0; }
            } else if (type === 'radar') {
                ds.borderColor = color(d.color);
                ds.backgroundColor = alpha(d.color, d.dashed ? 0.04 : 0.22);
                ds.pointBackgroundColor = color(d.color);
                ds.borderWidth = 2;
                if (d.dashed) { ds.borderDash = [6, 4]; }
            } else if (type === 'doughnut') {
                ds.backgroundColor = (d.colors || []).map(color);
                ds.borderColor = surface;
                ds.borderWidth = 2;
            } else {
                ds.backgroundColor = d.colors ? d.colors.map(function (c) { return alpha(c, 0.85); }) : alpha(d.color, 0.85);
                ds.borderRadius = 4;
                ds.maxBarThickness = 34;
            }
            return ds;
        });

        var options = {
            responsive: true,
            maintainAspectRatio: false,
            animation: { duration: 400 },
            indexAxis: cfg.horizontal ? 'y' : 'x',
            plugins: {
                legend: {
                    display: cfg.legend !== undefined ? !!cfg.legend : (datasets.length > 1 || type === 'doughnut'),
                    position: type === 'doughnut' ? 'bottom' : 'top',
                    labels: { color: text, boxWidth: 12, usePointStyle: true }
                },
                tooltip: {
                    callbacks: {
                        label: function (ctx) {
                            var u = ctx.dataset.yAxisID === 'y2' ? cfg.unit2 : unit;
                            return (ctx.dataset.label ? ctx.dataset.label + ': ' : (ctx.label ? ctx.label + ': ' : '')) + fmt(ctx.raw, u);
                        }
                    }
                }
            }
        };

        if (type === 'radar') {
            options.scales = {
                r: {
                    min: 0, max: cfg.max || 100,
                    ticks: { display: false, stepSize: cfg.step || 25 },
                    grid: { color: grid }, angleLines: { color: grid },
                    pointLabels: { color: text, font: { size: 11 } }
                }
            };
        } else if (type === 'doughnut') {
            options.cutout = '64%';
        } else {
            var value = {
                stacked: !!cfg.stacked, min: cfg.min, max: cfg.max, beginAtZero: cfg.min === undefined || cfg.min === null,
                ticks: { color: text, callback: function (v) { return fmt(v, unit); } },
                grid: { color: grid }, border: { display: false }
            };
            var category = { stacked: !!cfg.stacked, ticks: { color: text }, grid: { display: false }, border: { color: grid } };
            options.scales = cfg.horizontal ? { x: value, y: category } : { x: category, y: value };
            if (cfg.unit2 !== undefined) {
                options.scales.y2 = {
                    position: 'right', min: cfg.min2, max: cfg.max2,
                    ticks: { color: text, callback: function (v) { return fmt(v, cfg.unit2); } },
                    grid: { display: false }, border: { display: false }
                };
            }
        }

        charts.push(new Chart(canvas, { type: type, data: { labels: cfg.labels, datasets: datasets }, options: options }));
    }

    function buildAll() {
        if (typeof Chart === 'undefined') { return; }
        charts.forEach(function (c) { c.destroy(); });
        charts = [];
        Array.prototype.forEach.call(document.querySelectorAll('canvas[data-chart]'), build);
    }

    if (document.readyState === 'loading') { document.addEventListener('DOMContentLoaded', buildAll); } else { buildAll(); }
    document.addEventListener('xfl-theme', function () { setTimeout(buildAll, 0); });
})();
