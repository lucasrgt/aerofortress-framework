#!/usr/bin/env bash
# auth-smoke — render the auth blueprint (+ email/otp variants) into a throwaway app, then compile it
# and run its co-located tests. This is the regression guard the round-1 audit lacked: the LZ0022 break
# (a generated app that did not compile) slipped through because nothing rendered the templates and built.
#
# Two legs:
#   1. DOCTOR leg  — build the rendered API with the LZ* analyzers ON, and assert LZ0022 never reappears.
#                    (Today the rendered app still trips LZ0012/LZ0017/LZ0021 — tracked separately; once
#                     those are fixed, tighten this leg to assert a fully clean doctor build.)
#   2. COMPILE+TEST leg — build + run the generated test suite with analyzers OFF, to prove the rendered
#                    C# compiles and every shipped *.Tests.cs passes.
#
# Uses ProjectReferences to the freshly-built Lazuli.* projects (not the package feed), so it tests the
# working tree. Headless; no Docker (the in-memory provider backs the tests). Run from the repo root.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO_WIN="$(cd "$REPO" && pwd -W 2>/dev/null || echo "$REPO")"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

echo "==> building the CLI (carries the templates)"
dotnet build "$REPO/src/Lazuli.Cli/Lazuli.Cli.csproj" -c Debug >/dev/null
CLI="$REPO/src/Lazuli.Cli/bin/Debug/net10.0/Lazuli.Cli.dll"

mkdir -p "$WORK/src/Smoke.Api" "$WORK/tests/Smoke.Tests"

cat > "$WORK/src/Smoke.Api/Smoke.Api.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$REPO_WIN/src/Lazuli.Abstractions/Lazuli.Abstractions.csproj" />
    <ProjectReference Include="$REPO_WIN/src/Lazuli.AspNetCore/Lazuli.AspNetCore.csproj" />
    <ProjectReference Include="$REPO_WIN/src/Lazuli.Auth/Lazuli.Auth.csproj" />
    <ProjectReference Include="$REPO_WIN/src/Lazuli.Mail/Lazuli.Mail.csproj" />
    <ProjectReference Include="$REPO_WIN/src/Lazuli.Sms/Lazuli.Sms.csproj" />
    <ProjectReference Include="$REPO_WIN/analyzers/Lazuli.Doctor/Lazuli.Doctor.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.8" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.8" />
    <PackageReference Include="Konscious.Security.Cryptography.Argon2" Version="1.3.1" />
  </ItemGroup>
  <ItemGroup>
    <Compile Remove="**/*.Tests.cs" />
    <AdditionalFiles Include="**/*.Tests.cs" />
    <AdditionalFiles Include="**/*.ctx.md" />
  </ItemGroup>
</Project>
EOF

printf 'global using Lazuli.Abstractions;\nglobal using Lazuli.AspNetCore;\n' > "$WORK/src/Smoke.Api/GlobalUsings.cs"
printf 'var builder = WebApplication.CreateBuilder(args);\n\nvar app = builder.Build();\n\napp.Run();\n' > "$WORK/src/Smoke.Api/Program.cs"

cat > "$WORK/tests/Smoke.Tests/Smoke.Tests.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="Lazuli.Abstractions" />
    <Using Include="Lazuli.Testing" />
    <ProjectReference Include="../../src/Smoke.Api/Smoke.Api.csproj" />
    <ProjectReference Include="$REPO_WIN/src/Lazuli.Testing/Lazuli.Testing.csproj" />
    <ProjectReference Include="$REPO_WIN/src/Lazuli.Testing.InMemory/Lazuli.Testing.InMemory.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="../../src/Smoke.Api/**/*.Tests.cs" />
  </ItemGroup>
</Project>
EOF

echo "==> rendering auth + auth:email + auth:otp"
( cd "$WORK/src/Smoke.Api" && dotnet "$CLI" g auth >/dev/null && dotnet "$CLI" g auth:email >/dev/null && dotnet "$CLI" g auth:otp >/dev/null )

echo "==> DOCTOR leg: build with analyzers ON, assert no LZ0022"
DOCTOR_OUT="$(dotnet build "$WORK/src/Smoke.Api/Smoke.Api.csproj" -c Debug 2>&1 || true)"
if echo "$DOCTOR_OUT" | grep -q "LZ0022"; then
  echo "FAIL: LZ0022 reappeared in the generated app:"; echo "$DOCTOR_OUT" | grep "LZ0022"; exit 1
fi
echo "ok: no LZ0022. (tracked, still-open doctor findings:)"
echo "$DOCTOR_OUT" | grep -oE "(error|warning) LZ[0-9]+" | sort | uniq -c | sort -rn || true

echo "==> COMPILE+TEST leg: build + run generated tests (analyzers off)"
dotnet test "$WORK/tests/Smoke.Tests/Smoke.Tests.csproj" -p:RunAnalyzers=false

echo "==> auth-smoke OK"
