#!/bin/sh
# Volt City P1 sim (Assets/Scripts/Sim, engine-free VoltCity.Sim) checked with a plain .NET SDK until the Unity editor exists:
#   sh games/volt-city/unity/Tools/sim_dotnet_check.sh                 netstandard2.1 compile (-warnaserror) + EditMode tests (NUnit)
#   sh games/volt-city/unity/Tools/sim_dotnet_check.sh balance quick   balance gates (also: full shard=K/N seeds=N out=F | merge F…)
#   sh games/volt-city/unity/Tools/sim_dotnet_check.sh hash 77         one scripted shift hash (cross-process determinism)
# DOTNET=/path/to/dotnet overrides the SDK. Does NOT replace the Unity EditMode run (asmdef wiring, IL2CPP / Mono).
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
UNITY=$(dirname "$HERE")
# the SDK lives in game-deps (BuildSSD/tmp gets cleaned: it vanished 10-30); the old tmp path stays as a fallback
if [ -z "${DOTNET:-}" ]; then
    if command -v dotnet >/dev/null 2>&1; then DOTNET=$(command -v dotnet)
    elif [ -x /Volumes/BuildSSD/game-deps/dotnet-sdk/dotnet ]; then DOTNET=/Volumes/BuildSSD/game-deps/dotnet-sdk/dotnet
    else DOTNET=/Volumes/BuildSSD/tmp/dotnet-sdk/dotnet; fi
fi
WORK=${SIM_CHECK_DIR:-${TMPDIR:-/tmp}/voltcity-sim-dotnet}
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 VOLTCITY_DATA="$UNITY/Assets/Resources/data"
[ -x "$DOTNET" ] || { echo "SIM_DOTNET_CHECK: no dotnet SDK (set DOTNET=)"; exit 2; }
mkdir -p "$WORK/std" "$WORK/test" "$WORK/cli"
cat > "$WORK/std/VoltCity.Sim.Std.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup><Compile Include="$UNITY/Assets/Scripts/Sim/*.cs" /></ItemGroup>
</Project>
XML
cat > "$WORK/test/VoltCity.Sim.Check.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$UNITY/Assets/Scripts/Sim/*.cs" />
    <Compile Include="$UNITY/Assets/Tests/EditMode/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>
</Project>
XML
cat > "$WORK/cli/VoltCity.Sim.Cli.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <ServerGarbageCollection>false</ServerGarbageCollection>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$UNITY/Assets/Scripts/Sim/*.cs" />
    <Compile Include="Program.cs" />
  </ItemGroup>
</Project>
XML
cat > "$WORK/cli/Program.cs" <<'CS'
using System;
public static class Program
{
    public static int Main(string[] args)
    {
        string data = Environment.GetEnvironmentVariable("VOLTCITY_DATA");
        if (args.Length > 0 && args[0] == "hash")
        {
            var gd = VoltCity.Sim.GameData.LoadDirectory(data);
            Console.WriteLine("SHIFT_HASH " + VoltCity.Sim.SimProbe.ScriptedShiftHash(gd, long.Parse(args[1])));
            return 0;
        }
        var a = new System.Collections.Generic.List<string>(args.Length > 1 ? args[1..] : new[] { "quick" });
        a.Add("data=" + data);
        return VoltCity.Sim.BalanceRunner.Cli(a.ToArray(), Console.WriteLine);
    }
}
CS
case "${1:-test}" in
  balance|hash)
    "$DOTNET" build "$WORK/cli/VoltCity.Sim.Cli.csproj" -nologo -v q -c Release -o "$WORK/cli/out" 2>&1 | grep -E "error|Warn|warn" || true
    exec "$DOTNET" "$WORK/cli/out/VoltCity.Sim.Cli.dll" "$@" ;;
  *)
    "$DOTNET" build "$WORK/std/VoltCity.Sim.Std.csproj" -nologo -v q -warnaserror 2>&1 | tail -3
    "$DOTNET" test "$WORK/test/VoltCity.Sim.Check.csproj" -nologo -v q -c Release 2>&1 | grep -E "error|Failed|Passed!|Failed!|Total tests|Expected|But was|at VoltCity" | head -80 ;;
esac
