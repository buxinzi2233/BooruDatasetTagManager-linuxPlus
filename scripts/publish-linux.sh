#!/usr/bin/env bash
# Publish self-contained linux-x64 Avalonia MVP.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

OUT="${1:-dist/linux-x64}"
mkdir -p "$OUT"

echo "==> Publishing Bdtm.Avalonia -> $OUT"
dotnet publish src/Bdtm.Avalonia/Bdtm.Avalonia.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -o "$OUT"

# Optional desktop entry helper
cat > "$OUT/bdtm.desktop" <<DESK
[Desktop Entry]
Type=Application
Name=Booru Dataset Tag Manager (Linux MVP)
Exec=$(pwd)/$OUT/Bdtm.Avalonia
Terminal=false
Categories=Graphics;Utility;
DESK

echo "==> Published to $OUT"
ls -la "$OUT" | head -30
