"""Personagens simples (16×16, desenhados à mão) que ainda vão ganhar versão detalhada:
dragão, elfos, cachorros, programadores e robôs clássicos.

    python ferramentas/simples_novos.py

Cada quadro é uma lista de 16 linhas; cada letra é uma cor da skin e "." é transparente.
Desenhe virado para a direita (o espelhamento é automático).
"""
import json, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from build import dump

OUT = os.path.join(HERE, "..", "personagens")


def F(*rows):
    """Um quadro: completa até 16 linhas (em cima) e confere a largura."""
    rows = list(rows)
    for r in rows:
        assert len(r) <= 16, (len(r), r)
    rows = ["." * 16] * (16 - len(rows)) + [r.ljust(16, ".") for r in rows]
    assert len(rows) == 16, len(rows)
    return rows


def save(d):
    path = os.path.join(OUT, d["id"] + ".json")
    open(path, "w", encoding="utf-8").write(dump(d))
    print("gravado", os.path.relpath(path, os.path.join(HERE, "..")))


# ---------------------------------------------------------------- dragão
# B corpo, D escuro (costas, contorno), L barriga, W asa, V nervura da asa, K olho, N dente, R boca, C garra.
# Ele voa ("voo"): os quadros de andar são o bater de asas.

def dragao():
    head = ["............DD..", "...........BBBB.", "..........BBKBBB"]
    up = F(
        "................",
        "...V............",
        "..VWV...........",
        "..VWWV......DD..",
        "..VWWWV....BBBB.",
        "...VWWWV..BBKBBB",
        "....VWWD.BBBBBBN",
        ".D...DBBBBBB....",
        "DBD.DBBBBBBB....",
        ".DBBBBLLLLLB....",
        "..DDBBLLLLB.....",
        ".....BB..BB.....",
        ".....C...C......",
        "................")
    mid = F(
        "................",
        "................",
        "................",
        "............DD..",
        "...........BBBB.",
        "VV........BBKBBB",
        ".VWVV....BBBBBBN",
        ".D.VWWVDBBBB....",
        "DBD.VWWVBBBB....",
        ".DBBBBLLLLLB....",
        "..DDBBLLLLB.....",
        ".....BB..BB.....",
        ".....C...C......",
        "................")
    down = F(
        "................",
        "................",
        "................",
        "............DD..",
        "...........BBBB.",
        "..........BBKBBB",
        ".........BBBBBBN",
        ".D...DBBBBBB....",
        "DBD.DBBBBBBB....",
        ".DBBVWWWWVLB....",
        "..DVWWWWVLB.....",
        "..VWWWWVBBB.....",
        "...VWWV..C......",
        "....VV..........")
    # cuspindo fogo: boca aberta (o fogo em si é o efeito "fogo")
    fire_up = F(
        "................",
        "...V............",
        "..VWV...........",
        "..VWWV......DD..",
        "..VWWWV....BBBB.",
        "...VWWWV..BBKBBB",
        "....VWWD.BBBBRR.",
        ".D...DBBBBBBBBN.",
        "DBD.DBBBBBBB....",
        ".DBBBBLLLLLB....",
        "..DDBBLLLLB.....",
        ".....BB..BB.....",
        ".....C...C......",
        "................")
    fire_down = F(
        "................",
        "................",
        "................",
        "............DD..",
        "...........BBBB.",
        "..........BBKBBB",
        ".........BBBBRR.",
        ".D...DBBBBBBBBN.",
        "DBD.DBBBBBBB....",
        ".DBBVWWWWVLB....",
        "..DVWWWWVLB.....",
        "..VWWWWVBBB.....",
        "...VWWV..C......",
        "....VV..........")
    save({
        "id": "dragao", "nome": "Dragão", "categoria": "Fantasia",
        "descricao": "Voa em cima do terminal batendo as asas e cospe fogo quando está trabalhando. Guarda as gemas do seu código.",
        "voo": 5, "efeito": "fogo", "carga": "gema",
        "sprites": {"andar": [up, mid, down, mid], "trabalhar": [fire_up, fire_down], "parado": [mid]},
        "skins": [
            {"id": "verde", "nome": "Verde da floresta", "cores": {"B": "#43a047", "D": "#1b5e20", "L": "#fff59d", "W": "#81c784", "V": "#2e7d32",
                                                               "K": "#1a1a1a", "N": "#ffffff", "R": "#d32f2f", "C": "#eeeeee"}},
            {"id": "vermelho", "nome": "Vermelho do vulcão", "cores": {"B": "#e53935", "D": "#8e0000", "L": "#ffcc80", "W": "#ef9a9a", "V": "#b71c1c"}},
            {"id": "roxo", "nome": "Roxo da noite", "cores": {"B": "#7e57c2", "D": "#311b92", "L": "#d1c4e9", "W": "#b39ddb", "V": "#4527a0", "K": "#ffeb3b"}},
            {"id": "dourado", "nome": "Dourado do tesouro", "cores": {"B": "#fbc02d", "D": "#f57f17", "L": "#fff8e1", "W": "#ffe082", "V": "#ff8f00"}},
            {"id": "gelo", "nome": "Azul do gelo", "cores": {"B": "#4fc3f7", "D": "#01579b", "L": "#e1f5fe", "W": "#b3e5fc", "V": "#0277bd", "R": "#1565c0"}},
        ],
        "verbos": ["Cuspindo fogo no bug", "Chocando o ovo da feature", "Guardando o tesouro do repositório", "Voando sobre o código",
                   "Esquentando o servidor", "Contando as gemas", "Assustando os cavaleiros do QA", "Derretendo o legado",
                   "Soprando fumaça", "Afiando as garras", "Batendo as asas"],
        "dicas": ["Dragões não fazem deploy na sexta. Eles incendeiam na quinta.", "Todo dragão guarda um tesouro: o seu é o histórico do git.",
                  "Se o build quebrar, o dragão cospe fogo. Se passar, também."],
        "falas": ["ROOOAR!", "Quem mexeu no meu tesouro?", "Esse bug vai virar churrasco", "Tá quente aqui, né?",
                  "Mais uma gema pro monte!", "Cheiro de código queimado", "Voa, dragãozinho!", "Pfffff..."],
        "falasFim": ["Tesouro guardado!", "Tudo tostadinho!", "Missão cumprida, ROAR!", "De volta pra caverna!"],
    })


