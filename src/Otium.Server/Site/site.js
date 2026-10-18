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
    function resize() {
      dpr = Math.min(devicePixelRatio || 1, 2);
      w = canvas.clientWidth; h = canvas.clientHeight;
      canvas.width = w * dpr; canvas.height = h * dpr;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      const count = Math.round(w * h / 9000);
      stars = Array.from({ length: count }, () => ({
        x: Math.random() * w, y: Math.random() * h, r: Math.random() * 1.1 + 0.2,
        a: Math.random() * 0.5 + 0.15, s: Math.random() * 0.8 + 0.2, p: Math.random() * Math.PI * 2,
      }));
    }
    function frame(t) {
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
    resize();
    addEventListener("resize", resize);
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
    let earned = still ? 1.1043 : 0.0274, lastHour = null, lastKind = kindAt(startHour), nextLog = 0, shareCount = 1;

    function logLine(hour, html, cls) {
      if (!log) return;
      const line = document.createElement("div");
      if (cls) line.className = cls;
      line.innerHTML = `<span class="t">[${clockText(hour)}]</span> ${html}`;
      log.appendChild(line);
      while (log.children.length > 9) log.firstElementChild.remove();
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
    for (const [h, text, cls] of [[22.9, "Otium 0.1.0 · away for 3 min · starting the miners", ""], [23.0, "away: SRBMiner pearlhash → pearlpow.unmineable.com:443 (TLS)", "ok"], [23.02, "login USDT:T9yD…kM2x.ot1a2b3c4dg#ztjq-jr7s", ""], [23.05, "share accepted · 1/1 · 0.9 s", "ok"]]) logLine(h, text, cls);
    requestAnimationFrame(tick);
  }

  // ---------- Live earnings per graphics card ----------
  function earnings() {
    const data = JSON.parse($("#otium-data")?.textContent || "{}");
    const rows = $("#cards"), slider = $("#price"), out = $("#price-out");
    if (!rows || !slider || !data.cards?.length) return;
    function render() {
      const price = Number(slider.value);
      out.textContent = `$${price.toFixed(2)} / kWh`;
      const results = data.cards.map(card => {
        const best = card.coins
          .map(c => ({ ...c, net: c.usdPerDay - c.watts * 24 / 1000 * price }))
          .sort((a, b) => b.net - a.net)[0];
        return { card, best };
      });
      const max = Math.max(0.01, ...results.map(r => r.best.net));
      for (const { card, best } of results) {
        const row = rows.querySelector(`[data-card="${card.id}"]`);
        if (!row) continue;
        const loses = best.net <= 0;
        row.querySelector(".coin-used").textContent = loses ? "stays paused: power costs more" : `${best.coin}${best.note ? ` · ${best.note}` : ""}`;
        row.querySelector(".bar").classList.toggle("loss", loses);
        row.querySelector(".bar span").style.width = `${loses ? 4 : Math.max(3, best.net / max * 100)}%`;
        row.querySelector(".amount").textContent = loses ? "$0.00" : `$${best.net.toFixed(2)}`;
      }
    }
    slider.addEventListener("input", render);
    render();
  }

  // ---------- Scroll details ----------
  function reveal() {
    const items = $$(".reveal");
    if (still || !("IntersectionObserver" in window)) return;
    const fold = innerHeight * 0.92;
    const io = new IntersectionObserver(entries => {
      for (const e of entries) if (e.isIntersecting) { e.target.classList.add("shown"); e.target.classList.remove("pending"); io.unobserve(e.target); }
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
    for (const label of $$("[data-download-label]")) label.textContent = `Download for ${os}`;
  }

  sky(); dial(); earnings(); reveal(); shiftLine(); spotlights(); platform();
})();
