/* XFL CSMS - small helpers for every page:
   marks the menu entry of the page that is open (also for view / edit / create pages of a list). */
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

    if (document.readyState === 'loading') { document.addEventListener('DOMContentLoaded', markMenu); } else { markMenu(); }
})();
