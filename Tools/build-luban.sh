#!/usr/bin/env bash
set -euo pipefail
TOOLS_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
LUBAN_REPO="https://github.com/focus-creative-games/luban.git"
LUBAN_DIR="$TOOLS_ROOT/luban-source"
LUBAN_OUT="$TOOLS_ROOT/Luban"

if ! command -v git >/dev/null 2>&1; then
    printf '[ERROR] Git was not found.\n' >&2
    exit 1
fi
if ! command -v dotnet >/dev/null 2>&1; then
    printf '[ERROR] Install the .NET SDK required by Luban.\n' >&2
    exit 1
fi

if [[ ! -d "$LUBAN_DIR/.git" ]]; then
    printf '[INFO] Cloning Luban from %s ...\n' "$LUBAN_REPO"
    git clone "$LUBAN_REPO" "$LUBAN_DIR"
else
    printf '[INFO] Updating Luban source ...\n'
    git -C "$LUBAN_DIR" fetch origin
    git -C "$LUBAN_DIR" pull --ff-only
fi

printf '[INFO] Luban source revision:\n'
git -C "$LUBAN_DIR" rev-parse HEAD
printf '[INFO] Building Luban into %s ...\n' "$LUBAN_OUT"
dotnet build "$LUBAN_DIR/src/Luban/Luban.csproj" -c Release -o "$LUBAN_OUT"
git -C "$LUBAN_DIR" rev-parse HEAD > "$LUBAN_OUT/source-revision.txt"
printf '[INFO] Luban build succeeded.\n'
