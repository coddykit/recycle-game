// CoddyKit (Unity) — offline-safe save files, the Unity port of the fleet's src/save_repository.gd (games/CLAUDE.md §14,
// LESSONS B1/B2). Engine-free: plain System.IO on Application.persistentDataPath (passed in by the caller).
//
//   Write:  text -> <file>.tmp (flushed to disk) -> the current main file, when it is valid, becomes <file>.bak ->
//           <file>.tmp replaces <file>. A main file that does not validate is never copied over a good .bak: it is kept as
//           <file>.unreadable instead. A crash between "delete main" and "move tmp" leaves a complete, flushed .tmp that
//           Load() accepts (candidate order main -> tmp -> bak).
//   Load:   the first candidate that the game's validator accepts wins; every rejected candidate is kept as
//           <candidate>.unreadable (an older, different copy as .unreadable.1 … .7) and its error is listed in
//           LoadResult.Unreadable so the game can send the analytics-safe `save_unreadable` event (error code +
//           recovered 0/1, no PII). An unreadable save is NEVER deleted or overwritten.
// File.Replace / File.Move(overwrite) are avoided on purpose: their IL2CPP-Android behaviour differs between Unity
// versions; delete + move + the .tmp candidate gives the same guarantee with the two most basic calls.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CoddyKit
{
    /// <summary>The game's decoder: returns null when the text is a valid save, else a short error code.</summary>
    public delegate string SaveValidator(string text);

    public sealed class LoadResult
    {
        public bool Ok;
        public string Text;
        /// <summary>Which file was loaded ("main", "tmp", "bak"), "" when nothing loaded.</summary>
        public string Source = "";
        /// <summary>True when the data came from a fallback (tmp / bak), i.e. the main file was missing or broken.</summary>
        public bool Recovered;
        /// <summary>Error codes of the candidates that existed but did not validate (main first).</summary>
        public readonly List<string> Unreadable = new List<string>();
        /// <summary>Paths the rejected candidates were preserved at.</summary>
        public readonly List<string> KeptAs = new List<string>();
        public string Error = "";
    }

    public static class SaveRepository
    {
        public const int KeepMax = 8;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>Atomic write as described in the header. Returns "" on success, else the exception message.</summary>
        public static string Write(string path, string text, SaveValidator validator = null)
        {
            if (string.IsNullOrEmpty(path)) return "empty path";
            string tmp = path + ".tmp";
            string bak = path + ".bak";
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    byte[] bytes = Utf8.GetBytes(text ?? "");
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }
                if (File.Exists(path))
                {
                    string current = SafeRead(path);
                    if (current != null && (validator == null ? current.Length > 0 : validator(current) == null))
                        File.Copy(path, bak, true);
                    else
                        KeepUnreadable(path);
                    File.Delete(path);
                }
                File.Move(tmp, path);
                return "";
            }
            catch (Exception e)
            {
                return e.GetType().Name + ": " + e.Message;
            }
        }

        /// <summary>Loads the first candidate (main, tmp, bak) the validator accepts. Never deletes anything.</summary>
        public static LoadResult Load(string path, SaveValidator validator)
        {
            var r = new LoadResult();
            string[] files = { path, path + ".tmp", path + ".bak" };
            string[] names = { "main", "tmp", "bak" };
            for (int k = 0; k < files.Length; k++)
            {
                if (!File.Exists(files[k])) continue;
                string text = SafeRead(files[k]);
                string err = text == null ? "read_failed" : (validator != null ? validator(text) : (text.Length > 0 ? null : "empty"));
                if (err == null)
                {
                    r.Ok = true;
                    r.Text = text;
                    r.Source = names[k];
                    r.Recovered = k > 0;
                    return r;
                }
                if (names[k] == "tmp")
                {
                    // a half-written .tmp is the normal trace of an interrupted write, not a lost save: no .unreadable copy
                    continue;
                }
                r.Unreadable.Add(err);
                string kept = KeepUnreadable(files[k]);
                if (kept != "") r.KeptAs.Add(kept);
            }
            r.Error = r.Unreadable.Count > 0 ? "no_valid_save" : "no_save";
            return r;
        }

        /// <summary>Copies a rejected file to &lt;file&gt;.unreadable (an older, different copy is kept: .unreadable.1 … .7).
        /// Returns the copy's path ("" when the copy failed or the same body is already kept).</summary>
        public static string KeepUnreadable(string file)
        {
            try
            {
                byte[] body = File.ReadAllBytes(file);
                string target = file + ".unreadable";
                for (int n = 1; n < KeepMax; n++)
                {
                    if (!File.Exists(target)) break;
                    if (SameBytes(File.ReadAllBytes(target), body)) return target;
                    target = file + ".unreadable." + n;
                }
                if (File.Exists(target) && SameBytes(File.ReadAllBytes(target), body)) return target;
                File.WriteAllBytes(target, body);
                return target;
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static string SafeRead(string file)
        {
            try { return File.ReadAllText(file, Utf8); }
            catch (Exception) { return null; }
        }

        static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }

    /// <summary>A small kit state file (JSON object) written through SaveRepository (atomic, .bak). Unreadable content
    /// starts fresh but is preserved as .unreadable.</summary>
    public sealed class JsonStateFile
    {
        public readonly string Path;
        public Dictionary<string, object> Data = new Dictionary<string, object>();

        public JsonStateFile(string path) { Path = path; }

        static string Validate(string text) => MiniJson.TryParseObject(text) != null ? null : "invalid_json";

        public JsonStateFile Load()
        {
            var r = SaveRepository.Load(Path, Validate);
            Data = r.Ok ? MiniJson.TryParseObject(r.Text) : new Dictionary<string, object>();
            return this;
        }

        public string Save() => SaveRepository.Write(Path, MiniJson.Write(Data), Validate);
    }
}
