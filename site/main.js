(function () {
  "use strict";

  // Onde o instalador fica publicado (releases do GitHub).
  const REPO = "Serignolli/cmdcrew";
  const SETUP = "CmdCrew-Setup.exe";

  const { Crew, Showcase, frames, prepare, pixelScale } = window.Sprites;
  const { t } = window.I18N;
  window.I18N.apply();
  const CHARS = window.I18N.localizeChars(window.PERSONAGENS);
  const PROJECT = t("project", "meu-projeto");
  const byId = (id) => CHARS.find((c) => c.id === id);
  const pick = (a) => a[Math.floor(Math.random() * a.length)];
  const esc = (s) => String(s).replace(/[&<>]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;" })[c]);

  // Pausa as animações que não estão na tela.
  const watch = (el, crew) => new IntersectionObserver((es) => es.forEach((e) => (crew.paused = !e.isIntersecting))).observe(el);

  // ---------- topo: um personagem por vez ----------
  // Ele entra andando, trabalha um pouco e sai pelo outro lado; aí entra o próximo.

  const heroArt = document.querySelector(".hero-art");
  const heroName = document.getElementById("heroName");
  const heroOrder = CHARS.slice();
  let heroIdx = 0;
  const hero = new Crew(document.getElementById("heroStage"), { char: heroOrder[0], unit: 6, count: 1, groundPad: 13, speed: 1.6 });
  watch(heroArt, hero);
  function heroNext() {
    const ch = heroOrder[heroIdx++ % heroOrder.length];
    hero.setChar(ch, ch.skins[0].id);   // na apresentação, sempre a skin original
    hero.workers[0].delay = 0;
    heroName.innerHTML = (ch.destaque ? "<span class='star'>★</span> " : "") + esc(ch.nome);
    heroName.classList.remove("show"); void heroName.offsetWidth; heroName.classList.add("show");
    setTimeout(() => hero.leave(heroNext), 9500);
  }
  heroNext();

  // ---------- terminal de demonstração ----------

  const termStage = document.getElementById("termStage");
  const termBody = document.getElementById("termBody");
  const picker = document.getElementById("picker");
  let current = byId("oompa-loompas") || CHARS[0];
  const crew = new Crew(termStage, { char: current, unit: 4, count: 5, groundPad: 0 });
  watch(termStage, crew);

  const PROMPTS = t("prompts", [
    ["deixa o botão de login mais bonito", "src/components/LoginButton.tsx", "\"button\"", "npm test", "Deixei o botão com bordas arredondadas e um hover mais suave."],
    ["corrige o bug do carrinho de compras", "src/cart/total.ts", "\"calculateTotal\"", "npm test cart", "O desconto era aplicado duas vezes. Corrigido e com teste novo."],
    ["escreve os testes do módulo de pagamento", "src/payments/index.ts", "\"charge(\"", "npm test payments", "Adicionei 14 testes cobrindo sucesso, recusa e estorno."],
    ["descobre por que o build quebrou", "package.json", "\"import \\\"\"", "npm run build", "Faltava exportar um tipo. O build voltou a passar."],
    ["refatora esse arquivo gigante", "src/utils/helpers.js", "\"function\"", "npm run lint", "Dividi em 4 arquivos menores, sem mudar o comportamento."],
  ]);
  document.getElementById("termTitle").textContent = `claude — ~/${PROJECT}`;
  const GLYPHS = ["·", "✢", "✳", "✶", "✻", "✽", "✻", "✶", "✳", "✢"];
  let timers = [], spinTimer = null, promptIdx = 0;

  function line(cls, html) {
    const d = document.createElement("div");
    d.className = "line " + cls; d.innerHTML = html;
    termBody.appendChild(d);
    while (termBody.children.length > 14) termBody.removeChild(termBody.children[1]);
    return d;
  }
  const at = (s, fn) => timers.push(setTimeout(fn, s * 1000));

  function runScript() {
    timers.forEach((t) => (typeof t === "object" ? t.clear() : clearTimeout(t))); timers = []; clearInterval(spinTimer);
    termBody.innerHTML = "";
    line("t-welcome", `<b>✻</b> Welcome to <b>Claude Code</b>!<br><span class='t-dim'>/help for help · cwd: ~/${PROJECT}</span>`);
    const [prompt, file, search, cmd, done] = PROMPTS[promptIdx++ % PROMPTS.length];
    const input = line("t-input", "<span class='typed'></span><span class='caret'>&nbsp;</span>");
    const typed = input.querySelector(".typed");
    let i = 0;
    const typer = setInterval(() => { typed.textContent = prompt.slice(0, ++i); if (i >= prompt.length) clearInterval(typer); }, 38);
    timers.push({ clear: () => clearInterval(typer) });

    const start = 0.5 + prompt.length * 0.038;
    let spin, tip, secs = 0, verb = pick(current.verbos || [t("thinking", "Pensando")]);
    at(start, () => {
      input.remove();
      line("t-user", esc(prompt));
      crew.restart();
      spin = line("t-spin", "");
      tip = line("t-tip", "⎿ Tip: " + esc(pick(current.dicas || [""])));
      let g = 0, t0 = performance.now();
      const render = () => {
        secs = Math.floor((performance.now() - t0) / 1000);
        spin.innerHTML = `<span class="glyph">${GLYPHS[g++ % GLYPHS.length]}</span>${esc(verb)}… <span class="meta2">(${secs}s · ↓ ${(0.3 + secs * 0.21).toFixed(1)}k tokens · esc to interrupt)</span>`;
      };
      render();
      spinTimer = setInterval(render, 120);
      const verbT = setInterval(() => (verb = pick(current.verbos)), 2600);
      timers.push({ clear: () => clearInterval(verbT) });
      at(15.5, () => clearInterval(verbT));
    });
    // as ferramentas aparecem acima do spinner
    const tool = (s, name, out) => at(start + s, () => {
      const a = line("t-tool", name), b = out ? line("t-out", "⎿ " + out) : null;
      if (spin) { termBody.appendChild(spin); termBody.appendChild(tip); }
    });
    tool(2.5, `Read(${file})`, "Read 128 lines");
    tool(5.5, `Search(pattern: ${esc(search)})`, "Found 6 files");
    tool(8.5, `Update(${file})`, "Updated with 12 additions and 4 removals");
    tool(11.5, `Bash(${cmd})`, "✓ all checks passed");
    at(start + 15, () => {
      clearInterval(spinTimer);
      spin.remove(); tip.remove();
      line("t-done", esc(done));
      line("t-dim", `✻ ${pick(["Cooked", "Baked", "Brewed", "Crunched", "Churned"])} for ${secs}s`);
      crew.finish();
    });
    at(start + 21, runScript);
  }

  function selectChar(ch) {
    current = ch;
    // troca a equipe, mas deixa o palco vazio: eles só entram quando o Claude começa a trabalhar (crew.restart no roteiro)
    crew.setChar(ch, "*");
    crew.workers.forEach((w) => (w.gone = true));
    picker.querySelectorAll(".chip").forEach((b) => b.setAttribute("aria-selected", b.dataset.id === ch.id ? "true" : "false"));
    promptIdx = Math.floor(Math.random() * PROMPTS.length);
    runScript();
  }

  // ícone: primeiro quadro de "parado", reduzido para caber no círculo
  function icon(ch, size) {
    prepare(ch);
    const c = document.createElement("canvas");
    const dpr = window.devicePixelRatio || 1;
    c.width = c.height = size * dpr; c.style.width = c.style.height = size + "px";
    const f = frames(ch, ch.skins[0], "parado", 1, false)[0];
    const h = ch._bottom - ch._top + 1;
    const s = Math.min(size * dpr / h, size * dpr / ch._w) * 0.9;
    const g = c.getContext("2d"); g.imageSmoothingEnabled = false;
    g.drawImage(f, 0, ch._top, f.width, h, (size * dpr - f.width * s) / 2, (size * dpr - h * s) / 2, f.width * s, h * s);
    return c;
  }

  CHARS.forEach((ch) => {
    const b = document.createElement("button");
    b.className = "chip"; b.type = "button"; b.dataset.id = ch.id; b.setAttribute("role", "tab");
    b.appendChild(icon(ch, 28));
    b.insertAdjacentHTML("beforeend", (ch.destaque ? "<span class='star'>★</span>" : "") + esc(ch.nome));
    b.addEventListener("click", () => selectChar(ch));
    picker.appendChild(b);
  });
  selectChar(current);

  // ---------- galeria ----------

  const WORK_LABEL = t("work", {
    faiscas: "faíscas", poeira: "poeira", terra: "terra", neve: "neve", estrelas: "estrelinhas", magia: "magia", zzz: "cochilos (zzz)",
    notas: "cantoria ♪", claude: "estrelinhas ✻", codigo: "código", dados: "dados", bugs: "besourinhos", fumaca: "fumaça",
    moedas: "moedas", latidos: "latidos", coracoes: "coraçõezinhos", flores: "flores ✿",
  });

  // Cor que representa a skin nas bolinhas: entre as cores que mudam de uma skin para outra
  // (capacete, gorro, cabelo...), a mais usada e mais viva.
  function skinColor(ch, s) {
    const changing = new Set();
    ch.skins.slice(1).forEach((x) => Object.keys(x.cores || {}).forEach((k) => changing.add(k)));
    const counts = {};
    s._sprites.andar[0].forEach((r) => [...r].forEach((c) => { if (c !== "." && c !== " ") counts[c] = (counts[c] || 0) + 1; }));
    const rgb = (hex) => { const n = parseInt(hex.slice(1), 16); return [n >> 16, (n >> 8) & 255, n & 255]; };
    const score = (k) => {
      const [r, g, b] = rgb(s._colors[k]), mx = Math.max(r, g, b), mn = Math.min(r, g, b);
      const lum = (0.3 * r + 0.59 * g + 0.11 * b) / 255;
      return lum < 0.12 ? 0 : (counts[k] || 0) * (0.25 + (mx - mn) / 255);
    };
    const keys = Object.keys(counts).filter((k) => s._colors[k] && (!changing.size || changing.has(k)));
    const best = (keys.length ? keys : Object.keys(counts)).sort((a, b) => score(b) - score(a))[0];
    return s._colors[best] || "#888";
  }

  const gallery = document.getElementById("gallery");
  const shows = [];
  CHARS.forEach((ch) => {
    prepare(ch);
    const card = document.createElement("article");
    card.className = "card";
    const effects = new Set([ch.efeito, ...Object.values(ch.efeitosTrabalho || {})].join("+").split("+").filter(Boolean));
    const nWork = new Set(ch.skins.flatMap((s) => Object.keys(s._sprites).filter((k) => k.startsWith("trabalhar")))).size;
    card.innerHTML = (ch.destaque ? `<span class='badge'>${t("frameByFrame", "✨ quadro a quadro")}</span>` : "") +
      "<div class='stagebox'></div>" +
      `<h3>${esc(ch.nome)}</h3><p>${esc(ch.descricao || "")}</p>` +
      `<div class='tags'>${[...effects].map((e) => `<span>${WORK_LABEL[e] || e}</span>`).join("")}` +
      (nWork > 1 ? `<span>${nWork} ${t("jobs", "trabalhos")}</span>` : "") + `<span>${ch.skins.length} ${t("colors", "cores")}</span></div>` +
      "<div class='skins'></div>" +
      `<a class='btn btn-small try' href='#demo'>${t("tryIt", "Ver no terminal")}</a>`;
    const canvas = document.createElement("canvas");
    canvas.className = "stage";
    card.querySelector(".stagebox").appendChild(canvas);
    const show = new Showcase(canvas, ch, 7);
    shows.push(show);
    const skins = card.querySelector(".skins");
    ch.skins.forEach((s, i) => {
      const b = document.createElement("button");
      b.type = "button"; b.title = s.nome; b.style.background = skinColor(ch, s);
      b.setAttribute("aria-label", s.nome); b.setAttribute("aria-pressed", i === 0 ? "true" : "false");
      b.addEventListener("click", () => {
        show.setSkin(s);
        skins.querySelectorAll("button").forEach((x) => x.setAttribute("aria-pressed", x === b ? "true" : "false"));
      });
      skins.appendChild(b);
    });
    card.querySelector(".try").addEventListener("click", () => selectChar(ch));
    gallery.appendChild(card);
  });
  let lastShow = performance.now(), galleryVisible = true;
  new IntersectionObserver((es) => es.forEach((e) => (galleryVisible = e.isIntersecting))).observe(gallery);
  (function tick(t) {
    const dt = Math.min(0.1, (t - lastShow) / 1000); lastShow = t;
    if (galleryVisible) shows.forEach((s) => s.step(dt));
    requestAnimationFrame(tick);
  })(lastShow);

  // ---------- desfile no rodapé: um de cada ----------

  const parade = document.querySelector(".parade-wrap");
  const paradeBase = document.getElementById("parade");
  ["oompa-loompas", "programadores", "dragao", "clawd", "gatos", "pinguins"].filter(byId).forEach((id, i) => {
    const c = i === 0 ? paradeBase : paradeBase.cloneNode();
    c.style.position = "absolute"; c.style.left = "0"; c.style.width = "100%"; c.style.height = "150px";
    if (i) parade.appendChild(c);
    const p = new Crew(c, { char: byId(id), unit: 4, count: 1, bubbles: i % 2 === 0, startInside: true });
    p.workers[0].delay = i * 0.2;
    watch(parade, p);
  });
  parade.style.position = "relative";

  // ---------- download e modal de apoio ----------

  const dl = document.getElementById("dlWindows");
  dl.href = `https://github.com/${REPO}/releases/latest/download/${SETUP}`;
  document.getElementById("repoLink").href = `https://github.com/${REPO}`;
  document.getElementById("repoLink2").href = `https://github.com/${REPO}`;

  // ---------- sons (feitos no SFX Forge) ----------

  const players = {};
  document.querySelectorAll("[data-sound]").forEach((b) => b.addEventListener("click", () => {
    const name = b.dataset.sound;
    const a = players[name] || (players[name] = new Audio(`sons/${name}.wav`));
    a.currentTime = 0;
    a.play().catch(() => { /* navegador sem áudio */ });
  }));

  const STORE = "cmdcrew:support-shown";
  const support = document.getElementById("support");
  let shown = false, supportCrew = null;
  const wasShown = () => { try { return localStorage.getItem(STORE) === "1"; } catch { return shown; } };
  const markShown = () => { shown = true; try { localStorage.setItem(STORE, "1"); } catch { /* sem armazenamento */ } };
  document.addEventListener("click", (e) => {
    const a = e.target.closest('a[href*="/releases/latest/download/"]');
    if (!a || shown || wasShown()) return;
    markShown();
    // não bloqueia o download: o navegador segue o link e o modal abre logo depois
    setTimeout(() => {
      support.showModal();
      if (!supportCrew) supportCrew = new Crew(document.getElementById("supportStage"), { char: byId("clawd"), unit: 4, count: 3, bubbles: false });
      else supportCrew.restart();
    }, 400);
  });
  document.getElementById("supportClose").addEventListener("click", () => support.close());
  document.getElementById("supportPack").addEventListener("click", () => support.close());
  support.addEventListener("click", (e) => { if (e.target === support) support.close(); });
})();
