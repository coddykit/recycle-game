#!/bin/sh
# Volt City P3 game layer (Assets/Scripts/Game = VoltCity.Game, engine-free) checked with a plain .NET SDK until the Unity
# editor exists: netstandard2.1 / C# 9 compile with -warnaserror (Sim + CoddyKit.Core + Game) and the game EditMode tests
# (Assets/Tests/Game/*.cs, NUnit; the same files the Unity Test Runner runs).
#   sh games/volt-city/unity/Tools/game_dotnet_check.sh
# DOTNET=/path/to/dotnet overrides the SDK. Does NOT replace the Unity EditMode / PlayMode runs.
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
UNITY=$(dirname "$HERE")
# the SDK lives in game-deps (BuildSSD/tmp gets cleaned: it vanished 10-30); the old tmp path stays as a fallback
if [ -z "${DOTNET:-}" ]; then
    if command -v dotnet >/dev/null 2>&1; then DOTNET=$(command -v dotnet)
    elif [ -x /Volumes/BuildSSD/game-deps/dotnet-sdk/dotnet ]; then DOTNET=/Volumes/BuildSSD/game-deps/dotnet-sdk/dotnet
    else DOTNET=/Volumes/BuildSSD/tmp/dotnet-sdk/dotnet; fi
fi
WORK=${GAME_CHECK_DIR:-${TMPDIR:-/tmp}/voltcity-game-dotnet}
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 VOLTCITY_UNITY="$UNITY"
[ -x "$DOTNET" ] || { echo "GAME_DOTNET_CHECK: no dotnet SDK (set DOTNET=)"; exit 2; }
mkdir -p "$WORK/std" "$WORK/test"
SRC="<Compile Include=\"$UNITY/Assets/Scripts/Sim/*.cs\" /><Compile Include=\"$UNITY/Assets/CoddyKit/Core/**/*.cs\" /><Compile Include=\"$UNITY/Assets/Scripts/Game/*.cs\" />"
cat > "$WORK/std/VoltCity.Game.Std.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0067</NoWarn>
  </PropertyGroup>
  <ItemGroup>$SRC</ItemGroup>
</Project>
XML
cat > "$WORK/test/VoltCity.Game.Check.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0067</NoWarn>
  </PropertyGroup>
  <ItemGroup>$SRC<Compile Include="$UNITY/Assets/Tests/Game/*.cs" /></ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>
</Project>
XML
"$DOTNET" build "$WORK/std/VoltCity.Game.Std.csproj" -nologo -v q -warnaserror 2>&1 | grep -E "error|warn|Build succeeded|Error" | sort -u | head -60
"$DOTNET" test "$WORK/test/VoltCity.Game.Check.csproj" -nologo -v q -c Release 2>&1 | grep -E "error|Failed |Passed!|Failed!|Total tests|Expected|But was|at VoltCity|  Failed" | head -80
