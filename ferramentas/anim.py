"""Operações sobre grades de índices (-1 = transparente) para criar quadros de animação a partir de uma pose só."""
import numpy as np
from collections import deque

T = -1

def pad(g, top=0, bottom=0, left=0, right=0):
    h, w = g.shape
    out = np.full((h + top + bottom, w + left + right), T, int)
    out[top:top + h, left:left + w] = g
    return out

def flip(g): return g[:, ::-1].copy()

def runs(row):
    n = 0; prev = False
    for v in row:
        cur = v != T
        if cur and not prev: n += 1
        prev = cur
    return n

def leg_top(g, min_runs=2, max_frac=0.5):
    """Primeira linha (de baixo para cima) em que as pernas se juntam ao corpo."""
    h = g.shape[0]; y = h - 1
    while y > 0 and not (g[y] != T).any(): y -= 1
    bottom = y
    while y > h * (1 - max_frac) and runs(g[y]) >= min_runs: y -= 1
    return y + 1, bottom

def components(mask):
    h, w = mask.shape; lab = np.zeros((h, w), int); n = 0
    for y in range(h):
        for x in range(w):
            if mask[y, x] and not lab[y, x]:
                n += 1; q = deque([(y, x)]); lab[y, x] = n
                while q:
                    cy, cx = q.popleft()
                    for dy, dx in ((1,0),(-1,0),(0,1),(0,-1)):
                        ny, nx = cy + dy, cx + dx
                        if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not lab[ny, nx]:
                            lab[ny, nx] = n; q.append((ny, nx))
    return lab, n

def legs(g, top=None, bottom=None):
    """Separa as pernas: devolve (top, bottom, lista de máscaras ordenadas por x)."""
    if top is None: top, bottom = leg_top(g)
    if bottom is None: bottom = g.shape[0] - 1
    band = np.zeros(g.shape, bool); band[top:bottom + 1] = g[top:bottom + 1] != T
    lab, n = components(band)
    comps = []
    for i in range(1, n + 1):
        m = lab == i
        if m.sum() < 3: continue
        xs = np.where(m)[1]; comps.append((xs.mean(), m))
    comps.sort(key=lambda c: c[0])
    return top, bottom, [m for _, m in comps]

def paste_moved(dst, src, mask, fn):
    """Move os pixels de mask (de src) usando fn(y, x) -> (ny, nx)."""
    ys, xs = np.where(mask)
    vals = src[ys, xs]
    for y, x, v in zip(ys, xs, vals):
        ny, nx = fn(y, x)
        if 0 <= ny < dst.shape[0] and 0 <= nx < dst.shape[1]: dst[ny, nx] = v

def swing(g, masks_amt, top, bottom, lift=None):
    """Cisalha cada perna: o topo fica preso, o pé anda amt pontos (e sobe lift[i] pontos)."""
    out = g.copy()
    allm = np.zeros(g.shape, bool)
    for m, _ in masks_amt: allm |= m
    out[allm] = T
    for i, (m, amt) in enumerate(masks_amt):
        up = lift[i] if lift else 0
        span = max(1, bottom - top)
        def fn(y, x, amt=amt, up=up):
            t = (y - top) / span
            return y - int(round(up * t)), x + int(round(amt * t))
        paste_moved(out, g, m, fn)
    return out

def shift(g, dy=0, dx=0, rows=None):
    """Move a figura inteira (ou só as linhas rows=(a,b)) sem cortar (a grade precisa ter margem)."""
    out = np.full(g.shape, T, int) if rows is None else g.copy()
    h, w = g.shape
    a, b = (0, h) if rows is None else rows
    if rows is not None: out[a:b] = T
    for y in range(a, b):
        ny = y + dy
        if not (0 <= ny < h): continue
        row = g[y]
        for x in range(w):
            if row[x] != T and 0 <= x + dx < w: out[ny, x + dx] = row[x]
    return out

def move_rect(g, rect, dy=0, dx=0, fill=None):
    """Recorta rect=(x0,y0,x1,y1) e cola deslocado. Buraco deixado vira transparente (ou fill)."""
    x0, y0, x1, y1 = rect
    out = g.copy()
    piece = g[y0:y1, x0:x1].copy()
    out[y0:y1, x0:x1] = T if fill is None else fill
    for yy in range(piece.shape[0]):
        for xx in range(piece.shape[1]):
            v = piece[yy, xx]
            if v == T: continue
            ny, nx = y0 + yy + dy, x0 + xx + dx
            if 0 <= ny < g.shape[0] and 0 <= nx < g.shape[1]: out[ny, nx] = v
    return out

def shear(g, amt, pivot=None):
    """Inclina: a linha de baixo fica, o topo anda amt pontos (gingado de pinguim)."""
    h, w = g.shape
    bottom = pivot if pivot is not None else h - 1
    out = np.full(g.shape, T, int)
    for y in range(h):
        d = int(round(amt * (bottom - y) / max(1, bottom)))
        for x in range(w):
            if g[y, x] != T and 0 <= x + d < w: out[y, x + d] = g[y, x]
    return out

def tilt(g, amt, pivot_x=None):
    """Levanta a frente (colunas à direita de pivot_x sobem até amt pontos): latido/empinada."""
    h, w = g.shape
    px = pivot_x if pivot_x is not None else w // 2
    out = np.full(g.shape, T, int)
    for x in range(w):
        d = 0 if x <= px else int(round(amt * (x - px) / max(1, w - 1 - px)))
        for y in range(h):
            if g[y, x] != T and 0 <= y - d < h: out[y - d, x] = g[y, x]
    return out

def squash_top(g, rows, at):
    """Abaixa tudo acima da linha `at` em `rows` linhas (cabeça/tronco descem, pernas ficam)."""
    out = g.copy()
    h = g.shape[0]
    out[:at] = T
    for y in range(at):
        ny = y + rows
        if ny < h:
            m = g[y] != T
            if ny < at: out[ny][m] = g[y][m]
            else:
                row = out[ny].copy(); row[m] = g[y][m]; out[ny] = row
    # remove a linha duplicada: as linhas de 'at' a 'at+rows' ficam por cima das originais
    return out

def recolor(g, rect, mapping):
    out = g.copy(); x0, y0, x1, y1 = rect
    sub = out[y0:y1, x0:x1]
    for a, b in mapping.items(): sub[sub == a] = b
    return out

def replace_in(g, rect, src_rect_grid):
    out = g.copy(); x0, y0, x1, y1 = rect
    out[y0:y1, x0:x1] = src_rect_grid
    return out

def bbox(g):
    ys, xs = np.where(g != T)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
