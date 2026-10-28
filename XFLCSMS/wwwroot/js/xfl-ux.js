/* Xpert CSMS - interaction helpers for every page (styles in css/xfl-ux.css):
   - a POST form shows a spinner on its button while it is sent, and cannot be sent twice by a double click
   - <form data-xfl-draft> keeps what was typed in this browser until it is sent (ticket forms): closing the tab,
     a lost connection or an expired session no longer loses a long description; a form the server sent back with an
     error gets its text back too
   - "/" puts the cursor in the menu search */
(function () {
    'use strict';

    var DRAFT_DAYS = 7;

    function store() {
        try { var s = window.localStorage; var k = '__xfl'; s.setItem(k, k); s.removeItem(k); return s; } catch (e) { return null; }
    }

    // ---------- busy buttons ----------
    function busyForms() {
        document.addEventListener('submit', function (e) {
            var form = e.target;
            if (!(form instanceof HTMLFormElement) || (form.method || '').toLowerCase() !== 'post' || form.target || form.hasAttribute('data-no-busy')) { return; }
            if (form.getAttribute('data-xfl-busy') === '1') { e.preventDefault(); return; }
            var button = e.submitter || form.querySelector('button[type="submit"], button:not([type]), input[type="submit"]');
            // other handlers (AJAX forms, confirm boxes) run first: only mark the form when it really leaves the page
            setTimeout(function () {
                if (e.defaultPrevented) { return; }
                form.setAttribute('data-xfl-busy', '1');
                if (button && button.classList) { button.classList.add('is-busy'); button.setAttribute('aria-busy', 'true'); }
                // a form that answers with a download (CSV / Excel) stays on the page: let it be used again
                setTimeout(function () { release(form); }, 8000);
            }, 0);
        });
        // back / forward cache: the page comes back as it was left
        window.addEventListener('pageshow', function (e) {
            if (e.persisted) { Array.prototype.forEach.call(document.querySelectorAll('form[data-xfl-busy="1"]'), release); }
        });
    }

    function release(form) {
        form.removeAttribute('data-xfl-busy');
        Array.prototype.forEach.call(form.querySelectorAll('.is-busy'), function (b) { b.classList.remove('is-busy'); b.removeAttribute('aria-busy'); });
    }

    // ---------- drafts ----------
    function draftFields(form) {
        return Array.prototype.filter.call(form.elements, function (el) {
            if (!el.name || el.disabled || (el.readOnly && el.tagName !== 'TEXTAREA') || el.name === '__RequestVerificationToken') { return false; }
            if (el.tagName === 'TEXTAREA' || el.tagName === 'SELECT') { return true; }
            return el.tagName === 'INPUT' && /^(text|email|tel|number|search|url)$/i.test(el.type);
        });
    }

    function editorOf(el) {
        var $ = window.jQuery;
        return $ && $.fn.summernote && el.tagName === 'TEXTAREA' && $(el).next('.note-editor').length ? $(el) : null;
    }

    function valueOf(el) {
        var ed = editorOf(el);
        return ed ? ed.summernote('code') : el.value;
    }

    function setValue(el, v) {
        var ed = editorOf(el);
        if (ed) { ed.summernote('code', v); return; }
        if (el.tagName === 'SELECT' && !Array.prototype.some.call(el.options, function (o) { return o.value === v; })) { return; }
        el.value = v;
        el.dispatchEvent(new Event('input', { bubbles: true }));
        el.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function empty(v) {
        return !v || !String(v).replace(/<[^>]*>|&nbsp;|\s/g, '');
    }

    function drafts() {
        var s = store();
        if (!s) { return; }
        Array.prototype.forEach.call(document.querySelectorAll('form[data-xfl-draft]'), function (form, i) {
            var key = 'xfl-draft:' + location.pathname.toLowerCase() + ':' + (form.getAttribute('data-xfl-draft') || i);
            var fields = draftFields(form);
            var initial = {};
            fields.forEach(function (el) { initial[el.name] = valueOf(el); });

            var saved = null;
            try { saved = JSON.parse(s.getItem(key) || 'null'); } catch (e) { saved = null; }
            if (saved && (Date.now() - saved.t > DRAFT_DAYS * 864e5)) { saved = null; s.removeItem(key); }
            // sent last time: keep the text only when the server sent the form back with an error
            if (saved && saved.sent && !document.querySelector('.alert-danger, .validation-summary-errors')) { saved = null; s.removeItem(key); }

            var changed = saved && Object.keys(saved.v || {}).some(function (n) { return saved.v[n] !== initial[n] && !empty(saved.v[n]); });
            if (changed) {
                fields.forEach(function (el) {
                    if (Object.prototype.hasOwnProperty.call(saved.v, el.name) && saved.v[el.name] !== initial[el.name]) { setValue(el, saved.v[el.name]); }
                });
                bar(form, saved.t, function () {
                    s.removeItem(key);
                    fields.forEach(function (el) { setValue(el, initial[el.name]); });
                });
            }

            var timer = null;
            function save() {
                clearTimeout(timer);
                timer = setTimeout(function () {
                    var v = {}, any = false;
                    fields.forEach(function (el) { v[el.name] = valueOf(el); if (v[el.name] !== initial[el.name] && !empty(v[el.name])) { any = true; } });
                    try { if (any) { s.setItem(key, JSON.stringify({ t: Date.now(), v: v })); } else { s.removeItem(key); } } catch (e) { /* storage full */ }
                    mark(form, any);
                }, 600);
            }
            form.addEventListener('input', save);
            form.addEventListener('change', save);
            if (window.jQuery) { window.jQuery(form).on('summernote.change', 'textarea', save); }
            form.addEventListener('submit', function (e) {
                setTimeout(function () {
                    if (e.defaultPrevented) { return; }
                    clearTimeout(timer);
                    try {
                        var d = JSON.parse(s.getItem(key) || 'null');
                        if (d) { d.sent = true; s.setItem(key, JSON.stringify(d)); }
                    } catch (err) { /* ignore */ }
                }, 0);
            });
        });
    }

    function bar(form, time, discard) {
        var div = document.createElement('div');
        div.className = 'xfl-draft-bar';
        div.setAttribute('role', 'status');
        var when = new Date(time);
        div.innerHTML = '<i class="fas fa-history text-primary"></i><span>Your unsent text from <b></b> was put back.</span>'
            + '<button type="button" class="btn btn-sm btn-outline-secondary ml-auto">Discard it</button>';
        div.querySelector('b').textContent = when.toLocaleString([], { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
        div.querySelector('button').addEventListener('click', function () { discard(); div.parentNode.removeChild(div); });
        form.parentNode.insertBefore(div, form);
    }

    function mark(form, saved) {
        var tag = form.querySelector('.xfl-draft-saved');
        var button = form.querySelector('button[type="submit"], button:not([type])');
        if (!tag && button) {
            tag = document.createElement('span');
            tag.className = 'xfl-draft-saved';
            tag.setAttribute('aria-live', 'polite');
            button.parentNode.insertBefore(tag, button.nextSibling);
        }
        if (tag) { tag.textContent = saved ? 'Draft saved in this browser' : ''; }
    }

    // ---------- shortcuts ----------
    function shortcuts() {
        document.addEventListener('keydown', function (e) {
            if (e.key !== '/' || e.ctrlKey || e.metaKey || e.altKey) { return; }
            var t = e.target;
            if (t && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName))) { return; }
            var search = document.querySelector('.form-control-sidebar');
            if (!search) { return; }
            e.preventDefault();
            if (document.body.classList.contains('sidebar-collapse') && window.innerWidth < 992) { document.body.classList.remove('sidebar-collapse'); document.body.classList.add('sidebar-open'); }
            search.focus();
        });
    }

    function init() {
        busyForms();
        shortcuts();
        // after the rich-text editors are built (xfl-ui.js and page scripts)
        setTimeout(drafts, 0);
    }

    if (document.readyState === 'loading') { document.addEventListener('DOMContentLoaded', init); } else { init(); }
})();
