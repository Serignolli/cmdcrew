"""Descrições dos personagens (português e inglês), num lugar só.

    python ferramentas/descricoes.py

Atualiza o campo "descricao" de personagens/*.json (e dos HD em supporters-pack/personagens/, se a pasta existir)
e o inglês do site (site/i18n.js). Depois rode python ferramentas/gerar_site.py para o site pegar o português.
"""
import json, os, re

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, ".."))

DESCRICOES = {
    "chocolateiros": (
        "Operários da fábrica de chocolate que martelam o código até ele ficar crocante. Cada bug corrigido vira uma barra de chocolate e uma musiquinha de moral.",
        "Chocolate factory workers who hammer the code until it's crunchy. Every bug fixed becomes a chocolate bar and a little moral song."),
    "clawd": (
        "O mascote do Claude Code em pessoa. Digita mais rápido que o seu linter e solta estrelinhas a cada função que compila de primeira.",
        "Claude Code's mascot in the flesh. Types faster than your linter and gives off sparkles every time a function compiles on the first try."),
    "claude": (
        "O ícone do Claude ganhou perninhas e fugiu do spinner. Pulsa no ritmo do \"Thinking…\" e carrega um terminal inteiro na cabeça.",
        "Claude's icon grew little legs and ran away from the spinner. It pulses to the beat of \"Thinking…\" and carries a whole terminal on its head."),
    "dragao": (
        "Guardião do tesouro do repositório. Voa em cima do terminal e cospe fogo no código legado até sobrar só o que presta.",
        "Guardian of the repository's treasure. Flies over the terminal and breathes fire on legacy code until only the good parts are left."),
    "elfos": (
        "Caçadores de bugs da floresta. Enxergam um ponto e vírgula faltando a 200 linhas de distância e acertam com uma flecha.",
        "Bug hunters from the woods. They spot a missing semicolon 200 lines away and hit it with an arrow."),
    "gnomos": (
        "Anões barbudos que escavam o código-fonte atrás de gemas raras: funções reaproveitáveis e, de vez em quando, um bug fossilizado.",
        "Bearded dwarves who dig through the source code for rare gems: reusable functions and, every now and then, a fossilized bug."),
    "gatos": (
        "Revisores sêniores de código. Avaliam o PR deitando em cima do teclado e aprovam qualquer coisa em troca de um peixe.",
        "Senior code reviewers. They review the PR by lying on the keyboard and approve anything in exchange for a fish."),
    "pinguins": (
        "A equipe de refrigeração do data center. Deixam o processador geladinho, bamboleiam entre os servidores e aceitam pagamento em peixe fresco.",
        "The data center's cooling crew. They keep the processor nice and chilly, waddle between the servers and take payment in fresh fish."),
    "formigas": (
        "Uma colônia inteira em fila indiana carregando bytes. Cada cubinho de açúcar é um pacote de dados entregue no prazo.",
        "A whole colony marching single file and carrying bytes. Every sugar cube is a data packet delivered on time."),
    "robos": (
        "Robozinhos de manutenção que soldam circuitos, apertam os parafusos soltos do back-end e apitam de alegria quando os testes passam.",
        "Little maintenance robots that solder circuits, tighten the back-end's loose screws and beep with joy when the tests pass."),
    "robos-classicos": (
        "Uma tripulação de ficção científica: o baixinho que apita em binário, o dourado que traduz stack traces e o aspirador que limpa o cache.",
        "A science fiction crew: the short one that beeps in binary, the golden one that translates stack traces and the vacuum that clears the cache."),
    # Supporters Pack
    "anoes": (
        "Anões mineradores de código. Picam pedra atrás dos veios de ouro do repositório, empurram o carrinho de commits e carregam os bugs para fora da mina em sacos.",
        "Code-mining dwarves. They chip away at the repository looking for veins of gold, push the commit cart and haul the bugs out of the mine in sacks."),
    "elfos-hd": (
        "Elfos arqueiros caçadores de bugs. Acham o erro antes do compilador e acertam bem no meio da stack trace.",
        "Bug-hunting elf archers. They find the error before the compiler does and hit it right in the middle of the stack trace."),
}


def set_json_description(path, text):
    """troca só o valor de "descricao" no arquivo, sem mexer na formatação do resto"""
    s = open(path, encoding="utf-8").read()
    new = re.sub(r'("descricao":\s*)"(?:[^"\\]|\\.)*"', lambda m: m.group(1) + json.dumps(text, ensure_ascii=False), s, count=1)
    if new != s:
        open(path, "w", encoding="utf-8", newline="\n").write(new)
        return True
    return False


def main():
    changed = []
    for folder in ("personagens", os.path.join("supporters-pack", "personagens")):
        d = os.path.join(ROOT, folder)
        if not os.path.isdir(d):
            continue
        for f in sorted(os.listdir(d)):
            cid = f[:-5]
            if f.endswith(".json") and cid in DESCRICOES and set_json_description(os.path.join(d, f), DESCRICOES[cid][0]):
                changed.append(os.path.join(folder, f))
    # inglês do site: "descricao" dentro de CHARS_EN.<id>
    p = os.path.join(ROOT, "site", "i18n.js")
    s = open(p, encoding="utf-8").read()
    for cid, (_, en) in DESCRICOES.items():
        key = re.escape(cid if re.match(r"^\w+$", cid) else '"' + cid + '"')
        s = re.sub(r'(\n    ' + key + r': \{\n      nome: "[^"]*",\n      descricao: )"(?:[^"\\]|\\.)*"',
                   lambda m: m.group(1) + json.dumps(en, ensure_ascii=False), s)
    open(p, "w", encoding="utf-8", newline="").write(s)
    print("\n".join(changed) or "nada mudou nos personagens")


if __name__ == "__main__":
    main()
