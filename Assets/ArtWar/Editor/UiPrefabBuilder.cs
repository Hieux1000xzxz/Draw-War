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
            Save(PracticeView.Build().gameObject, UiFolder + "/Practice.prefab");
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Brings existing prefabs up to date without laying them out again: saves the practice room if it's missing
        /// and adds the "Practice" button to the title page of Menus.prefab (the other title buttons move over to make
        /// room). Everything else edited in the prefabs is kept.
        /// </summary>
        [MenuItem("Draw War/UI/Add Missing UI (keep edits)")]
        public static string AddMissing()
        {
            string log = "";
            string practicePath = UiFolder + "/Practice.prefab";
            if (!File.Exists(practicePath))
            {
                UiKit.ClearCache();
                Save(PracticeView.Build().gameObject, practicePath);
                log += "Practice.prefab saved. ";
            }

            string menusPath = UiFolder + "/Menus.prefab";
            var root = PrefabUtility.LoadPrefabContents(menusPath);
            var menu = root.GetComponent<MenuView>();
            if (menu != null && menu.practiceButton == null && menu.titlePage != null)
            {
                var sheet = menu.titlePage.transform;
                menu.practiceButton = UiKit.Button(sheet, "Practice", new Vector2(60f, 45f), new Vector2(270f, 92f),
                    MenuView.PracticeGreen, 28, "Luyện vẽ", "Practice");
                Place(menu.codexButton, 350f);
                Place(menu.helpButton, 640f);
                Place(menu.settingsButton, 930f);
                PrefabUtility.SaveAsPrefabAsset(root, menusPath);
                log += "Practice button added to Menus.prefab. ";
            }
            if (menu != null && menu.versusButton == null && menu.titlePage != null && menu.playButton != null)
            {
                // the PLAY button makes room for a "2 PLAYERS" button beside it
                var play = (RectTransform)menu.playButton.transform;
                play.anchoredPosition = new Vector2(250f, play.anchoredPosition.y);
                play.sizeDelta = new Vector2(370f, play.sizeDelta.y);
                menu.versusButton = UiKit.Button(menu.titlePage.transform, "Versus", new Vector2(640f, play.anchoredPosition.y),
                    new Vector2(370f, play.sizeDelta.y), MenuView.VersusRed, 36, "2 NGƯỜI CHƠI", "2 PLAYERS");
                menu.versusButton.GetComponentInChildren<TMPro.TMP_Text>().textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                PrefabUtility.SaveAsPrefabAsset(root, menusPath);
                log += "2-player button added to Menus.prefab. ";
            }
            if (menu != null && menu.onlineButton == null && menu.titlePage != null && menu.playButton != null && menu.versusButton != null)
            {
                // PLAY · 2 PLAYERS · ONLINE in one row
                var play = (RectTransform)menu.playButton.transform;
                var vs = (RectTransform)menu.versusButton.transform;
                float y = play.anchoredPosition.y, h = play.sizeDelta.y;
                play.anchoredPosition = new Vector2(90f, y);
                play.sizeDelta = new Vector2(340f, h);
                vs.anchoredPosition = new Vector2(460f, y);
                vs.sizeDelta = new Vector2(340f, h);
                menu.onlineButton = UiKit.Button(menu.titlePage.transform, "Online", new Vector2(830f, y), new Vector2(340f, h),
                    MenuView.OnlinePurple, 34, "CHƠI ONLINE", "ONLINE");
                menu.onlineButton.GetComponentInChildren<TMPro.TMP_Text>().textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                log += "Online button added to Menus.prefab. ";
            }
            if (menu != null && menu.onlinePage == null && menu.backdrop != null)
            {
                MenuView.BuildOnline(menu, menu.backdrop.transform);
                menu.onlinePage.SetActive(false);
                log += "Online page added to Menus.prefab. ";
            }
            if (log.Contains("Online")) PrefabUtility.SaveAsPrefabAsset(root, menusPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            return log == "" ? "nothing missing" : log;
        }

        static void Place(UnityEngine.UI.Button button, float x)
        {
            if (button == null) return;
            var rt = (RectTransform)button.transform;
            rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
            rt.sizeDelta = new Vector2(270f, rt.sizeDelta.y);
            var label = UiKit.ButtonLabel(button);
            if (label != null) label.fontSizeMax = Mathf.Min(label.fontSizeMax, 28f);
        }

        static void Save(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
