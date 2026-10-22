#!/usr/bin/env bash
# Compila el proyecto en batch (con Unity CERRADO) y muestra los errores y warnings de C#.
# Sale con código distinto de 0 si no compila. Tarda unos 20 s.
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe"
PROJ="$(cd "$(dirname "$0")/../FPS_CASINO_PROJECT" && pwd)"
LOG="$(mktemp)"
trap 'rm -f "$LOG"' EXIT

source "$(dirname "$0")/unity_closed.sh"
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" -logFile "$LOG"
code=$?
grep -E "(error|warning) CS" "$LOG" | sort -u
[ $code -eq 0 ] && echo "Compila OK" || echo "NO COMPILA (código $code)"
exit $code
