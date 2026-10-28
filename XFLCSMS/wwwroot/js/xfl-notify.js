/* Bell + real-time toasts (Views/Shared/Notifications/_Bell.cshtml), and the Messages icon (Views/Shared/Chat/_ChatLink.cshtml):
   chat events of the same connection are passed on to the Messages page as the DOM event "xfl:chat". */
(function () {
    var bell = document.getElementById('xflBell');
    if (!bell) return;
    var d = bell.dataset;
    var chatLink = document.getElementById('xflChatLink');
    var reconnected = [];

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

    window.xflNotify = {
        reload: load, markAll: function () { return post(d.read).then(load); },
        live: false, onReconnected: function (fn) { reconnected.push(fn); }
    };

    // ---- Messages icon: unread count, and a toast for a new message outside the Messages page
    var onChatPage = !!document.getElementById('xflChat');
    window.xflChatBadge = function (n) {
        var badge = document.getElementById('xflChatCount');
        if (!badge) return;
        badge.textContent = n > 99 ? '99+' : n;
        badge.classList.toggle('d-none', !n);
    };
    var unreadTimer = null;
    function loadChatUnread() {
        if (!chatLink) return;
        clearTimeout(unreadTimer);
        unreadTimer = setTimeout(function () {
            fetch(chatLink.dataset.unread, { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                .then(function (r) { return r.ok ? r.json() : null; })
                .then(function (u) { if (u) window.xflChatBadge(u.unread); })
                .catch(function () { });
        }, 200);
    }
    function chatEvent(ev) {
        document.dispatchEvent(new CustomEvent('xfl:chat', { detail: ev }));
        if (!chatLink) return;
        if (!onChatPage) loadChatUnread();
        var m = ev.message;
        if (ev.type === 'message' && ev.toast && m && !m.system && !m.edited && !m.deleted && String(m.senderId) !== chatLink.dataset.me && !onChatPage
            && window.jQuery && jQuery(document).Toasts) {
            var text = m.body || ((m.files || []).length + ' file(s)');
            if (text.length > 140) text = text.substring(0, 140) + '…';
            jQuery(document).Toasts('create', {
                class: 'bg-primary xfl-toast', title: esc(m.senderName), subtitle: 'Message',
                body: esc(text) + '<div class="mt-1"><a class="text-white font-weight-bold" style="text-decoration:underline" href="' +
                    chatLink.dataset.page + '?c=' + ev.conversationId + '">Open</a></div>',
                autohide: true, delay: 8000, position: 'bottomRight', icon: 'far fa-comments'
            });
        }
    }
    if (!onChatPage) loadChatUnread();

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
        // no real-time connection: refresh the bell (and the chat count) every minute
        setInterval(function () { load(); if (!onChatPage) loadChatUnread(); }, 60000);
        return;
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl(d.hub, { accessTokenFactory: function () { return d.token; } })
        .withAutomaticReconnect([0, 2000, 10000, 30000, 60000])
        .build();
    connection.on('notify', function (n) { toast(n); load(); });
    connection.on('chat', chatEvent);
    connection.onreconnecting(function () { window.xflNotify.live = false; });
    connection.onclose(function () { window.xflNotify.live = false; setTimeout(start, 30000); });
    connection.onreconnected(function () {
        window.xflNotify.live = true;
        load();
        loadChatUnread();
        reconnected.forEach(function (fn) { try { fn(); } catch (e) { } });
    });
    function start() {
        connection.start().then(function () { window.xflNotify.live = true; }).catch(function () { setTimeout(start, 30000); });
    }
    start();
})();
