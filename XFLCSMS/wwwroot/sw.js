/* Xpert CSMS service worker (makes the app installable and keeps working when the network drops).
   - Static files (css, js, fonts, images, icons) are cached and served from the cache, refreshed in the background.
   - Pages are always fetched from the server (tickets must never be stale); when the server cannot be
     reached, the "You are offline" page is shown instead.
   - Only GET requests are touched: form posts, deletes and downloads always go to the server. */
'use strict';

var VERSION = 'xfl-csms-v3';
var STATIC_CACHE = VERSION + '-static';
var OFFLINE_URL = '/Home/Offline';

var PRECACHE = [
    OFFLINE_URL,
    '/manifest.webmanifest',
    '/icons/icon-192.png',
    '/icons/icon-512.png',
    '/img/xfl-logo-centered.png',
    '/css/all.min.css',
    '/css/adminlte.min.css',
    '/css/xfl-polish.css',
    '/css/xfl-glass.css',
    '/css/xfl-story.css',
    '/plugins/jquery/jquery.min.js',
    '/plugins/bootstrap/js/bootstrap.bundle.min.js',
    '/js/adminlte.min.js',
    '/js/xfl-theme.js',
    '/js/xfl-motion.js',
    '/js/xfl-story.js',
    '/js/xfl-csrf.js',
    '/js/xfl-ui.js',
    '/js/xfl-pwa.js'
];

self.addEventListener('install', function (event) {
    event.waitUntil(
        caches.open(STATIC_CACHE)
            .then(function (cache) {
                // one missing file must not stop the installation
                return Promise.all(PRECACHE.map(function (url) {
                    return cache.add(new Request(url, { cache: 'reload' })).catch(function () { });
                }));
            })
            .then(function () { return self.skipWaiting(); })
    );
});

self.addEventListener('activate', function (event) {
    event.waitUntil(
        caches.keys()
            .then(function (keys) {
                return Promise.all(keys.filter(function (k) { return k.indexOf(VERSION) !== 0; }).map(function (k) { return caches.delete(k); }));
            })
            .then(function () { return self.clients.claim(); })
    );
});

function isStatic(url) {
    return /\.(?:css|js|woff2?|ttf|eot|svg|png|jpe?g|gif|ico|webp|webmanifest)$/i.test(url.pathname)
        && url.pathname.toLowerCase().indexOf('/uplods/') !== 0;
}

self.addEventListener('fetch', function (event) {
    var request = event.request;
    if (request.method !== 'GET') { return; }

    var url = new URL(request.url);

    // pages: network first, offline page as the fallback
    if (request.mode === 'navigate') {
        event.respondWith(
            fetch(request).catch(function () {
                return caches.match(OFFLINE_URL).then(function (r) { return r || new Response('Offline', { status: 503, headers: { 'Content-Type': 'text/plain' } }); });
            })
        );
        return;
    }

    // same-site static files and Google Fonts: cache first, update in the background
    var fonts = url.hostname === 'fonts.googleapis.com' || url.hostname === 'fonts.gstatic.com';
    if ((url.origin === self.location.origin && isStatic(url)) || fonts) {
        event.respondWith(
            caches.open(STATIC_CACHE).then(function (cache) {
                return cache.match(request).then(function (cached) {
                    var network = fetch(request).then(function (response) {
                        if (response && (response.ok || response.type === 'opaque')) { cache.put(request, response.clone()); }
                        return response;
                    }).catch(function () { return cached; });
                    return cached || network;
                });
            })
        );
    }
    // everything else (AJAX, downloads, reports): straight to the server
});
