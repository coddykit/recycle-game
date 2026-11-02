#!/bin/sh
# Semester Rush — checks the ENGINE-BOUND code without a Unity editor (session check; the real gate is the editor):
#   1. semantic compile of CoddyKit.Core/Runtime + SemesterRush.Sim/Game/App/World/UI (netstandard2.1, C# 9) against the
#      UnityEngine 2021.3 reference modules (NuGet UnityEngine.Modules 2021.3.33) + check-only stubs for the Input System
#      package. Two Unity-6-only APIs are allowed to fail here (QualitySettings.count, SystemLanguage.Hindi; both compile
#      in the real Unity 6 editor — Volt City 2026-10-28);
#   2. Roslyn SYNTAX parse (C# 9) of every .cs under Assets, incl. Editor scripts (UnityEditor.dll is not redistributable).
#   sh semester-rush/unity/Tools/unity_ref_check.sh            (needs dotnet + api.nuget.org)
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
UNITY=$(dirname "$HERE")
W=${UNITY_REF_DIR:-${TMPDIR:-/tmp}/semesterrush-unityref}
DOTNET=${DOTNET:-dotnet}
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
mkdir -p "$W/proj" "$W/parse"
if [ ! -d "$W/lib/net45" ]; then
  curl -fsSL -o "$W/mods.nupkg" https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg
  (cd "$W" && python3 -c "import zipfile;z=zipfile.ZipFile('mods.nupkg');[z.extract(n,'.') for n in z.namelist() if n.startswith('lib/net45/') and n.endswith('.dll')]")
fi
cat > "$W/proj/Stubs.cs" <<'EOF'
namespace UnityEngine.InputSystem
{
    public enum TouchPhase { None, Began, Moved, Ended, Canceled, Stationary }
    public class ButtonControl { public bool isPressed; public bool wasPressedThisFrame; public bool wasReleasedThisFrame; }
    public class Vector2Control { public UnityEngine.Vector2 ReadValue() => default; }
    public class Pointer { public static Pointer current; public Vector2Control position = new Vector2Control(); public ButtonControl press = new ButtonControl(); }
    public class Mouse : Pointer { public static new Mouse current; public ButtonControl leftButton = new ButtonControl(); public Vector2Control scroll = new Vector2Control(); }
    public class Touchscreen : Pointer { public static new Touchscreen current; }
}
namespace UnityEngine.InputSystem.EnhancedTouch
{
    using System.Collections.Generic;
    public static class EnhancedTouchSupport { public static void Enable() { } public static void Disable() { } }
    public struct Touch
    {
        public static IReadOnlyList<Touch> activeTouches => new List<Touch>();
        public UnityEngine.Vector2 screenPosition => default;
        public UnityEngine.InputSystem.TouchPhase phase => default;
        public int touchId => 0;
        public UnityEngine.Vector2 startScreenPosition => default;
        public double startTime => 0;
        public double time => 0;
    }
}
EOF
{
echo '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9.0</LangVersion>'
echo '<Nullable>disable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><NoWarn>CS0067;CS0436;CS0108;MSB3277;CS1701;CS1702</NoWarn>'
echo '<DefineConstants>UNITY_ANDROID</DefineConstants></PropertyGroup><ItemGroup>'
for d in Scripts/Sim Scripts/Game CoddyKit/Core CoddyKit/Runtime Scripts/App Scripts/World Scripts/UI; do echo "<Compile Include=\"$UNITY/Assets/$d/**/*.cs\" />"; done
echo '<Compile Include="Stubs.cs" /></ItemGroup><ItemGroup>'
for f in "$W"/lib/net45/*.dll; do echo "<Reference Include=\"$(basename "$f" .dll)\"><HintPath>$f</HintPath></Reference>"; done
echo '</ItemGroup></Project>'
} > "$W/proj/Check.csproj"
echo "== semantic compile vs UnityEngine 2021.3 (+ Input System stubs)"
ERRS=$("$DOTNET" build "$W/proj/Check.csproj" -nologo -v q 2>&1 | grep -E "error CS" | sed "s#$UNITY/Assets/##; s# \[.*##" | sort -u || true)
OTHER=$(printf "%s\n" "$ERRS" | grep -v -E "QualitySettings' does not contain a definition for 'count'|SystemLanguage' does not contain a definition for 'Hindi'" | grep . || true)
printf "%s\n" "$ERRS" | grep . | sed 's/^/  /' || true
cat > "$W/parse/Parse.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup>
<ItemGroup><PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" /></ItemGroup></Project>
EOF
cat > "$W/parse/Program.cs" <<'EOF'
using System; using System.IO; using System.Linq; using Microsoft.CodeAnalysis; using Microsoft.CodeAnalysis.CSharp;
int bad = 0, n = 0;
foreach (var defs in new[] { new[] { "UNITY_EDITOR", "UNITY_INCLUDE_TESTS" }, new[] { "UNITY_ANDROID", "CODDY_ADMOB", "CODDY_RC", "CODDY_PLAY_REVIEW", "CODDY_FIREBASE" } })
foreach (var f in Directory.GetFiles(args[0], "*.cs", SearchOption.AllDirectories)) {
  n++;
  var t = CSharpSyntaxTree.ParseText(File.ReadAllText(f), new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: defs));
  foreach (var d in t.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)) { bad++; Console.WriteLine("  " + f.Replace(args[0], "") + ": " + d); }
}
Console.WriteLine($"PARSE files={n / 2} errors={bad}");
return bad == 0 ? 0 : 1;
EOF
echo "== C# 9 syntax parse of every Assets/**/*.cs (both define sets)"
PARSE=0; "$DOTNET" run --project "$W/parse/Parse.csproj" -v q -- "$UNITY/Assets" 2>&1 | tail -20 || PARSE=1
if [ -n "$OTHER" ] || [ $PARSE != 0 ]; then echo "UNITY_REF_CHECK: FAIL"; exit 1; fi
echo "UNITY_REF_CHECK: PASS (only the 2 known Unity-6-only APIs differ)"
