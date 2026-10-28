/* Xpert CSMS - small helpers for every page:
   - marks the menu entry of the page that is open (also for view / edit / create pages of a list)
   - adds the light / dark switch (the theme itself is set by xfl-theme.js)
   - turns <textarea id="editor"> into the rich-text editor (Summernote, MIT licence, served from wwwroot/lib)
   - makes #issuesTable searchable / sortable (DataTables)
   - Delete buttons: <button data-delete-url="/Admin/DeleteX/5" data-delete-name="..."> asks, sends DELETE and removes the row */
(function () {
    'use strict';

    function norm(path) { return (path || '').toLowerCase().replace(/\/+$/, ''); }
    function actionOf(link) { var p = norm(link.pathname).split('/'); return p.length >= 3 ? p[2] : ''; }

    function markMenu() {
        var links = Array.prototype.slice.call(document.querySelectorAll('.nav-sidebar a.nav-link')).filter(function (a) {
            var href = a.getAttribute('href');
            return href && href !== '#' && href.indexOf('javascript:') !== 0;
        });
        if (!links.length) { return; }
        // the side menu marks its entry on the server; this is only for menus that do not
        if (document.querySelector('.nav-sidebar a.nav-link.active')) { return; }

        var here = norm(location.pathname);
        var parts = here.split('/');                       // "", controller, action, id
        var action = parts.length >= 3 ? parts[2] : 'dashbord';
        var found = null;

        function first(test) { for (var i = 0; i < links.length; i++) { if (test(actionOf(links[i]), links[i])) { return links[i]; } } return null; }

        // 1. the entry of exactly this page
        found = first(function (a) { return a === action; });

        // 2. a page that belongs to a list: its view, edit and create pages
        if (!found && action.indexOf('ticket') >= 0) {
            found = first(function (a) { return a === 'adminview' || a === 'allticketlist' || a === 'mackerticketlist'; });
        }
        if (!found && action.indexOf('todo') >= 0) {
            found = first(function (a) { return a.indexOf('todo') >= 0; });
        }
        if (!found) {
            var stem = action.replace(/^(create|edit|view|update|delete)/, '').replace(/(view|edit)$/, '');
            if (stem.length >= 4) { found = first(function (a) { return a.indexOf(stem) === 0 && a !== action; }); }
        }
        if (!found) { return; }

        found.classList.add('active');
        // open the groups the entry sits in
        var node = found.parentElement;
        while (node && !node.classList.contains('nav-sidebar')) {
            if (node.tagName === 'LI' && node.querySelector(':scope > ul.nav-treeview')) { node.classList.add('menu-open'); node.classList.add('menu-is-opening'); }
            if (node.tagName === 'UL' && node.classList.contains('nav-treeview')) { node.style.display = 'block'; }
            node = node.parentElement;
        }
    }

    function themeButton() {
        if (!window.xflTheme || document.querySelector('.xfl-theme-toggle')) { return; }
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'xfl-theme-toggle';
        function paint() {
            var dark = window.xflTheme.get() === 'dark';
            btn.innerHTML = '<i class="fas ' + (dark ? 'fa-sun' : 'fa-moon') + '"></i><span class="xfl-theme-label">' + (dark ? 'Light' : 'Dark') + '</span>';
            btn.title = dark ? 'Switch to light mode' : 'Switch to dark mode';
            btn.setAttribute('aria-label', btn.title);
            btn.setAttribute('aria-pressed', dark ? 'true' : 'false');
        }
        btn.addEventListener('click', function () { window.xflTheme.toggle(); });
        document.addEventListener('xfl-theme', paint);
        paint();

        var bar = document.querySelector('.main-header .navbar-nav.ml-auto');
        if (bar) {
            var li = document.createElement('li');
            li.className = 'nav-item';
            btn.classList.add('nav-link');
            li.appendChild(btn);
            bar.insertBefore(li, bar.firstChild);
        } else {
            // sign-in, registration and password pages have no top bar
            btn.classList.add('xfl-theme-floating');
            document.body.appendChild(btn);
        }
    }

    function editors() {
        var $ = window.jQuery;
        if (!$ || !$.fn.summernote) { return; }
        $('textarea#editor').each(function () {
            var $t = $(this);
            if ($t.next('.note-editor').length) { return; }
            var readOnly = $t.prop('readonly') || $t.prop('disabled');
            $t.summernote({
                height: readOnly ? null : 240,
                minHeight: 140,
                placeholder: $t.attr('placeholder') || 'Describe the issue: what happened, where, and the steps to see it again.',
                dialogsInBody: true,
                disableDragAndDrop: readOnly,
                maximumImageFileSize: 1024 * 1024,      // pasted pictures are kept inside the text; 1 MB each at most
                toolbar: readOnly ? [] : [
                    ['style', ['style']],
                    ['font', ['bold', 'italic', 'underline', 'strikethrough', 'clear']],
                    ['color', ['color']],
                    ['para', ['ul', 'ol', 'paragraph']],
                    ['insert', ['table', 'link', 'picture', 'hr']],
                    ['view', ['undo', 'redo', 'fullscreen']]
                ],
                callbacks: {
                    onImageUploadError: function () { window.alert('The picture is larger than 1 MB. Please attach it as a file instead.'); }
                }
            });
            if (readOnly) { $t.summernote('disable'); }
        });
    }

    function tables() {
        var $ = window.jQuery;
        if (!$ || !$.fn.DataTable) { return; }
        $('#issuesTable').each(function () {
            if (!$.fn.DataTable.isDataTable(this)) { $(this).DataTable({ columnDefs: [{ orderable: false, targets: 'no-sort' }] }); }
        });
    }

    function deletes() {
        document.addEventListener('click', function (e) {
            var btn = e.target.closest ? e.target.closest('[data-delete-url]') : null;
            if (!btn) { return; }
            e.preventDefault();
            var name = btn.getAttribute('data-delete-name');
            if (!window.confirm('Delete ' + (name ? '"' + name + '"' : 'this item') + '? This cannot be undone.')) { return; }
            btn.disabled = true;
            fetch(btn.getAttribute('data-delete-url'), { method: 'DELETE', credentials: 'same-origin' })
                .then(function (r) {
                    if (r.ok) {
                        var row = btn.closest('tr');
                        var $ = window.jQuery;
                        var table = row && $ && $.fn.DataTable && $(row).closest('table');
                        if (table && $.fn.DataTable.isDataTable(table[0])) { table.DataTable().row(row).remove().draw(false); }
                        else if (row) { row.parentNode.removeChild(row); }
                        else if (btn.getAttribute('data-delete-done')) { location.href = btn.getAttribute('data-delete-done'); }
                        return;
                    }
                    btn.disabled = false;
                    return r.text().then(function (m) { window.alert(m || ('Delete failed: ' + r.status)); });
                })
                .catch(function () { btn.disabled = false; window.alert('Delete failed: the server could not be reached.'); });
        });
    }

    // Chart.js picks its text and grid colours from its defaults: follow the theme
    function charts() {
        var C = window.Chart;
        if (!C || !C.defaults) { return; }
        var dark = window.xflTheme && window.xflTheme.get() === 'dark';
        C.defaults.color = dark ? '#c9d3dd' : '#5b6b7d';
        C.defaults.borderColor = dark ? 'rgba(255, 255, 255, .12)' : 'rgba(0, 0, 0, .1)';
        var list = C.instances ? Object.keys(C.instances).map(function (k) { return C.instances[k]; }) : [];
        list.forEach(function (chart) {
            try {
                var scales = chart.options.scales || {};
                Object.keys(scales).forEach(function (id) {
                    var sc = scales[id];
                    if (sc.ticks) { sc.ticks.color = C.defaults.color; }
                    if (sc.grid) { sc.grid.color = C.defaults.borderColor; }
                });
                if (chart.options.plugins && chart.options.plugins.legend && chart.options.plugins.legend.labels) { chart.options.plugins.legend.labels.color = C.defaults.color; }
                chart.update('none');
            } catch (e) { /* a chart that failed to draw */ }
        });
    }

    function init() {
        markMenu();
        themeButton();
        editors();
        tables();
        deletes();
        charts();
        document.addEventListener('xfl-theme', charts);
    }

    if (document.readyState === 'loading') { document.addEventListener('DOMContentLoaded', init); } else { init(); }
})();
