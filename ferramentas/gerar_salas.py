"""Gera salas/*.json: uma sala para cada personagem.

    python ferramentas/gerar_salas.py

Cada sala é desenhada em "pontos" numa cena de 96×50 (o programa transforma cada ponto em U×U pixels).
O JSON só tem retângulos e algumas animações prontas, interpretadas igualzinho pelo programa do Windows
(src/Rooms.cs) e pelo de macOS/Linux (go/sala.go):

  imagem          opcional: arte de fundo em PNG (em salas/, esticada na cena); os retângulos do fundo vão por cima
  fundo / frente  lista de retângulos ["#cor", x, y, l, a]; o fundo vem antes do personagem e a frente depois
  lugares         onde o personagem trabalha (x do centro, animação, para que lado olha, quanto tempo fica)
  animacoes       cada uma com "tipo" e, opcional, "quando" (estados em que aparece:
                  trabalhando, esperando, pronto, erro, parado)
    piscar      pontos que acendem e apagam (estrelas, vaga-lumes, LEDs)
    brilho      cintilar branco em pedras preciosas
    tela        monitor: código subindo, visto verde no fim, X vermelho no erro, "!" esperando
    fumaca      fumacinha subindo (caneca, chaminé)
    luz         brilho redondo tremendo (lamparina, tocha); muda de cor no fim e no erro
    estado      retângulo com a cor do estado (lâmpada, painel)
    quadros     alterna listas de retângulos (fogo, esteira, bandeira)
    particulas  pontinhos andando sem parar dentro de uma área (neve, bolhas, brasas)
"""
import json, os, random, re

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "salas")
W, H = 96, 50   # tamanho da cena (RoomTheme.SceneW × SceneH)


class Sala:
    def __init__(self, id, nome, descricao, personagens, cor, chao, altura, skins=None):
        self.d = dict(id=id, nome=nome, descricao=descricao, personagens=personagens, cor=cor,
                      chao=chao, altura=altura, skins=skins or {}, lugares=[], fundo=[], animacoes=[], frente=[])

    def r(self, c, x, y, w, h): self.d["fundo"].append([c, x, y, w, h])
    def f(self, c, x, y, w, h): self.d["frente"].append([c, x, y, w, h])

    def box(self, c, x, y, w, h, frente=False):
        """retângulo com os cantos comidos"""
        add = self.f if frente else self.r
        add(c, x + 1, y, w - 2, h); add(c, x, y + 1, w, h - 2)

    def lugar(self, x, tipo="trabalhar", andar="andar", direita=True, min=4, max=8):
        self.d["lugares"].append(dict(x=x, tipo=tipo, andar=andar, direita=direita, min=min, max=max))

    def arte(self, imagem, chao=None):
        """usa a arte do PixelLab (salas/<imagem>, 192×100) no lugar dos retângulos. Os retângulos continuam no
        código: renderizados em 2× eles são o rascunho (init_image) que o PixelLab detalha."""
        d = {}
        for k, v in self.d.items():
            d[k] = v
            if k == "cor": d["imagem"] = imagem
        d["fundo"] = []
        if chao is not None: d["chao"] = chao
        self.d = d

    def a(self, tipo, **kw):
        kw["tipo"] = tipo
        self.d["animacoes"].append(kw)

    def salvar(self):
        os.makedirs(OUT, exist_ok=True)
        path = os.path.join(OUT, self.d["id"] + ".json")
        text = json.dumps(self.d, ensure_ascii=False, indent=1)
        text = _compact(text)
        open(path, "w", encoding="utf-8", newline="\n").write(text + "\n")
        return path


def _compact(text):
    """deixa numa linha só as listas que só têm números e textos curtos (os retângulos)"""
    flat = r"(?:\"[^\"\n]*\"|-?\d+(?:\.\d+)?)"
    pattern = re.compile(r"\[\s*(" + flat + r"(?:,\s*" + flat + r")*)\s*\]")
    return pattern.sub(lambda m: "[" + re.sub(r",\s+", ", ", m.group(1)) + "]", text)


