/* Xpert CSMS - installable app (PWA).
   - registers the service worker (/sw.js)
   - shows an "Install app" button when the browser offers installation (Chrome / Edge on PC and Android)
   - on iPhone / iPad, where Safari has no install prompt, explains "Share > Add to Home Screen" once
   - keeps the session alive while a signed-in page is open, so a long ticket form is not lost */
(function () {
    'use strict';

    var deferredPrompt = null;
    var standalone = (window.matchMedia && window.matchMedia('(display-mode: standalone)').matches) || window.navigator.standalone === true;

    if ('serviceWorker' in navigator && (location.protocol === 'https:' || location.hostname === 'localhost' || location.hostname === '127.0.0.1')) {
        window.addEventListener('load', function () {
            navigator.serviceWorker.register('/sw.js', { scope: '/' }).catch(function (e) {
                if (window.console) { console.warn('Service worker not registered:', e); }
            });
        });
    }

    function store(key, value) { try { if (value === undefined) { return localStorage.getItem(key); } localStorage.setItem(key, value); } catch (e) { return null; } }

    function installButton() {
        if (document.getElementById('xfl-install')) { return document.getElementById('xfl-install'); }
        var btn = document.createElement('button');
        btn.id = 'xfl-install';
        btn.type = 'button';
        btn.className = 'btn btn-primary btn-sm xfl-install-btn';
        btn.innerHTML = '<i class="fas fa-download mr-1"></i>Install app';
        btn.title = 'Install Xpert CSMS on this device';
        btn.style.display = 'none';

        // signed-in pages: in the top bar; sign-in pages: floating bottom right
        var bar = document.querySelector('.main-header .navbar-nav.ml-auto');
        if (bar) {
            var li = document.createElement('li');
            li.className = 'nav-item d-flex align-items-center mr-2';
            li.appendChild(btn);
            bar.insertBefore(li, bar.firstChild);
        } else {
            btn.classList.add('xfl-install-floating');
            document.body.appendChild(btn);
        }

        btn.addEventListener('click', function () {
            if (deferredPrompt) {
                deferredPrompt.prompt();
                deferredPrompt.userChoice.then(function () { deferredPrompt = null; btn.style.display = 'none'; });
            } else {
                iosHint(true);
            }
        });
        return btn;
    }

    window.addEventListener('beforeinstallprompt', function (e) {
        e.preventDefault();
        deferredPrompt = e;
        var show = function () { installButton().style.display = ''; };
        if (document.body) { show(); } else { document.addEventListener('DOMContentLoaded', show); }
    });

    window.addEventListener('appinstalled', function () {
        deferredPrompt = null;
        var btn = document.getElementById('xfl-install');
        if (btn) { btn.style.display = 'none'; }
    });

    function isIos() {
        var ua = navigator.userAgent || '';
        return /iphone|ipad|ipod/i.test(ua) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    }

    function iosHint(force) {
        if (standalone || !isIos()) { return; }
        if (!force && store('xfl-ios-hint')) { return; }
        store('xfl-ios-hint', '1');
        var box = document.createElement('div');
        box.className = 'xfl-ios-hint alert alert-info shadow';
        box.setAttribute('role', 'status');
        box.innerHTML = '<button type="button" class="close" aria-label="Close">&times;</button>' +
            '<b>Install Xpert CSMS</b><br>Tap <i class="fas fa-share-square"></i> <b>Share</b>, then <b>Add to Home Screen</b>.';
        box.querySelector('.close').addEventListener('click', function () { box.parentNode.removeChild(box); });
        document.body.appendChild(box);
    }

    function keepAlive() {
        // only signed-in pages have the side menu
        if (!document.querySelector('.main-sidebar')) { return; }
        setInterval(function () {
            if (document.hidden) { return; }
            fetch('/RegisterLogin/KeepAlive', { credentials: 'same-origin', cache: 'no-store' }).catch(function () { });
        }, 5 * 60 * 1000);
    }

    function init() {
        if (isIos() && !standalone) {
            installButton().style.display = '';
            iosHint(false);
        }
        keepAlive();
    }

    if (document.readyState === 'loading') { document.addEventListener('DOMContentLoaded', init); } else { init(); }
})();