# ---------------------------------------------------------------- elfos
# H cabelo, S pele, K olho, M boca, T túnica, D cinto/detalhe, L calça, O bota, W arco, A corda/flecha.

def elfos():
    head = [
        ".....HHHHH......",
        "....HHHHHHH.....",
        ".S.HHSSSSSHH.S..",
        "..SHSKSSSKSHS...",
        "...HSSSSSSSH....",
        "...HHSSMSSHH....",
        "...HH.SSS.HH....",
    ]
    walk_a = F(*head,
        "....TTTTTTT.....",
        "...TTTTTTTTT....",
        "..STTDDDDDTTS...",
        "....TTTTTTT.....",
        "....LL...LL.....",
        "....LL...LL.....",
        "...OOO...OOO....",
        "................")
    walk_b = F(*head[1:],
        "....TTTTTTT.....",
        "...TTTTTTTTT....",
        "..STTDDDDDTTS...",
        "....TTTTTTT.....",
        ".....LL.LL......",
        ".....LL.LL......",
        "....OOO.OOO.....",
        "................",
        "................")
    # Arco: a madeira (W, marrom) fica parada à frente do elfo; só a corda (A, cinza) se mexe.
    wood = [(13, 4), (14, 5), (15, 6), (15, 7), (15, 8), (15, 9), (15, 10), (14, 11), (13, 12)]
    def with_bow(string, extra):
        g = [list(r) for r in walk_a]
        g[10][12] = "."                      # a mão direita sai do lado do corpo para segurar o arco
        for x, y in string: g[y][x] = "A"
        for x, y in wood: g[y][x] = "W"
        g[8][15] = "S"                       # mão da frente segurando a madeira
        for x, y, c in extra: g[y][x] = c
        return ["".join(r) for r in g]
    # puxando: a corda vira um "V" até a mão (perto do peito), com a flecha (Q haste, A ponta) encaixada
    drawn = with_bow([(13, 5), (12, 6), (11, 7), (11, 9), (12, 10), (13, 11)],
                     [(10, 8, "S"), (11, 8, "Q"), (12, 8, "Q"), (13, 8, "Q"), (14, 8, "A")])
    # soltou: corda reta de ponta a ponta, sem flecha (ela sai voando no efeito "flechas")
    released = with_bow([(13, y) for y in range(5, 12)], [(12, 10, "S")])
    save({
        "id": "elfos", "nome": "Elfos", "categoria": "Fantasia",
        "descricao": "Ágeis e organizados. Andam leves por cima do terminal e acertam cada bug com uma flecha.",
        "efeito": "flechas", "carga": "folha",
        "sprites": {"andar": [walk_a, walk_b], "trabalhar": [drawn, drawn, released]},
        "skins": [
            {"id": "floresta", "nome": "Floresta (cabelo dourado)", "cores": {"H": "#f5d76e", "S": "#f1c27d", "K": "#1b5e20", "M": "#c0392b",
                                                                            "T": "#2e7d32", "D": "#6d4c41", "L": "#5d4037", "O": "#3e2723",
                                                                            "W": "#795548", "A": "#cfd8dc", "Q": "#bcaaa4"}},
            {"id": "noite", "nome": "Noite (cabelo prateado)", "cores": {"H": "#e0e6ea", "S": "#e8c9ad", "K": "#283593", "T": "#283593", "D": "#90a4ae",
                                                                         "L": "#1a237e", "O": "#263238"}},
            {"id": "outono", "nome": "Outono (ruivo)", "cores": {"H": "#d84315", "S": "#f5cba7", "K": "#4e342e", "T": "#ef6c00", "D": "#5d4037", "L": "#6d4c41"}},
            {"id": "neve", "nome": "Neve", "cores": {"H": "#ffffff", "S": "#ffe0d0", "K": "#0277bd", "T": "#e3f2fd", "D": "#64b5f6", "L": "#b0bec5",
                                                     "O": "#78909c"}},
        ],
        "verbos": ["Mirando no bug", "Afinando a harpa", "Colhendo ervas do backlog", "Lendo runas antigas", "Tecendo a documentação",
                   "Andando sem fazer barulho", "Encantando as funções", "Consultando as estrelas", "Trançando o cabelo",
                   "Protegendo a floresta de dependências"],
        "dicas": ["Um elfo nunca erra a flecha. Às vezes o bug que se mexe.", "Código élfico: bonito, antigo e ninguém mais sabe mexer.",
                  "Os elfos acham seu README muito curto."],
        "falas": ["Na mosca!", "Bug abatido!", "Que floresta bonita de pastas", "Ouço um bug se aproximando",
                  "Mais uma flecha!", "Isso é código élfico antigo", "Silêncio, estou mirando", "A floresta agradece"],
        "falasFim": ["Todos os bugs abatidos!", "Até a próxima lua!", "A floresta está em paz!", "Missão élfica cumprida!"],
    })