# ---------------------------------------------------------------- Escritório
def escritorio():
    s = Sala("escritorio", "Escritório do dev", "O dev digita sem parar, o monitor enche de código e a caneca solta fumaça.",
             ["devs-hd", "programadores"], "#7c6cf0", 41, 22, {"programadores": ["cansado", "cansada", "gamer", "hacker"]})
    s.lugar(40, "trabalhar", min=5, max=10)
    s.lugar(60, "trabalhar-digitar", min=4, max=8)   # quem não tem notebook faz o "trabalhar" de sempre
    s.lugar(82, "parado", min=2.5, max=4)
    s.r("#3d3657", 0, 0, W, 40)
    for x in range(2, W, 6): s.r("#433c60", x, 0, 2, 38)
    s.r("#2a2440", 0, 38, W, 2)
    s.r("#7a5236", 0, 40, W, H - 40)
    s.r("#6a4630", 0, 43, W, 1); s.r("#6a4630", 0, 46, W, 1)
    for x in range(7, W, 19):
        s.r("#6a4630", x, 40, 1, 3); s.r("#6a4630", x + 9, 44, 1, 2); s.r("#6a4630", x + 4, 47, 1, 3)
    s.r("#8e3b46", 18, 45, 32, 3); s.r("#a8505b", 19, 45, 30, 1)
    # janela com céu noturno e lua
    s.r("#2a2440", 5, 4, 24, 22)
    s.r("#1b2447", 6, 5, 22, 10); s.r("#243060", 6, 15, 22, 10)
    s.r("#f4e9c1", 21, 7, 4, 4); s.r("#1b2447", 23, 7, 2, 2)
    s.r("#2a2440", 16, 5, 2, 20); s.r("#2a2440", 6, 14, 22, 2)
    s.r("#8b6a4f", 4, 26, 26, 2)
    # quadro com a estrelinha do Claude
    s.r("#1f1a2e", 35, 7, 12, 10); s.r("#e8d8b0", 36, 8, 10, 8)
    s.r("#d97757", 40, 10, 2, 4); s.r("#d97757", 38, 11, 6, 2); s.r("#b85f42", 40, 11, 2, 2)
    # estante
    s.r("#4a2f1f", 73, 9, 20, 31); s.r("#2e1d13", 74, 10, 18, 29)
    books = ["#c0392b", "#2e86c1", "#f1c40f", "#27ae60", "#8e44ad", "#e67e22", "#16a085", "#d35400"]
    for shelf in range(3):
        top = 10 + shelf * 10
        x, i = 75, shelf * 3
        while x < 90:
            w = 2 + (1 if i * 7 % 3 == 0 else 0); h = 6 + (1 if i % 3 == 1 else 0)
            s.r(books[i % len(books)], x, top + 9 - h, w, h)
            x += w + (1 if i % 4 == 3 else 0); i += 1
        s.r("#6d4630", 73, top + 9, 20, 1)
    # planta
    s.r("#2f6b3a", 30, 27, 4, 5); s.r("#3f8f4a", 27, 29, 4, 4); s.r("#3f8f4a", 33, 28, 4, 5); s.r("#4fb05c", 30, 24, 3, 5)
    s.r("#b5651d", 28, 33, 8, 8); s.r("#8e4f17", 28, 33, 8, 1); s.r("#9b5518", 29, 39, 6, 2)
    # mesa, monitor, teclado e caneca
    s.r("#8b5a3c", 45, 29, 28, 2); s.r("#6d4630", 45, 31, 28, 1)
    s.r("#5a3a26", 46, 32, 2, 9); s.r("#5a3a26", 70, 32, 2, 9)
    s.r("#6d4630", 60, 32, 10, 6); s.r("#c9a26b", 64, 34, 2, 1)
    s.r("#2b2b33", 57, 25, 3, 4); s.r("#2b2b33", 54, 28, 9, 1)
    s.r("#1e1e26", 49, 13, 19, 13); s.r("#0f1a24", 50, 14, 17, 11)
    s.r("#cfd8dc", 49, 28, 5, 1)
    s.r("#e57373", 66, 26, 3, 3); s.r("#c62828", 69, 27, 1, 1); s.r("#4e342e", 66, 26, 3, 1)
    s.a("piscar", pontos=[[8, 7], [12, 10], [19, 12], [9, 18], [24, 19], [14, 21]], cores=["#fff6c8", "#cfd8ff"], vel=2.3, limiar=-0.3)
    s.arte("escritorio.png")
    s.a("tela", x=50, y=19, w=14, h=7)
    s.a("fumaca", x=46, y=23, cor="#b0bec5", n=2, altura=5, quando=["trabalhando"])
    return s


# ---------------------------------------------------------------- Mina
def mina():
    s = Sala("mina", "Mina", "O mineiro pica pedra, empurra o carrinho pelos trilhos e a lamparina tremeluz.",
             ["anoes", "gnomos"], "#d98e3a", 44, 26,
             {"anoes": ["classico", "ferro-ruivo", "ouro", "azul", "verde"]})
    s.lugar(22, "trabalhar", min=5, max=9)
    s.lugar(70, "trabalhar-saco", andar="trabalhar-carrinho", min=2.5, max=3.5)
    s.r("#2d2226", 0, 0, W, H)
    rnd = random.Random(7)
    for _ in range(70):
        x, y, k = rnd.randrange(W), rnd.randrange(42), rnd.randrange(100)
        c = "#3a2c2f" if k < 50 else "#241b1e" if k < 85 else "#46363a"
        sz = 1 + k % 3
        s.r(c, x, y, sz + 1, sz)
    s.r("#140f12", 62, 16, 22, 28); s.r("#140f12", 64, 13, 18, 3); s.r("#140f12", 67, 11, 12, 2)
    s.r("#1c1518", 62, 16, 2, 28)
    s.r("#8b5e34", 0, 3, W, 3); s.r("#6b4526", 0, 6, W, 1)
    s.r("#7a5230", 3, 6, 4, 38); s.r("#5e3e22", 6, 6, 1, 38)
    s.r("#7a5230", 88, 6, 4, 38); s.r("#5e3e22", 91, 6, 1, 38)
    s.r("#5b4a4f", 38, 30, 12, 14); s.r("#5b4a4f", 36, 34, 16, 10); s.r("#6e5c61", 39, 31, 5, 3); s.r("#463a3e", 44, 38, 7, 6)
    s.r("#4dd0e1", 41, 36, 2, 2); s.r("#4dd0e1", 46, 33, 2, 1); s.r("#ffca3a", 47, 39, 2, 2)
    s.r("#4a3426", 0, 44, W, H - 44); s.r("#3b291e", 0, 44, W, 1)
    for x in range(1, W, 5): s.r("#5d3b22", x, 46, 3, 2)
    s.r("#9aa3ad", 0, 45, W, 1)
    for _ in range(12): s.r("#5c4332", rnd.randrange(W), 48 + rnd.randrange(2), 2, 1)
    s.r("#e57373", 14, 12, 2, 2); s.r("#4dd0e1", 55, 8, 2, 2); s.r("#ffca3a", 30, 20, 2, 1); s.r("#81c784", 85, 30, 1, 2)
    s.r("#55555e", 48, 6, 1, 5)
    s.arte("mina.png")
    s.a("brilho", pontos=[[42, 13], [16, 33], [46, 35], [42, 39], [50, 38]])
    s.a("luz", x=48.5, y=14, raio=20, cor="#ffc862", pronto="#8ff0b0", erro="#ff7070")
    s.a("quadros", vel=0, quadros=[[["#caa04a", 46, 11, 5, 1], ["#caa04a", 46, 16, 5, 1], ["#caa04a", 46, 12, 1, 4], ["#caa04a", 50, 12, 1, 4]]])
    s.a("estado", x=47, y=12, w=3, h=4, cores={"trabalhando": "#ffe08a", "esperando": "#ffe08a", "pronto": "#8ff0b0", "erro": "#ff9a8a", "parado": "#c9b27a"})
    s.a("piscar", pontos=[[48, 13, 1, 2]], cores=["#fff6d0"], vel=13, limiar=-0.2)
    return s


