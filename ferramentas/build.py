"""Base comum para montar personagens: quadros, variações de cor e gravação do JSON."""
import json, colorsys, sys, os
import numpy as np

from pix import palette, index, to_rgba, show
from anim import *

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "personagens", "")
FREE = OUT   # personagens gratuitos (as frases dos HD vêm das versões simples, que ficam aqui)
PREV = os.path.join(os.path.dirname(os.path.abspath(__file__)), "previa", "")
LETTERS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789#$%&*+=?@!()-/:;<>[]^_{|}~"


# ---------------- quadros ----------------

def base(rgba_list, maxk=30):
    pal = palette(rgba_list, maxk)
    return pal, [pad(index(r, pal), 3, 0, 3, 3) for r in rgba_list]


def walk_side(g, amp=2):
    top, bot = leg_top(g)
    top, bot, ms = legs(g, top, bot)
    if len(ms) < 2: return [g, shift(g, -1)]
    A = swing(g, [(m, amp if i % 2 == 0 else -amp) for i, m in enumerate(ms)], top, bot, [1 if i % 2 == 0 else 0 for i in range(len(ms))])
    B = swing(g, [(m, -amp if i % 2 == 0 else amp) for i, m in enumerate(ms)], top, bot, [0 if i % 2 == 0 else 1 for i in range(len(ms))])
    up = shift(g, -1)
    return [A, up, B, up]


def walk_front(g, lift=2, sway=1, legs_ok=True):
    top, bot = leg_top(g, max_frac=0.3)
    top, bot, ms = legs(g, top, bot)
    up = shift(g, -1)
    if legs_ok and len(ms) >= 2 and bot - top >= 3:
        cx = np.mean([np.where(m)[1].mean() for m in ms])
        side = [np.where(m)[1].mean() < cx for m in ms]
        A = swing(g, [(m, 0) for m in ms], top, bot, [lift if s else 0 for s in side])
        B = swing(g, [(m, 0) for m in ms], top, bot, [0 if s else lift for s in side])
        if sway: A, B = shear(A, sway), shear(B, -sway)
        return [A, up, B, up]
    return [shear(g, sway + 1), up, shear(g, -sway - 1), up]


def frac_rect(g, fx0, fy0, fx1, fy1):
    x0, y0, x1, y1 = bbox(g)
    w, h = x1 - x0, y1 - y0
    return (x0 + int(w * fx0), y0 + int(h * fy0), x0 + int(round(w * fx1)), y0 + int(round(h * fy1)))


def work_rect(g, fr, dy=-2, dx=0):
    return [g, move_rect(g, frac_rect(g, *fr), dy, dx)]


def work_nod(g, frac=0.5):
    x0, y0, x1, y1 = bbox(g)
    return [g, shift(g, 1, rows=(0, y0 + int((y1 - y0) * frac)))]


def work_hop(g, n=2): return [g, shift(g, -n)]


def work_tilt(g, amt=3, pivot=0.45):
    x0, y0, x1, y1 = bbox(g)
    return [g, tilt(g, amt, x0 + int((x1 - x0) * pivot))]


def close_eyes(g, rects, dark):
    out = g.copy()
    for (x0, y0, x1, y1) in rects:
        for x in range(x0, x1):
            fur = g[y0 - 1, x]
            out[y0:y1, x] = fur
        yl = y1 - 2
        for x in range(x0, x1):
            out[yl - (1 if x in (x0, x1 - 1) else 0), x] = dark
    return out


# ---------------- cores ----------------

def hexc(c): return "#%02x%02x%02x" % tuple(int(max(0, min(255, v))) for v in c)


def hls(c):
    return colorsys.rgb_to_hls(*[v / 255 for v in c])


def recolor_pal(pal, pred, fn):
    """Devolve {índice: nova cor} para as cores de pal que satisfazem pred(h,l,s)."""
    out = {}
    for i, c in enumerate(pal):
        h, l, s = hls(c)
        if pred(h, l, s):
            nh, nl, ns = fn(h, l, s)
            out[i] = tuple(int(round(v * 255)) for v in colorsys.hls_to_rgb(nh % 1, max(0, min(1, nl)), max(0, min(1, ns))))
    return out


