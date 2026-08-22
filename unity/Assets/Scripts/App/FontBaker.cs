#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Runway.App
{
    /// <summary>
    /// THE HAND, BAKED ONCE. Unity cannot load a .ttf off disk at runtime — the Font
    /// constructor takes an OS font NAME, not a path — so Patrick Hand reaches the game
    /// through Resources. DrawnUI already builds a dynamic TMP font asset from the .ttf
    /// on first use, which works but pays for an atlas every launch; this editor pass
    /// bakes the asset once so the runtime just loads it.
    ///
    /// It runs on editor load, does nothing when the asset already exists, and every
    /// failure is a log line rather than an exception: the runtime ladder in
    /// DrawnUI.Hand is the fallback, and it works on its own.
    /// </summary>
    [InitializeOnLoad]
    public static class FontBaker
    {
        const string TtfPath = "Assets/Resources/Fonts/PatrickHand-Regular.ttf";
        const string AssetPath = "Assets/Resources/Fonts/PatrickHand SDF.asset";

        static FontBaker()
        {
            EditorApplication.delayCall += Bake;
        }

        [MenuItem("RUNWAY!/Rebuild the hand font")]
        public static void Rebuild()
        {
            AssetDatabase.DeleteAsset(AssetPath);
            Bake();
        }

        static void Bake()
        {
            try
            {
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath) != null) return;
                if (!File.Exists(Path.Combine(Application.dataPath, "..", TtfPath)))
                {
                    Debug.LogWarning("RUNWAY! " + TtfPath + " is missing — copy it from "
                                     + "Assets/Art/fonts/ so the hand survives a build.");
                    return;
                }
                var ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
                if (ttf == null) return;   // still importing; the next delayCall gets it

                TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(ttf);
                if (fontAsset == null)
                {
                    Debug.LogWarning("RUNWAY! TMP would not build a font asset from " + TtfPath);
                    return;
                }
                fontAsset.name = "PatrickHand SDF";
                AssetDatabase.CreateAsset(fontAsset, AssetPath);

                // the material and the atlas have to live inside the asset, or the
                // font loads at runtime with nothing to draw with
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = fontAsset.name + " Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }
                if (fontAsset.atlasTextures != null)
                {
                    for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                    {
                        if (fontAsset.atlasTextures[i] == null) continue;
                        fontAsset.atlasTextures[i].name = "Atlas " + i;
                        AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[i], fontAsset);
                    }
                }
                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
                Debug.Log("RUNWAY! baked the hand font into " + AssetPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RUNWAY! font bake skipped (" + e.Message
                                 + ") — DrawnUI builds it at runtime instead.");
            }
        }
    }
}
#endif
