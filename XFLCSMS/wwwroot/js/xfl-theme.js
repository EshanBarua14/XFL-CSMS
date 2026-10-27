/* XFL CSMS - light / dark theme.
   Loaded in <head> before anything is drawn, so a page never flashes white in dark mode.
   The choice is kept in this browser (localStorage "xfl-theme"); with no choice yet the
   operating system's setting is used. The toggle button itself is added by xfl-ui.js. */
(function () {
    'use strict';
    var KEY = 'xfl-theme';

    function stored() {
        try { var t = localStorage.getItem(KEY); return t === 'dark' || t === 'light' ? t : null; } catch (e) { return null; }
    }
    function preferred() {
        return stored() || (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    }

    function applyToBody(theme) {
        var b = document.body;
        if (!b) { return; }
        b.classList.toggle('dark-mode', theme === 'dark');
        // AdminLTE's top bar has its own light / dark classes
        var bars = document.querySelectorAll('.main-header.navbar');
        for (var i = 0; i < bars.length; i++) {
            bars[i].classList.toggle('navbar-white', theme !== 'dark');
            bars[i].classList.toggle('navbar-light', theme !== 'dark');
            bars[i].classList.toggle('navbar-dark', theme === 'dark');
        }
    }

    function apply(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        applyToBody(theme);
        var evt;
        try { evt = new CustomEvent('xfl-theme', { detail: theme }); } catch (e) { evt = document.createEvent('CustomEvent'); evt.initCustomEvent('xfl-theme', false, false, theme); }
        document.dispatchEvent(evt);
    }

    var current = preferred();
    document.documentElement.setAttribute('data-theme', current);

    // <body> does not exist yet: add the class the moment it is parsed, and once more when the page is complete
    var mo = null;
    if (window.MutationObserver) {
        mo = new MutationObserver(function () {
            if (document.body) { applyToBody(current); }
            if (document.querySelector('.main-header.navbar')) { applyToBody(current); mo.disconnect(); }
        });
        mo.observe(document.documentElement, { childList: true, subtree: true });
    }
    document.addEventListener('DOMContentLoaded', function () { applyToBody(current); if (mo) { mo.disconnect(); } });

    window.xflTheme = {
        get: function () { return current; },
        set: function (theme) {
            current = theme === 'dark' ? 'dark' : 'light';
            try { localStorage.setItem(KEY, current); } catch (e) { /* private window: works for this page only */ }
            apply(current);
        },
        toggle: function () { this.set(current === 'dark' ? 'light' : 'dark'); }
    };

    // follow a change made in another tab
    window.addEventListener('storage', function (e) {
        if (e.key === KEY && (e.newValue === 'dark' || e.newValue === 'light') && e.newValue !== current) { current = e.newValue; apply(current); }
    });
})();