# ---------------------------------------------------------------- cachorros
# F pelo, D mancha, E orelha, K olho, N focinho, M língua, P pata.

def cachorros():
    head = ["...........FFF..", "..........FFFKF."]
    walk_a = F(*head,
        ".F.......EFFFFFN",
        ".F.......EFFFFF.",
        "..F......EFF.M..",
        "..FFFFFFFFFF....",
        "..FFDFFFFDFF....",
        "..FFFFFFFFFF....",
        "..F.F....F.F....",
        "..F.F....F.F....",
        "..P.P....P.P....")
    walk_b = F(*head,
        "F........EFFFFFN",
        ".F.......EFFFFF.",
        "..F......EFF.M..",
        "..FFFFFFFFFF....",
        "..FFDFFFFDFF....",
        "..FFFFFFFFFF....",
        "...FF.....FF....",
        "..F..F...F..F...",
        "..P..P...P..P...")
    bark_open = F(
        "...........FFF..",
        "..........FFFKF.",
        ".F.......EFFFFFN",
        ".F.......EFFFF..",
        "..F......EFFFMM.",
        "..F......FFF....",
        "..FFFFFFFFFF....",
        "..FFDFFFFDFF....",
        "..FFFFFFFFFF....",
        "..F.F....F.F....",
        "..F.F....F.F....",
        "..P.P....P.P....")
    dig_a = F(
        ".F..............",
        "..F.........FFF.",
        "..FFFFFFFFFEFFKF",
        "..FFDFFFFDFEFFFN",
        "..FFFFFFFFFEFM..",
        "..F.F....F.FF...",
        "..F.F.....F..F..",
        "..P.P.....P..P..")
    dig_b = F(
        "F...............",
        ".F..........FFF.",
        "..FFFFFFFFFEFFKF",
        "..FFDFFFFDFEFFFN",
        "..FFFFFFFFFEFM..",
        "..F.F....FF.....",
        "..F.F....F.F....",
        "..P.P...P...P...")
    save({
        "id": "cachorros", "nome": "Cachorros", "categoria": "Animais",
        "descricao": "Vira-lata caramelo e companhia. Latem para os bugs, cavam o repositório e trazem a bolinha de volta.",
        "efeito": "latidos", "carga": "osso",
        "efeitosTrabalho": {"trabalhar": "latidos", "trabalhar-cavar": "terra"},
        "sprites": {"andar": [walk_a, walk_b], "trabalhar": [bark_open, walk_a], "trabalhar-cavar": [dig_a, dig_b]},
        "skins": [
            {"id": "caramelo", "nome": "Vira-lata caramelo", "cores": {"F": "#d9914a", "D": "#c27a36", "E": "#8d5524", "K": "#1a1a1a",
                                                                       "N": "#1a1a1a", "M": "#ef5350", "P": "#f3e0c7"}},
            {"id": "chocolate", "nome": "Chocolate", "cores": {"F": "#6d4c41", "D": "#5d4037", "E": "#3e2723", "K": "#1a1a1a", "N": "#1a1a1a", "P": "#8d6e63"}},
            {"id": "dalmata", "nome": "Dálmata", "cores": {"F": "#fafafa", "D": "#212121", "E": "#212121", "P": "#fafafa"}},
            {"id": "cinza", "nome": "Cinzento", "cores": {"F": "#9e9e9e", "D": "#757575", "E": "#616161", "P": "#bdbdbd"}},
            {"id": "salsicha", "nome": "Salsicha", "cores": {"F": "#8d4a1f", "D": "#7a3e18", "E": "#5d2e10", "P": "#c68642"}},
        ],
        "verbos": ["Farejando o bug", "Enterrando o osso", "Buscando a bolinha", "Abanando o rabo", "Latindo para o carteiro",
                   "Cavando o repositório", "Pedindo petisco", "Correndo atrás do rabo", "Vigiando o servidor", "Farejando os logs"],
        "dicas": ["Quem é o bom garoto? O código que passa nos testes.", "Nenhum osso foi enterrado no main durante este deploy.",
                  "Os cachorros aceitam pagamento em petiscos."],
        "falas": ["Au au!", "Bolinha?!", "Achei um osso no código!", "Passeio?", "Quem é o bom garoto?", "Grrr... bug!",
                  "Posso subir no sofá?", "Cheiro de deploy!", "Petisco!", "Au!"],
        "falasFim": ["Au au! Pronto!", "Cadê meu petisco?", "Hora do passeio!", "Missão cumprida!"],
    })