def hue_between(a, b):
    a /= 360; b /= 360
    return lambda h, l, s: s > 0.18 and (a <= h <= b if a <= b else (h >= a or h <= b))


# ---------------- saída ----------------

class Char:
    def __init__(self, id, **meta):
        self.id = id; self.meta = meta; self.skins = []

    def skin(self, id, nome, pal, frames, efeito=None, carga=None):
        self.skins.append(dict(id=id, nome=nome, pal=pal, frames=frames, efeito=efeito, carga=carga))

    def variant(self, id, nome, of, changes, efeito=None, carga=None):
        self.skins.append(dict(id=id, nome=nome, base=of, changes=changes, efeito=efeito, carga=carga))

    def write(self):
        own = [s for s in self.skins if "frames" in s]
        H = max(f.shape[0] for s in own for fs in s["frames"].values() for f in fs)
        W = max(f.shape[1] for s in own for fs in s["frames"].values() for f in fs)
        d = dict(id=self.id)
        for k in ("nome", "categoria", "descricao", "detalhe", "efeito", "carga"): d[k] = self.meta[k]
        skins_json = []
        byid = {s["id"]: s for s in self.skins}
        for s in self.skins:
            sj = {"id": s["id"], "nome": s["nome"]}
            if "base" in s:
                sj["base"] = s["base"]
                sj["cores"] = {LETTERS[i]: hexc(c) for i, c in s["changes"].items()}
            else:
                pal = s["pal"]
                assert len(pal) <= len(LETTERS)
                sj["cores"] = {LETTERS[i]: hexc(c) for i, c in enumerate(pal)}
                sp = {}
                for kind, fs in s["frames"].items():
                    rows = []
                    for f in fs:
                        # centraliza na largura comum e alinha por baixo
                        h, w = f.shape
                        left = (W - w) // 2
                        g = pad(f, H - h, 0, left, W - w - left)
                        rows.append(["".join("." if v < 0 else LETTERS[v] for v in r) for r in g])
                    sp[kind] = rows
                sj["sprites"] = sp
            if s.get("efeito"): sj["efeito"] = s["efeito"]
            if s.get("carga"): sj["carga"] = s["carga"]
            skins_json.append(sj)
        d["skins"] = skins_json
        for k in ("verbos", "dicas", "falas", "falasFim"): d[k] = self.meta[k]
        text = dump(d)
        open(OUT + self.id + ".json", "w", encoding="utf-8").write(text)
        # prévia
        imgs = []
        for s in own:
            for kind in ("andar", "trabalhar", "parado"):
                for f in s["frames"].get(kind, []): imgs.append(to_rgba(f, s["pal"]))
        os.makedirs(PREV, exist_ok=True)
        show(imgs, PREV + self.id + ".png", 2)
        print(self.id, "skins", len(self.skins), "size", W, H, "kb", len(text) // 1024)


def dump(d):
    """JSON no mesmo estilo dos arquivos existentes: cada linha de desenho numa linha."""
    def enc(v, ind):
        sp = "  " * ind
        if isinstance(v, dict):
            items = [sp + "  " + json.dumps(k, ensure_ascii=False) + ": " + enc(x, ind + 1) for k, x in v.items()]
            return "{\n" + ",\n".join(items) + "\n" + sp + "}"
        if isinstance(v, list):
            if v and all(isinstance(x, str) for x in v) and len(v) > 0 and len(v[0]) > 0 and set(v[0]) <= set(LETTERS + "."):
                return "[\n" + ",\n".join(sp + "  " + json.dumps(x) for x in v) + "\n" + sp + "]"
            if all(isinstance(x, (str, int, float)) for x in v):
                return json.dumps(v, ensure_ascii=False)
            return "[\n" + ",\n".join(sp + "  " + enc(x, ind + 1) for x in v) + "\n" + sp + "]"
        return json.dumps(v, ensure_ascii=False)
    return enc(d, 0) + "\n"


def phrases_of(simple_id):
    d = json.load(open(FREE + simple_id + ".json", encoding="utf-8"))
    return {k: d.get(k, []) for k in ("verbos", "dicas", "falas", "falasFim")}


def darkest(pal):
    return int(np.argmin([sum(c) for c in pal]))
