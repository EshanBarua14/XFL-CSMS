/* XFL CSMS - the animated ticket story on the sign-in page (Views/RegisterLogin/_LoginStory.cshtml, css/xfl-story.css).
   Runs the timeline: sets data-phase on #xsStory (the CSS animates the people, bubbles, screens and stars for each phase),
   moves the ticket along its flight paths, and marks the matching step under the scene.
   Pauses with the button, when the tab is hidden and when the scene is scrolled out of view.
   With "reduce motion" on it shows the finished story, still. */
(function () {
    'use strict';
    var root = document.getElementById('xsStory');
    if (!root) { return; }
    var ticket = document.getElementById('xsTicket');
    var stateText = ticket && ticket.querySelector('.xs-ticket-state');
    var caption = document.getElementById('xsCaption');
    var steps = root.querySelectorAll('.xs-steps li');
    var pauseBtn = document.getElementById('xsPause');
    var reduced = !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);

    var HOME = { x: 118, y: 292 }, MANAGER = { x: 452, y: 316 }, ENGINEER = { x: 772, y: 316 };

    // phase, length (ms), step under the scene, caption, ticket state, flight path, where the ticket sits
    var PHASES = [
        { name: 'raise', ms: 3400, step: 1, text: 'A maker at the brokerage house hits a problem and raises a ticket.', ticket: 'new', at: HOME },
        { name: 'fly1', ms: 1500, step: 1, text: 'The ticket goes straight to XFL Support.', path: 'xsPath1' },
        { name: 'review', ms: 3600, step: 2, text: 'The XFL support manager reviews it, sets the priority and assigns an engineer.', at: MANAGER, events: [[2500, 'assigned']] },
        { name: 'fly2', ms: 1500, step: 2, text: 'Assigned: the ticket lands on the engineer’s desk.', path: 'xsPath2' },
        { name: 'solve', ms: 4000, step: 3, text: 'The support engineer finds the cause and deploys the fix.', at: ENGINEER, events: [[3100, 'solved']] },
        { name: 'fly3', ms: 1800, step: 3, text: 'Solved: the maker is told at once.', path: 'xsPath3' },
        { name: 'rate', ms: 5000, step: 4, text: 'The maker is happy and rates the support five stars.', at: HOME },
        { name: 'reset', ms: 700, step: 4, text: 'Every ticket, from the first report to a five-star review, in one place.' }
    ];
    var STATE_LABEL = { 'new': 'NEW', assigned: 'ASSIGNED', solved: 'SOLVED' };

    var index = -1, phaseStart = 0, elapsedBefore = 0, timer = null, raf = null, paused = false, hiddenOrAway = false, firedEvents = {};

    function setTicketState(s) {
        root.setAttribute('data-ticket', s);
        if (stateText) { stateText.textContent = STATE_LABEL[s] || ''; }
    }
    function place(p) { if (ticket && p) { ticket.setAttribute('transform', 'translate(' + p.x.toFixed(1) + ',' + p.y.toFixed(1) + ')'); } }
    function setCaption(text) {
        if (!caption || caption.textContent === text) { return; }
        caption.classList.add('xs-swap');
        setTimeout(function () { caption.textContent = text; caption.classList.remove('xs-swap'); }, reduced ? 0 : 220);
    }
    function markStep(n) {
        for (var i = 0; i < steps.length; i++) {
            var s = +steps[i].getAttribute('data-step');
            steps[i].classList.toggle('is-done', s < n);
            steps[i].classList.toggle('is-current', s === n);
        }
    }

    // the ticket follows its path, easing in and out, with a small hop in the middle
    function fly(phase, t) {
        var path = document.getElementById(phase.path);
        if (!path || !path.getTotalLength) { return; }
        var k = Math.min(1, Math.max(0, t / phase.ms));
        var e = k < .5 ? 4 * k * k * k : 1 - Math.pow(-2 * k + 2, 3) / 2;
        var len = path.getTotalLength();
        place(path.getPointAtLength(len * e));
    }

    function tick() {
        raf = null;
        if (paused || hiddenOrAway || index < 0) { return; }
        var phase = PHASES[index];
        var t = elapsedBefore + (performance.now() - phaseStart);
        if (phase.path) { fly(phase, t); }
        (phase.events || []).forEach(function (ev, i) {
            var key = index + ':' + i;
            if (!firedEvents[key] && t >= ev[0]) { firedEvents[key] = true; setTicketState(ev[1]); }
        });
        if (t >= phase.ms) { enter((index + 1) % PHASES.length); return; }
        raf = requestAnimationFrame(tick);
    }

    function enter(i) {
        index = i;
        var phase = PHASES[i];
        firedEvents = {};
        elapsedBefore = 0;
        phaseStart = performance.now();
        if (phase.name === 'raise') { setTicketState('new'); }
        if (phase.at) { place(phase.at); }
        root.setAttribute('data-phase', phase.name);
        markStep(phase.step);
        setCaption(phase.text);
        if (!raf) { raf = requestAnimationFrame(tick); }
    }

    function halt() {
        if (index >= 0 && !(paused && hiddenOrAway)) { elapsedBefore += performance.now() - phaseStart; }
        if (raf) { cancelAnimationFrame(raf); raf = null; }
        root.classList.add('xs-paused');
    }
    function resume() {
        if (paused || hiddenOrAway) { return; }
        phaseStart = performance.now();
        root.classList.remove('xs-paused');
        if (index < 0) { enter(0); return; }
        if (!raf) { raf = requestAnimationFrame(tick); }
    }

    if (reduced) {
        // the end of the story, still: everyone smiles, five stars, all steps done
        root.setAttribute('data-phase', 'still');
        setTicketState('solved');
        markStep(5);
        if (caption) { caption.textContent = 'A maker raises a ticket, XFL support assigns and solves it, and the maker rates the service.'; }
        return;
    }

    if (pauseBtn) {
        pauseBtn.addEventListener('click', function () {
            var wasPaused = paused;
            if (!wasPaused) { halt(); paused = true; } else { paused = false; resume(); }
            pauseBtn.setAttribute('aria-pressed', paused ? 'true' : 'false');
            pauseBtn.title = paused ? 'Play the animation' : 'Pause the animation';
            pauseBtn.innerHTML = '<i class="fas ' + (paused ? 'fa-play' : 'fa-pause') + '" aria-hidden="true"></i><span class="sr-only">' + pauseBtn.title + '</span>';
        });
    }

    function away(isAway) {
        if (isAway === hiddenOrAway) { return; }
        if (isAway) { if (!paused) { halt(); } hiddenOrAway = true; } else { hiddenOrAway = false; resume(); }
    }
    document.addEventListener('visibilitychange', function () { away(document.hidden); });
    if ('IntersectionObserver' in window) {
        new IntersectionObserver(function (entries) { away(!entries[0].isIntersecting && !document.hidden ? true : document.hidden); }, { threshold: 0.05 }).observe(root);
    }

    place(HOME);
    // start once the page has painted, so the first scene is not lost while the page loads
    setTimeout(function () { if (!paused && !hiddenOrAway) { enter(0); } }, 600);
})();