# ---------------------------------------------------------------- Jardim (gnomos de jardim)
def jardim():
    s = Sala("jardim", "Jardim", "O gnomo rega, cava e rastela o canteiro enquanto as borboletas passeiam.",
             ["gnomos-jardim"], "#5bb85b", 42, 26)
    s.lugar(26, "trabalhar", min=4, max=7)
    s.lugar(54, "trabalhar-cavar", min=3, max=5)
    s.lugar(74, "trabalhar-rastelar", min=3, max=5)
    for i, c in enumerate(["#8fd3ff", "#9fdaff", "#afe1ff", "#bfe8ff", "#cdeefe"]):
        s.r(c, 0, i * 6, W, 6)
    s.r("#ffe066", 80, 4, 8, 8); s.r("#fff3b0", 82, 6, 3, 3)
    # cerca
    for x in range(2, W, 8):
        s.r("#f3ead3", x, 22, 4, 16); s.r("#f3ead3", x + 1, 21, 2, 1); s.r("#d8ccb0", x + 3, 22, 1, 16)
    s.r("#f3ead3", 0, 26, W, 2); s.r("#f3ead3", 0, 33, W, 2); s.r("#d8ccb0", 0, 27, W, 1); s.r("#d8ccb0", 0, 34, W, 1)
    # gramado e canteiro
    s.r("#5fae4a", 0, 37, W, 5); s.r("#4f9a3c", 0, 41, W, 1)
    s.r("#7a5236", 0, 42, W, H - 42); s.r("#6a4630", 0, 45, W, 1); s.r("#8b6040", 0, 43, W, 1)
    for x in range(3, W, 7): s.r("#6a4630", x, 47, 2, 1)
    # flores no canteiro de trás
    flores = ["#ec407a", "#ffeb3b", "#ab47bc", "#ff7043", "#ffffff"]
    for i, x in enumerate(range(4, W, 9)):
        h = 4 + i % 3
        s.r("#3f8f4a", x + 1, 37 - h, 1, h)
        c = flores[i % len(flores)]
        s.r(c, x, 37 - h - 2, 3, 2); s.r("#ffd54f" if c != "#ffeb3b" else "#ff8f00", x + 1, 37 - h - 2, 1, 1)
    # regador e carrinho de mão
    s.r("#90a4ae", 88, 38, 5, 4); s.r("#90a4ae", 86, 38, 2, 1); s.r("#78909c", 92, 37, 1, 2)
    s.arte("jardim.png")   # os vãos pretos da cerca foram pintados de verde à mão
    s.a("particulas", area=[0, 4, W, 14], cores=["#ffffff"], n=3, vx=3, vy=0, tam=[4, 2])
    s.a("piscar", pontos=[[20, 16], [44, 12], [66, 18]], cores=["#ff80ab", "#ffd54f", "#b388ff"], vel=6, limiar=0)
    s.a("particulas", area=[10, 8, 76, 14], cores=["#ff80ab", "#b388ff"], n=2, vx=6, vy=1.5, tam=[2, 1])
    return s


# ---------------------------------------------------------------- Covil do dragão
def covil():
    s = Sala("covil", "Covil do dragão", "O dragão voa sobre o tesouro, cospe fogo e as tochas iluminam a caverna.",
             ["dragao"], "#e0663a", 42, 22)
    s.lugar(30, "trabalhar", min=4, max=7)
    s.lugar(64, "parado", min=2.5, max=4)
    s.r("#2a1d2e", 0, 0, W, H)
    rnd = random.Random(11)
    for _ in range(60):
        x, y, k = rnd.randrange(W), rnd.randrange(40), rnd.randrange(100)
        s.r("#352538" if k < 60 else "#1f1522", x, y, 2 + k % 3, 1 + k % 2)
    # arco da caverna
    s.r("#160e18", 0, 0, W, 3); s.r("#160e18", 0, 3, 6, 6); s.r("#160e18", 90, 3, 6, 6); s.r("#160e18", 0, 9, 3, 6); s.r("#160e18", 93, 9, 3, 6)
    # estalactites
    for x, h in [(14, 5), (22, 3), (40, 6), (58, 4), (76, 5), (84, 3)]:
        s.r("#160e18", x, 3, 3, h); s.r("#160e18", x + 1, 3 + h, 1, 2)
    # chão de pedra
    s.r("#3b2a33", 0, 42, W, H - 42); s.r("#2c1f27", 0, 42, W, 1)
    for x in range(4, W, 11): s.r("#4a3640", x, 45, 4, 1); s.r("#2c1f27", x + 6, 47, 3, 1)
    # monte de tesouro
    s.r("#b8860b", 58, 36, 34, 6); s.r("#daa520", 62, 32, 26, 5); s.r("#ffd54f", 67, 29, 16, 4); s.r("#ffe082", 72, 27, 7, 2)
    for x, y in [(60, 37), (65, 34), (70, 31), (76, 30), (82, 33), (86, 37), (74, 35)]:
        s.r("#fff3b0", x, y, 1, 1)
    s.r("#e53935", 66, 33, 2, 2); s.r("#26c6da", 80, 31, 2, 2); s.r("#7e57c2", 85, 35, 2, 2)
    # baú
    s.r("#6d4c2f", 44, 35, 10, 7); s.r("#8d6e46", 44, 34, 10, 2); s.r("#ffca28", 48, 37, 2, 2); s.r("#4e342e", 44, 38, 10, 1)
    # tochas nas paredes
    for x in (8, 88):
        s.r("#6d4c2f", x, 22, 2, 7); s.r("#4e342e", x - 1, 21, 4, 2)
    s.arte("covil.png")   # 2ª tentativa, força 30 (a de 45 saiu chapada); as tochas da arte ficam logo abaixo do fogo
    s.a("luz", x=9, y=18, raio=12, cor="#ff9b42", pronto="#8ff0b0", erro="#ff5050")
    s.a("luz", x=89, y=18, raio=12, cor="#ff9b42", pronto="#8ff0b0", erro="#ff5050")
    fogo = lambda x, k: [["#ff9800", x - 1, 17 + k, 4, 4 - k], ["#ffeb3b", x, 18 + k, 2, 3 - k], ["#f44336", x + (1 if k else 0), 16 + k, 1, 1]]
    s.a("quadros", vel=7, quadros=[fogo(8, 0) + fogo(88, 1), fogo(8, 1) + fogo(88, 0)])
    s.a("brilho", pontos=[[74, 28], [80, 32], [86, 37], [70, 34], [64, 38]])   # no monte de ouro da arte
    s.a("particulas", area=[4, 6, 8, 14], cores=["#ffb74d", "#ff7043"], n=3, vx=0.5, vy=-7, tam=[1, 1])
    s.a("particulas", area=[84, 6, 8, 14], cores=["#ffb74d", "#ff7043"], n=3, vx=-0.5, vy=-7, tam=[1, 1])
    return s


