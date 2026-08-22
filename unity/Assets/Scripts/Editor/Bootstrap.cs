using System.IO;
using UnityEditor;
using UnityEngine;

namespace Runway.EditorTools
{
    // One-time headless project bootstrap: the two steps the first interactive
    // open would normally do (TMP essentials, the SDF font bake), callable via
    // -executeMethod so no human ever has to open the editor to get a build.
    public static class Bootstrap
    {
        public static void ImportTmpEssentials()
        {
            if (Directory.Exists("Assets/TextMesh Pro/Resources"))
            {
                Debug.Log("BOOTSTRAP: TMP essentials already present");
                return;
            }
            string pkg = Path.Combine("Library/PackageCache/com.unity.ugui@8bb446d869cd",
                "Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(pkg))
            {
                // the cache hash changes with package versions — find it
                foreach (var dir in Directory.GetDirectories("Library/PackageCache"))
                {
                    string cand = Path.Combine(dir, "Package Resources/TMP Essential Resources.unitypackage");
                    if (File.Exists(cand)) { pkg = cand; break; }
                }
            }
            if (!File.Exists(pkg))
            {
                Debug.LogError("BOOTSTRAP: TMP essentials package not found");
                EditorApplication.Exit(1);
                return;
            }
            AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("BOOTSTRAP: TMP essentials imported from " + pkg);
        }

        public static void BakeFonts()
        {
            Runway.App.FontBaker.Rebuild();
            Debug.Log("BOOTSTRAP: fonts baked");
        }
    }
}
