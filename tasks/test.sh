#!/usr/bin/env bash
# Ejecuta los tests EditMode en batch (con Unity CERRADO) y muestra el resumen y los que fallan.
# Sale con código distinto de 0 si algún test falla o no compila.
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe"
PROJ="$(cd "$(dirname "$0")/../FPS_CASINO_PROJECT" && pwd)"
# Ruta absoluta: con una relativa Unity guarda los resultados en AppData.
RESULTS="$(cd "$(dirname "$0")" && pwd)/test-results.xml"
LOG="$(mktemp)"
trap 'rm -f "$LOG"' EXIT

[ -f "$PROJ/Temp/UnityLockfile" ] && { echo "Unity tiene el proyecto abierto: ciérralo antes."; exit 2; }
rm -f "$RESULTS"
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -runTests -testPlatform EditMode -testResults "$RESULTS" -logFile "$LOG"
code=$?
grep -E "error CS" "$LOG" | sort -u
[ -f "$RESULTS" ] || { echo "No hay resultados (¿no compila?)"; exit 1; }
grep -oE '<test-run [^>]*' "$RESULTS" | grep -oE '(total|passed|failed|skipped)="[0-9]+"' | tr '\n' ' '; echo
grep -oE '<test-case [^>]*result="Failed"' "$RESULTS" | grep -oE 'fullname="[^"]*"'
exit $code
