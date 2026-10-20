// Otium's website: the night sky, the 24-hour dial with its miner log, live earnings, and scroll details.
// Everything meant to be read is in the HTML; this only animates it and runs the earnings slider.
(() => {
  "use strict";
  const still = matchMedia("(prefers-reduced-motion: reduce)").matches;
  const $ = (s, root = document) => root.querySelector(s);
  const $$ = (s, root = document) => [...root.querySelectorAll(s)];
  const svgNs = "http://www.w3.org/2000/svg";

  // ---------- Night sky ----------
  function sky() {
    const canvas = $("#sky");
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    let stars = [], w = 0, h = 0, dpr = 1;
    function resize(width, height) {
      dpr = Math.min(devicePixelRatio || 1, 2);
      w = width; h = height;
      canvas.width = w * dpr; canvas.height = h * dpr;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      const count = Math.round(w * h / 9000);
      stars = Array.from({ length: count }, () => ({
        x: Math.random() * w, y: Math.random() * h, r: Math.random() * 1.1 + 0.2,
        a: Math.random() * 0.5 + 0.15, s: Math.random() * 0.8 + 0.2, p: Math.random() * Math.PI * 2,
      }));
    }
    let last = 0;
    function frame(t) {
      // 30 frames a second: the twinkle and the parallax are slow, so it looks the same for half the work.
      if (t - last < 32 && !still) { requestAnimationFrame(frame); return; }
      last = t;
      ctx.clearRect(0, 0, w, h);
      const scroll = scrollY * 0.05;
      for (const s of stars) {
        const twinkle = still ? 1 : 0.6 + 0.4 * Math.sin(t / 1000 * s.s + s.p);
        ctx.globalAlpha = s.a * twinkle;
        ctx.fillStyle = "#cfe9e1";
        ctx.beginPath();
        ctx.arc(s.x, (s.y - scroll * s.s + h) % h, s.r, 0, Math.PI * 2);
        ctx.fill();
      }
      if (!still && !document.hidden) requestAnimationFrame(frame);
    }
    // A ResizeObserver reports the canvas's size after layout, so reading it never forces one while the page loads.
    if ("ResizeObserver" in window) {
      new ResizeObserver(e => resize(e[0].contentRect.width, e[0].contentRect.height)).observe(canvas);
    } else {
      const measure = () => resize(canvas.clientWidth, canvas.clientHeight);
      measure();
      addEventListener("resize", measure);
    }
    document.addEventListener("visibilitychange", () => { if (!document.hidden && !still) requestAnimationFrame(frame); });
    requestAnimationFrame(frame);
  }

  // ---------- The 24-hour dial and the miner log ----------
  // An example day: at the computer from 08:30 to 17:00 and 18:30 to 23:00, away otherwise. Power is expensive
  // from 17:00 to 18:30, so Otium waits. Otium's fee slice is 29 minutes of mining.
  const day = [
    { from: 0, to: 8.5, kind: "mining" },
    { from: 8.5, to: 17, kind: "using" },
    { from: 17, to: 18.5, kind: "paused" },
    { from: 18.5, to: 23, kind: "using" },
    { from: 23, to: 24, kind: "mining" },
  ];
  const fee = { from: 4.2, to: 4.2 + 29 / 60 };
  const usdtPerMiningHour = 2.6 / 9.5; // about $2.60 a day from the 9.5 mining hours, the RTX 3080 example
  const C = 200, R = 158;

  function point(hour, radius) {
    const a = (hour / 24) * Math.PI * 2 - Math.PI / 2;
    return [C + radius * Math.cos(a), C + radius * Math.sin(a)];
  }
  function arcPath(from, to, radius) {
    const [x0, y0] = point(from, radius), [x1, y1] = point(to, radius);
    const large = to - from > 12 ? 1 : 0;
    return `M ${x0.toFixed(2)} ${y0.toFixed(2)} A ${radius} ${radius} 0 ${large} 1 ${x1.toFixed(2)} ${y1.toFixed(2)}`;
  }
  function el(name, attrs, parent) {
    const node = document.createElementNS(svgNs, name);
    for (const [k, v] of Object.entries(attrs)) node.setAttribute(k, v);
    if (parent) parent.appendChild(node);
    return node;
  }
  function kindAt(hour) {
    if (hour >= fee.from && hour < fee.to) return "fee";
    return day.find(p => hour >= p.from && hour < p.to)?.kind ?? "using";
  }
  const clockText = hour => {
    const m = Math.floor((hour % 24) * 60);
    return `${String(Math.floor(m / 60)).padStart(2, "0")}:${String(m % 60).padStart(2, "0")}`;
  };

  function dial() {
    const svg = $("#dial");
    if (!svg) return;
    el("circle", { class: "face", cx: C, cy: C, r: 190 }, svg);
    for (let i = 0; i < 96; i++) {
      const major = i % 4 === 0;
      const [x0, y0] = point(i / 4, major ? 176 : 181), [x1, y1] = point(i / 4, 187);
      el("line", { class: `tick${major ? " major" : ""}`, x1: x0, y1: y0, x2: x1, y2: y1 }, svg);
    }
    for (const h of [0, 6, 12, 18]) {
      const [x, y] = point(h, 212);
      const t = el("text", { class: "hour", x, y, "text-anchor": "middle", "dominant-baseline": "middle" }, svg);
      t.textContent = String(h).padStart(2, "0");
    }
    let delay = 0;
    for (const part of day) {
      const path = el("path", { class: `arc ${part.kind}`, d: arcPath(part.from, part.to, R) }, svg);
      if (part.kind !== "paused" && !still) {
        const len = path.getTotalLength();
        path.style.strokeDasharray = `${len}`;
        path.style.setProperty("--len", `${len}`);
        path.classList.add("draw");
        path.style.animationDelay = `${delay}ms`;
      }
      delay += 140;
    }
    el("path", { class: "arc fee", d: arcPath(fee.from, fee.to, R) }, svg);
    const sparks = el("g", {}, svg);
    const hand = el("line", { class: "hand", x1: C, y1: C - 132, x2: C, y2: C - 146 }, svg);
    const dot = el("circle", { class: "hand-dot", cx: C, cy: C - R, r: 6 }, svg);

    const clock = $("#dial-clock"), amount = $("#dial-amount"), state = $("#dial-state"), log = $("#log");
    const states = {
      mining: "Away: mining Pearl at 101.4 TH/s",
      fee: "Fee slice: mining for Otium",
      paused: "Power too expensive: waiting",
      using: "You're at the computer: paused",
    };
    const DAY_MS = 24000; // one simulated day every 24 seconds
    const startHour = still ? 3 : 23.1;
    let start = performance.now() - startHour * DAY_MS / 24;
    let earned = still ? 1.1043 : 0.0274, lastHour = null, lastKind = kindAt(startHour), nextLog = 0, shareCount = 2;

    function logLine(hour, html, cls) {
      if (!log) return;
      const line = document.createElement("div");
      if (cls) line.className = cls;
      line.innerHTML = `<span class="t">[${clockText(hour)}]</span> ${html}`;
      log.appendChild(line);
      // More lines than fit: the oldest slide out of view under the fade, so the window is always full.
      while (log.children.length > 18) log.firstElementChild.remove();
    }
    function spark(hour) {
      const [x, y] = point(hour, R);
      const c = el("circle", { class: "spark", cx: x, cy: y, r: 2.4 }, sparks);
      const born = performance.now();
      (function fly(now) {
        const t = Math.min(1, (now - born) / 900);
        const e = 1 - Math.pow(1 - t, 3);
        c.setAttribute("cx", x + (C - x) * e * 0.22);
        c.setAttribute("cy", y + (C - y) * e * 0.22);
        c.setAttribute("opacity", String(1 - t));
        if (t < 1) requestAnimationFrame(fly); else c.remove();
      })(born);
    }
    function tick(now) {
      const hour = ((now - start) / DAY_MS * 24) % 24;
      if (lastHour !== null && lastHour < 12 && hour >= 12) earned = 0; // the night's total starts over at noon
      const kind = kindAt(hour);
      if (lastHour !== null && kind === "mining") {
        const dh = hour >= lastHour ? hour - lastHour : 0;
        earned += dh * usdtPerMiningHour;
      }
      const [x, y] = point(hour, R), [hx0, hy0] = point(hour, 128), [hx1, hy1] = point(hour, 144);
      dot.setAttribute("cx", x); dot.setAttribute("cy", y);
      hand.setAttribute("x1", hx0); hand.setAttribute("y1", hy0);
      hand.setAttribute("x2", hx1); hand.setAttribute("y2", hy1);
      clock.textContent = clockText(hour);
      amount.textContent = earned.toFixed(4);
      if (kind !== lastKind) {
        state.textContent = states[kind];
        state.dataset.kind = kind;
        if (kind === "fee") logLine(hour, "fee slice: the next 29 min mine to Otium's address", "warn");
        else if (kind === "paused") logLine(hour, "power 0.27 $/kWh is above break-even: paused", "warn");
        else if (kind === "using") logLine(hour, "you're back: miners stopped");
        else if (lastKind === "fee") logLine(hour, "fee slice done: mining to your address again", "ok");
        else if (lastKind) logLine(hour, "away: SRBMiner pearlhash → pearlpow.unmineable.com:443 (TLS)", "ok");
      }
      if ((kind === "mining" || kind === "fee") && now > nextLog) {
        const roll = Math.random();
        if (roll < 0.45) logLine(hour, `share accepted · ${++shareCount}/${shareCount} · ${(0.6 + Math.random()).toFixed(1)} s`, "ok");
        else if (roll < 0.7) logLine(hour, `GPU0 RTX 3080 · ${(100.6 + Math.random() * 1.6).toFixed(1)} TH/s · ${236 + Math.round(Math.random() * 6)} W · ${60 + Math.round(Math.random() * 3)}°C`);
        else if (roll < 0.85) logLine(hour, `XMRig rx/0 · ${(2.36 + Math.random() * 0.1).toFixed(2)} kH/s · 6 threads`);
        else logLine(hour, `new job from pool · diff ${(1.9 + Math.random() * 0.4).toFixed(2)}M`);
        nextLog = now + 650 + Math.random() * 700;
      }
      if ((kind === "mining" || kind === "fee") && !still && Math.random() < 0.25) spark(hour);
      lastHour = hour; lastKind = kind;
      if (!still) requestAnimationFrame(tick);
    }
    // Seed the log so it reads like a running miner from the first frame.
    // The evening so far, so the window opens full.
    for (const [h, text, cls] of [
      [8.5, "you're back: miners stopped", ""],
      [17.0, "power 0.27 $/kWh is above break-even: paused", "warn"],
      [18.5, "you're back: miners stopped", ""],
      [22.9, "Otium 0.1.0 · away for 3 min · starting the miners", ""],
      [22.92, "away: XMRig rx/0 → rx.unmineable.com:443 (TLS)", ""],
      [23.0, "away: SRBMiner pearlhash → pearlpow.unmineable.com:443 (TLS)", "ok"],
      [23.02, "login USDT:T9yD…kM2x.ot1a2b3c4dg#ztjq-jr7s", ""],
      [23.04, "GPU0 RTX 3080 · 101.2 TH/s · 238 W · 60°C", ""],
      [23.05, "share accepted · 1/1 · 0.9 s", "ok"],
      [23.07, "XMRig rx/0 · 2.41 kH/s · 6 threads", ""],
      [23.09, "share accepted · 2/2 · 1.3 s", "ok"],
      [23.1, "new job from pool · diff 2.07M", ""],
    ]) logLine(h, text, cls);
    requestAnimationFrame(tick);
  }

  // ---------- What your PC could earn (the hero estimate) ----------
  // Same formula as the server's first render: the card's best coin after fees, less its electricity, for the hours
  // the computer is left alone, over a 30-day month.
  function earnings() {
    const data = JSON.parse($("#otium-data")?.textContent || "{}");
    const hours = $("#hours"), price = $("#price"), month = $("#est-month");
    if (!hours || !price || !month || !data.cards?.length) return;
    const select = $("#card");
    let cardId = select?.value ?? data.cards[0].id;
    function choose(id) {
      cardId = id;
      render();
    }
    let shown = Number(month.textContent.replace(/[^0-9.]/g, "")) || 0;
    let tween = 0;

    function animateTo(value, length = still ? 0 : 650) {
      const from = shown, start = performance.now();
      cancelAnimationFrame(tween);
      (function step(now) {
        const t = length ? Math.min(1, (now - start) / length) : 1;
        shown = from + (value - from) * (1 - Math.pow(1 - t, 3));
        month.textContent = `$${Math.round(shown)}`;
        if (t < 1) tween = requestAnimationFrame(step);
      })(start);
    }
    function render() {
      const h = Number(hours.value), p = Number(price.value);
      $("#hours-out").textContent = `${h} h a day`;
      $("#price-out").textContent = `$${p.toFixed(2)} / kWh`;
      const card = data.cards.find(c => c.id === cardId) ?? data.cards[0];
      const best = card.coins
        .map(c => ({ ...c, day: (c.usdPerDay - c.watts * 24 / 1000 * p) * h / 24 }))
        .sort((a, b) => b.day - a.day)[0];
      const day = Math.max(0, best.day);
      // Internet sharing adds the same whatever the card or power price (it uses next to no power).
      const sharing = data.sharingPerDay || 0;
      animateTo((day + sharing) * 30);
      const summary = $("#est-summary");
      if (summary) summary.textContent = day <= 0
        ? `An ${card.name} would mostly wait at this electricity price${sharing > 0 ? "; internet sharing still earns" : ""}.`
        : `An ${card.name} mining while you're away (${h} h a day)${sharing > 0 ? ", plus internet sharing" : ""}.`;
      $("#est-sub").firstChild.textContent = day <= 0
        ? `Mining: waits at this power price on an ${card.name}.`
        : `Mining: $${day.toFixed(2)}/day on an ${card.name} (${best.coin}).`;
      const raw = (data.payoutUsdt || 1.5) / day, days = Math.ceil(raw);
      $("#est-payout").textContent = day <= 0 ? ""
        : `First payout ${raw < 1 ? "within a day" : `in about ${days} day${days === 1 ? "" : "s"}`}.`;
      // How calculators and comparison sites count (24 hours a day, no electricity), for comparing like for like.
      const compare = $("#est-compare"), gross = Math.max(0, ...card.coins.map(c => c.usdPerDay));
      if (compare) compare.textContent = gross > 0
        ? `Mining around the clock, before electricity: $${Math.round(gross * 30)} a month, the way most comparison sites count it.`
        : "";
    }
    select?.addEventListener("change", () => choose(select.value));
    hours.addEventListener("input", render);
    price.addEventListener("input", render);
    // On arrival the month's figure counts up to what the server already wrote there. It starts on the first frame, so
    // where frames don't run (a hidden tab, a page preview) the figure stays as written instead of showing "$0".
    if (!still && shown > 0) {
      const target = shown;
      requestAnimationFrame(() => { if (shown === target) { shown = 0; animateTo(target, 1100); } });
    }
  }

  // ---------- Big figures count up when they come into view ----------
  // "$96.25" and "≈ $62": the first number in the element's own text counts up from zero once, keeping its decimals.
  // ---------- Questions and "See the breakdown and adjust" open and close smoothly ----------
  // The box's height slides between closed and open (in every browser); clicking again midway turns it around.
  function smoothDetails() {
    if (still || !Element.prototype.animate) return;
    $$(".est-more-body > *").forEach((el, i) => el.style.setProperty("--n", i));
    for (const d of $$("details")) {
      const summary = d.querySelector(":scope > summary");
      if (!summary) continue;
      let anim = null;
      summary.addEventListener("click", e => {
        e.preventDefault();
        const closing = d.open && !d.classList.contains("closing");
        const from = d.getBoundingClientRect().height;
        anim?.cancel();
        if (closing) d.classList.add("closing");
        else { d.classList.remove("closing"); d.open = true; }
        const to = closing ? summary.getBoundingClientRect().height + d.offsetHeight - d.clientHeight : d.getBoundingClientRect().height;
        d.style.overflow = "hidden";
        const duration = closing ? 280 : 380, mine = d.animate({ height: [`${from}px`, `${to}px`] }, { duration, easing: "cubic-bezier(.2,.8,.2,1)" });
        anim = mine;
        const done = () => {
          if (anim !== mine) return;
          anim = null;
          mine.cancel();
          d.style.overflow = "";
          if (closing) { d.open = false; d.classList.remove("closing"); }
        };
        mine.onfinish = done;
        // Where frames don't run (a hidden tab), the box still ends up open or closed.
        setTimeout(done, duration + 150);
      });
    }
  }

  // ---------- A link to a question ("What that covers") opens it ----------
  function openLinkedQuestion() {
    const open = () => {
      const target = location.hash.length > 1 ? document.getElementById(location.hash.slice(1)) : null;
      if (target?.tagName === "DETAILS") target.open = true;
    };
    addEventListener("hashchange", open);
    open();
  }

  function countUps() {
    const items = $$("[data-count]");
    if (still || !items.length || !("IntersectionObserver" in window)) return;
    const io = new IntersectionObserver(entries => {
      for (const e of entries) {
        if (!e.isIntersecting) continue;
        io.unobserve(e.target);
        const text = [...e.target.childNodes].find(n => n.nodeType === Node.TEXT_NODE && /\d/.test(n.textContent));
        const match = text?.textContent.match(/(\d+(?:\.\d+)?)/);
        if (!text || !match) continue;
        const original = text.textContent, target = Number(match[1]), decimals = (match[1].split(".")[1] || "").length;
        let start = null;
        requestAnimationFrame(function step(now) {
          start ??= now;
          const t = Math.min(1, (now - start) / 1100), value = target * (1 - Math.pow(1 - t, 3));
          text.textContent = t < 1 ? original.replace(match[1], value.toFixed(decimals)) : original;
          if (t < 1) requestAnimationFrame(step);
        });
      }
    }, { rootMargin: "0px 0px -10% 0px" });
    for (const item of items) io.observe(item);
  }

  // ---------- Scroll details ----------
  function reveal() {
    const items = $$(".reveal");
    if (still || !("IntersectionObserver" in window)) return;
    const fold = innerHeight * 0.92;
    const io = new IntersectionObserver(entries => {
      // Blocks that come into view together (a heading and the panel beside it) arrive one after the other.
      entries.filter(e => e.isIntersecting).forEach((e, i) => {
        e.target.style.transitionDelay = `${i * 110}ms`;
        e.target.classList.add("shown");
        e.target.classList.remove("pending");
        io.unobserve(e.target);
      });
    }, { rootMargin: "0px 0px -8% 0px" });
    for (const item of items) {
      if (item.getBoundingClientRect().top > fold) { item.classList.add("pending"); io.observe(item); }
    }
  }
  function shiftLine() {
    const line = $(".shift-line");
    if (!line) return;
    const moments = $$(".moment", line);
    function update() {
      const r = line.getBoundingClientRect();
      const p = Math.min(1, Math.max(0, (innerHeight * 0.75 - r.top) / (r.height + innerHeight * 0.3)));
      line.style.setProperty("--progress", p.toFixed(3));
      moments.forEach((m, i) => m.classList.toggle("lit", p >= i / moments.length + 0.02));
    }
    addEventListener("scroll", update, { passive: true });
    update();
  }
  function spotlights() {
    for (const b of $$(".button")) {
      b.addEventListener("pointermove", e => {
        const r = b.getBoundingClientRect();
        b.style.setProperty("--x", `${e.clientX - r.left}px`);
        b.style.setProperty("--y", `${e.clientY - r.top}px`);
      });
    }
    for (const p of $$(".panel")) {
      p.addEventListener("pointermove", e => {
        const r = p.getBoundingClientRect();
        p.style.setProperty("--mx", `${e.clientX - r.left}px`);
        p.style.setProperty("--my", `${e.clientY - r.top}px`);
      });
    }
  }
  function platform() {
    const ua = navigator.userAgent;
    const os = /Windows/i.test(ua) ? "Windows" : /Mac OS X|Macintosh/i.test(ua) && !/iPhone|iPad/i.test(ua) ? "macOS" : /Linux/i.test(ua) && !/Android/i.test(ua) ? "Linux" : null;
    if (!os) return;
    // Windows is the tested platform; the Mac and Linux builds are beta for the first release.
    for (const label of $$("[data-download-label]")) label.textContent = os === "Windows" ? "Download for Windows" : `Download for ${os} (beta)`;
  }

  // ---------- Hash field behind the closing line ----------
  // Rows of hex, a few characters changing at a time, like hashes going by. Still for reduced motion.
  function hashField() {
    const pre = $(".ascii");
    if (!pre) return;
    const hex = "0123456789abcdef", cols = 120, rows = 18;
    const grid = Array.from({ length: rows }, () => Array.from({ length: cols }, () => hex[(Math.random() * 16) | 0]));
    const draw = () => { pre.textContent = grid.map(r => r.join("")).join("\n"); };
    draw();
    if (still) return;
    let visible = false;
    if ("IntersectionObserver" in window) new IntersectionObserver(e => { visible = e[0].isIntersecting; }).observe(pre);
    setInterval(() => {
      if (!visible || document.hidden) return;
      for (let i = 0; i < 90; i++) grid[(Math.random() * rows) | 0][(Math.random() * cols) | 0] = hex[(Math.random() * 16) | 0];
      draw();
    }, 140);
  }

  sky(); dial(); earnings(); reveal(); countUps(); smoothDetails(); openLinkedQuestion(); shiftLine(); spotlights(); platform(); hashField();
})();
