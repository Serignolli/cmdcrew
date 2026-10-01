"""Converte pixel art gerada por IA (com grade imperfeita, JPEG) em grades de índices de paleta."""
import numpy as np
from PIL import Image
from analyze import load, bgmask, ROOT

_cache = {}
def source(name):
    if name not in _cache:
        a = load(name); _cache[name] = (a, bgmask(a))
    return _cache[name]

def phase(sig, p):
    ang = 2*np.pi*np.arange(len(sig))/p
    z = ((sig - sig.mean())*np.exp(1j*ang)).sum()
    # as bordas (picos de diferença) ficam em x ≡ ph (mod p)
    return (np.angle(z) / (2*np.pi) * p) % p

def grab(name, box, p, extra_bg=None):
    """Amostra a região box=(x0,y0,x1,y1) numa grade de passo p. Retorna RGBA (h,w,4) uint8."""
    a, bg = source(name)
    x0,y0,x1,y1 = box
    sub = a[y0:y1, x0:x1]; sbg = bg[y0:y1, x0:x1].copy()
    if extra_bg is not None: sbg |= extra_bg(sub)
    gx = np.abs(np.diff(sub, axis=1)).sum(2).sum(0).astype(float)
    gy = np.abs(np.diff(sub, axis=0)).sum(2).sum(1).astype(float)
    px = phase(gx, p) + 0.5; py = phase(gy, p) + 0.5
    xs = np.arange(px - p*np.ceil(px/p), x1-x0, p); ys = np.arange(py - p*np.ceil(py/p), y1-y0, p)
    H, W = len(ys), len(xs)
    out = np.zeros((H, W, 4), np.uint8)
    for j, yy in enumerate(ys):
        for i, xx in enumerate(xs):
            cy0, cy1 = int(round(yy + p*0.25)), int(round(yy + p*0.75))
            cx0, cx1 = int(round(xx + p*0.25)), int(round(xx + p*0.75))
            cy0, cx0 = max(cy0,0), max(cx0,0)
            cy1, cx1 = min(max(cy1,cy0+1), y1-y0), min(max(cx1,cx0+1), x1-x0)
            if cy0 >= cy1 or cx0 >= cx1: continue
            m = sbg[cy0:cy1, cx0:cx1]
            if m.mean() > 0.5: continue
            pix = sub[cy0:cy1, cx0:cx1][~m]
            out[j, i, :3] = np.median(pix, 0)
            out[j, i, 3] = 255
    return trim(out)

def trim(rgba):
    al = rgba[..., 3] > 0
    if not al.any(): return rgba
    ys = np.where(al.any(1))[0]; xs = np.where(al.any(0))[0]
    return rgba[ys[0]:ys[-1]+1, xs[0]:xs[-1]+1]

def remove_islands(rgba, minsize=3):
    """Apaga pontinhos soltos (restos do fundo)."""
    from collections import deque
    al = rgba[..., 3] > 0; h, w = al.shape; seen = np.zeros_like(al)
    for y in range(h):
        for x in range(w):
            if al[y,x] and not seen[y,x]:
                comp=[]; q=deque([(y,x)]); seen[y,x]=1
                while q:
                    cy,cx=q.popleft(); comp.append((cy,cx))
                    for dy in (-1,0,1):
                        for dx in (-1,0,1):
                            ny,nx=cy+dy,cx+dx
                            if 0<=ny<h and 0<=nx<w and al[ny,nx] and not seen[ny,nx]:
                                seen[ny,nx]=1; q.append((ny,nx))
                if len(comp) < minsize:
                    for cy,cx in comp: rgba[cy,cx,3]=0
    return trim(rgba)

