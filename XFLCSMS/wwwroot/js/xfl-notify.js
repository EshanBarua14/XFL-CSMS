/* Bell + real-time toasts (Views/Shared/Notifications/_Bell.cshtml). */
(function () {
    var bell = document.getElementById('xflBell');
    if (!bell) return;
    var d = bell.dataset;

    function esc(s) { var div = document.createElement('div'); div.textContent = s == null ? '' : s; return div.innerHTML; }

    function post(url, data) {
        var body = new URLSearchParams(data || {});
        return fetch(url, { method: 'POST', credentials: 'same-origin', headers: { 'RequestVerificationToken': d.csrf, 'X-Requested-With': 'XMLHttpRequest' }, body: body });
    }

    function render(feed) {
        var count = document.getElementById('xflBellCount');
        count.textContent = feed.unread > 99 ? '99+' : feed.unread;
        count.classList.toggle('d-none', !feed.unread);
        document.getElementById('xflBellTitle').textContent = feed.unread ? feed.unread + ' unread' : 'Notifications';
        var list = document.getElementById('xflBellList');
        if (!feed.items.length) { list.innerHTML = '<span class="dropdown-item text-muted small">No notifications yet.</span>'; return; }
        list.innerHTML = feed.items.map(function (n) {
            return '<a class="dropdown-item xfl-bell-item' + (n.read ? '' : ' xfl-unread') + '" href="' + d.open + '/' + n.id + '">' +
                '<div class="d-flex justify-content-between"><b class="text-truncate">' + esc(n.title) + '</b><small class="text-muted ml-2 text-nowrap">' + esc(n.time) + '</small></div>' +
                '<div class="small text-muted text-wrap">' + esc(n.body) + '</div></a>';
        }).join('');
    }

    function load() {
        return fetch(d.feed, { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (f) { if (f) render(f); })
            .catch(function () { });
    }

    window.xflNotify = { reload: load, markAll: function () { return post(d.read).then(load); } };

    document.getElementById('xflBellMarkAll').addEventListener('click', function (e) { e.preventDefault(); e.stopPropagation(); window.xflNotify.markAll(); });
    load();

    function toast(n) {
        var link = n.url ? '<div class="mt-1"><a class="text-white font-weight-bold" style="text-decoration:underline" href="' + d.open + '/' + n.id + '">Open</a></div>' : '';
        if (window.jQuery && jQuery(document).Toasts) {
            jQuery(document).Toasts('create', {
                class: 'bg-info xfl-toast', title: esc(n.title), subtitle: esc(n.time), body: esc(n.body) + link,
                autohide: true, delay: 8000, position: 'bottomRight', icon: 'fas fa-bell'
            });
        }
    }

    if (!d.token || !window.signalR) {
        setInterval(load, 60000); // no real-time connection: refresh the bell every minute
        return;
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl(d.hub, { accessTokenFactory: function () { return d.token; } })
        .withAutomaticReconnect([0, 2000, 10000, 30000, 60000])
        .build();
    connection.on('notify', function (n) { toast(n); load(); });
    connection.onreconnected(load);
    function start() { connection.start().catch(function () { setTimeout(start, 30000); }); }
    start();
})();
