# Lo usan compile.sh y test.sh (con "source"): para si Unity tiene abierto el proyecto $PROJ.
# Si queda un Temp/UnityLockfile sin ningún Unity abierto (pasa cuando una ejecución en batch aborta), lo borra.
if [ -f "$PROJ/Temp/UnityLockfile" ]; then
    abierto=$(powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Where-Object { \$_.CommandLine -like '*FPS_CASINO_PROJECT*' } | Measure-Object | Select-Object -ExpandProperty Count")
    if [ "${abierto//[^0-9]/}" != "0" ]; then
        echo "Unity tiene el proyecto abierto: ciérralo antes."
        exit 2
    fi
    rm -f "$PROJ/Temp/UnityLockfile"
fi
