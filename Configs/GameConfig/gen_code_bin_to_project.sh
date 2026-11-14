#!/usr/bin/env bash
set -euo pipefail
CONFIG_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
WORKSPACE="$(cd -- "$CONFIG_ROOT/../.." && pwd)"
LUBAN_DLL="${LUBAN_DLL:-$WORKSPACE/Tools/Luban/Luban.dll}"

if [[ ! -f "$LUBAN_DLL" ]]; then
    printf 'Luban.dll not found: %s\n' "$LUBAN_DLL" >&2
    printf 'Install the agreed Luban release manually or set LUBAN_DLL before exporting.\n' >&2
    exit 1
fi
if ! command -v dotnet >/dev/null 2>&1; then
    printf 'Install the dotnet runtime required by the agreed Luban release.\n' >&2
    exit 1
fi

cd -- "$CONFIG_ROOT"
dotnet "$LUBAN_DLL" -t client -c cs-bin -d bin --conf "$CONFIG_ROOT/luban.conf" \
    -x code.lineEnding=lf \
    -x "outputCodeDir=$WORKSPACE/MechStorm_Unity/Assets/GameScripts/HotFix/GameProto/GameConfig" \
    -x "outputDataDir=$WORKSPACE/MechStorm_Unity/Assets/AssetRaw/Configs/bytes"
