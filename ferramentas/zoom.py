"""Prévia ampliada de quadros de um personagem: python zoom.py <id> <skin1,skin2> <tipo1,tipo2> <saida.png> [escala]"""
import json, os, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))


def render(cid, skin_ids, kinds, out, S=5):
    d = json.load(open(os.path.join(HERE, "..", "personagens", cid + ".json"), encoding="utf-8"))
    sk = {s["id"]: s for s in d["skins"]}
    first = d["skins"][0]
    tiles = []
    for sid in skin_ids:
        s = sk[sid]
        chain = [s]
        while "base" in chain[-1]: chain.append(sk[chain[-1]["base"]])
        cores = {} if "base" in s or s is first else dict(first.get("cores", {}))
        for x in reversed(chain): cores.update(x.get("cores", {}))
        sp = next((x["sprites"] for x in chain if "sprites" in x), d.get("sprites"))
        for k in kinds:
            for f in sp.get(k, []):
                im = Image.new("RGB", (len(f[0]) * S, len(f) * S), (40, 44, 52))
                for y, row in enumerate(f):
                    for x, ch in enumerate(row):
                        if ch != ".": im.paste(tuple(int(cores[ch][i:i + 2], 16) for i in (1, 3, 5)), (x * S, y * S, x * S + S, y * S + S))
                tiles.append(im)
    W = sum(t.size[0] + 6 for t in tiles); H = max(t.size[1] for t in tiles)
    o = Image.new("RGB", (W, H), (20, 20, 20)); x = 0
    for t in tiles: o.paste(t, (x, 0)); x += t.size[0] + 6
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    o.save(out)


if __name__ == "__main__":
    render(sys.argv[1], sys.argv[2].split(","), sys.argv[3].split(","), sys.argv[4], int(sys.argv[5]) if len(sys.argv) > 5 else 5)