# ---------------------------------------------------------------- Floresta (elfos)
def floresta():
    s = Sala("floresta", "Bosque dos elfos", "O elfo treina tiro ao alvo entre as árvores e os vaga-lumes piscam.",
             ["elfos-hd", "elfos"], "#4caf7a", 42, 24)
    s.lugar(24, "trabalhar", min=4, max=8)
    s.lugar(56, "parado", min=2, max=3.5)
    for i, c in enumerate(["#16324a", "#1b3d52", "#21485a", "#285463"]):
        s.r(c, 0, i * 8, W, 8)
    s.r("#2f5f5a", 0, 32, W, 10)
    # lua e árvores ao fundo
    s.r("#f4f1d0", 66, 4, 6, 6); s.r("#16324a", 69, 4, 3, 3)
    for x, h in [(2, 30), (16, 26), (34, 32), (52, 24), (78, 30)]:
        s.r("#173a2c", x, 40 - h, 8, h); s.r("#1e4a37", x - 3, 40 - h - 4, 14, 8); s.r("#1e4a37", x - 1, 40 - h - 8, 10, 5)
    # árvore da frente com o alvo
    s.r("#5d4037", 84, 4, 8, 38); s.r("#4e342e", 89, 4, 3, 38)
    s.r("#2e7d32", 78, 0, 18, 8); s.r("#388e3c", 80, 0, 14, 5)
    s.r("#f5f5f5", 79, 18, 10, 10); s.r("#e53935", 80, 19, 8, 8); s.r("#f5f5f5", 82, 21, 4, 4); s.r("#e53935", 83, 22, 2, 2)
    # chão de musgo
    s.r("#3e6b35", 0, 42, W, H - 42); s.r("#4c7f40", 0, 42, W, 1)
    for x in range(2, W, 6): s.r("#5c9a4c", x, 41, 1, 2); s.r("#33592c", x + 3, 46, 2, 1)
    s.r("#8d6e63", 8, 39, 6, 3); s.r("#a1887f", 9, 38, 4, 1); s.r("#e53935", 40, 40, 3, 2); s.r("#ffffff", 41, 40, 1, 1)
    s.arte("floresta.png")
    s.a("piscar", pontos=[[12, 20], [28, 14], [44, 24], [60, 16], [70, 28], [36, 34], [20, 30]], cores=["#e6ff7a", "#b2ff59"], vel=1.6, limiar=0.3)
    s.a("particulas", area=[0, 4, 78, 36], cores=["#c5e1a5"], n=3, vx=2, vy=-1, tam=[1, 1])
    return s


# ---------------------------------------------------------------- Fábrica de chocolate
def fabrica():
    s = Sala("fabrica", "Fábrica de chocolate", "Os chocolateiros martelam, o rio de chocolate borbulha e a esteira não para.",
             ["chocolateiros"], "#b06a3b", 41, 22)
    s.lugar(26, "trabalhar", min=4, max=8)
    s.lugar(66, "trabalhar", min=3, max=6)
    s.r("#f6d6e8", 0, 0, W, 34)
    for x in range(0, W, 8): s.r("#f0c4dc", x, 0, 4, 34)
    s.r("#c48bb0", 0, 33, W, 1)
    # canos
    s.r("#b0bec5", 6, 0, 4, 20); s.r("#90a4ae", 9, 0, 1, 20); s.r("#b0bec5", 6, 18, 16, 4); s.r("#90a4ae", 6, 21, 16, 1)
    s.r("#b0bec5", 86, 0, 4, 28); s.r("#90a4ae", 89, 0, 1, 28)
    # pirulitos gigantes
    for x, c in [(32, "#e91e63"), (48, "#ffeb3b"), (74, "#4caf50")]:
        s.r("#fafafa", x + 2, 16, 1, 18); s.r(c, x, 10, 6, 6); s.r("#ffffff", x + 1, 11, 2, 2)
    # rio de chocolate
    s.r("#6d3b1f", 0, 34, W, 7); s.r("#7b4526", 0, 34, W, 1); s.r("#5a2f17", 0, 39, W, 2)
    # chão
    s.r("#e8c39e", 0, 41, W, H - 41); s.r("#d4a77a", 0, 41, W, 1)
    for x in range(0, W, 8): s.r("#d4a77a", x, 41, 1, H - 41)
    # esteira
    s.r("#455a64", 38, 26, 30, 3); s.r("#263238", 38, 29, 30, 1); s.r("#37474f", 40, 30, 2, 4); s.r("#37474f", 64, 30, 2, 4)
    s.arte("fabrica.png", chao=43)   # força 30; o piso de lajotas da arte começa logo abaixo do rio
    barra = lambda x: [["#5b3416", x, 23, 5, 3], ["#7d4a22", x + 1, 23, 3, 1], ["#7b1fa2", x + 3, 24, 2, 2]]
    s.a("quadros", vel=1.5, quadros=[barra(39) + barra(50) + barra(61), barra(42) + barra(53), barra(45) + barra(56)])
    s.a("particulas", area=[0, 34, W, 5], cores=["#8d5a36", "#a0693f"], n=6, vx=1, vy=-1.2, tam=[1, 1])
    s.a("particulas", area=[0, 35, W, 4], cores=["#4e2a13"], n=4, vx=4, vy=0, tam=[3, 1])
    return s


