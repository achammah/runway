#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Runway.App
{
    /// <summary>
    /// THE TYPE, BAKED ONCE. Unity cannot load a .ttf off disk at runtime — the Font
    /// constructor takes an OS font NAME, not a path — so the game's faces reach it
    /// through Resources. DrawnUI already builds dynamic TMP assets on first use,
    /// which works but pays for an atlas every launch; this editor pass bakes them
    /// once so the runtime just loads them.
    ///
    /// Three kinds of face go through here:
    ///   · the hand, Patrick Hand, which writes everything on paper
    ///   · the display hand, Baloo2 Bold, which every .gd screen sets its headings in
    ///   · the borrowed glyphs — ★ ✓ ⏰ ⚠ ☐ → and the rest, which NEITHER shipped
    ///     face contains. Godot got them free (its importer has
    ///     allow_system_fallback on and TextServer borrows an OS face); TMP borrows
    ///     nothing, so the same characters are rasterised out of an OS face here and
    ///     frozen into a static atlas that ships. Only the pictures of the glyphs
    ///     travel — the font programs stay on the machine that baked them.
    ///
    /// It runs on editor load, does nothing when an asset already exists, and every
    /// failure is a log line rather than an exception: the runtime ladder in DrawnUI
    /// is the fallback, and it works on its own.
    /// </summary>
    [InitializeOnLoad]
    public static class FontBaker
    {
        const string Dir = "Assets/Resources/Fonts/";
        const string HandTtf = Dir + "PatrickHand-Regular.ttf";
        const string HandAsset = Dir + "PatrickHand SDF.asset";
        const string DisplayTtf = Dir + "Baloo2-Bold.ttf";
        const string DisplayAsset = Dir + "Baloo2 SDF.asset";

        static string GlyphAsset(int i) { return Dir + "RunwayGlyphs " + i + " SDF.asset"; }

        static FontBaker()
        {
            EditorApplication.delayCall += BakeAll;
        }

        [MenuItem("RUNWAY!/Rebuild the fonts")]
        public static void Rebuild()
        {
            AssetDatabase.DeleteAsset(HandAsset);
            AssetDatabase.DeleteAsset(DisplayAsset);
            for (int i = 0; i < DrawnUI.GlyphFaceCount; i++) AssetDatabase.DeleteAsset(GlyphAsset(i));
            BakeAll();
        }

        public static void BakeAll()
        {
            BakeShipped(HandTtf, HandAsset, "PatrickHand SDF");
            BakeShipped(DisplayTtf, DisplayAsset, "Baloo2 SDF");
            for (int i = 0; i < DrawnUI.GlyphFaceCount; i++) BakeGlyphs(i);
        }

        // ── a face that ships in Resources ─────────────────────────────────────

        static void BakeShipped(string ttfPath, string assetPath, string assetName)
        {
            try
            {
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) return;
                if (!File.Exists(Path.Combine(Application.dataPath, "..", ttfPath)))
                {
                    Debug.LogWarning("RUNWAY! " + ttfPath + " is missing — copy it from "
                                     + "Assets/Art/fonts/ so the type survives a build.");
                    return;
                }
                var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
                if (ttf == null) return;   // still importing; the next delayCall gets it

                TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(ttf);
                if (fontAsset == null)
                {
                    Debug.LogWarning("RUNWAY! TMP would not build a font asset from " + ttfPath);
                    return;
                }
                fontAsset.name = assetName;
                Save(fontAsset, assetPath);
                Debug.Log("RUNWAY! baked " + assetName + " into " + assetPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("RUNWAY! font bake skipped for " + assetPath + " (" + e.Message
                                 + ") — DrawnUI builds it at runtime instead.");
            }
        }

        // ── the borrowed glyphs ────────────────────────────────────────────────

        /// One OS face, cut down to the characters the game actually writes and
        /// frozen. The asset that lands in Resources carries an atlas and a glyph
        /// table and no font program, so it works on a machine that has never heard
        /// of the face it came from.
        static void BakeGlyphs(int i)
        {
            string assetPath = GlyphAsset(i);
            try
            {
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) return;

                string path = DrawnUI.GlyphFacePath(i);
                TMP_FontAsset fontAsset = null;
                if (File.Exists(path))
                    fontAsset = TMP_FontAsset.CreateFontAsset(path, 0, 90, 9,
                                                              GlyphRenderMode.SDFAA, 512, 512);
                if (fontAsset == null)
                    fontAsset = TMP_FontAsset.CreateFontAsset(DrawnUI.GlyphFaceFamily(i),
                                                              DrawnUI.GlyphFaceStyle(i), 90);
                if (fontAsset == null)
                {
                    Debug.LogWarning("RUNWAY! no glyph face at " + path + " and none named "
                                     + DrawnUI.GlyphFaceFamily(i)
                                     + " — ★ ✓ ⏰ ⚠ → stay boxes on this machine.");
                    return;
                }

                string missing;
                fontAsset.TryAddCharacters(DrawnUI.GlyphSet, out missing);
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
                fontAsset.name = "RunwayGlyphs " + i + " SDF";
                Save(fontAsset, assetPath);

                int got = DrawnUI.GlyphSet.Length - (missing == null ? 0 : missing.Length);
                Debug.Log("RUNWAY! baked " + got + "/" + DrawnUI.GlyphSet.Length
                          + " borrowed glyphs from " + DrawnUI.GlyphFaceFamily(i)
                          + " into " + assetPath
                          + (string.IsNullOrEmpty(missing) ? "" : "  · not in this face: " + missing));
            }
            catch (Exception e)
            {
                Debug.LogWarning("RUNWAY! glyph bake skipped for " + assetPath + " (" + e.Message
                                 + ") — DrawnUI asks the OS for the face at runtime instead.");
            }
        }

        // ── writing one out ────────────────────────────────────────────────────

        /// The material and the atlas have to live INSIDE the asset, or the font
        /// loads at runtime with nothing to draw with.
        static void Save(TMP_FontAsset fontAsset, string assetPath)
        {
            AssetDatabase.CreateAsset(fontAsset, assetPath);
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
        }
    }
}
#endif
