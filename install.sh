#!/bin/sh
# CmdCrew para macOS e Linux: baixa a última versão, instala e abre o assistente.
#
#   curl -fsSL https://raw.githubusercontent.com/Serignolli/cmdcrew/main/install.sh | sh
#
# Instala em ~/.local/share/cmdcrew (Linux) ou ~/Library/Application Support/CmdCrew/app (macOS),
# com o atalho ~/.local/bin/cmdcrew. Não precisa de sudo. Rodar de novo atualiza (a configuração fica).
set -eu

REPO="Serignolli/cmdcrew"

case "$(uname -s)" in
  Darwin) os=darwin ;;
  Linux) os=linux ;;
  *) echo "Este instalador é para macOS e Linux. No Windows, baixe o CmdCrew-Setup.exe:"
     echo "  https://github.com/$REPO/releases/latest/download/CmdCrew-Setup.exe"
     exit 1 ;;
esac
case "$(uname -m)" in
  x86_64|amd64) arch=amd64 ;;
  arm64|aarch64) arch=arm64 ;;
  *) echo "Processador não suportado: $(uname -m)"; exit 1 ;;
esac

if [ "$os" = darwin ]; then
  dest="$HOME/Library/Application Support/CmdCrew/app"
else
  dest="${XDG_DATA_HOME:-$HOME/.local/share}/cmdcrew"
fi
url="https://github.com/$REPO/releases/latest/download/cmdcrew-$os-$arch.tar.gz"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

echo "Baixando o CmdCrew ($os/$arch)..."
if command -v curl >/dev/null 2>&1; then
  curl -fsSL "$url" -o "$tmp/cmdcrew.tar.gz"
else
  wget -q "$url" -O "$tmp/cmdcrew.tar.gz"
fi
tar -xzf "$tmp/cmdcrew.tar.gz" -C "$tmp"

updating=false
[ -f "$dest/config.json" ] && updating=true
[ -x "$dest/bin/cmdcrew" ] && "$dest/bin/cmdcrew" quit >/dev/null 2>&1 || true

# atualização: troca o programa, os personagens e as salas; config.json e pacotes/ ficam
mkdir -p "$dest"
rm -rf "$dest/bin" "$dest/personagens" "$dest/salas" "$dest/agentes" "$dest/recursos"
cp -R "$tmp/cmdcrew/." "$dest/"
chmod +x "$dest/bin/cmdcrew"
[ "$os" = darwin ] && xattr -dr com.apple.quarantine "$dest" 2>/dev/null || true

mkdir -p "$HOME/.local/bin"
ln -sf "$dest/bin/cmdcrew" "$HOME/.local/bin/cmdcrew"
echo "Instalado em $dest"

case ":$PATH:" in
  *":$HOME/.local/bin:"*) ;;
  *) echo
     echo "Dica: ~/.local/bin não está no seu PATH. Para usar o comando cmdcrew, adicione ao seu shell:"
     echo "  export PATH=\"\$HOME/.local/bin:\$PATH\"" ;;
esac

if [ "$updating" = true ]; then
  # os ganchos apontam para o mesmo lugar; liga de novo para pegar agentes e eventos novos
  "$dest/bin/cmdcrew" install "$("$dest/bin/cmdcrew" agentes-ligados 2>/dev/null || echo detectados)"
  echo "CmdCrew atualizado! A sua configuração continua a mesma. Para mudar: cmdcrew config"
elif [ -r /dev/tty ]; then
  "$dest/bin/cmdcrew" setup < /dev/tty
else
  "$dest/bin/cmdcrew" install
  echo "Pronto! Para escolher a equipe: cmdcrew config"
fi
