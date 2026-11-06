// Otium's website: the earnings estimate, smooth question boxes and a light fade-in on scroll.
// Everything meant to be read is in the HTML; this only runs the earnings slider and softens a few transitions.
(() => {
  "use strict";
  const still = matchMedia("(prefers-reduced-motion: reduce)").matches;
  const $ = (s, root = document) => root.querySelector(s);
  const $$ = (s, root = document) => [...root.querySelectorAll(s)];

  // ---------- Sizes from data ----------
  // Bar heights and fee shares arrive as data-h / data-w (percent) and are set here through the CSSOM, which the CSP
  // allows, unlike inline style attributes. The price bars' stagger (--i) is each bar's position.
  function sizes() {
    for (const el of $$("[data-h]")) el.style.height = `${el.dataset.h}%`;
    for (const el of $$("[data-w]")) el.style.width = `${el.dataset.w}%`;
    $$(".price-bars span").forEach((el, i) => el.style.setProperty("--i", i));
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
        const closing = d.open && !d.classList.contains("folding");
        const from = d.getBoundingClientRect().height;
        anim?.cancel();
        // "folding", not "closing": .closing is the front page's last section, whose padding would make the question jump.
        if (closing) d.classList.add("folding");
        else { d.classList.remove("folding"); d.open = true; }
        const to = closing ? summary.getBoundingClientRect().height + d.offsetHeight - d.clientHeight : d.getBoundingClientRect().height;
        d.style.overflow = "hidden";
        const duration = closing ? 280 : 380, mine = d.animate({ height: [`${from}px`, `${to}px`] }, { duration, easing: "cubic-bezier(.2,.8,.2,1)" });
        anim = mine;
        const done = () => {
          if (anim !== mine) return;
          anim = null;
          mine.cancel();
          d.style.overflow = "";
          if (closing) { d.open = false; d.classList.remove("folding"); }
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
  function platform() {
    const ua = navigator.userAgent;
    const os = /Windows/i.test(ua) ? "Windows" : /Mac OS X|Macintosh/i.test(ua) && !/iPhone|iPad/i.test(ua) ? "macOS" : /Linux/i.test(ua) && !/Android/i.test(ua) ? "Linux" : null;
    if (!os) return;
    // Windows is the tested platform; the Mac and Linux builds are beta for the first release.
    for (const label of $$("[data-download-label]")) label.textContent = os === "Windows" ? "Download for Windows" : `Download for ${os} (beta)`;
  }

  sizes(); earnings(); reveal(); countUps(); smoothDetails(); openLinkedQuestion(); platform();
})();
