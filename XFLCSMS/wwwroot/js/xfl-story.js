/* Xpert CSMS - the animated ticket story on the sign-in page (Views/RegisterLogin/_LoginStory.cshtml, css/xfl-story.css).
   Runs the timeline: sets data-phase on #xsStory (the CSS animates the people, bubbles, screens and stars for each phase),
   moves the ticket along its flight paths (and a chat message between the engineer and the maker), updates the ticket's
   level and the development stage, and marks the matching step under the scene.
   Pauses with the button, when the tab is hidden and when the scene is scrolled out of view.
   With "reduce motion" on it shows the finished story, still. */
(function () {
    'use strict';
    var root = document.getElementById('xsStory');
    if (!root) { return; }
    var ticket = document.getElementById('xsTicket');
    var stateText = ticket && ticket.querySelector('.xs-ticket-state');
    var levelText = document.getElementById('xsLevel');
    var stageText = document.getElementById('xsStage');
    var chat = document.getElementById('xsChat');
    var caption = document.getElementById('xsCaption');
    var steps = root.querySelectorAll('.xs-steps li');
    var pauseBtn = document.getElementById('xsPause');
    var reduced = !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);

    var HOME = { x: 118, y: 292 }, MANAGER = { x: 452, y: 316 }, ENGINEER = { x: 772, y: 316 };

    // phase, length (ms), step under the scene, caption, flight path, where the ticket sits,
    // and timed events: a ticket state ('assigned', 'solved') or a function
    var PHASES = [
        { name: 'raise', ms: 3400, step: 1, text: 'A maker at the brokerage house hits a problem and raises a ticket.', at: HOME },
        { name: 'fly1', ms: 1500, step: 1, text: 'The ticket joins the XFL ticket queue at level L1.', path: 'xsPath1' },
        { name: 'review', ms: 4200, step: 2, text: 'Auto-assigned to a free engineer. Engineers can self-assign, managers can assign.', at: MANAGER, events: [[2500, 'assigned']] },
        { name: 'fly2', ms: 1500, step: 2, text: 'Assigned: the ticket lands on an L1 engineer’s desk.', path: 'xsPath2' },
        { name: 'solve', ms: 3800, step: 3, text: 'The engineer works it and escalates it to L2, or asks a colleague to take it over.', at: ENGINEER, events: [[2300, function () { setLevel('L2'); }]] },
        { name: 'track', ms: 5200, step: 4, text: 'Code changes are tracked stage by stage; the client chats with the engineer.', at: ENGINEER, chat: true,
            events: [[200, stage('ANALYSIS')], [1100, stage('DEVELOP')], [2000, stage('REVIEW')], [2900, stage('QA / UAT')], [3800, stage('DEPLOYED')], [3900, 'solved']] },
        { name: 'fly3', ms: 1800, step: 5, text: 'Closed with the resolution: the maker is told at once.', path: 'xsPath3' },
        { name: 'rate', ms: 5200, step: 5, text: 'The maker rates the engineer or reopens it. The house MD sees it all, read only.', at: HOME },
        { name: 'reset', ms: 700, step: 5, text: 'Every ticket, from the first report to a five-star review, in one place.' }
    ];
    var STATE_LABEL = { 'new': 'NEW', assigned: 'ASSIGNED', solved: 'SOLVED' };

    var index = -1, phaseStart = 0, elapsedBefore = 0, timer = null, raf = null, paused = false, hiddenOrAway = false, firedEvents = {};

    function setTicketState(s) {
        root.setAttribute('data-ticket', s);
        if (stateText) { stateText.textContent = STATE_LABEL[s] || ''; }
    }
    function setLevel(l) {
        root.setAttribute('data-level', l);
        if (levelText) { levelText.textContent = l; }
    }
    function stage(name) { return function () { if (stageText) { stageText.textContent = name; } }; }
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

    // the chat message goes from the engineer to the maker and back during the phase
    function chatAt(phase, t) {
        var path = document.getElementById('xsPath3');
        if (!chat || !path || !path.getTotalLength) { return; }
        var k = Math.min(1, Math.max(0, t / phase.ms)) * 2;
        var leg = k < 1 ? k : 2 - k;
        var e = leg < .5 ? 2 * leg * leg : 1 - Math.pow(-2 * leg + 2, 2) / 2;
        var p = path.getPointAtLength(path.getTotalLength() * e);
        chat.setAttribute('transform', 'translate(' + p.x.toFixed(1) + ',' + (p.y - 20).toFixed(1) + ')');
    }

    function tick() {
        raf = null;
        if (paused || hiddenOrAway || index < 0) { return; }
        var phase = PHASES[index];
        var t = elapsedBefore + (performance.now() - phaseStart);
        if (phase.path) { fly(phase, t); }
        if (phase.chat) { chatAt(phase, t); }
        (phase.events || []).forEach(function (ev, i) {
            var key = index + ':' + i;
            if (!firedEvents[key] && t >= ev[0]) { firedEvents[key] = true; if (typeof ev[1] === 'function') { ev[1](); } else { setTicketState(ev[1]); } }
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
        if (phase.name === 'raise') { setTicketState('new'); setLevel('L1'); stage('ANALYSIS')(); }
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
        setLevel('L2');
        stage('DEPLOYED')();
        markStep(6);
        if (caption) { caption.textContent = 'A maker raises a ticket, the queue assigns it, engineers escalate and fix it, the maker rates it, and the MD sees it all.'; }
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
