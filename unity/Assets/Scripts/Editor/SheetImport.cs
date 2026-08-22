#if UNITY_EDITOR
using UnityEditor;

namespace Runway.EditorTools
{
    /// The six film sheets ship as IMPORTED textures (GPU-ready, BC7) instead
    /// of streamed PNGs: a 16MB PNG cost 2.8-6.6s of runtime decode through
    /// UnityWebRequest and the birth loop's load was killed by its own screen
    /// moving on. Imported, they load in milliseconds and stay compressed on
    /// the GPU (a 94MB RGBA sway becomes ~24MB). Godot pays this exact cost at
    /// export time; this is the Unity spelling of the same decision.
    public class SheetImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("Assets/Resources/Sheets/")) return;
            var imp = (TextureImporter)assetImporter;
            imp.maxTextureSize = 8192;           // the sheets are 5120 wide
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.mipmapEnabled = false;           // drawn at 1:1-ish, never minified
            imp.sRGBTexture = true;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.isReadable = false;
            imp.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
    }
}
#endif