# ---------------------------------------------------------------- Quintal (cachorros)
def quintal():
    s = Sala("quintal", "Quintal", "O cachorro cava, late para as borboletas e tira um cochilo perto da casinha.",
             ["cachorros"], "#e0a040", 42, 20)
    s.lugar(18, "trabalhar-cavar", min=4, max=7)   # o buraco da arte fica à esquerda
    s.lugar(60, "trabalhar", min=3, max=5)
    for i, c in enumerate(["#7ec8f0", "#8ed0f2", "#9fd8f5", "#b0e0f8"]):
        s.r(c, 0, i * 7, W, 7)
    s.r("#fff176", 6, 4, 7, 7); s.r("#fffde7", 8, 6, 3, 3)
    # cerca de madeira
    s.r("#c8995a", 0, 22, W, 2); s.r("#c8995a", 0, 30, W, 2)
    for x in range(1, W, 6): s.r("#d9aa6a", x, 18, 4, 18); s.r("#b5884c", x + 3, 18, 1, 18); s.r("#d9aa6a", x + 1, 17, 2, 1)
    # casinha
    s.r("#c62828", 66, 19, 26, 3); s.r("#c62828", 68, 16, 22, 3); s.r("#c62828", 71, 13, 16, 3); s.r("#b71c1c", 66, 21, 26, 1)
    s.r("#ffcc80", 68, 22, 22, 20); s.r("#f0b060", 68, 22, 22, 1); s.r("#4e342e", 74, 29, 10, 13); s.r("#3e2723", 74, 29, 10, 2)
    s.r("#5d4037", 77, 24, 4, 3); s.r("#ffffff", 78, 25, 2, 1)
    # grama e buraco
    s.r("#66bb6a", 0, 36, W, 6); s.r("#57a85b", 0, 41, W, 1)
    s.r("#5ea95f", 0, 42, W, H - 42)
    for x in range(1, W, 5): s.r("#7cc87d", x, 35, 1, 2); s.r("#4c944d", x + 2, 45, 1, 1)
    s.r("#6d4c41", 36, 42, 12, 2); s.r("#8d6e63", 34, 41, 3, 1); s.r("#8d6e63", 47, 41, 3, 1)
    # osso e tigela
    s.r("#f3ead3", 52, 44, 6, 1); s.r("#f3ead3", 51, 43, 2, 3); s.r("#f3ead3", 57, 43, 2, 3)
    s.r("#1e88e5", 6, 40, 8, 3); s.r("#1565c0", 7, 42, 6, 1); s.r("#a1887f", 7, 40, 6, 1)
    s.arte("quintal.png")   # 2ª tentativa, força 30 (a de 45 saiu chapada, com buracos pretos na cerca)
    s.a("particulas", area=[0, 2, W, 14], cores=["#ffffff"], n=3, vx=2.5, vy=0, tam=[5, 2])
    s.a("piscar", pontos=[[22, 12], [44, 9], [58, 14]], cores=["#fff176", "#ff8a65"], vel=7, limiar=0)
    return s


