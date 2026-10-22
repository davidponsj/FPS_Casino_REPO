#!/usr/bin/env bash
# Lista los GUIDs que usan escenas, prefabs y assets de Assets/ pero que no existen en ningún .meta
# (= "Missing Script" o referencia perdida). Salida: "guid  archivo-que-lo-usa", una línea por par.
# Uso: bash tasks/check_refs.sh [carpeta-del-proyecto]
set -euo pipefail

PROJ="${1:-$(dirname "$0")/../FPS_CASINO_PROJECT}"
cd "$PROJ"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

# GUIDs que existen: los .meta del proyecto y de los paquetes (Packages/ y la caché de Library).
dirs=(Assets Packages)
[ -d Library/PackageCache ] && dirs+=(Library/PackageCache)
# find + xargs: grep -r con --include tarda minutos en recorrer Library/PackageCache.
find "${dirs[@]}" -name '*.meta' -print0 | xargs -0 grep -hoE '^guid: [0-9a-f]{32}' | cut -c7- | sort -u > "$tmp/known"

# GUIDs usados. Los que empiezan por 16 ceros son recursos internos de Unity (no tienen .meta).
grep -rhoE --include='*.unity' --include='*.prefab' --include='*.asset' --include='*.mat' --include='*.controller' \
    'guid: [0-9a-f]{32}' Assets | cut -c7- | grep -v '^0000000000000000' | sort -u > "$tmp/used"

comm -23 "$tmp/used" "$tmp/known" > "$tmp/missing"
[ -s "$tmp/missing" ] || exit 0

grep -rlF --include='*.unity' --include='*.prefab' --include='*.asset' --include='*.mat' --include='*.controller' \
    -f "$tmp/missing" Assets | sort | while read -r file; do
    grep -oF -f "$tmp/missing" "$file" | sort -u | sed "s|\$|  $file|"
done
