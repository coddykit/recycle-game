#!/bin/sh
# Volt City — C# 9 SYNTAX check of every engine-dependent script (Roslyn parse, two define sets: editor + tests, and
# Android player + all vendor bridges). Not a semantic compile: UnityEngine / UI Toolkit / Input System / URP API use is
# only checked by the Unity editor (docs/UNITY.md section 2). Exit 1 on any syntax error.
#   sh games/volt-city/unity/Tools/cs_parse_check.sh [dir…]      (default: Assets)
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
UNITY=$(dirname "$HERE")
# the SDK lives in game-deps (BuildSSD/tmp gets cleaned: it vanished 10-30); the old tmp path stays as a fallback
if [ -z "${DOTNET:-}" ]; then
    if command -v dotnet >/dev/null 2>&1; then DOTNET=$(command -v dotnet)
    elif [ -x /Volumes/BuildSSD/game-deps/dotnet-sdk/dotnet ]; then DOTNET=/Volumes/BuildSSD/game-deps/dotnet-sdk/dotnet
    else DOTNET=/Volumes/BuildSSD/tmp/dotnet-sdk/dotnet; fi
fi
WORK=${PARSE_CHECK_DIR:-${TMPDIR:-/tmp}/voltcity-cs-parse}
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
[ -x "$DOTNET" ] || { echo "CS_PARSE_CHECK: no dotnet SDK (set DOTNET=)"; exit 2; }
mkdir -p "$WORK"
cat > "$WORK/CsParse.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup><PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" /></ItemGroup>
</Project>
XML
cat > "$WORK/Program.cs" <<'CS'
using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
public static class Program
{
    public static int Main(string[] args)
    {
        var sets = new[]
        {
            new[] { "UNITY_EDITOR", "UNITY_INCLUDE_TESTS", "UNITY_2023_1_OR_NEWER", "UNITY_6000_0_OR_NEWER" },
            new[] { "UNITY_ANDROID", "CODDY_ADMOB", "CODDY_RC", "CODDY_PLAY_REVIEW", "UNITY_2023_1_OR_NEWER", "UNITY_6000_0_OR_NEWER" },
        };
        int files = 0, errors = 0;
        foreach (var dir in args)
            foreach (var f in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                files++;
                string text = File.ReadAllText(f);
                foreach (var defs in sets)
                {
                    var opts = new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: defs);
                    var tree = CSharpSyntaxTree.ParseText(text, opts, f);
                    foreach (var d in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        errors++;
                        Console.WriteLine(d.ToString());
                    }
                }
            }
        Console.WriteLine($"CS_PARSE: {files} files, {errors} syntax errors (C# 9, 2 define sets)");
        return errors == 0 ? 0 : 1;
    }
}
CS
"$DOTNET" build "$WORK/CsParse.csproj" -nologo -v q -c Release -o "$WORK/out" 2>&1 | grep -E " error " || true
if [ "$#" -eq 0 ]; then set -- "$UNITY/Assets"; fi
exec "$DOTNET" "$WORK/out/CsParse.dll" "$@"
