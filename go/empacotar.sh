#!/bin/sh
# Monta dist/cmdcrew-<sistema>-<arquitetura>.tar.gz para macOS e Linux (rodar na raiz do repositório ou em go/).
# O Ebitengine não precisa de cgo nesses sistemas, então tudo sai de uma máquina só (inclusive do Windows, com Git Bash).
#
#   sh go/empacotar.sh                 todos
#   sh go/empacotar.sh linux/amd64     só um
set -eu
cd "$(dirname "$0")/.."
root=$(pwd)
targets=${*:-"darwin/arm64 darwin/amd64 linux/amd64 linux/arm64"}
mkdir -p dist
for t in $targets; do
  os=${t%/*}; arch=${t#*/}
  work=$(mktemp -d)
  pkg="$work/cmdcrew"
  mkdir -p "$pkg/bin" "$pkg/recursos/sons"
  (cd go && GOOS=$os GOARCH=$arch CGO_ENABLED=0 go build -trimpath -ldflags="-s -w" -o "$pkg/bin/cmdcrew" .)
  cp -R personagens salas agentes "$pkg/"
  cp src/recursos/PixelifySans.ttf src/recursos/OFL-PixelifySans.txt "$pkg/recursos/"
  cp src/recursos/sons/*.wav "$pkg/recursos/sons/"
  cp LICENSE "$pkg/"
  tar -C "$work" -czf "dist/cmdcrew-$os-$arch.tar.gz" cmdcrew
  rm -rf "$work"
  echo "dist/cmdcrew-$os-$arch.tar.gz"
done
