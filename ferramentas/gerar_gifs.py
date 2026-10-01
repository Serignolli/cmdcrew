"""Gera as imagens animadas do site: as salas (capturadas do próprio programa) e os personagens do Supporters Pack.

    python ferramentas/gerar_gifs.py

Os personagens HD não vão para o site como dados (site/dados.js só tem os gratuitos): aparecem só como GIF,
para o pacote não ficar disponível no repositório público. Precisa do bin/CmdCrew.exe compilado e da pasta
supporters-pack/ (que não vai para o git).
"""
import glob, json, os, shutil, subprocess, tempfile
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, ".."))
EXE = os.path.join(ROOT, "bin", "CmdCrew.exe")
PACK = os.path.join(ROOT, "supporters-pack", "personagens")
OUT = os.path.join(ROOT, "site", "img")

# salas: (arquivo, tema, estado, segundos antes de começar, quadros)
SALAS = [
    ("sala-escritorio", "escritorio", "trabalhando", 2.0, 40),
    ("sala-permissao", "escritorio", "permissao", 2.0, 24),
    ("sala-pronto", "escritorio", "pronto", 1.3, 30),
    ("sala-mina", "mina", "trabalhando", 2.0, 40),
]

# personagens do pack: as animações que entram no GIF, em ordem
ANIMS = ["andar", "parado", "trabalhar", "trabalhar-carrinho", "trabalhar-saco", "trabalhar-cavar", "trabalhar-rastelar"]


def save_gif(frames, path, ms):
    # paleta única para todos os quadros (sem tremedeira de cor entre eles)
    big = Image.new("RGBA", (frames[0].width, frames[0].height * len(frames)))
    for i, f in enumerate(frames): big.paste(f, (0, i * f.height))
    pal = big.convert("RGB").quantize(colors=255, method=Image.Quantize.MEDIANCUT)
    out = []
    for f in frames:
        q = f.convert("RGB").quantize(palette=pal, dither=Image.Dither.NONE)
        if f.mode == "RGBA":
            # pixel transparente vira o índice 255
            alpha = f.getchannel("A").point(lambda a: 255 if a < 128 else 0)
            q.paste(255, mask=alpha)
        out.append(q)
    kw = dict(save_all=True, append_images=out[1:], duration=ms, loop=0, disposal=2, optimize=False)
    if frames[0].mode == "RGBA": kw["transparency"] = 255
    out[0].save(path, **kw)
    print(os.path.relpath(path, ROOT), len(frames), "quadros,", os.path.getsize(path) // 1024, "KB")


def salas():
    tmp = tempfile.mkdtemp()
    for name, tema, estado, secs, n in SALAS:
        base = os.path.join(tmp, name + ".png")
        subprocess.run([EXE, "shot", tema, estado, base, str(secs), str(n)], check=True)
        files = sorted(glob.glob(os.path.join(tmp, name + "-*.png")))
        save_gif([Image.open(f).convert("RGB") for f in files], os.path.join(OUT, name + ".gif"), 100)
    shutil.rmtree(tmp, ignore_errors=True)


def hexrgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


def personagem(path):
    d = json.load(open(path, encoding="utf-8"))
    skin = d["skins"][0]
    sprites = skin.get("sprites") or d.get("sprites")
    cores = {k: hexrgb(v) for k, v in skin["cores"].items()}
    w = max(len(r) for fs in sprites.values() for f in fs for r in f)
    h = max(len(f) for fs in sprites.values() for f in fs)
    scale = 2 if d.get("detalhe", 1) > 1 else 6
    frames = []
    for anim in ANIMS:
        for _ in range(2 if anim == "andar" else 1):
            for f in sprites.get(anim, []):
                img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
                pad = h - len(f)
                for y, row in enumerate(f):
                    for x, c in enumerate(row):
                        if c not in ". ": img.putpixel((x, y + pad), cores.get(c, (128, 128, 128, 255)))
                frames.append(img.resize((w * scale, h * scale), Image.NEAREST))
    save_gif(frames, os.path.join(OUT, "pack-" + d["id"] + ".gif"), 110)


def main():
    os.makedirs(OUT, exist_ok=True)
    salas()
    for f in sorted(glob.glob(os.path.join(PACK, "*.json"))): personagem(f)


if __name__ == "__main__":
    main()
