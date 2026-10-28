/* Xpert CSMS - motion helpers for every page (the styles are in css/xfl-glass.css):
   - charts (Chart.js) grow in, bar by bar, the moment Chart.js loads
   - the numbers of the dashboards and reports count up when they come into view
   - the ticket workflow strip of the dashboards fills its bars when it comes into view
   - xflToast(text, { title, type, delay }) shows a frosted toast; the page's success / error message is also kept on screen this way
   With "reduce motion" switched on in the operating system nothing moves: values and charts are shown at once.
   Loaded in <head> (not deferred) so it is in place before Chart.js. */
(function () {
    'use strict';
    var reduced = !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);

    // ---- charts: Chart.js sets window.Chart when it loads; set the defaults right then ----
    function chartDefaults(C) {
        try {
            if (!C || !C.defaults) { return; }
            if (reduced) { C.defaults.animation = false; return; }
            C.defaults.animation.duration = 1100;
            C.defaults.animation.easing = 'easeOutQuart';
            C.defaults.animation.delay = function (ctx) {
                return ctx.type === 'data' && ctx.mode === 'default' ? ctx.dataIndex * 90 + ctx.datasetIndex * 60 : 0;
            };
        } catch (e) { /* an older Chart.js: keep its own animation */ }
    }
    if (window.Chart) {
        chartDefaults(window.Chart);
    } else {
        try {
            Object.defineProperty(window, 'Chart', {
                configurable: true,
                enumerable: true,
                get: function () { return undefined; },
                set: function (v) {
                    Object.defineProperty(window, 'Chart', { value: v, writable: true, configurable: true, enumerable: true });
                    chartDefaults(v);
                }
            });
        } catch (e) { /* the property cannot be watched: Chart.js keeps its defaults */ }
    }

    // ---- numbers count up ----
    var NUM = /^(\s*[^\d\s-]*\s*)(-?[\d,]*\d(?:\.\d+)?)(.*)$/;
    function countUp(el) {
        if (el.getAttribute('data-xfl-counted') || el.children.length) { return; }
        var m = NUM.exec(el.textContent);
        if (!m) { return; }
        el.setAttribute('data-xfl-counted', '1');
        var raw = m[2], target = parseFloat(raw.replace(/,/g, ''));
        if (!isFinite(target) || target === 0 || reduced) { return; }
        var decimals = raw.indexOf('.') >= 0 ? raw.split('.')[1].length : 0;
        var commas = raw.indexOf(',') >= 0;
        var fmt = function (v) {
            var s = v.toFixed(decimals);
            if (commas) { var p = s.split('.'); p[0] = p[0].replace(/\B(?=(\d{3})+(?!\d))/g, ','); s = p.join('.'); }
            return m[1] + s + m[3];
        };
        var start = null, dur = Math.min(1600, 700 + Math.log10(Math.abs(target) + 1) * 300);
        el.style.fontVariantNumeric = 'tabular-nums';
        function step(t) {
            if (start === null) { start = t; }
            var k = Math.min(1, (t - start) / dur), e = 1 - Math.pow(1 - k, 3);
            el.textContent = fmt(target * e);
            if (k < 1) { requestAnimationFrame(step); } else { el.textContent = fmt(target); }
        }
        el.textContent = fmt(0);
        requestAnimationFrame(step);
    }

    function whenSeen(nodes, fn) {
        if (!nodes.length) { return; }
        if (!('IntersectionObserver' in window)) { nodes.forEach(fn); return; }
        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (en) { if (en.isIntersecting) { io.unobserve(en.target); fn(en.target); } });
        }, { threshold: 0.2 });
        nodes.forEach(function (n) { io.observe(n); });
    }

    // ---- toasts ----
    function esc(s) { var d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; }
    function toast(text, opts) {
        opts = opts || {};
        var type = opts.type || 'info', delay = opts.delay || 6000;
        var host = document.querySelector('.xfl-toasts');
        if (!host) { host = document.createElement('div'); host.className = 'xfl-toasts'; host.setAttribute('aria-live', 'polite'); document.body.appendChild(host); }
        var icon = { success: 'fa-check-circle', danger: 'fa-exclamation-circle', warning: 'fa-exclamation-triangle', info: 'fa-bell' }[type] || 'fa-bell';
        var el = document.createElement('div');
        el.className = 'toast bg-' + type;
        el.setAttribute('role', type === 'danger' ? 'alert' : 'status');
        el.style.setProperty('--xfl-toast-time', delay + 'ms');
        el.innerHTML = '<div class="toast-header"><i class="fas ' + icon + ' mr-2"></i><strong class="mr-auto">' + esc(opts.title || ({ success: 'Done', danger: 'Something went wrong', warning: 'Please check' }[type] || 'Notice')) +
            '</strong><button type="button" class="ml-2 close" aria-label="Close"><span aria-hidden="true">&times;</span></button></div><div class="toast-body">' + esc(text) + '</div>';
        function close() {
            if (el.classList.contains('xfl-out')) { return; }
            el.classList.add('xfl-out');
            setTimeout(function () { if (el.parentNode) { el.parentNode.removeChild(el); } }, reduced ? 0 : 360);
        }
        el.querySelector('.close').addEventListener('click', close);
        host.appendChild(el);
        var timer = setTimeout(close, delay);
        el.addEventListener('mouseenter', function () { clearTimeout(timer); el.style.setProperty('animation-play-state', 'paused'); });
        el.addEventListener('mouseleave', function () { timer = setTimeout(close, 2500); });
        return el;
    }
    window.xflToast = toast;

    function init() {
        var nums = Array.prototype.slice.call(document.querySelectorAll('.small-box .inner > h3, .xfl-kpi-value, .kpi-value, .xfl-flow-value, [data-xfl-count]'));
        whenSeen(nums, countUp);
        whenSeen(Array.prototype.slice.call(document.querySelectorAll('.xfl-flow')), function (el) { el.classList.add('xfl-in'); });

        // the page's one-off message (ticket created, saved, ...) also pops up as a toast, so it is seen after scrolling
        var ok = document.querySelector('.xfl-content > .alert-success');
        if (ok) { toast(ok.textContent.replace(/×/g, '').trim(), { type: 'success', title: 'Done' }); }
    }

    if (document.readyState === 'loading') { document.addEventListener('DOMContentLoaded', init); } else { init(); }
})();
