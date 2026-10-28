/* Messages page (Views/Shared/Chat/Chat.cshtml). Real-time updates arrive from xfl-notify.js as the DOM event
   "xfl:chat"; without a live connection the page polls. Every rule is checked on the server. */
(function () {
    'use strict';
    var root = document.getElementById('xflChat');
    if (!root) return;
    var d = root.dataset;
    var me = parseInt(d.me, 10);
    var $ = function (id) { return document.getElementById(id); };

    var state = { list: [], current: null, info: null, oldest: null, newest: 0, files: [], loadingOlder: false, peopleMode: null, selected: {} };

    // ------------------------------------------------------------ helpers
    function esc(s) { var div = document.createElement('div'); div.textContent = s == null ? '' : s; return div.innerHTML; }
    function initials(name) {
        return (name || '?').split(/\s+/).filter(Boolean).slice(0, 2).map(function (w) { return w[0].toUpperCase(); }).join('') || '?';
    }
    function linkify(html) {
        return html.replace(/(https?:\/\/[^\s<]+)/g, function (u) { return '<a href="' + u + '" target="_blank" rel="noopener noreferrer">' + u + '</a>'; });
    }
    function size(bytes) {
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(0) + ' KB';
        return (bytes / 1024 / 1024).toFixed(1) + ' MB';
    }
    function fileIcon(name) {
        var ext = (name.split('.').pop() || '').toLowerCase();
        if (ext === 'pdf') return 'far fa-file-pdf';
        if (ext === 'doc' || ext === 'docx') return 'far fa-file-word';
        if (ext === 'xls' || ext === 'xlsx' || ext === 'csv') return 'far fa-file-excel';
        if (ext === 'ppt' || ext === 'pptx') return 'far fa-file-powerpoint';
        if (ext === 'zip' || ext === 'rar' || ext === '7z') return 'far fa-file-archive';
        if (['png', 'jpg', 'jpeg', 'gif', 'bmp', 'webp'].indexOf(ext) >= 0) return 'far fa-file-image';
        return 'far fa-file-alt';
    }
    function time(iso) { var t = new Date(iso); return t.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }); }
    function listTime(iso) {
        var t = new Date(iso), now = new Date();
        if (t.toDateString() === now.toDateString()) return time(iso);
        var diff = (now - t) / 86400000;
        if (diff < 6) return t.toLocaleDateString([], { weekday: 'short' });
        return t.toLocaleDateString([], { day: '2-digit', month: 'short' });
    }
    function dayLabel(iso) {
        var t = new Date(iso), now = new Date(), y = new Date(); y.setDate(now.getDate() - 1);
        if (t.toDateString() === now.toDateString()) return 'Today';
        if (t.toDateString() === y.toDateString()) return 'Yesterday';
        return t.toLocaleDateString([], { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
    }

    function get(url) {
        return fetch(url, { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } }).then(answer);
    }
    function post(url, data) {
        var body = data instanceof FormData ? data : new URLSearchParams(data || {});
        return fetch(url, { method: 'POST', credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' }, body: body }).then(answer);
    }
    function answer(r) {
        if (r.status === 401) { location.reload(); return Promise.reject(new Error('Signed out')); }
        return r.text().then(function (t) {
            var json = null;
            try { json = t ? JSON.parse(t) : null; } catch (e) { }
            if (!r.ok) throw new Error((json && json.error) || t || ('Error ' + r.status));
            return json;
        });
    }
    function params(o) {
        var p = new URLSearchParams();
        Object.keys(o).forEach(function (k) {
            if (Array.isArray(o[k])) o[k].forEach(function (v) { p.append(k, v); });
            else if (o[k] != null) p.append(k, o[k]);
        });
        return p;
    }
    function q(url, o) { return url + (url.indexOf('?') >= 0 ? '&' : '?') + params(o).toString(); }
    function notifyError(e) { alert(e.message || e); }
    function live() { return !!(window.xflNotify && window.xflNotify.live); }

    // ------------------------------------------------------------ chat list
    function loadList() {
        return get(d.conversations).then(function (rows) { state.list = rows || []; renderList(); updateTotal(); });
    }

    function updateTotal() {
        var total = state.list.reduce(function (n, c) { return n + (c.unread || 0); }, 0);
        if (window.xflChatBadge) window.xflChatBadge(total);
    }

    function avatarClass(c) { return c.isGroup ? 'group' : ''; }

    function renderList() {
        var filter = ($('chatFilter').value || '').toLowerCase();
        var rows = state.list.filter(function (c) { return !filter || (c.title || '').toLowerCase().indexOf(filter) >= 0 || (c.subtitle || '').toLowerCase().indexOf(filter) >= 0; });
        var list = $('chatList');
        if (!rows.length) {
            list.innerHTML = '<div class="xfl-chat-empty">' + (state.list.length ? 'No chat matches.' : 'No chats yet. Start one with <b>New chat</b>.') + '</div>';
            return;
        }
        list.innerHTML = rows.map(function (c) {
            return '<button type="button" role="listitem" class="xfl-chat-item' + (c.id === state.current ? ' active' : '') + (c.unread ? ' unread' : '') + '" data-id="' + c.id + '">' +
                '<span class="xfl-chat-avatar ' + avatarClass(c) + '">' + (c.isGroup ? '<i class="fas fa-users"></i>' : esc(initials(c.title))) + '</span>' +
                '<span class="xfl-chat-item-body"><span class="xfl-chat-item-top"><b>' + esc(c.title) + '</b><small>' + esc(listTime(c.time)) + '</small></span>' +
                '<span class="xfl-chat-item-preview"><span>' + esc(c.preview || c.subtitle || '') + '</span>' +
                (c.unread ? '<span class="xfl-chat-badge">' + (c.unread > 99 ? '99+' : c.unread) + '</span>' : '') + '</span></span></button>';
        }).join('');
    }

    $('chatList').addEventListener('click', function (e) {
        var item = e.target.closest('.xfl-chat-item');
        if (item) open(parseInt(item.dataset.id, 10));
    });
    $('chatFilter').addEventListener('input', renderList);

    // ------------------------------------------------------------ one chat
    function open(id) {
        state.current = id;
        state.oldest = null;
        state.newest = 0;
        $('chatMessages').innerHTML = '';
        $('chatPlaceholder').classList.add('d-none');
        $('chatPane').classList.remove('d-none');
        root.classList.add('open');
        renderList();
        try { history.replaceState(null, '', q(location.pathname, { c: id })); } catch (e) { }

        return Promise.all([loadInfo(), get(q(d.messages, { id: id }))]).then(function (r) {
            if (state.current !== id) return;
            renderMessages(r[1] || [], 'replace');
            markRead();
            $('chatInput').focus();
        }).catch(notifyError);
    }

    function loadInfo() {
        var id = state.current;
        return get(q(d.info, { id: id })).then(function (info) {
            if (state.current !== id) return;
            state.info = info;
            $('chatTitle').textContent = info.title;
            $('chatSubtitle').textContent = info.subtitle || '';
            var av = $('chatAvatar');
            av.className = 'xfl-chat-avatar ' + (info.isGroup ? 'group' : '');
            av.innerHTML = info.isGroup ? '<i class="fas fa-users"></i>' : esc(initials(info.title));
            $('chatCompose').classList.toggle('d-none', !info.canSend);
            $('chatBlocked').classList.toggle('d-none', info.canSend);
            $('chatBlocked').innerHTML = info.canSend ? '' : '<i class="fas fa-lock mr-1"></i>' + esc(info.reason);
            $('chatAttachLabel').classList.toggle('d-none', !info.canSendFiles);
            updateSeen();
        });
    }

    $('chatBack').addEventListener('click', function () { root.classList.remove('open'); });

    function canChange(m) {
        var mins = state.info ? state.info.editMinutes : 0;
        return m.senderId === me && !m.system && !m.deleted && mins > 0 && (Date.now() - new Date(m.time).getTime()) < mins * 60000;
    }

    function messageHtml(m) {
        if (m.system) return '<div class="xfl-chat-system" data-id="' + m.id + '">' + esc(m.body) + '</div>';
        var mine = m.senderId === me;
        var html = '<div class="xfl-chat-msg' + (mine ? ' mine' : '') + '" data-id="' + m.id + '" data-time="' + m.time + '"><div class="xfl-chat-bubble">';
        if (!mine && state.info && state.info.isGroup) html += '<div class="xfl-chat-sender">' + esc(m.senderName) + '</div>';
        if (m.deleted) {
            html += '<div class="xfl-chat-deleted"><i class="fas fa-ban mr-1"></i>Message deleted</div>';
        } else {
            (m.files || []).forEach(function (f) {
                var url = q(d.file, { id: f.id });
                if (f.image) {
                    html += '<a href="' + url + '" title="Download ' + esc(f.name) + '"><img class="xfl-chat-img" loading="lazy" alt="' + esc(f.name) + '" src="' + q(d.file, { id: f.id, inline: true }) + '"></a>';
                } else {
                    html += '<a class="xfl-chat-file" href="' + url + '"><i class="' + fileIcon(f.name) + '"></i><span><b>' + esc(f.name) + '</b><small>' + size(f.size) + ' · Download</small></span></a>';
                }
            });
            if (m.body) html += '<div class="xfl-chat-text">' + linkify(esc(m.body)) + '</div>';
        }
        html += '<div class="xfl-chat-meta">' + (m.edited && !m.deleted ? 'edited · ' : '') + esc(time(m.time)) + '<span class="xfl-chat-seen"></span></div>';
        if (canChange(m)) {
            html += '<div class="xfl-chat-actions">' + (m.body || !(m.files || []).length ? '<button type="button" data-act="edit" title="Edit"><i class="fas fa-pen"></i></button>' : '') +
                '<button type="button" data-act="delete" title="Delete"><i class="fas fa-trash"></i></button></div>';
        }
        return html + '</div></div>';
    }

    function dayDivider(iso) { return '<div class="xfl-chat-day" data-day="' + new Date(iso).toDateString() + '"><span>' + esc(dayLabel(iso)) + '</span></div>'; }

    /** mode: replace (first load), older (prepend), newer (append) */
    function renderMessages(rows, mode) {
        var box = $('chatMessages');
        if (mode === 'replace') box.innerHTML = '';
        if (!rows.length) {
            if (mode === 'replace') box.innerHTML = '<div class="xfl-chat-empty">No messages yet. Say hello!</div>';
            return;
        }
        var empty = box.querySelector('.xfl-chat-empty');
        if (empty) empty.remove();

        var nearBottom = box.scrollHeight - box.scrollTop - box.clientHeight < 80;
        var html = '';
        var lastDay = mode === 'newer' ? lastDayShown() : null;
        rows.forEach(function (m) {
            var day = new Date(m.time).toDateString();
            if (day !== lastDay) { html += dayDivider(m.time); lastDay = day; }
            html += messageHtml(m);
        });

        if (mode === 'older') {
            var before = box.scrollHeight;
            var more = box.querySelector('.xfl-chat-more');
            if (more) more.remove();
            // the first divider of the old block may repeat: drop the existing one of the same day
            var firstDay = box.querySelector('.xfl-chat-day');
            if (firstDay && firstDay.dataset.day === new Date(rows[rows.length - 1].time).toDateString()) firstDay.remove();
            box.insertAdjacentHTML('afterbegin', html);
            box.scrollTop = box.scrollHeight - before;
        } else {
            box.insertAdjacentHTML('beforeend', html);
            if (mode === 'replace' || nearBottom || rows.some(function (m) { return m.senderId === me; })) box.scrollTop = box.scrollHeight;
        }

        rows.forEach(function (m) {
            if (state.oldest == null || m.id < state.oldest) state.oldest = m.id;
            if (m.id > state.newest) state.newest = m.id;
        });
        if ((mode === 'replace' && rows.length >= 50) || (mode === 'older' && rows.length >= 50)) {
            box.insertAdjacentHTML('afterbegin', '<div class="xfl-chat-more"><button type="button" class="btn btn-sm btn-light" id="chatOlder">Show earlier messages</button></div>');
        }
        updateSeen();
    }

    function lastDayShown() {
        var days = $('chatMessages').querySelectorAll('.xfl-chat-day');
        return days.length ? days[days.length - 1].dataset.day : null;
    }

    function replaceMessage(m) {
        var el = $('chatMessages').querySelector('[data-id="' + m.id + '"]');
        if (el) { el.outerHTML = messageHtml(m); updateSeen(); return true; }
        return false;
    }

    $('chatMessages').addEventListener('click', function (e) {
        if (e.target.closest('#chatOlder')) {
            if (state.loadingOlder) return;
            state.loadingOlder = true;
            var id = state.current;
            get(q(d.messages, { id: id, before: state.oldest })).then(function (rows) {
                if (state.current === id) renderMessages(rows || [], 'older');
            }).catch(notifyError).finally(function () { state.loadingOlder = false; });
            return;
        }
        var act = e.target.closest('[data-act]');
        if (!act) return;
        var msg = act.closest('.xfl-chat-msg');
        var id = msg.dataset.id;
        if (act.dataset.act === 'delete') {
            if (!confirm('Delete this message for everybody?')) return;
            post(d.delete, { messageId: id }).catch(notifyError);
        } else {
            var current = msg.querySelector('.xfl-chat-text');
            var text = prompt('Edit message', current ? current.textContent : '');
            if (text == null) return;
            post(d.edit, { messageId: id, body: text }).catch(notifyError);
        }
    });

    /** One-to-one chats: "Seen" under my latest message the other person has read. */
    function updateSeen() {
        var box = $('chatMessages');
        box.querySelectorAll('.xfl-chat-seen').forEach(function (s) { s.textContent = ''; });
        var info = state.info;
        if (!info || info.isGroup) return;
        var other = (info.members || []).filter(function (m) { return m.id !== me; })[0];
        if (!other || !other.lastRead) return;
        var mine = box.querySelectorAll('.xfl-chat-msg.mine');
        for (var i = mine.length - 1; i >= 0; i--) {
            if (parseInt(mine[i].dataset.id, 10) <= other.lastRead) {
                mine[i].querySelector('.xfl-chat-seen').innerHTML = ' · <i class="fas fa-check-double"></i> Seen';
                break;
            }
        }
    }

    var readTimer = null;
    function markRead() {
        if (!state.current || document.hidden) return;
        clearTimeout(readTimer);
        var id = state.current;
        readTimer = setTimeout(function () {
            var c = state.list.filter(function (x) { return x.id === id; })[0];
            post(d.read, { id: id }).then(function () {
                if (c) { c.unread = 0; renderList(); updateTotal(); }
            }).catch(function () { });
        }, 300);
    }
    document.addEventListener('visibilitychange', function () { if (!document.hidden) markRead(); });

    // ------------------------------------------------------------ composer
    var input = $('chatInput');
    function grow() { input.style.height = 'auto'; input.style.height = Math.min(input.scrollHeight, 140) + 'px'; }
    input.addEventListener('input', grow);
    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) { e.preventDefault(); send(); }
    });
    $('chatCompose').addEventListener('submit', function (e) { e.preventDefault(); send(); });

    var maxBytes = (parseInt(d.maxMb, 10) || 20) * 1024 * 1024;
    var allowed = (d.extensions || '').split(',').filter(Boolean);
    function addFiles(list) {
        Array.prototype.forEach.call(list, function (f) {
            var ext = '.' + (f.name.split('.').pop() || '').toLowerCase();
            if (allowed.length && allowed.indexOf(ext) < 0) { alert(f.name + ': this file type is not allowed (' + allowed.join(' ') + ').'); return; }
            if (f.size > maxBytes) { alert(f.name + ' is larger than ' + d.maxMb + ' MB.'); return; }
            state.files.push(f);
        });
        renderFiles();
    }
    function renderFiles() {
        var box = $('chatFiles');
        box.classList.toggle('d-none', !state.files.length);
        box.innerHTML = state.files.map(function (f, i) {
            return '<span class="xfl-chat-chip"><i class="' + fileIcon(f.name) + ' mr-1"></i>' + esc(f.name) + ' <small class="text-muted">' + size(f.size) + '</small>' +
                '<button type="button" data-i="' + i + '" aria-label="Remove ' + esc(f.name) + '">&times;</button></span>';
        }).join('');
    }
    $('chatFiles').addEventListener('click', function (e) {
        var b = e.target.closest('button[data-i]');
        if (b) { state.files.splice(parseInt(b.dataset.i, 10), 1); renderFiles(); }
    });
    $('chatFileInput').addEventListener('change', function () { addFiles(this.files); this.value = ''; });

    var compose = $('chatCompose');
    ['dragenter', 'dragover'].forEach(function (ev) {
        compose.addEventListener(ev, function (e) {
            if (!state.info || !state.info.canSendFiles) return;
            e.preventDefault(); compose.classList.add('drag');
        });
    });
    ['dragleave', 'drop'].forEach(function (ev) { compose.addEventListener(ev, function () { compose.classList.remove('drag'); }); });
    compose.addEventListener('drop', function (e) {
        if (!state.info || !state.info.canSendFiles) return;
        e.preventDefault();
        if (e.dataTransfer && e.dataTransfer.files) addFiles(e.dataTransfer.files);
    });
    input.addEventListener('paste', function (e) {
        if (!state.info || !state.info.canSendFiles || !e.clipboardData) return;
        var files = Array.prototype.filter.call(e.clipboardData.files || [], function (f) { return f.size > 0; });
        if (files.length) { e.preventDefault(); addFiles(files); }
    });

    var sending = false;
    function send() {
        if (sending || !state.current) return;
        var text = input.value.trim();
        if (!text && !state.files.length) return;
        var form = new FormData();
        form.append('body', text);
        state.files.forEach(function (f) { form.append('files', f, f.name); });
        sending = true;
        $('chatSendBtn').disabled = true;
        var id = state.current;
        post(q(d.send, { id: id }), form).then(function () {
            input.value = '';
            grow();
            state.files = [];
            renderFiles();
            if (!live()) return poll();
        }).catch(notifyError).finally(function () {
            sending = false;
            $('chatSendBtn').disabled = false;
            input.focus();
        });
    }

    // ------------------------------------------------------------ people dialog (new chat / new group / add)
    var contacts = null;
    function loadContacts() {
        return contacts ? Promise.resolve(contacts) : get(d.contacts).then(function (rows) { contacts = rows || []; return contacts; });
    }

    function openPeople(mode) {
        state.peopleMode = mode;
        state.selected = {};
        $('chatPeopleTitle').textContent = mode === 'direct' ? 'New chat' : mode === 'group' ? 'New group' : 'Add people';
        $('chatGroupNameRow').classList.toggle('d-none', mode !== 'group');
        $('chatGroupName').value = '';
        $('chatPeopleFilter').value = '';
        $('chatPeopleOk').classList.toggle('d-none', mode === 'direct');
        $('chatPeopleOk').textContent = mode === 'group' ? 'Create group' : 'Add';
        $('chatPeopleError').classList.add('d-none');
        $('chatPeople').innerHTML = '<div class="text-muted small p-2">Loading…</div>';
        contacts = null; // the rules may have changed
        jQuery('#chatInfoModal').modal('hide');
        jQuery('#chatPeopleModal').modal('show');
        loadContacts().then(renderPeople).catch(peopleError);
    }

    function renderPeople() {
        var filter = ($('chatPeopleFilter').value || '').toLowerCase();
        var inChat = state.peopleMode === 'add' && state.info ? state.info.members.map(function (m) { return m.id; }) : [];
        var rows = (contacts || []).filter(function (p) {
            return inChat.indexOf(p.id) < 0 && (!filter || p.name.toLowerCase().indexOf(filter) >= 0 || (p.subtitle || '').toLowerCase().indexOf(filter) >= 0);
        });
        $('chatPeople').innerHTML = rows.length ? rows.map(function (p) {
            var sel = state.selected[p.id];
            return '<button type="button" class="list-group-item list-group-item-action' + (sel ? ' selected' : '') + '" data-id="' + p.id + '" aria-pressed="' + (sel ? 'true' : 'false') + '">' +
                '<span class="xfl-chat-avatar ' + (p.xfl ? '' : 'client') + '">' + esc(initials(p.name)) + '</span>' +
                '<span class="flex-fill"><b>' + esc(p.name) + '</b><small>' + esc(p.subtitle || '') + '</small></span>' +
                (state.peopleMode !== 'direct' ? '<i class="' + (sel ? 'fas fa-check-square text-primary' : 'far fa-square text-muted') + '"></i>' : '') + '</button>';
        }).join('') : '<div class="text-muted small p-2">' + ((contacts || []).length ? 'Nobody matches.' : 'There is nobody you can chat with under the current rules.') + '</div>';
    }

    function peopleError(e) {
        var box = $('chatPeopleError');
        box.textContent = e.message || e;
        box.classList.remove('d-none');
    }

    $('chatPeopleFilter').addEventListener('input', renderPeople);
    $('chatPeople').addEventListener('click', function (e) {
        var b = e.target.closest('[data-id]');
        if (!b) return;
        var id = parseInt(b.dataset.id, 10);
        if (state.peopleMode === 'direct') {
            post(d.start, { userId: id }).then(function (r) {
                jQuery('#chatPeopleModal').modal('hide');
                return loadList().then(function () { return open(r.id); });
            }).catch(peopleError);
            return;
        }
        if (state.selected[id]) delete state.selected[id]; else state.selected[id] = true;
        renderPeople();
    });
    $('chatPeopleOk').addEventListener('click', function () {
        var ids = Object.keys(state.selected);
        if (!ids.length) { peopleError(new Error('Choose at least one person.')); return; }
        if (state.peopleMode === 'group') {
            post(d.group, params({ title: $('chatGroupName').value, members: ids })).then(function (r) {
                jQuery('#chatPeopleModal').modal('hide');
                return loadList().then(function () { return open(r.id); });
            }).catch(peopleError);
        } else {
            post(d.add, params({ id: state.current, members: ids })).then(function () {
                jQuery('#chatPeopleModal').modal('hide');
                return loadInfo();
            }).catch(peopleError);
        }
    });

    $('chatNewDirect').addEventListener('click', function () { openPeople('direct'); });
    if ($('chatNewGroup')) $('chatNewGroup').addEventListener('click', function () { openPeople('group'); });

    // ------------------------------------------------------------ chat details
    function infoError(e) { var box = $('chatInfoError'); box.textContent = e.message || e; box.classList.remove('d-none'); }

    function renderInfo() {
        var info = state.info;
        if (!info) return;
        $('chatInfoTitle').textContent = info.title;
        $('chatInfoError').classList.add('d-none');
        $('chatRenameRow').classList.toggle('d-none', !info.canManage);
        $('chatRenameInput').value = info.isGroup ? info.title : '';
        $('chatLeaveBtn').classList.toggle('d-none', !info.isGroup);
        $('chatAddBtn').classList.toggle('d-none', !info.canAdd);
        $('chatMembers').innerHTML = info.members.map(function (m) {
            var remove = info.canManage && m.id !== me ? '<button type="button" class="btn btn-sm btn-link text-danger ml-auto" data-remove="' + m.id + '" title="Remove from the group"><i class="fas fa-user-minus"></i></button>' : '';
            return '<li class="list-group-item d-flex align-items-center"><span class="xfl-chat-avatar mr-2">' + esc(initials(m.name)) + '</span>' +
                '<span><b>' + esc(m.name) + (m.id === me ? ' (you)' : '') + '</b>' + (m.owner ? ' <span class="badge badge-info">owner</span>' : '') +
                (m.active ? '' : ' <span class="badge badge-secondary">disabled</span>') + '<small class="d-block text-muted">' + esc(m.subtitle || '') + '</small></span>' + remove + '</li>';
        }).join('');
    }

    $('chatInfoBtn').addEventListener('click', function () {
        loadInfo().then(function () { renderInfo(); jQuery('#chatInfoModal').modal('show'); }).catch(notifyError);
    });
    $('chatAddBtn').addEventListener('click', function () { openPeople('add'); });
    $('chatRenameBtn').addEventListener('click', function () {
        post(d.rename, { id: state.current, title: $('chatRenameInput').value }).then(loadInfo).then(renderInfo).then(loadList).catch(infoError);
    });
    $('chatMembers').addEventListener('click', function (e) {
        var b = e.target.closest('[data-remove]');
        if (!b || !confirm('Remove this person from the group?')) return;
        post(d.remove, { id: state.current, userId: b.dataset.remove }).then(loadInfo).then(renderInfo).catch(infoError);
    });
    $('chatLeaveBtn').addEventListener('click', function () {
        if (!confirm('Leave this group? You will no longer see its messages.')) return;
        post(d.leave, { id: state.current }).then(function () {
            jQuery('#chatInfoModal').modal('hide');
            state.current = null;
            $('chatPane').classList.add('d-none');
            $('chatPlaceholder').classList.remove('d-none');
            root.classList.remove('open');
            return loadList();
        }).catch(infoError);
    });

    // ------------------------------------------------------------ live updates
    var listTimer = null;
    function refreshListSoon() { clearTimeout(listTimer); listTimer = setTimeout(function () { loadList().catch(function () { }); }, 250); }

    document.addEventListener('xfl:chat', function (e) {
        var ev = e.detail || {};
        if (ev.type === 'message' && ev.message) {
            if (ev.conversationId === state.current) {
                if (!replaceMessage(ev.message) && ev.message.id > state.newest) renderMessages([ev.message], 'newer');
                markRead();
            }
            refreshListSoon();
        } else if (ev.type === 'conversation') {
            refreshListSoon();
            if (ev.conversationId === state.current) loadInfo().catch(function () { });
        } else if (ev.type === 'read') {
            if (ev.conversationId === state.current && state.info) {
                state.info.members.forEach(function (m) { if (m.id === ev.userId) m.lastRead = ev.messageId; });
                updateSeen();
            }
            if (ev.userId === me) refreshListSoon();
        }
    });

    /** Without a live connection: new messages of the open chat and the list are fetched. */
    function poll() {
        var id = state.current;
        var jobs = [loadList()];
        if (id) {
            jobs.push(get(q(d.messages, { id: id, after: state.newest })).then(function (rows) {
                if (state.current === id && rows && rows.length) { renderMessages(rows, 'newer'); markRead(); }
            }));
            jobs.push(loadInfo());
        }
        return Promise.all(jobs).catch(function () { });
    }
    setInterval(function () { if (!document.hidden && !live()) poll(); }, 5000);
    setInterval(function () { if (!document.hidden && live()) loadList().catch(function () { }); }, 60000);
    if (window.xflNotify && window.xflNotify.onReconnected) window.xflNotify.onReconnected(poll);

    // ------------------------------------------------------------ start
    loadList().then(function () {
        if (d.openWith) {
            return post(d.start, { userId: d.openWith }).then(function (r) { return loadList().then(function () { return open(r.id); }); });
        }
        var c = parseInt(d.openConversation, 10);
        if (c) return open(c);
        if (window.innerWidth >= 768 && state.list.length) return open(state.list[0].id);
    }).catch(notifyError);
})();
