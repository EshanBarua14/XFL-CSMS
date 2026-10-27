/* XFL CSMS - anti-forgery token for scripted requests.
   Every POST / DELETE is checked on the server (AutoValidateAntiforgeryToken). Forms carry the token in a
   hidden field; this file adds it as a header to jQuery AJAX calls and fetch() calls of this site,
   taken from <meta name="csrf-token"> written by _XflHead. */
(function () {
    'use strict';
    var HEADER = 'RequestVerificationToken';

    function token() {
        var m = document.querySelector('meta[name="csrf-token"]');
        return m ? m.getAttribute('content') : '';
    }
    function unsafe(method) {
        return !/^(GET|HEAD|OPTIONS|TRACE)$/i.test(method || 'GET');
    }
    function sameOrigin(url) {
        try { return new URL(url, location.href).origin === location.origin; } catch (e) { return true; }
    }

    if (window.jQuery) {
        window.jQuery.ajaxPrefilter(function (options, original, xhr) {
            if (unsafe(options.type) && sameOrigin(options.url)) {
                xhr.setRequestHeader(HEADER, token());
            }
        });
    }

    if (window.fetch) {
        var nativeFetch = window.fetch;
        window.fetch = function (input, init) {
            init = init || {};
            var method = init.method || (input && input.method) || 'GET';
            var url = typeof input === 'string' ? input : (input && input.url) || '';
            if (unsafe(method) && sameOrigin(url)) {
                var headers = new Headers(init.headers || (input && input.headers) || {});
                if (!headers.has(HEADER)) { headers.set(HEADER, token()); }
                init.headers = headers;
                if (!init.credentials) { init.credentials = 'same-origin'; }
            }
            return nativeFetch.call(this, input, init);
        };
    }
})();
