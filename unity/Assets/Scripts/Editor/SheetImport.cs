#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Runway.EditorTools
{
    /// THE IMPORT FLAGS FOR EVERYTHING THE BUILD STAGES INTO RESOURCES, and there
    /// are two such places with two different jobs.
    ///
    /// `Resources/Sheets` holds the big baked films: the six title sheets, the
    /// twenty dice cups and the title's 48 frames. They ship as IMPORTED textures
    /// instead of streamed PNGs because a 16MB PNG cost 2.8-6.6s of runtime decode
    /// through UnityWebRequest and the birth loop's load was killed by its own
    /// screen moving on. Imported they arrive in milliseconds and stay compressed
    /// on the GPU. Godot pays this exact cost at export time; this is the Unity
    /// spelling of the same decision.
    ///
    /// `Resources/Art` is the mirror `Build.EnsureArtResources` stages: the sprites,
    /// the journal icons, the rooms and the title's animation layers, at their own
    /// relative paths. Same argument, smaller files, and one extra flag —
    /// `alphaIsTransparency`, which the sheets do not need (they are opaque RGB)
    /// and cutout art does (without it the bilinear filter drags black out of the
    /// transparent pixels and every sprite wears a halo).
    ///
    /// WHAT THE COMPRESSION SETTING ACTUALLY YIELDS, read out of `resources.assets`
    /// rather than assumed: `Compressed` on Standalone picks **DXT1 at 4 bits a
    /// pixel** for a source with no alpha and DXT5 at 8 for one with. That is why
    /// the six title sheets are 4 bpp today, and it is why forcing BC7 everywhere
    /// would DOUBLE them rather than shrink them. `CompressedHQ` picks **BC7 at 8
    /// bits a pixel** — the same size as the DXT5 it replaces and strictly better
    /// on the gradients and soft edges that hand-drawn art is full of. So: no
    /// alpha gets `Compressed`, alpha gets `CompressedHQ`, and neither is a guess.
    ///
    /// `npotScale` is `None` in BOTH branches and must stay that way. `ToNearest` is
    /// what `Assets/Art` is set to today, and it turns `chr_loop_dropout_03.png`
    /// from 378x378 into 256x256 — a 32% linear downscale of a founder's animation
    /// frame, silently. `Build.EnsureArtResources` only stages sources that are
    /// already a multiple of 4 in both dimensions, so nothing here needs rescuing
    /// by a resample and nothing may be rescued by one.
    public class SheetImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (path.Contains("Assets/Resources/Sheets/")) { Sheets(); return; }
            if (path.Contains("Assets/Resources/Art/")) { Mirror(path); return; }
        }

        void Sheets()
        {
            var imp = (TextureImporter)assetImporter;
            imp.maxTextureSize = 8192;           // the sheets are 5120 wide
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.mipmapEnabled = false;           // drawn at 1:1-ish, never minified
            imp.sRGBTexture = true;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.isReadable = false;
            imp.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }

        void Mirror(string path)
        {
            var imp = (TextureImporter)assetImporter;
            imp.textureType = TextureImporterType.Default;
            // the largest mirrored source is 2048x1360; anything bigger would be
            // DOWNSCALED here, silently, which is why ArtBakeProbe compares every
            // imported texture against its own source's IHDR rather than sampling
            imp.maxTextureSize = 2048;
            imp.mipmapEnabled = false;           // UI quads, never minified; mips cost +33%
            imp.npotScale = TextureImporterNPOTScale.None;   // NEVER ToNearest
            imp.isReadable = false;
            imp.sRGBTexture = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.alphaIsTransparency = true;      // stops halos on the edges of cutout art

            // The colour type comes off the PNG's own IHDR rather than the importer:
            // `DoesSourceTextureHaveAlpha` is only valid at some points of the import
            // and the header is three bytes of arithmetic away.
            int w, h;
            bool alpha;
            // dataPath is <project>/Assets and assetPath starts at Assets/, so this
            // resolves without depending on the process's working directory
            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            bool read = global::Runway.Build.PngHeader(abs, out w, out h, out alpha);
            imp.textureCompression = (read && !alpha)
                ? TextureImporterCompression.Compressed      // RGB  → DXT1, 4 bits a pixel
                : TextureImporterCompression.CompressedHQ;   // RGBA → BC7,  8 bits a pixel
        }
    }
}
#endif