def lab(c):
    c = np.asarray(c, float)/255.0
    c = np.where(c > 0.04045, ((c+0.055)/1.055)**2.4, c/12.92)
    M = np.array([[0.4124,0.3576,0.1805],[0.2126,0.7152,0.0722],[0.0193,0.1192,0.9505]])
    xyz = c @ M.T / np.array([0.9505, 1.0, 1.089])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787*xyz + 16/116)
    return np.stack([116*f[...,1]-16, 500*(f[...,0]-f[...,1]), 200*(f[...,1]-f[...,2])], -1)

def palette(sprites, maxk=24, mindist=9.0):
    """Paleta comum para uma lista de RGBA: junta cores próximas (em Lab) até sobrar maxk ou todas distarem > mindist."""
    cols = {}
    for s in sprites:
        for c in s[s[...,3] > 0][:, :3]:
            t = tuple(int(v) for v in c); cols[t] = cols.get(t, 0) + 1
    keys = list(cols); cl = [np.array(k, float) for k in keys]; w = [cols[k] for k in keys]
    if len(cl) > 120:
        # pré-agrupa (k-means ponderado em Lab) para a junção fina ficar rápida
        X = np.array(cl); Wt = np.array(w, float); XL = lab(X)
        rng = np.random.default_rng(0)
        C = XL[rng.choice(len(XL), 120, replace=False, p=Wt / Wt.sum())]
        for _ in range(12):
            a = ((XL[:, None] - C[None]) ** 2).sum(-1).argmin(1)
            for k in range(len(C)):
                m = a == k
                if m.any(): C[k] = (XL[m] * Wt[m, None]).sum(0) / Wt[m].sum()
        cl, w = [], []
        for k in range(len(C)):
            m = a == k
            if m.any():
                cl.append((X[m] * Wt[m, None]).sum(0) / Wt[m].sum()); w.append(int(Wt[m].sum()))
    L = [lab(c) for c in cl]
    import heapq
    while True:
        n = len(cl)
        if n <= 1: break
        A = np.array(L)
        D = np.sqrt(((A[:,None,:]-A[None,:,:])**2).sum(-1)); np.fill_diagonal(D, 1e9)
        # custo: distância ponderada pelo tamanho (cores raras e distintas sobrevivem)
        Wv = np.array(w, float)
        cost = D * np.sqrt(np.minimum(Wv[:,None], Wv[None,:]))
        if n > maxk * 6:
            i, j = np.unravel_index(np.argmin(D), D.shape)
        else:
            i, j = np.unravel_index(np.argmin(cost), cost.shape)
            if n <= maxk and D[i,j] > mindist: break
        tot = w[i] + w[j]
        cl[i] = (cl[i]*w[i] + cl[j]*w[j]) / tot; w[i] = tot; L[i] = lab(cl[i])
        del cl[j]; del w[j]; del L[j]
    order = np.argsort([-x for x in w])
    return [tuple(int(round(v)) for v in cl[k]) for k in order]

def index(rgba, pal):
    P = lab(np.array(pal, float)); out = np.full(rgba.shape[:2], -1, int)
    al = rgba[..., 3] > 0
    L = lab(rgba[..., :3].astype(float))
    d = ((L[..., None, :] - P[None, None])**2).sum(-1)
    out[al] = d.argmin(-1)[al]
    return out

def to_rgba(idx, pal):
    h, w = idx.shape; out = np.zeros((h, w, 4), np.uint8)
    for y in range(h):
        for x in range(w):
            if idx[y,x] >= 0: out[y,x,:3] = pal[idx[y,x]]; out[y,x,3] = 255
    return out

def show(rgbas, path, scale=4, bg=(40, 44, 52)):
    W = sum(r.shape[1]*scale + 8 for r in rgbas) + 8; H = max(r.shape[0] for r in rgbas)*scale + 16
    im = Image.new("RGB", (W, H), bg); x = 8
    for r in rgbas:
        t = Image.fromarray(r, "RGBA").resize((r.shape[1]*scale, r.shape[0]*scale), Image.NEAREST)
        im.paste(t, (x, H - 8 - t.size[1]), t); x += t.size[0] + 8
    im.save(path)
