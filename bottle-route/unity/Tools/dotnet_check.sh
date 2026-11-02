#!/bin/sh
# Bottle Route — the engine-free code checked with a plain .NET SDK (no Unity editor needed):
#   1. netstandard2.1 / C# 9 compile with -warnaserror of BottleRoute.Sim, CoddyKit.Core and BottleRoute.Game
#      (the API + language level of Unity 6's .NET Standard 2.1 profile);
#   2. the NUnit EditMode tests that do not need UnityEngine (Assets/Tests/EditMode, Assets/Tests/Game,
#      Assets/CoddyKit/Tests top level) — the same files the Unity Test Runner runs.
# It does NOT replace the Unity EditMode / PlayMode runs (IL2CPP, asmdef wiring, engine code).
#   sh bottle-route/unity/Tools/dotnet_check.sh            (DOTNET=/path/to/dotnet overrides the SDK)
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
UNITY=$(dirname "$HERE")
DOTNET=${DOTNET:-$(command -v dotnet || echo /Volumes/BuildSSD/game-deps/dotnet-sdk/dotnet)}
WORK=${BR_CHECK_DIR:-${TMPDIR:-/tmp}/bottleroute-dotnet}
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 BOTTLEROUTE_UNITY="$UNITY"
[ -x "$DOTNET" ] || { echo "BR_DOTNET_CHECK: no dotnet SDK (set DOTNET=)"; exit 2; }
mkdir -p "$WORK/std" "$WORK/test"
SRC="<Compile Include=\"$UNITY/Assets/Scripts/Sim/**/*.cs\" /><Compile Include=\"$UNITY/Assets/CoddyKit/Core/**/*.cs\" />"
[ -d "$UNITY/Assets/Scripts/Game" ] && SRC="$SRC<Compile Include=\"$UNITY/Assets/Scripts/Game/**/*.cs\" />"
cat > "$WORK/std/BottleRoute.Std.csproj" <<XML
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
cat > "$WORK/test/BottleRoute.Check.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0067</NoWarn>
  </PropertyGroup>
  <ItemGroup>$SRC
    <Compile Include="$UNITY/Assets/Tests/EditMode/*.cs" />
    <Compile Include="$UNITY/Assets/Tests/Game/*.cs" />
    <Compile Include="$UNITY/Assets/CoddyKit/Tests/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>
</Project>
XML
echo "== netstandard2.1 compile (-warnaserror)"
"$DOTNET" build "$WORK/std/BottleRoute.Std.csproj" -nologo -v q -warnaserror 2>&1 | grep -E "error|warn|Build succeeded|Error" | sort -u | head -60
echo "== NUnit tests"
"$DOTNET" test "$WORK/test/BottleRoute.Check.csproj" -nologo -v q -c Release ${BR_TEST_FILTER:+--filter "$BR_TEST_FILTER"} 2>&1 | grep -E "error|Failed |Passed!|Failed!|Total tests|Expected|But was|at BottleRoute|  Failed|^  [A-Z].*Exception|BALANCE|^ *day|^ *[0-9]+ " | head -120