# ---------------------------------------------------------------- Sala de estar (gatos)
def sala_gatos():
    s = Sala("sala-gatos", "Sala de estar", "O gato cochila no tapete, brinca com o novelo e vigia a janela.",
             ["gatos"], "#e08a5a", 42, 18)
    s.lugar(36, "trabalhar", min=5, max=9)
    s.lugar(64, "parado", min=2, max=4)
    s.r("#f3e0c7", 0, 0, W, 36)
    for x in range(0, W, 10):
        for y in range(3, 34, 8): s.r("#ead2b2", x + (5 if (y // 8) % 2 else 0), y, 2, 2)
    s.r("#c49a6c", 0, 34, W, 2); s.r("#a87e52", 0, 35, W, 1)
    # janela ensolarada
    s.r("#8d6e63", 8, 5, 26, 20); s.r("#b3e5fc", 9, 6, 24, 18); s.r("#81d4fa", 9, 16, 24, 8)
    s.r("#8d6e63", 20, 6, 2, 18); s.r("#8d6e63", 9, 14, 24, 2); s.r("#a1887f", 6, 25, 30, 2)
    s.r("#ef9a9a", 6, 4, 3, 22); s.r("#ef9a9a", 33, 4, 3, 22)
    # sofá
    s.r("#5c6bc0", 52, 24, 34, 6); s.r("#3f51b5", 52, 28, 34, 8); s.r("#5c6bc0", 49, 26, 5, 10); s.r("#5c6bc0", 84, 26, 5, 10)
    s.r("#7986cb", 56, 26, 12, 2); s.r("#7986cb", 70, 26, 12, 2); s.r("#3949ab", 54, 36, 2, 2); s.r("#3949ab", 82, 36, 2, 2)
    s.r("#ffca28", 74, 22, 6, 4)
    # quadro de peixe
    s.r("#6d4c41", 60, 6, 16, 11); s.r("#e1f5fe", 61, 7, 14, 9); s.r("#64b5f6", 64, 10, 7, 3); s.r("#64b5f6", 71, 9, 2, 5)
    # piso de madeira e tapete
    s.r("#b07a4a", 0, 36, W, H - 36); s.r("#9c6a3e", 0, 40, W, 1); s.r("#9c6a3e", 0, 45, W, 1)
    for x in range(5, W, 14): s.r("#9c6a3e", x, 36, 1, 4); s.r("#9c6a3e", x + 7, 41, 1, 4)
    s.r("#d84b6b", 20, 42, 34, 4); s.r("#f06292", 21, 42, 32, 1); s.r("#ad1457", 21, 45, 32, 1)
    # novelo
    s.r("#ab47bc", 8, 39, 5, 4); s.r("#ce93d8", 9, 40, 2, 1); s.r("#ab47bc", 13, 42, 6, 1)
    s.a("particulas", area=[9, 6, 24, 10], cores=["#ffffff"], n=2, vx=1.5, vy=0, tam=[4, 1])
    s.a("piscar", pontos=[[12, 8], [28, 12], [16, 18], [30, 20]], cores=["#fffde7"], vel=1.1, limiar=0.6)
    return s


# ---------------------------------------------------------------- Data center gelado (pinguins)
def polo():
    s = Sala("polo", "Data center gelado", "Os pinguins mantêm os servidores fresquinhos enquanto a neve cai lá fora.",
             ["pinguins-hd", "pinguins"], "#5aa8e0", 42, 20)
    s.lugar(30, "trabalhar", min=4, max=7)
    s.lugar(62, "trabalhar-peixe", min=3, max=6)
    s.r("#cfe8f7", 0, 0, W, 36)
    for x in range(0, W, 12): s.r("#bddcef", x, 0, 1, 36)
    # janela com neve e aurora
    s.r("#90a4ae", 4, 4, 30, 18); s.r("#0d2238", 5, 5, 28, 16); s.r("#1b5e20", 5, 9, 28, 2); s.r("#26a69a", 5, 11, 28, 1)
    s.r("#ffffff", 5, 18, 28, 3); s.r("#e3f2fd", 5, 17, 10, 1)
    # racks de servidor
    for x in (44, 60, 76):
        s.r("#37474f", x, 6, 14, 36); s.r("#263238", x + 1, 7, 12, 34)
        for y in range(9, 40, 5): s.r("#455a64", x + 2, y, 10, 3); s.r("#1c262b", x + 9, y + 1, 2, 1)
        s.r("#e0f7fa", x, 5, 14, 2)   # gelo em cima
        s.r("#e0f7fa", x + 2, 7, 1, 2); s.r("#e0f7fa", x + 10, 7, 1, 3)
    # chão de gelo
    s.r("#e3f2fd", 0, 42, W, H - 42); s.r("#b3d9f2", 0, 42, W, 1)
    for x in range(3, W, 9): s.r("#c7e4f7", x, 45, 5, 1); s.r("#ffffff", x + 4, 47, 2, 1)
    s.r("#b3d9f2", 0, 36, W, 6); s.r("#a3cde8", 0, 41, W, 1)
    s.arte("polo.png")
    leds = [[x, y] for x in (55, 71, 87) for y in (10, 15, 19)]   # LEDs dos racks da arte
    s.a("piscar", pontos=leds, cores=["#69f0ae", "#40c4ff", "#69f0ae", "#ffd740"], vel=4.3, limiar=-0.1)
    s.a("piscar", pontos=[[8, 7], [14, 6], [22, 8], [29, 6]], cores=["#ffffff"], vel=2, limiar=0.2)
    s.a("particulas", area=[5, 5, 28, 13], cores=["#ffffff"], n=8, vx=1, vy=5, tam=[1, 1])
    s.a("particulas", area=[0, 0, W, 42], cores=["#ffffff", "#e1f5fe"], n=6, vx=0.6, vy=3, tam=[1, 1], quando=["pronto"])
    return s


# ---------------------------------------------------------------- Formigueiro
def formigueiro():
    s = Sala("formigueiro", "Formigueiro", "As formigas carregam açúcar pelos túneis e guardam tudo na despensa.",
             ["formigas"], "#c8783a", 40, 16)
    s.lugar(26, "trabalhar", min=4, max=7)
    s.lugar(70, "trabalhar", min=3, max=6)
    # superfície
    s.r("#9ad3f0", 0, 0, W, 6); s.r("#66bb6a", 0, 6, W, 3); s.r("#4caf50", 0, 8, W, 1)
    for x in range(1, W, 4): s.r("#81c784", x, 5, 1, 2)
    s.r("#43a047", 70, 1, 8, 5); s.r("#2e7d32", 73, 2, 2, 3)   # folha
    # terra em camadas
    s.r("#8d6748", 0, 9, W, 41); s.r("#7a5a3e", 0, 22, W, 1); s.r("#7a5a3e", 0, 34, W, 1)
    rnd = random.Random(5)
    for _ in range(50): s.r(["#9c7656", "#6f5038", "#a5825f"][rnd.randrange(3)], rnd.randrange(W), 10 + rnd.randrange(38), 1 + rnd.randrange(2), 1)
    # túneis (o de baixo é onde elas andam)
    s.r("#4a3424", 0, 26, W, 16); s.r("#3d2b1e", 0, 26, W, 1)
    s.r("#4a3424", 44, 9, 6, 17); s.r("#4a3424", 12, 14, 18, 6); s.r("#4a3424", 26, 18, 4, 8)
    s.r("#4a3424", 70, 14, 20, 7); s.r("#4a3424", 72, 20, 4, 6)
    # despensa com açúcar e sementes
    for x, y in [(14, 16), (17, 16), (20, 16), (15, 14), (18, 14)]:
        s.r("#ffffff", x, y, 3, 3); s.r("#cfd8dc", x + 2, y, 1, 3)
    for x in range(72, 88, 3): s.r("#ffca28", x, 18, 2, 2); s.r("#f9a825", x + 1, 16, 2, 2)
    # raízes
    s.r("#c49a6c", 34, 9, 1, 6); s.r("#c49a6c", 35, 14, 1, 4); s.r("#c49a6c", 60, 9, 1, 4); s.r("#c49a6c", 59, 12, 1, 6)
    s.r("#3d2b1e", 0, 40, W, 1); s.r("#5b4330", 0, 41, W, 9)
    s.a("piscar", pontos=[[16, 15], [19, 17], [80, 17]], cores=["#ffffff"], vel=3, limiar=0.75)
    s.a("particulas", area=[0, 0, W, 5], cores=["#ffffff"], n=2, vx=2, vy=0, tam=[4, 1])
    return s


# ---------------------------------------------------------------- Oficina dos robôs
def oficina():
    s = Sala("oficina", "Oficina dos robôs", "Os robozinhos soldam peças, a esteira roda e o painel pisca sem parar.",
             ["robos"], "#5ab0b8", 42, 20)
    s.lugar(28, "trabalhar", min=4, max=7)
    s.lugar(66, "trabalhar", min=3, max=6)
    s.r("#59636b", 0, 0, W, 36)
    for x in range(0, W, 16):
        s.r("#4c555c", x, 0, 1, 36)
        for y in (2, 33): s.r("#7b868f", x + 2, y, 1, 1); s.r("#7b868f", x + 13, y, 1, 1)
    s.r("#4c555c", 0, 17, W, 1)
    # faixa de atenção
    for x in range(0, W, 6): s.r("#fbc02d", x, 34, 3, 2); s.r("#212121", x + 3, 34, 3, 2)
    # painel de controle
    s.r("#263238", 6, 6, 22, 14); s.r("#37474f", 7, 7, 20, 12)
    s.r("#0f1a24", 9, 9, 10, 7)
    # engrenagens na parede
    for cx, cy, r in [(78, 9, 4), (86, 15, 3)]:
        s.r("#90a4ae", cx - r, cy - 1, 2 * r + 1, 3); s.r("#90a4ae", cx - 1, cy - r, 3, 2 * r + 1); s.r("#b0bec5", cx - r + 1, cy - r + 1, 2 * r - 1, 2 * r - 1); s.r("#59636b", cx - 1, cy - 1, 3, 3)
    # esteira
    s.r("#37474f", 38, 30, 34, 4); s.r("#263238", 38, 33, 34, 1)
    s.r("#455a64", 40, 34, 2, 8); s.r("#455a64", 68, 34, 2, 8)
    # chão de metal
    s.r("#78848c", 0, 42, W, H - 42); s.r("#5f6a71", 0, 42, W, 1)
    for x in range(0, W, 8): s.r("#6b767d", x, 42, 1, H - 42); s.r("#8e9aa1", x + 3, 46, 1, 1)
    s.r("#78848c", 0, 36, W, 6); s.r("#6b767d", 0, 41, W, 1)
    esteira = lambda k: [["#90a4ae", 38 + i * 4 + k, 30, 2, 1] for i in range(8)]
    peca = lambda x: [["#ffb300", x, 27, 4, 3], ["#ffe082", x + 1, 27, 2, 1]]
    s.a("quadros", vel=4, quadros=[esteira(0) + peca(42) + peca(58), esteira(1) + peca(43) + peca(59), esteira(2) + peca(44) + peca(60), esteira(3) + peca(45) + peca(61)])
    s.a("tela", x=9, y=9, w=10, h=7)
    s.a("piscar", pontos=[[21, 9, 2, 1], [24, 9, 2, 1], [21, 12, 2, 1], [24, 12, 2, 1], [21, 15, 2, 1], [24, 15, 2, 1]], cores=["#ff5252", "#69f0ae", "#ffd740"], vel=5, limiar=0)
    s.a("estado", x=46, y=2, w=4, h=3, cores={"trabalhando": "#ffb300", "esperando": "#ff9100", "pronto": "#00e676", "erro": "#ff1744", "parado": "#607d8b"}, piscar=True)
    return s


# ---------------------------------------------------------------- Hangar da nave (robôs clássicos)
def nave():
    s = Sala("nave", "Hangar da nave", "Os robôs consertam a nave enquanto as estrelas passam pela janela.",
             ["robos-classicos"], "#7aa0ff", 42, 22,
             {"robos-classicos": ["astro", "dourado", "astro-vermelho", "aspirador"]})
    s.lugar(30, "trabalhar", min=4, max=7)
    s.lugar(70, "parado", min=2, max=4)
    s.r("#2b3245", 0, 0, W, 36)
    s.r("#1d2232", 0, 0, W, 3); s.r("#3a4360", 0, 3, W, 1)
    # janelão com o espaço
    s.r("#141826", 8, 6, 80, 18); s.r("#05060f", 10, 8, 76, 14)
    s.r("#141826", 34, 6, 2, 18); s.r("#141826", 60, 6, 2, 18)
    s.r("#5c6bc0", 70, 12, 8, 8); s.r("#7986cb", 71, 13, 4, 3); s.r("#3949ab", 74, 17, 3, 2)   # planeta
    # consoles
    s.r("#3a4360", 4, 28, 20, 8); s.r("#4a5578", 4, 28, 20, 1); s.r("#0b1020", 6, 30, 16, 4)
    s.r("#3a4360", 72, 28, 20, 8); s.r("#4a5578", 72, 28, 20, 1); s.r("#0b1020", 74, 30, 16, 4)
    # piso
    s.r("#4a5168", 0, 36, W, H - 36); s.r("#3a4058", 0, 41, W, 1); s.r("#5a627c", 0, 36, W, 1)
    for x in range(0, W, 12): s.r("#3a4058", x, 36, 1, H - 36)
    for x in range(6, W, 12): s.r("#ffca28", x, 46, 4, 1)
    s.a("particulas", area=[10, 8, 76, 14], cores=["#ffffff", "#bbdefb", "#fff9c4"], n=14, vx=-14, vy=0, tam=[1, 1])
    s.a("piscar", pontos=[[7, 31, 2, 1], [10, 32, 3, 1], [15, 31, 2, 1], [19, 32, 2, 1], [76, 31, 3, 1], [81, 32, 2, 1], [85, 31, 2, 1]], cores=["#40c4ff", "#69f0ae", "#ff4081"], vel=3.7, limiar=-0.2)
    s.a("estado", x=46, y=2, w=4, h=1, cores={"trabalhando": "#40c4ff", "esperando": "#ffab40", "pronto": "#69f0ae", "erro": "#ff5252", "parado": "#546e7a"}, piscar=True)
    return s


# ---------------------------------------------------------------- Estúdio do Claude
def estudio():
    s = Sala("estudio", "Estúdio do Claude", "O Clawd digita no terminal laranja e as estrelinhas do Claude giram na parede.",
             ["clawd", "claude"], "#d97757", 41, 20)
    s.lugar(42, "trabalhar", min=5, max=9)
    s.lugar(80, "parado", min=2, max=4)
    s.r("#f0eee6", 0, 0, W, 38); s.r("#e5e2d6", 0, 30, W, 8); s.r("#d9d5c6", 0, 30, W, 1)
    # a estrela do Claude na parede
    sx, sy = 14, 6
    for dx, dy, w, h in [(4, 0, 2, 10), (0, 4, 10, 2), (1, 1, 2, 2), (7, 1, 2, 2), (1, 7, 2, 2), (7, 7, 2, 2)]:
        s.r("#d97757", sx + dx, sy + dy, w, h)
    s.r("#b85f42", sx + 4, sy + 4, 2, 2)
    # estante com livros laranja e creme
    s.r("#c9b79c", 72, 8, 20, 2); s.r("#c9b79c", 72, 18, 20, 2)
    for i, x in enumerate(range(73, 91, 3)):
        s.r(["#d97757", "#262624", "#f5a37f", "#b85f42", "#91877a", "#d97757"][i], x, 3 + i % 2, 2, 5 - i % 2)
        s.r(["#262624", "#d97757", "#91877a", "#f5a37f", "#b85f42", "#262624"][i], x, 13 + (i + 1) % 2, 2, 5 - (i + 1) % 2)
    # mesa e terminal
    s.r("#262624", 46, 12, 22, 16); s.r("#141413", 47, 13, 20, 14)
    s.r("#3d3d3a", 55, 28, 4, 2); s.r("#3d3d3a", 52, 29, 10, 1)
    s.r("#c9b79c", 40, 30, 34, 2); s.r("#a8977e", 40, 32, 34, 1); s.r("#a8977e", 42, 33, 2, 8); s.r("#a8977e", 70, 33, 2, 8)
    s.r("#f0eee6", 46, 29, 6, 1)
    # planta
    s.r("#5f8a4f", 26, 22, 3, 8); s.r("#76a862", 23, 24, 4, 4); s.r("#76a862", 29, 23, 4, 5)
    s.r("#d97757", 24, 30, 8, 8); s.r("#b85f42", 24, 30, 8, 1)
    # piso
    s.r("#cbbfa8", 0, 38, W, H - 38); s.r("#b8ab92", 0, 38, W, 1)
    for x in range(0, W, 10): s.r("#bfb39b", x, 42, 6, 1); s.r("#bfb39b", x + 5, 46, 6, 1)
    s.arte("estudio.png", chao=48)   # força 30; na arte o piso fica mais embaixo e o sol laranja é maior
    s.a("tela", x=48, y=14, w=18, h=12, fundo="#141413", cores=["#d97757", "#f0eee6", "#f5a37f", "#91877a"])
    s.a("piscar", pontos=[[17, 3], [9, 13], [25, 13], [17, 22]], cores=["#f5a37f"], vel=3, limiar=0.2)
    return s


# ---------------------------------------------------------------- Genérica
def generica():
    s = Sala("generica", "Salinha", "Uma salinha simples para qualquer personagem, com mesa, janela e um monitor.",
             [], "#8a7cf0", 42, 22)
    s.lugar(34, "trabalhar", min=4, max=8)
    s.lugar(74, "parado", min=2, max=4)
    s.r("#4a5a78", 0, 0, W, 38); s.r("#405070", 0, 30, W, 8); s.r("#36445f", 0, 30, W, 1)
    s.r("#36445f", 8, 5, 22, 16); s.r("#9fd8f5", 9, 6, 20, 14); s.r("#c8ecfb", 9, 6, 20, 5); s.r("#36445f", 18, 6, 2, 14)
    s.r("#5d4a6e", 66, 6, 20, 12); s.r("#e8d8b0", 67, 7, 18, 10); s.r("#f28b82", 71, 10, 10, 4)
    s.r("#8b5a3c", 44, 30, 26, 2); s.r("#6d4630", 46, 32, 2, 10); s.r("#6d4630", 66, 32, 2, 10)
    s.r("#1e1e26", 50, 16, 16, 12); s.r("#2b2b33", 56, 28, 4, 2)
    s.r("#6b5a48", 0, 38, W, H - 38); s.r("#5a4a3a", 0, 38, W, 1)
    for x in range(4, W, 12): s.r("#5a4a3a", x, 43, 6, 1)
    s.a("tela", x=51, y=17, w=14, h=10)
    return s


# ---------------------------------------------------------------- Torre do mago (magos do código, Supporters Pack)
def torre():
    s = Sala("torre", "Torre do mago", "O mago do código conjura feitiços, o caldeirão borbulha e as estrelas piscam na janela.",
             ["magos-hd"], "#9c6cf0", 42, 24)
    s.lugar(24, "trabalhar", min=4, max=8)
    s.lugar(62, "parado", direita=False, min=2.5, max=4)
    # rascunho para o PixelLab (a arte final mudou a janela para o meio e a vela para a esquerda)
    s.r("#2a2438", 0, 0, W, 42)
    for y in range(0, 42, 5):
        for x in range((y // 5 % 2) * 4 - 4, W, 8): s.r("#332c44", x, y, 7, 4)
    s.r("#1a1530", 12, 6, 14, 20); s.r("#3d3556", 11, 5, 16, 1); s.r("#3d3556", 11, 26, 16, 1)
    s.r("#0e1030", 13, 7, 12, 18); s.r("#f4f1d0", 19, 10, 4, 4); s.r("#0e1030", 20, 10, 2, 2)
    s.r("#5d4037", 66, 6, 22, 36); s.r("#4e342e", 67, 7, 20, 34)
    for y in (8, 17, 26, 35):
        s.r("#6d4c41", 67, y + 6, 20, 1)
        for j, c in enumerate(["#7e57c2", "#26a69a", "#ef5350", "#ffca3a", "#42a5f5", "#8d6e63", "#ab47bc"]): s.r(c, 68 + j * 3, y, 2, 6)
    s.r("#37474f", 36, 34, 14, 8); s.r("#263238", 35, 33, 16, 2); s.r("#4dd0e1", 37, 33, 12, 1)
    s.r("#3e2e22", 0, 42, W, 8); s.r("#4a3728", 0, 42, W, 1)
    for x in range(0, W, 12): s.r("#33261c", x, 46, 10, 1)
    s.r("#7b1fa2", 30, 44, 30, 4); s.r("#9c27b0", 31, 45, 28, 2)
    s.r("#fff3c4", 57, 26, 2, 4); s.r("#5d4037", 56, 30, 4, 3)
    s.arte("torre.png", chao=44)
    s.a("piscar", pontos=[[38, 14], [50, 12], [41, 22], [53, 19], [47, 25]], cores=["#fff6c8", "#cfd8ff"], vel=2.3, limiar=-0.3)
    s.a("particulas", area=[34, 22, 12, 7], cores=["#4dd0e1", "#b388ff"], n=3, vx=0, vy=-4, tam=[1, 1])
    s.a("luz", x=12, y=21, raio=10, cor="#ffb74d", pronto="#8ff0b0", erro="#ff5050")
    return s


SALAS = [escritorio, mina, jardim, covil, floresta, fabrica, quintal, sala_gatos, polo, formigueiro, oficina, nave, estudio, generica, torre]


if __name__ == "__main__":
    for make in SALAS:
        p = make().salvar()
        print(os.path.relpath(p, os.path.join(HERE, "..")))
