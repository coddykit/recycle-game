// Volt City P4 — import settings for Assets/Resources/audio (no editor on the Mini yet: the rules live in code so the first
// import is already right). Music loops: Streaming + Vorbis 0.5 (never fully in memory on a Low phone); ambience:
// CompressedInMemory; one-shot SFX: DecompressOnLoad, forced mono (tiny, instant start). Android only overrides; preload off
// for music. UNCOMPILED until the editor is installed — verify AudioImporterSampleSettings field names on Unity 6.
using UnityEditor;
using UnityEngine;

namespace VoltCity.EditorTools
{
    public sealed class VoltAudioImport : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/audio/";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var imp = (AudioImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool music = name.StartsWith("music_");
            bool amb = name.StartsWith("amb_");
            var s = imp.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.5f : 0.6f;
            s.loadType = music ? AudioClipLoadType.Streaming : amb ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.preloadAudioData = !music;
            imp.defaultSampleSettings = s;
            imp.forceToMono = !(music || amb);
            imp.loadInBackground = music || amb;
        }
    }
}
