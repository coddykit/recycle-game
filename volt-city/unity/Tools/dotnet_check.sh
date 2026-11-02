#!/bin/sh
# Compiles the engine-free CoddyKit.Core (netstandard2.1, C# 9 = Unity 6's language level) and runs its NUnit tests
# (Assets/CoddyKit/Tests/*.cs, top level only; tests that need UnityEngine live in Tests/Engine/) with a plain .NET SDK.
# This is the only executable check available before the Unity editor is installed; it does NOT replace the Unity
# EditMode run (IL2CPP / Mono differences, asmdef wiring, engine code).
#   DOTNET=/path/to/dotnet sh games/volt-city/unity/Tools/dotnet_check.sh
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
UNITY=$(dirname "$HERE")
# the SDK lives in game-deps (BuildSSD/tmp gets cleaned: it vanished 10-30); the old tmp path stays as a fallback
if [ -z "${DOTNET:-}" ]; then
    if command -v dotnet >/dev/null 2>&1; then DOTNET=$(command -v dotnet)
    elif [ -x /Volumes/BuildSSD/game-deps/dotnet-sdk/dotnet ]; then DOTNET=/Volumes/BuildSSD/game-deps/dotnet-sdk/dotnet
    else DOTNET=/Volumes/BuildSSD/tmp/dotnet-sdk/dotnet; fi
fi
WORK=${DOTNET_CHECK_DIR:-${TMPDIR:-/tmp}/coddykit-dotnet-check}
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
[ -x "$DOTNET" ] || { echo "DOTNET_CHECK: no dotnet SDK (set DOTNET=)"; exit 2; }
mkdir -p "$WORK"
cat > "$WORK/CoddyKit.Core.Check.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0067</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$UNITY/Assets/CoddyKit/Core/**/*.cs" />
    <Compile Include="$UNITY/Assets/CoddyKit/Tests/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>
</Project>
XML
# netstandard2.1 compile of Core alone = the API surface Unity's .NET Standard 2.1 profile offers
cat > "$WORK/CoddyKit.Core.Std.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <OutputPath>std-out</OutputPath>
  </PropertyGroup>
  <ItemGroup><Compile Include="$UNITY/Assets/CoddyKit/Core/**/*.cs" /></ItemGroup>
</Project>
XML
"$DOTNET" build "$WORK/CoddyKit.Core.Std.csproj" -nologo -v q -warnaserror 2>&1 | tail -3
"$DOTNET" test "$WORK/CoddyKit.Core.Check.csproj" -nologo -v q 2>&1 | grep -E "error|Failed|Passed!|Failed!|Total tests|Expected|But was|at CoddyKit" | head -60
