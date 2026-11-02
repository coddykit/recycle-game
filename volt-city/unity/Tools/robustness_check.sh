#!/bin/sh
# Engine-free only. Default: short CI soak + all 5,000 corruptions. SOAK_SECONDS=1800 for the 30-minute acceptance run.
set -eu
HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
UNITY=$(dirname "$HERE")
if [ -z "${DOTNET:-}" ]; then
    if command -v dotnet >/dev/null 2>&1; then DOTNET=$(command -v dotnet)
    elif [ -x /Volumes/BuildSSD/game-deps/dotnet-sdk/dotnet ]; then DOTNET=/Volumes/BuildSSD/game-deps/dotnet-sdk/dotnet
    else DOTNET=/Volumes/BuildSSD/tmp/dotnet-sdk/dotnet; fi
fi
[ -x "$DOTNET" ] || { echo 'ROBUSTNESS FAIL: .NET SDK required (DOTNET=)'; exit 2; }
WORK=$(mktemp -d "${TMPDIR:-/tmp}/volt-city-robustness.XXXXXX")
trap 'rm -rf "$WORK"' EXIT HUP INT TERM
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
cat > "$WORK/Robustness.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework><OutputType>Exe</OutputType><LangVersion>9.0</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup>
  <ItemGroup><Compile Include="$UNITY/Assets/Scripts/Sim/*.cs" /><Compile Include="$UNITY/Assets/CoddyKit/Core/**/*.cs" /><Compile Include="$HERE/Robustness.cs" /></ItemGroup>
</Project>
XML
"$DOTNET" build "$WORK/Robustness.csproj" -c Release -v q --nologo
DLL="$WORK/bin/Release/net8.0/Robustness.dll"
H1=$("$DOTNET" "$DLL" "$UNITY/Assets/Resources/data" hash)
H2=$("$DOTNET" "$DLL" "$UNITY/Assets/Resources/data" hash)
[ -n "$H1" ] && [ "$H1" = "$H2" ] || { echo 'ROBUSTNESS FAIL: cross-process determinism'; exit 1; }
echo "$H1"
"$DOTNET" "$DLL" "$UNITY/Assets/Resources/data" "${SOAK_SECONDS:-10}"
