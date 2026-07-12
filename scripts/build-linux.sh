#!/usr/bin/env bash
# Build and test Linux-portable Bdtm libraries + Avalonia app (no WinForms).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "==> Restoring / building Core"
dotnet restore src/Bdtm.Core/Bdtm.Core.csproj
dotnet build src/Bdtm.Core/Bdtm.Core.csproj -c Release --no-restore

echo "==> Restoring / building Onnx"
dotnet restore src/Bdtm.Onnx/Bdtm.Onnx.csproj
dotnet build src/Bdtm.Onnx/Bdtm.Onnx.csproj -c Release --no-restore

echo "==> Restoring / building Avalonia"
dotnet restore src/Bdtm.Avalonia/Bdtm.Avalonia.csproj
dotnet build src/Bdtm.Avalonia/Bdtm.Avalonia.csproj -c Release --no-restore

echo "==> Running Bdtm.Core.Tests"
dotnet test tests/Bdtm.Core.Tests/Bdtm.Core.Tests.csproj -c Release --nologo

echo "==> Running Bdtm.Onnx.Tests"
dotnet test tests/Bdtm.Onnx.Tests/Bdtm.Onnx.Tests.csproj -c Release --nologo

echo "==> Done"
