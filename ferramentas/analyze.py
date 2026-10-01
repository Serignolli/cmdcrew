import sys, numpy as np
from PIL import Image
from collections import deque
import os
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "")

def load(name):
    return np.asarray(Image.open(ROOT+name).convert("RGB")).astype(np.int32)

def bgmask(a, thr=228):
    # fundo = quase branco e pouco saturado, conectado à borda
    mx = a.max(2); mn = a.min(2)
    cand = (mn > thr) & (mx - mn < 30)
    h, w = cand.shape
    bg = np.zeros_like(cand)
    q = deque()
    for x in range(w):
        for y in (0, h-1):
            if cand[y,x] and not bg[y,x]: bg[y,x]=1; q.append((y,x))
    for y in range(h):
        for x in (0, w-1):
            if cand[y,x] and not bg[y,x]: bg[y,x]=1; q.append((y,x))
    while q:
        y,x = q.popleft()
        for dy,dx in ((1,0),(-1,0),(0,1),(0,-1)):
            ny,nx=y+dy,x+dx
            if 0<=ny<h and 0<=nx<w and cand[ny,nx] and not bg[ny,nx]:
                bg[ny,nx]=1; q.append((ny,nx))
    return bg.astype(bool)

def components(fg, gap=6, minarea=400):
    # caixas de componentes (com dilatação simples por gap)
    h,w=fg.shape
    from itertools import product
    d = fg.copy()
    for _ in range(gap):
        e = d.copy()
        e[1:]|=d[:-1]; e[:-1]|=d[1:]; e[:,1:]|=d[:,:-1]; e[:,:-1]|=d[:,1:]
        d=e
    lab = np.zeros((h,w),np.int32); n=0; boxes=[]
    for y in range(h):
        for x in range(w):
            if d[y,x] and not lab[y,x]:
                n+=1; q=deque([(y,x)]); lab[y,x]=n; y0=y1=y; x0=x1=x; cnt=0
                while q:
                    cy,cx=q.popleft(); cnt+=1
                    y0=min(y0,cy);y1=max(y1,cy);x0=min(x0,cx);x1=max(x1,cx)
                    for dy,dx in ((1,0),(-1,0),(0,1),(0,-1)):
                        ny,nx=cy+dy,cx+dx
                        if 0<=ny<h and 0<=nx<w and d[ny,nx] and not lab[ny,nx]:
                            lab[ny,nx]=n; q.append((ny,nx))
                if cnt>=minarea: boxes.append((x0,y0,x1+1,y1+1))
    return boxes

def pitch(a, box):
    x0,y0,x1,y1 = box
    sub = a[y0:y1, x0:x1]
    res=[]
    for axis in (1,0):
        d = np.abs(np.diff(sub, axis=axis)).sum(2).sum(1-axis if axis==1 else 1).astype(float) if False else None
        g = np.abs(np.diff(sub, axis=axis)).sum(2)
        sig = g.sum(0) if axis==1 else g.sum(1)
        sig = sig - sig.mean()
        best=None
        for p in np.arange(3.0, 16.0, 0.02):
            ph = np.arange(len(sig))
            ang = 2*np.pi*ph/p
            s = abs((sig*np.exp(1j*ang)).sum())/len(sig)
            if best is None or s>best[0]: best=(s,p)
        res.append(round(best[1],2))
    return res

if __name__=="__main__":
    for name in sys.argv[1:]:
        a=load(name); bg=bgmask(a)
        boxes=components(~bg)
        print(name, a.shape)
        for b in sorted(boxes, key=lambda b:(b[1]//150, b[0])):
            print("  ", b, "size", (b[2]-b[0], b[3]-b[1]), "pitch", pitch(a,b))