# ---------------------------------------------------------------- programadores
# H cabelo, S pele, K olho, Z olheira, M boca, C moletom, D detalhe, P calça, O tênis,
# L notebook/controle, E tela/botões, X fone, Y almofada do fone.

def person(face, legs_together=False, hands=None):
    body = [
        "....CCCCCCCC....",
        "...CCCDCCDCCC...",
        "..SCCCCCCCCCCS..",
        "...CCCCCCCCCC...",
        "....PPPPPPPP....",
    ] if hands is None else hands
    legs = [
        "....PPP..PPP....",
        "....PPP..PPP....",
        "...OOOO..OOOO...",
    ] if not legs_together else [
        ".....PPP.PPP....",
        ".....PPP.PPP....",
        "....OOOO.OOOO...",
    ]
    return F(*face, *body, *legs)


TYPING_A = ["....CCCCCCCC....", "...CSLLLLLLCC...", "...CCLEEEELSC...", "...CCLLLLLLCC...", "....PPPPPPPP...."]
TYPING_B = ["....CCCCCCCC....", "...CCLLLLLLSC...", "...CSLEEEELCC...", "...CCLLLLLLCC...", "....PPPPPPPP...."]


def programadores():
    tired = [
        "....H.HHHH.H....",
        "....HHHHHHHH....",
        "....HSSSSSSH....",
        "....SKSSSSKS....",
        "....SZSSSSZS....",
        ".....SSMMSS.....",
        "......SSSS......",
    ]
    tired_up = tired[:1]  # cabelo bagunçado balança ao andar
    gamer = [
        ".....HHHHHH.....",
        "...XHHHHHHHHX...",
        "...XHSSSSSSHX...",
        "...YSKSSSSKSY...",
        "...YSSSSSSSSY...",
        ".....SSMMSS..X..",
        "......SSSS.XX...",
    ]
    hacker = [
        ".....CCCCCC.....",
        "....CCCCCCCC....",
        "....CSSSSSSC....",
        "....CKKKKKKC....",
        "....CSSSSSSC....",
        ".....CSMMSC.....",
        "......CSSC......",
    ]
    def sprites(face):
        return {
            "andar": [person(face), person(face, True)],
            "trabalhar": [person(face, hands=TYPING_A), person(face, hands=TYPING_B)],
        }
    save({
        "id": "programadores", "nome": "Programadores", "categoria": "Programação",
        "descricao": "O dev cansado com a caneca de café, o gamer de fone e o hacker de capuz. Digitam sem parar enquanto o Claude trabalha.",
        "efeito": "codigo", "carga": "cafe",
        "sprites": sprites(tired),
        "skins": [
            {"id": "cansado", "nome": "Dev cansado (com café)", "efeito": "zzz", "carga": "cafe",
             "cores": {"H": "#4e342e", "S": "#f1c27d", "K": "#1a1a1a", "Z": "#b39ddb", "M": "#8d6e63", "C": "#607d8b", "D": "#455a64",
                       "P": "#37474f", "O": "#eeeeee", "L": "#9e9e9e", "E": "#80deea"},
             "sprites": sprites(tired)},
            {"id": "cansada", "nome": "Dev cansada (com café)", "base": "cansado", "cores": {"H": "#1a1a1a", "S": "#8d5524", "C": "#ad1457", "D": "#880e4f"}},
            {"id": "gamer", "nome": "Gamer (fone e controle)", "efeito": "moedas", "carga": "disquete",
             "cores": {"H": "#d84315", "S": "#f5cba7", "K": "#1a1a1a", "M": "#c0392b", "C": "#2e7d32", "D": "#1b5e20", "P": "#1565c0",
                       "O": "#c62828", "X": "#212121", "Y": "#e53935", "L": "#424242", "E": "#ffca28"},
             "sprites": sprites(gamer)},
            {"id": "hacker", "nome": "Hacker (capuz e óculos escuros)", "efeito": "codigo", "carga": "pizza",
             "cores": {"S": "#8d5524", "K": "#0a0a0a", "M": "#5d4037", "C": "#212121", "D": "#00e676", "P": "#263238", "O": "#424242",
                       "L": "#1a1a1a", "E": "#00e676"},
             "sprites": sprites(hacker)},
        ],
        "verbos": ["Compilando", "Refatorando", "Caçando bugs", "Fazendo deploy na sexta", "Lendo a documentação",
                   "Resolvendo conflito de merge", "Tomando mais um café", "Escrevendo testes", "Culpando o cache",
                   "Consultando o Stack Overflow", "Reiniciando o servidor", "Revisando o pull request"],
        "dicas": ["Funciona na minha máquina é um estado de espírito, não uma garantia.",
                  "Todo bug corrigido às 18h de sexta volta segunda às 9h.", "Café não é dependência. É infraestrutura."],
        "falas": ["Funciona na minha máquina!", "Quem mexeu no main?", "É só um ponto e vírgula...", "Café acabou?!",
                  "Achei o bug!", "Hack the planet!", "git push --force? Não!", "Mais um console.log", "Era cache.", "Level up!"],
        "falasFim": ["Deploy feito!", "Todos os testes passaram!", "Merge aprovado!", "Commitado!"],
    })


