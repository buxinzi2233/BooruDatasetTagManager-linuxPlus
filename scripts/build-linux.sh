#!/usr/bin/env bash
# Build and test Linux-portable Bdtm libraries (no WinForms).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "==> Restoring Bdtm.Core"
dotnet restore src/Bdtm.Core/Bdtm.Core.csproj

echo "==> Building Bdtm.Core"
dotnet build src/Bdtm.Core/Bdtm.Core.csproj -c Release --no-restore

echo "==> Restoring Bdtm.Onnx"
dotnet restore src/Bdtm.Onnx/Bdtm.Onnx.csproj

echo "==> Building Bdtm.Onnx"
dotnet build src/Bdtm.Onnx/Bdtm.Onnx.csproj -c Release --no-restore

echo "==> Running Bdtm.Core.Tests"
dotnet test tests/Bdtm.Core.Tests/Bdtm.Core.Tests.csproj -c Release --nologo

echo "==> Running Bdtm.Onnx.Tests"
dotnet test tests/Bdtm.Onnx.Tests/Bdtm.Onnx.Tests.csproj -c Release --nologo

echo "==> Done"
