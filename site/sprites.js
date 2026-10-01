// Motor de animação do site: lê os mesmos JSON do programa (window.PERSONAGENS, gerado por
// ferramentas/gerar_site.py) e faz a equipe andar, trabalhar, pular e falar num <canvas>.
// Espelha o que o OompaLoompas.exe faz em cima do terminal (src/Overlay.cs).
(function () {
  "use strict";

  const BLANK = new Set([".", " "]);

  // Objetos que eles carregam na cabeça (iguais aos de src/Data.cs).
  const PROPS = {
    chocolate: [["FFFPPPPP", "BLBLBPPP", "BBBBBPPP"], { F: "#d9d9d9", P: "#7b1fa2", B: "#5b3416", L: "#7d4a22" }],
    caixa: [["KKKKKKKK", "KCCTTCCK", "KCCTTCCK", "KCCCCCCK", "KKKKKKKK"], { K: "#6d4c2f", C: "#d2a86e", T: "#b98a4e" }],
    peixe: [["B.BBBB..", "BBBBBKBB", "B.BBBB.."], { B: "#64b5f6", K: "#0d47a1" }],
    acucar: [["WWWW", "WWWS", "WWWS", "WSSS"], { W: "#ffffff", S: "#cfd8dc" }],
    engrenagem: [["..G.G..", ".GGGGG.", "GGG.GGG", ".GGGGG.", "..G.G.."], { G: "#90a4ae" }],
    gema: [[".CCC.", "CCWCC", ".CCC.", "..C.."], { C: "#26c6da", W: "#e0f7fa" }],
    folha: [[".GGG.", "GGJGG", ".GGG."], { G: "#43a047", J: "#2e7d32" }],
    claude: [["...O...", ".O.O.O.", "..OOO..", "OOOCOOO", "..OOO..", ".O.O.O.", "...O..."], { O: "#d97757", C: "#b85f42" }],
    terminal: [["KKKKKKKKK", "KGKKKKKKK", "KKGKKKKKK", "KGKKGGGKK", "KKKKKKKKK"], { K: "#262624", G: "#f0eee6" }],
    cafe: [[".s..s..", "..s..s.", "RRRRR..", "RWRRRRR", "RWRRR.R", "RRRRRRR", ".RRR..."], { R: "#e53935", W: "#ffcdd2", s: "#cfd8dc" }],
    osso: [["WW....WW", "WWWWWWWW", "WW....WW"], { W: "#f3ead3" }],
    disquete: [["KKKKKK", "KSSSKK", "KSSSKK", "KKKKKK", "KWWWWK", "KWWWWK"], { K: "#1e3a8a", S: "#b0bec5", W: "#eceff1" }],
    bola: [[".YYY.", "WYYYY", "YWWYY", "YYYWW", ".YYY."], { Y: "#c6e03a", W: "#f5f5f5" }],
    pizza: [["OOOOOOO", ".YRYYY.", "..YYR..", "...Y..."], { O: "#c68642", Y: "#ffd54f", R: "#d32f2f" }],
    flor: [[".P.P.", "PPYPP", ".PPP.", "..G..", ".GG.."], { P: "#ec407a", Y: "#ffeb3b", G: "#43a047" }],
  };

  // ---------- dados ----------

  // Normaliza um personagem como o programa faz: skins com desenho próprio ou herdado ("base"),
  // todos os quadros com a mesma largura/altura, alinhados por baixo.
  function prepare(ch) {
    if (ch._ready) return ch;
    const byId = {};
    ch.skins.forEach((s) => (byId[s.id] = s));
    const first = ch.skins[0];
    ch.skins.forEach((s) => {
      let sprites = s.sprites || null, colors = Object.assign({}, s.cores || {});
      let efeito = s.efeito, carga = s.carga, b = s.base, seen = new Set([s.id]);
      while (b && byId[b] && !seen.has(b)) {
        const bs = byId[b]; seen.add(b);
        if (!sprites && bs.sprites) sprites = bs.sprites;
        for (const k in bs.cores || {}) if (!(k in colors)) colors[k] = bs.cores[k];
        if (efeito == null) efeito = bs.efeito;
        if (carga == null) carga = bs.carga;
        b = bs.base;
      }
      if (!s.base && s !== first) for (const k in first.cores || {}) if (!(k in colors)) colors[k] = first.cores[k];
      s._sprites = sprites || ch.sprites || ch.skins.find((x) => x.sprites).sprites;
      s._colors = colors;
      s._efeito = efeito != null ? efeito : ch.efeito;
      s._carga = carga != null ? carga : ch.carga;
    });
    let w = 0, h = 0;
    const sets = [...new Set(ch.skins.map((s) => s._sprites))];
    sets.forEach((set) => Object.values(set).forEach((fs) => fs.forEach((f) => {
      h = Math.max(h, f.length); f.forEach((r) => (w = Math.max(w, r.length)));
    })));
    let top = h, bottom = 0;
    sets.forEach((set) => {
      let t = h;
      for (const k in set) {
        set[k] = set[k].map((f) => {
          const rows = [];
          for (let y = 0; y < h; y++) {
            const r = y < h - f.length ? "" : f[y - (h - f.length)];
            rows.push(r.padEnd(w, "."));
            if ([...rows[y]].some((c) => !BLANK.has(c))) { t = Math.min(t, y); bottom = Math.max(bottom, y); }
          }
          return rows;
        });
      }
      set._top = t; top = Math.min(top, t);
    });
    ch._w = w; ch._h = h; ch._top = top; ch._bottom = bottom;
    ch._ready = true;
    return ch;
  }

  function workKinds(set) {
    const k = Object.keys(set).filter((x) => x === "trabalhar" || x.startsWith("trabalhar-"));
    return k.length ? k : ["andar"];
  }

  // Pixels de tela por ponto do desenho, como PixelScale no programa.
  function pixelScale(ch, unit) { return Math.max(1, Math.round(unit / (ch.detalhe || 1))); }

  function renderRows(rows, colors, scale, flip) {
    const w = rows[0].length, h = rows.length;
    const c = document.createElement("canvas");
    c.width = w * scale; c.height = h * scale;
    const g = c.getContext("2d");
    for (let y = 0; y < h; y++) {
      const r = rows[y];
      for (let x = 0; x < r.length; x++) {
        const ch = r[x];
        if (BLANK.has(ch)) continue;
        g.fillStyle = colors[ch] || "#888";
        g.fillRect((flip ? w - 1 - x : x) * scale, y * scale, scale, scale);
      }
    }
    return c;
  }

  const cache = new Map();
  function frames(ch, skin, kind, scale, flip) {
    prepare(ch);
    const key = ch.id + "|" + skin.id + "|" + kind + "|" + scale + "|" + flip;
    if (cache.has(key)) return cache.get(key);
    const set = skin._sprites;
    let list = set[kind];
    if (!list) list = kind === "parado" ? set.andar.slice(0, 1) : set.andar;
    const out = list.map((f) => renderRows(f, skin._colors, scale, flip));
    cache.set(key, out);
    return out;
  }

  function prop(name, scale, flip) {
    const p = PROPS[name];
    if (!p) return null;
    const key = "prop|" + name + "|" + scale + "|" + flip;
    if (!cache.has(key)) {
      const w = Math.max(...p[0].map((r) => r.length));
      cache.set(key, renderRows(p[0].map((r) => r.padEnd(w, ".")), p[1], scale, flip));
    }
    return cache.get(key);
  }

  const pick = (a) => a[Math.floor(Math.random() * a.length)];
  const R = (a, b) => a + Math.random() * (b - a);

  // ---------- efeitos ----------

  const FX = {
    faiscas: { sq: ["#ffd700", "#ffa500", "#ffffff", "#ff4500"], n: 7, from: "front", vx: [-110, 110], vy: [-190, -60], g: 420, life: [0.35, 0.7] },
    poeira: { sq: ["#beaa8c", "#96826e"], n: 4, from: "feet", vx: [-25, 25], vy: [-35, -10], g: 0, life: [0.4, 0.8] },
    terra: { sq: ["#79553a", "#5d402d", "#966e46"], n: 6, from: "front", vx: [-90, 40], vy: [-170, -80], g: 420, life: [0.4, 0.8] },
    neve: { sq: ["#ffffff", "#b4dcff"], n: 5, from: "mid", vx: [-30, 30], vy: [-60, -20], g: 60, life: [0.6, 1.1] },
    estrelas: { sq: ["#ffd700", "#fff096", "#ff69b4"], n: 3, from: "mid", vx: [-40, 40], vy: [-80, -30], g: 60, life: [0.5, 0.9] },
    magia: { txt: ["✦", "✧", "★", "✶"], col: ["#b388ff", "#64ffda", "#ffd700"], n: 2 },
    zzz: { txt: ["z", "z", "Z"], col: ["#5a6ec8"], n: 1, life: 2.2 },
    notas: { txt: ["♪", "♫"], col: ["#783ca0", "#28783c", "#c85028"], n: 1 },
    claude: { txt: ["✻", "✳", "✢", "✶", "✽"], col: ["#d97757", "#f5a37f", "#b85f42"], n: 1 },
    codigo: { txt: ["0", "1", "{ }", "</>", ";", "=>"], col: ["#00e676", "#69f0ae", "#2ea043"], n: 2 },
    dados: { txt: ["%", "Σ", "π", "▲", "μ"], col: ["#42a5f5", "#26c6da", "#ffca28"], n: 2 },
    fumaca: { sq: ["#787878", "#969696", "#5f5f5f"], n: 3, from: "head", vx: [-10, 10], vy: [-30, -16], g: 0, life: [1, 1.6], grow: 1.8 },
    moedas: { sq: ["#ffd700", "#ffd54f"], n: 2, from: "mid", vx: [-50, 50], vy: [-190, -120], g: 420, life: [0.6, 0.9], big: true },
    latidos: { txt: ["au!", "au au!", "auu!"], col: ["#ffb74d", "#fff176"], n: 1 },
    coracoes: { txt: ["♥"], col: ["#ec407a", "#f48fb1", "#e53935"], n: 1 },
    flores: { txt: ["✿", "❀", "✾"], col: ["#ec407a", "#ffeb3b", "#ab47bc", "#66bb6a"], n: 2, from: "front", rise: true },
    fogo: { sq: ["#ffeb3b", "#ff9800", "#f44336", "#fff3b0"], n: 12, from: "mouth", vx: [140, 240], vy: [-25, 10], g: -40, life: [0.25, 0.55], forward: true },
    flechas: { txt: ["➳"], col: ["#8d6e63"], n: 1, arrow: true },
  };

  // ---------- equipe ----------

  const WALK = 0, WORK = 1, IDLE = 2, JUMP = 3;

  class Crew {
    // opts: { unit, count, char, skin ("*" = misturar), bubbles, groundPad, speed }
    constructor(canvas, opts) {
      this.canvas = canvas;
      this.ctx = canvas.getContext("2d");
      this.opts = Object.assign({ unit: 3, count: 4, skin: "*", bubbles: true, groundPad: 0, speed: 1 }, opts);
      this.workers = []; this.parts = [];
      this.ending = -1; this.hidden = false; this.alpha = 1;
      this.nextBubble = 2.5;
      this.resize();
      this.setChar(this.opts.char);
      this.last = performance.now();
      this._loop = this._loop.bind(this);
      requestAnimationFrame(this._loop);
      window.addEventListener("resize", () => this.resize());
    }

    resize() {
      const r = this.canvas.getBoundingClientRect();
      this.dpr = window.devicePixelRatio || 1;
      this.W = r.width; this.H = r.height;
      this.canvas.width = Math.round(r.width * this.dpr);
      this.canvas.height = Math.round(r.height * this.dpr);
    }

    setChar(ch, skin) {
      this.ch = prepare(ch);
      if (skin) this.opts.skin = skin;
      this.px = pixelScale(this.ch, this.opts.unit);
      this.spriteW = this.ch._w * this.px;
      this.workers = []; this.parts = []; this.ending = -1; this.alpha = 1; this.hidden = false;
      const order = [...this.ch.skins].sort(() => Math.random() - 0.5);
      for (let i = 0; i < this.opts.count; i++) {
        const skin = this.opts.skin === "*" ? order[i % order.length] : (this.ch.skins.find((s) => s.id === this.opts.skin) || this.ch.skins[0]);
        const w = {
          skin, x: NaN, target: 0, timer: 0, ft: 0, frame: 0, dir: i % 2 ? -1 : 1, state: WALK,
          delay: i * 0.55 + Math.random() * 0.4, speedMul: R(0.8, 1.2), y: 0, vy: 0, squash: 0,
          meet: 2, bubble: null, bubbleT: 0, carrying: false, idleHold: 1, looked: false,
          works: workKinds(skin._sprites), work: null, effect: skin._efeito, phase: Math.random() * 6.28,
        };
        this.pickWork(w);
        this.workers.push(w);
      }
    }

    get ground() { return this.H - this.opts.groundPad; }
    spriteTop() { return this.ground - (this.ch._bottom + 1) * this.px; }
    headTop(w) { return this.spriteTop() + w.skin._sprites._top * this.px - w.y - this.fly(w); }
    // quem voa ("voo" no JSON) fica no alto, subindo e descendo devagar
    fly(w) {
      if (!this.ch.voo) return 0;
      return this.ch.voo * this.px + Math.round(Math.sin(performance.now() / 1000 * 3.2 + (w.phase || 0)) * this.opts.unit * 1.3);
    }
    gravity() { return 900 * this.opts.unit / 3; }

    pickWork(w) {
      w.work = pick(w.works);
      const fx = this.ch.efeitosTrabalho && this.ch.efeitosTrabalho[w.work];
      w.effect = fx || w.skin._efeito;
    }

    randomTarget() {
      const m = this.spriteW * 0.75;
      return m + Math.random() * Math.max(1, this.W - 2 * m);
    }

    launch(w, h) {
      if (w.y > 0) return;
      w.vy = Math.sqrt(2 * this.gravity() * h * this.opts.unit);
      w.y = 0.01;
    }

    decide(w) {
      const r = Math.random();
      w.frame = 0; w.ft = 0; w.looked = false;
      if (r < 0.46) {
        w.state = WORK; this.pickWork(w); w.timer = R(2.5, 6.5);
        if (w.carrying) { w.carrying = false; this.spawn("poeira", w, 3); }
      } else if (r < 0.8) {
        w.state = WALK; w.target = this.randomTarget(); w.carrying = !!PROPS[w.skin._carga] && Math.random() < 0.45;
      } else if (r < 0.88 && w.y <= 0 && !this.ch.voo) {
        w.state = JUMP; this.launch(w, R(5, 8));
      } else {
        w.state = IDLE; w.timer = R(0.9, 2.5); if (Math.random() < 0.5) w.dir = -w.dir;
      }
    }

    // Termina o trabalho: comemoram pulando, dizem tchau e somem.
    finish() {
      if (this.ending >= 0) return;
      this.ending = 0;
      const vis = this.workers.filter((w) => w.delay <= 0 && !isNaN(w.x));
      this.workers.forEach((w) => {
        if (!vis.includes(w)) { w.gone = true; return; }
        w.state = IDLE; w.timer = 999; w.carrying = false; w.frame = 0; w.cheer = Math.random() * 0.5; w.bubble = null;
      });
      const fim = this.ch.falasFim || [];
      if (this.opts.bubbles && fim.length)
        vis.sort(() => Math.random() - 0.5).slice(0, Math.min(3, Math.ceil(vis.length / 2))).forEach((w) => { w.bubble = pick(fim); w.bubbleT = 3; });
    }

    restart() { this.setChar(this.ch); }

    // Manda todo mundo embora andando para fora da cena; chama done() quando o último sair.
    leave(done) {
      this.leaving = done || (() => {});
      for (const w of this.workers) {
        if (w.gone || isNaN(w.x)) { w.gone = true; continue; }
        w.state = WALK; w.carrying = false; w.bubble = null; w.frame = 0;
        w.target = w.x < this.W / 2 ? -this.spriteW : this.W + this.spriteW;
        w.exiting = true;
      }
      if (this.workers.every((w) => w.gone)) { const cb = this.leaving; this.leaving = null; cb(); }
    }

    update(dt) {
      if (this.ending >= 0) {
        this.ending += dt;
        if (this.ending > 2.4) this.alpha = Math.max(0, 1 - (this.ending - 2.4) / 0.9);
        if (this.alpha <= 0) this.hidden = true;
      }
      const speed = 34 * this.opts.unit / 3 * this.opts.speed;
      for (const w of this.workers) {
        if (w.gone) continue;
        if (w.delay > 0) { w.delay -= dt; continue; }
        if (isNaN(w.x)) {
          // entram pelo canto (como no terminal) ou já começam dentro da cena
          w.x = this.opts.startInside ? this.randomTarget() : w.dir > 0 ? -this.spriteW : this.W + this.spriteW;
          w.target = this.randomTarget(); w.state = WALK;
        }
        if (w.bubbleT > 0 && (w.bubbleT -= dt) <= 0) w.bubble = null;
        if (w.squash > 0) w.squash -= dt;
        if (w.meet > 0) w.meet -= dt;
        w.ft += dt;
        if (w.y > 0) {
          w.vy -= this.gravity() * dt; w.y += w.vy * dt;
          if (w.y <= 0) { w.y = 0; w.vy = 0; w.squash = 0.12; this.spawn("poeira", w, 2); if (w.state === JUMP) this.decide(w); }
        }
        const walkN = this.ch.skins[0]._sprites.andar.length;
        switch (w.state) {
          case WALK: {
            const dx = w.target - w.x, sp = speed * w.speedMul;
            if (Math.abs(dx) <= sp * dt) {
              w.x = w.target;
              if (w.exiting) {
                w.gone = true;
                if (this.leaving && this.workers.every((x) => x.gone)) { const cb = this.leaving; this.leaving = null; cb(); }
              } else this.decide(w);
              break;
            }
            w.dir = dx > 0 ? 1 : -1; w.x += w.dir * sp * dt;
            const step = (walkN >= 8 ? 0.095 : walkN >= 4 ? 0.13 : 0.18) / this.opts.speed;
            if (w.ft > step) { w.ft = 0; w.frame++; }
            break;
          }
          case WORK: {
            w.timer -= dt;
            const n = w.skin._sprites[w.work] ? w.skin._sprites[w.work].length : 2;
            if (w.ft > (n >= 4 ? 0.11 : 0.3)) {
              w.ft = 0; w.frame = (w.frame + 1) % n;
              if (w.frame === n - 1) this.spawn(w.effect, w, 0);
            }
            if (w.timer <= 0) this.decide(w);
            break;
          }
          case JUMP: break;
          default:
            if (this.ending >= 0) {
              w.cheer -= dt;
              if (w.cheer <= 0 && w.y <= 0 && this.ending < 2) {
                this.launch(w, R(4, 7)); w.cheer = R(0.15, 0.5); if (Math.random() < 0.3) w.dir = -w.dir;
              }
              break;
            }
            w.timer -= dt;
            this.animateIdle(w);
            if (!w.looked && w.timer < 0.6 && Math.random() < 0.5) { w.dir = -w.dir; w.looked = true; }
            if (w.timer <= 0) this.decide(w);
        }
      }
      if (this.ending < 0) this.meetings();
      this.updateParts(dt);
      if (this.opts.bubbles && this.ending < 0 && (this.nextBubble -= dt) <= 0) {
        const falas = this.ch.falas || [];
        const cand = this.workers.filter((w) => !w.gone && w.delay <= 0 && !w.bubble && w.x > 40 && w.x < this.W - 40);
        if (cand.length && falas.length) { const w = pick(cand); w.bubble = pick(falas); w.bubbleT = 3.2; }
        this.nextBubble = R(4, 9);
      }
    }

    animateIdle(w) {
      const n = w.skin._sprites.parado ? w.skin._sprites.parado.length : 1;
      if (n < 2) { w.frame = 0; return; }
      if (w.ft < (w.frame === 0 ? w.idleHold : 0.09)) return;
      w.ft = 0; w.frame = (w.frame + 1) % n;
      if (w.frame === 0) w.idleHold = R(1.2, 3.4);
    }

    meetings() {
      const ws = this.workers;
      for (let i = 0; i < ws.length; i++) {
        const a = ws[i];
        if (a.state !== WALK || a.gone || a.delay > 0 || a.meet > 0) continue;
        for (let j = i + 1; j < ws.length; j++) {
          const b = ws[j];
          if (b.state !== WALK || b.gone || b.delay > 0 || b.meet > 0 || a.dir === b.dir) continue;
          const gap = b.x - a.x;
          if (Math.abs(gap) > this.spriteW * 0.9 || Math.abs(gap) < this.spriteW * 0.5 || (gap > 0) !== (a.dir > 0)) continue;
          a.meet = b.meet = 10;
          if (Math.random() > 0.45) continue;
          [a, b].forEach((w) => { w.state = IDLE; w.timer = R(1.3, 1.8); w.frame = 0; w.looked = true; });
          a.dir = gap > 0 ? 1 : -1; b.dir = -a.dir;
          this.launch(Math.random() < 0.5 ? a : b, 3);
          this.addText((a.x + b.x) / 2, this.headTop(a) - 6, R(-6, 6), -22, 1.3, Math.random() < 0.3 ? "!" : "♥", pick(["#ec407a", "#ffca28"]));
          break;
        }
      }
    }

    // ---------- partículas ----------

    spawn(effect, w, count) {
      if (!effect) return;
      if (effect.includes("+")) { effect.split("+").forEach((e) => this.spawn(e.trim(), w, count)); return; }
      const fx = FX[effect];
      if (!fx) return;
      const u = this.opts.unit;
      const frontX = w.x + w.dir * this.spriteW * 0.45;
      const headY = this.headTop(w), headX = w.x + w.dir * this.spriteW * 0.2;
      const midY = this.ground - (this.ch._bottom - w.skin._sprites._top) * this.px * 0.3 - this.fly(w);
      const n = count || fx.n;
      for (let i = 0; i < n; i++) {
        if (fx.arrow) {
          this.addText(frontX, midY - u * 3, w.dir * R(240, 300), R(-8, -2), 0.8, w.dir > 0 ? "➳" : "⟵", pick(fx.col));
        } else if (fx.txt) {
          const x = fx.rise ? frontX + R(-6, 6) : headX + R(-8, 8);
          const y = fx.rise ? midY : headY + R(0, 10);
          this.addText(x, y, (fx.rise ? R(-30, 30) : w.dir * R(4, 18)), (fx.rise ? R(-60, -30) : R(-34, -18)), fx.life || R(1.2, 1.7), pick(fx.txt), pick(fx.col));
        } else {
          const x = fx.from === "mouth" ? w.x + w.dir * this.spriteW * 0.5 : fx.from === "front" ? frontX : fx.from === "head" ? headX + R(-6, 6) : fx.from === "feet" ? w.x + R(-this.spriteW * 0.3, this.spriteW * 0.3) : frontX + R(-8, 8);
          const y = fx.from === "mouth" ? headY + R(2, 4) * u : fx.from === "head" ? headY + R(0, 8) : fx.from === "mid" ? midY : this.ground - u * 2;
          const dir = effect === "terra" ? -w.dir : fx.forward ? w.dir : 1;
          this.parts.push({ x, y, vx: R(fx.vx[0], fx.vx[1]) * dir, vy: R(fx.vy[0], fx.vy[1]), g: fx.g, life: R(fx.life[0], fx.life[1]),
            max: 1, color: pick(fx.sq), size: u * (fx.big ? 2 : 1 + (effect === "poeira" || effect === "terra" ? Math.floor(Math.random() * 2) : 0)), grow: fx.grow ? u * fx.grow : 0 });
          const p = this.parts[this.parts.length - 1]; p.max = p.life;
        }
      }
    }

    addText(x, y, vx, vy, life, text, color) {
      if (this.parts.length < 300) this.parts.push({ x, y, vx, vy, g: 0, life, max: life, text, color });
    }

    updateParts(dt) {
      for (let i = this.parts.length - 1; i >= 0; i--) {
        const p = this.parts[i];
        if ((p.life -= dt) <= 0) { this.parts.splice(i, 1); continue; }
        p.vy += p.g * dt; p.x += p.vx * dt; p.y += p.vy * dt;
        if (p.g > 0 && p.y > this.ground - p.size) { p.y = this.ground - p.size; p.vy *= -0.3; p.vx *= 0.6; }
      }
    }

    // ---------- desenho ----------

    draw() {
      const g = this.ctx, dpr = this.dpr;
      g.setTransform(dpr, 0, 0, dpr, 0, 0);
      g.clearRect(0, 0, this.W, this.H);
      if (this.hidden) return;
      g.globalAlpha = this.alpha;
      g.imageSmoothingEnabled = false;
      const top = this.spriteTop();
      for (const w of this.workers) {
        if (w.gone || w.delay > 0 || isNaN(w.x)) continue;
        const flip = w.dir < 0;
        const kind = w.state === WORK ? w.work : w.state === WALK ? "andar" : "parado";
        const set = frames(this.ch, w.skin, kind, this.px, flip);
        const f = w.state === JUMP || this.ending >= 0 ? set[0] : set[w.frame % set.length];
        const x = Math.round(w.x - f.width / 2), y = Math.round(top - w.y - this.fly(w));
        if (w.squash > 0) {
          const d = Math.max(this.opts.unit, Math.round(f.height / 10));
          g.drawImage(f, x - d / 2, y + d, f.width + d, f.height - d);
        } else g.drawImage(f, x, y);
        if (w.carrying && w.state === WALK) {
          const c = prop(w.skin._carga, this.opts.unit, flip);
          if (c) g.drawImage(c, Math.round(w.x - c.width / 2), this.headTop(w) - c.height - this.opts.unit + (w.frame % 2) * this.opts.unit);
        }
      }
      for (const p of this.parts) {
        g.globalAlpha = this.alpha * Math.min(1, p.life / 0.4);
        if (p.text) {
          g.fillStyle = p.color; g.font = `700 ${10 + this.opts.unit * 2}px "Segoe UI Symbol", system-ui, sans-serif`;
          g.fillText(p.text, p.x, p.y);
        } else {
          const s = p.size + (p.grow ? p.grow * (1 - p.life / p.max) : 0);
          g.fillStyle = p.color; g.fillRect(Math.round(p.x), Math.round(p.y), s, s);
        }
      }
      g.globalAlpha = this.alpha;
      for (const w of this.workers) if (w.bubble && !w.gone && !isNaN(w.x)) this.drawBubble(g, w);
      g.globalAlpha = 1;
    }

    drawBubble(g, w) {
      g.font = '600 13px "Nunito", system-ui, sans-serif';
      const tw = g.measureText(w.bubble).width;
      const bw = Math.ceil(tw) + 18, bh = 26;
      const headY = this.headTop(w) - (w.carrying ? 14 : 0);
      const by = Math.max(2, headY - bh - 10);
      const bx = Math.max(2, Math.min(this.W - bw - 2, Math.round(w.x - bw / 2)));
      g.fillStyle = "#2b1d14";
      g.fillRect(bx, by, bw, bh);
      g.fillStyle = "#fffaf0";
      g.fillRect(bx + 2, by + 2, bw - 4, bh - 4);
      const tx = Math.max(bx + 8, Math.min(bx + bw - 16, Math.round(w.x) - 4));
      g.fillStyle = "#2b1d14"; g.fillRect(tx, by + bh, 8, 3); g.fillRect(tx + 2, by + bh + 3, 4, 3);
      g.fillStyle = "#fffaf0"; g.fillRect(tx + 2, by + bh - 2, 4, 3);
      g.fillStyle = "#2b1d14"; g.fillText(w.bubble, bx + 9, by + 18);
    }

    _loop(t) {
      // Com poucos quadros por segundo (aba em segundo plano, máquina lenta), avança em passos
      // pequenos até alcançar o tempo real, sem deixar a física pular.
      let dt = Math.min(1, (t - this.last) / 1000);
      this.last = t;
      if (!this.paused) {
        while (dt > 0) { const s = Math.min(dt, 0.05); this.update(s); dt -= s; }
        this.draw();
      }
      requestAnimationFrame(this._loop);
    }
  }

  // Um personagem só, parado no lugar, alternando entre andar, trabalhar e descansar (cards da galeria).
  class Showcase {
    constructor(canvas, ch, unit) {
      this.canvas = canvas; this.ctx = canvas.getContext("2d");
      this.ch = prepare(ch); this.unit = unit;
      this.skin = ch.skins[0]; this.t = 0; this.phase = 0; this.frame = 0; this.ft = 0; this.parts = [];
      this.px = pixelScale(this.ch, unit);
      const dpr = window.devicePixelRatio || 1;
      this.W = this.ch._w * this.px + 40; this.H = (this.ch._bottom - this.ch._top + 1) * this.px + 30 + (ch.voo ? Math.min(ch.voo, 6) * this.px + 6 : 0);
      canvas.style.width = this.W + "px"; canvas.style.height = this.H + "px";
      canvas.width = Math.round(this.W * dpr); canvas.height = Math.round(this.H * dpr); this.dpr = dpr;
      this.kinds = ["andar", ...workKinds(this.skin._sprites), "parado"];
      this.kind = "andar"; this.hold = 2.4;
    }
    setSkin(s) { this.skin = s; this.kinds = ["andar", ...workKinds(s._sprites), "parado"]; }
    step(dt) {
      this.t += dt; this.ft += dt;
      if (this.t > this.hold) {
        this.t = 0; this.phase = (this.phase + 1) % this.kinds.length; this.kind = this.kinds[this.phase];
        this.frame = 0; this.hold = this.kind === "parado" ? 2.2 : this.kind === "andar" ? 2.4 : 2.8;
      }
      const set = this.skin._sprites[this.kind] || this.skin._sprites.andar;
      const n = set.length;
      const step = this.kind === "andar" ? (n >= 8 ? 0.095 : n >= 4 ? 0.13 : 0.18) : this.kind === "parado" ? (this.frame === 0 ? 1.4 : 0.09) : (n >= 4 ? 0.11 : 0.3);
      if (this.ft > step) { this.ft = 0; this.frame = (this.frame + 1) % n; }
      const g = this.ctx;
      g.setTransform(this.dpr, 0, 0, this.dpr, 0, 0); g.clearRect(0, 0, this.W, this.H); g.imageSmoothingEnabled = false;
      const f = frames(this.ch, this.skin, this.kind, this.px, false)[this.frame % n];
      const ground = this.H - 4;
      g.fillStyle = "rgba(43,29,20,.13)";
      g.beginPath(); g.ellipse(this.W / 2, ground - 1, this.ch._w * this.px * 0.32, 4, 0, 0, Math.PI * 2); g.fill();
      const fly = this.ch.voo ? Math.round(Math.min(this.ch.voo, 6) * this.px + Math.sin(performance.now() / 1000 * 3.2) * 3) : 0;
      g.drawImage(f, Math.round(this.W / 2 - f.width / 2), ground - (this.ch._bottom + 1) * this.px - fly);
    }
  }

  window.Sprites = { prepare, frames, pixelScale, Crew, Showcase };
})();
