using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArtWar.EditorTools
{
    /// <summary>
    /// Saves the hand-drawn UI skin as sprites and the HUD, menus and handbook card as prefabs in
    /// Assets/ArtWar/Resources/UI, where the game loads them from. After that, edit the prefabs freely:
    /// layout, colours, fonts, sprites. Rebuilding overwrites those edits, so it asks first.
    /// </summary>
    public static class UiPrefabBuilder
    {
        const string UiFolder = "Assets/ArtWar/Resources/UI";
        const string SkinFolder = UiFolder + "/Skin";

        [MenuItem("Draw War/UI/Rebuild Skin Sprites")]
        public static void RebuildSkinMenu()
        {
            if (!EditorUtility.DisplayDialog("Rebuild UI skin",
                    "Repaint the hand-drawn button, panel, sheet, bar and knob sprites? Edited sprite files will be replaced.",
                    "Repaint", "Cancel")) return;
            SaveSkin();
        }

        [MenuItem("Draw War/UI/Rebuild UI Prefabs")]
        public static void RebuildPrefabsMenu()
        {
            if (File.Exists(UiFolder + "/HUD.prefab") && !EditorUtility.DisplayDialog("Rebuild UI prefabs",
                    "Lay out HUD, Menus and CodexCard again from code? Changes made to those prefabs will be lost.",
                    "Rebuild", "Cancel")) return;
            Execute();
        }

        /// <summary>Fonts and skin sprites (only the missing ones) and all three prefabs, without asking.</summary>
        public static string Execute()
        {
            SaveFonts(onlyMissing: true);
            SaveSkin(onlyMissing: true);
            SavePrefabs();
            return "UI fonts, skin and prefabs saved in Assets/ArtWar/Resources";
        }

        const string FontFolder = "Assets/ArtWar/Resources/Fonts";

        /// <summary>Every letter the game writes in either language, put in the atlas up front.</summary>
        const string Letters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~×·…" +
            "àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ" +
            "ÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬÈÉẺẼẸÊỀẾỂỄỆÌÍỈĨỊÒÓỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢÙÚỦŨỤƯỪỨỬỮỰỲÝỶỸỴĐ";

        /// <summary>
        /// TextMeshPro font assets: Pangolin (all Latin and Vietnamese letters baked in, more added on the fly), with
        /// Noto Sans Symbols 2 and Liberation Sans behind it for ★ ☆ ▾ ◀ ▶ ✎ →, plus the shadow and outline materials.
        /// </summary>
        public static void SaveFonts(bool onlyMissing = false)
        {
            string pangolinPath = FontFolder + "/Pangolin SDF.asset", symbolsPath = FontFolder + "/Noto Symbols SDF.asset";
            if (onlyMissing && File.Exists(pangolinPath)) return;
            AssetDatabase.DeleteAsset(pangolinPath);
            AssetDatabase.DeleteAsset(symbolsPath);

            var symbols = SaveFontAsset(FontFolder + "/NotoSansSymbols2-Regular.ttf", symbolsPath, "Noto Symbols SDF", 512, "★☆▾◀▶✎");
            var pangolin = SaveFontAsset(FontFolder + "/Pangolin-Regular.ttf", pangolinPath, "Pangolin SDF", 1024, Letters);
            pangolin.fallbackFontAssetTable = new System.Collections.Generic.List<TMPro.TMP_FontAsset> { symbols };
            var liberation = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (liberation != null) pangolin.fallbackFontAssetTable.Add(liberation);
            EditorUtility.SetDirty(pangolin);

            SaveMaterial(UiKit.MakeShadowMaterial(pangolin.material), FontFolder + "/Pangolin SDF Shadow.mat");
            SaveMaterial(UiKit.MakeOutlineMaterial(pangolin.material), FontFolder + "/Pangolin SDF Outline.mat");
            AssetDatabase.SaveAssets();
            UiKit.ClearCache();
        }

        static TMPro.TMP_FontAsset SaveFontAsset(string source, string path, string name, int atlas, string characters)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(source);
            var asset = TMPro.TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                atlas, atlas, TMPro.AtlasPopulationMode.Dynamic, true);
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
            asset.atlasTexture.name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.TryAddCharacters(characters, out _);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static void SaveMaterial(Material m, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
        }

        public static void SaveSkin(bool onlyMissing = false)
        {
            Directory.CreateDirectory(SkinFolder);
            foreach (SkinPart part in System.Enum.GetValues(typeof(SkinPart)))
            {
                string path = $"{SkinFolder}/{UiKit.SkinName(part)}.png";
                if (onlyMissing && File.Exists(path)) continue;
                File.WriteAllBytes(path, DoodleSkin.Paint(part).EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.spriteBorder = DoodleSkin.Border(part);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            string grid = SkinFolder + "/grid_paper.png";
            if (!onlyMissing || !File.Exists(grid))
            {
                File.WriteAllBytes(grid, DoodleSkin.GridTile().EncodeToPNG());
                AssetDatabase.ImportAsset(grid, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(grid);
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            UiKit.ClearCache();
        }

        public static void SavePrefabs()
        {
            Directory.CreateDirectory(UiFolder);
            UiKit.ClearCache(); // lay out with the saved sprite assets, so the prefabs reference them

            Save(HudView.Build().gameObject, UiFolder + "/HUD.prefab");
            Save(MenuView.Build().gameObject, UiFolder + "/Menus.prefab");
            Save(CodexCardView.Build().gameObject, UiFolder + "/CodexCard.prefab");
            AssetDatabase.SaveAssets();
        }

        static void Save(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