# ---------------------------------------------------------------- robôs clássicos
# Cada um tem desenho próprio (sprites dentro da skin).

def robos_classicos():
    # astromecânico: W branco, B azul, G cinza, E olho, R luz vermelha
    def astro(eye_left, light):
        dome_eye = "....BWWEWWWB...." if eye_left else "....BWWWWEWB...."
        return F(
            "......BBBB......",
            ".....BWWWWB.....",
            dome_eye,
            "....WWWWWW" + ("R" if light else "W") + "W....",
            "....GGGGGGGG....",
            "...GWWBBWWWWG...",
            "...WWWBBWWWWW...",
            "...WWWWWWBBWW...",
            "...WWBBWWBBWW...",
            "...WWWWWWWWWW...",
            "..GG..WWWW..GG..",
            "..GG...GG...GG..",
            ".GGG..GGGG..GGG.")
    # dourado de protocolo: Y dourado, D dourado escuro, E olho, K juntas
    gold_head = [
        ".....YYYYY......",
        "....YYYYYYY.....",
        "....YEYYYEY.....",
        "....YYYYYYY.....",
        "....YYDDDYY.....",
        ".....YYYYY......",
        "......KKK.......",
    ]
    gold_a = F(*gold_head,
        "...YYYYYYYYY....",
        "..YYYDYYYDYYY...",
        "..Y.YYYYYYY.Y...",
        "..Y.YYDDDYY.Y...",
        "....KKKKKKK.....",
        "....YY...YY.....",
        "....YY...YY.....",
        "...YYY...YYY....")
    gold_b = F(*gold_head,
        "...YYYYYYYYY....",
        "..YYYDYYYDYYY...",
        "..Y.YYYYYYY.Y...",
        "..Y.YYDDDYY.Y...",
        "....KKKKKKK.....",
        ".....YY.YY......",
        ".....YY.YY......",
        "....YYY.YYY.....")
    gold_panic_a = F(
        ".Y...YYYYY...Y..",
        ".Y..YYYYYYY..Y..",
        "..Y.YEYYYEY.Y...",
        "..Y.YYYYYYY.Y...",
        "...YYYDDDYYY....",
        ".....YYYYY......",
        "......KKK.......",
        "....YYYYYYY.....",
        "...YYDYYYDYY....",
        "....YYYYYYY.....",
        "....YYDDDYY.....",
        "....KKKKKKK.....",
        "....YY...YY.....",
        "....YY...YY.....",
        "...YYY...YYY....")
    # aspirador: G cinza, B para-choque, L luz, W escova
    def vacuum(light, brush_a):
        return F(
            ".....GGGGGG.....",
            "...GGG" + ("L" if light else "G") + "GGGGGG...",
            "..BBBBBBBBBBBB..",
            "..BGGGGGGGGGGB..",
            "...W.......W...." if brush_a else "..W.W.....W.W...")
    save({
        "id": "robos-classicos", "nome": "Robôs clássicos", "categoria": "Outros",
        "descricao": "O baixinho que apita, o dourado que fala demais e o aspirador que não para. Inspirados nos robôs das ficções científicas.",
        "efeito": "faiscas", "carga": "engrenagem",
        "sprites": {"andar": [astro(True, False), astro(False, False)], "trabalhar": [astro(True, True), astro(False, False)]},
        "skins": [
            {"id": "astro", "nome": "Astromecânico (apita)", "efeito": "notas",
             "cores": {"W": "#eceff1", "B": "#1e88e5", "G": "#78909c", "E": "#212121", "R": "#e53935"}},
            {"id": "astro-vermelho", "nome": "Astromecânico vermelho", "base": "astro", "cores": {"B": "#d32f2f"}},
            {"id": "dourado", "nome": "Dourado de protocolo", "efeito": "estrelas",
             "cores": {"Y": "#fbc02d", "D": "#c49000", "E": "#fff176", "K": "#5d4037"},
             "sprites": {"andar": [gold_a, gold_b], "trabalhar": [gold_panic_a, gold_a]}},
            {"id": "aspirador", "nome": "Aspirador", "efeito": "poeira", "carga": "caixa",
             "cores": {"G": "#9e9e9e", "B": "#263238", "L": "#00e676", "W": "#eeeeee"},
             "sprites": {"andar": [vacuum(False, True), vacuum(False, False)], "trabalhar": [vacuum(True, True), vacuum(False, False)]}},
        ],
        "verbos": ["Calculando as probabilidades", "Apitando em binário", "Aspirando os logs", "Consultando o banco de memória",
                   "Traduzindo seis milhões de idiomas", "Recarregando as baterias", "Varrendo o repositório", "Girando a cúpula",
                   "Obedecendo às três leis", "Soldando o circuito"],
        "dicas": ["As chances de um deploy na sexta dar certo são de 3.720 para 1.", "Robôs de limpeza também varrem dependências não usadas.",
                  "Bip bop: este commit foi aprovado por um astromecânico."],
        "falas": ["Bip bop!", "Oh, não! Estamos perdidos!", "Vrrrrrr...", "Fui feito para sofrer", "Bip bip buuu!",
                  "Poeira detectada!", "Que desagradável!", "Bateria em 12%"],
        "falasFim": ["Bip bop, pronto!", "Graças ao criador!", "Voltando para a base!", "Tudo limpinho!"],
    })


if __name__ == "__main__":
    dragao(); elfos(); cachorros(); programadores(); robos_classicos()
