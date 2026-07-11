#!/usr/bin/env bash
# Build and test the Linux-portable Bdtm.Core library (no WinForms/Avalonia yet).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "==> Restoring Bdtm.Core"
dotnet restore src/Bdtm.Core/Bdtm.Core.csproj

echo "==> Building Bdtm.Core"
dotnet build src/Bdtm.Core/Bdtm.Core.csproj -c Release --no-restore

echo "==> Restoring Bdtm.Core.Tests"
dotnet restore tests/Bdtm.Core.Tests/Bdtm.Core.Tests.csproj

echo "==> Running Bdtm.Core.Tests"
dotnet test tests/Bdtm.Core.Tests/Bdtm.Core.Tests.csproj -c Release --no-restore

echo "==> Done"
