#!/usr/bin/env bash
# Prueba de check_refs.sh con un proyecto falso: una referencia rota, una buena y una interna de Unity.
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
proj=$(mktemp -d)
trap 'rm -rf "$proj"' EXIT
mkdir -p "$proj/Assets" "$proj/Packages"

ok=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
missing=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
echo "guid: $ok" > "$proj/Assets/Good.cs.meta"
cat > "$proj/Assets/PF_Test.prefab" <<EOF
  m_Script: {fileID: 11500000, guid: $ok, type: 3}
  m_Script: {fileID: 11500000, guid: $missing, type: 3}
  m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}
EOF

out=$(bash "$here/check_refs.sh" "$proj")
expected="$missing  Assets/PF_Test.prefab"
[ "$out" = "$expected" ] || { echo "FALLA. Esperado: '$expected'  Obtenido: '$out'"; exit 1; }

echo "guid: $missing" > "$proj/Assets/Fixed.cs.meta"
out=$(bash "$here/check_refs.sh" "$proj")
[ -z "$out" ] || { echo "FALLA: con todo arreglado debería salir vacío. Obtenido: '$out'"; exit 1; }
echo "OK"
