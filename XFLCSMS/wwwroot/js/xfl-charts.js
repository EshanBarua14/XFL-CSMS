/* XFL CSMS - chart helpers (Chart.js 4, wwwroot/lib/chart.js) shared by the reports and the dashboards.
   One palette that reads on light and dark backgrounds; status and priority always use the same colours.
   Text and grid colours follow the light / dark switch (xfl-ui.js recolours charts when it changes). */
(function () {
    'use strict';

    var C = {
        blue: '#2f6fdb', teal: '#139e9e', amber: '#d99a00', red: '#d64545', purple: '#7a5af5',
        green: '#2e9e57', pink: '#d6589f', slate: '#6b7a8c', orange: '#e0702e', indigo: '#4b5bd6'
    };
    var series = [C.blue, C.teal, C.amber, C.purple, C.green, C.pink, C.orange, C.indigo, C.red, C.slate];
    var status = { 'Open': C.blue, 'In Queue': C.amber, 'In Progress': C.purple, 'Closed': C.green };
    var priority = { 'High': C.red, 'Medium': C.amber, 'Low': C.teal };

    function alpha(hex, a) {
        var n = parseInt(hex.slice(1), 16);
        return 'rgba(' + (n >> 16 & 255) + ',' + (n >> 8 & 255) + ',' + (n & 255) + ',' + a + ')';
    }
    function dark() { return document.documentElement.getAttribute('data-theme') === 'dark'; }
    function ink() { return dark() ? '#c9d3dd' : '#5b6b7d'; }
    function grid() { return dark() ? 'rgba(255,255,255,.10)' : 'rgba(0,0,0,.08)'; }

    function base(extra) {
        var o = {
            responsive: true,
            maintainAspectRatio: false,
            animation: { duration: 400 },
            plugins: {
                legend: { labels: { color: ink(), boxWidth: 12, usePointStyle: true } },
                tooltip: { mode: 'index', intersect: false }
            }
        };
        return Object.assign(o, extra || {});
    }
    function axes(stacked, horizontal, percentAxis) {
        var a = {
            x: { stacked: !!stacked, ticks: { color: ink(), maxRotation: 45, autoSkip: true }, grid: { color: grid(), display: !!horizontal } },
            y: { stacked: !!stacked, beginAtZero: true, ticks: { color: ink(), precision: 0 }, grid: { color: grid(), display: !horizontal } }
        };
        if (percentAxis) {
            a.y2 = { position: 'right', min: 0, max: 100, ticks: { color: ink(), callback: function (v) { return v + '%'; } }, grid: { display: false } };
        }
        return a;
    }
    function el(id) {
        var c = document.getElementById(id);
        return c && window.Chart ? c : null;
    }
    function empty(canvas, values) {
        var total = (values || []).reduce(function (s, v) { return s + (+v || 0); }, 0);
        if (total > 0) { return false; }
        var box = canvas.parentNode;
        box.innerHTML = '<div class="xfl-chart-empty"><i class="fas fa-chart-bar"></i><span>No tickets for this chart</span></div>';
        return true;
    }

    var X = window.XflCharts = {
        colors: C,
        series: series,
        statusColor: function (label, i) { return status[label] || series[i % series.length]; },
        priorityColor: function (label, i) { return priority[label] || series[i % series.length]; },

        line: function (id, labels, sets) {
            var c = el(id); if (!c) { return null; }
            if (empty(c, [].concat.apply([], sets.map(function (s) { return s.data; })))) { return null; }
            return new Chart(c, {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: sets.map(function (s, i) {
                        var col = s.color || series[i];
                        return { label: s.label, data: s.data, borderColor: col, backgroundColor: alpha(col, .12), fill: s.fill !== false, cubicInterpolationMode: 'monotone', tension: .4, pointRadius: labels.length > 40 ? 0 : 3, borderWidth: 2 };
                    })
                },
                options: base({ interaction: { mode: 'index', intersect: false }, scales: axes(false, false) })
            });
        },

        bar: function (id, labels, sets, opts) {
            opts = opts || {};
            var c = el(id); if (!c) { return null; }
            if (empty(c, [].concat.apply([], sets.filter(function (s) { return !s.percent; }).map(function (s) { return s.data; })))) { return null; }
            var horizontal = !!opts.horizontal;
            var hasPct = sets.some(function (s) { return s.percent; });
            var scales = axes(!!opts.stacked, horizontal, hasPct && !horizontal);
            if (horizontal) {
                scales = { x: scales.y, y: scales.x };
                scales.x.grid.display = true; scales.y.grid.display = false;
                scales.y.ticks.autoSkip = false;
            }
            return new Chart(c, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: sets.map(function (s, i) {
                        var col = s.color || series[i];
                        if (s.percent) {
                            return { type: 'line', label: s.label, data: s.data, yAxisID: 'y2', borderColor: col, backgroundColor: col, pointRadius: 4, borderWidth: 2, tension: .2, spanGaps: true };
                        }
                        var bg = s.colors ? s.colors.map(function (x) { return alpha(x, .85); }) : alpha(col, .85);
                        return { label: s.label, data: s.data, backgroundColor: bg, borderColor: s.colors || col, borderWidth: 1, borderRadius: 4, maxBarThickness: 42 };
                    })
                },
                options: base({ indexAxis: horizontal ? 'y' : 'x', scales: scales, plugins: { legend: { display: sets.length > 1, labels: { color: ink(), boxWidth: 12, usePointStyle: true } }, tooltip: { mode: 'index', intersect: false } } })
            });
        },

        doughnut: function (id, labels, values, colors) {
            var c = el(id); if (!c) { return null; }
            if (empty(c, values)) { return null; }
            return new Chart(c, {
                type: 'doughnut',
                data: { labels: labels, datasets: [{ data: values, backgroundColor: colors, borderColor: dark() ? '#242a31' : '#fff', borderWidth: 2 }] },
                options: base({ cutout: '62%', plugins: { legend: { position: 'bottom', labels: { color: ink(), boxWidth: 12, usePointStyle: true } } } })
            });
        },

        /* recolour every chart's text and grid after the light / dark switch */
        retheme: function () {
            if (!window.Chart || !Chart.instances) { return; }
            Object.keys(Chart.instances).forEach(function (k) {
                var ch = Chart.instances[k];
                try {
                    var sc = ch.options.scales || {};
                    Object.keys(sc).forEach(function (a) {
                        if (sc[a].ticks) { sc[a].ticks.color = ink(); }
                        if (sc[a].grid) { sc[a].grid.color = grid(); }
                    });
                    if (ch.options.plugins && ch.options.plugins.legend && ch.options.plugins.legend.labels) { ch.options.plugins.legend.labels.color = ink(); }
                    if (ch.config.type === 'doughnut') { ch.data.datasets.forEach(function (d) { d.borderColor = dark() ? '#242a31' : '#fff'; }); }
                    ch.update('none');
                } catch (e) { /* ignore */ }
            });
        }
    };

    document.addEventListener('xfl-theme', function () { setTimeout(X.retheme, 0); });
})();
