using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Trims the rendered unit illustrations to their opaque area and imports them into Resources/UnitArt.</summary>
public static class ImportArt
{
    const string Src = @"C:\Games\Art War\ArtSource\png2x\";
    const string Dst = "Assets/ArtWar/Resources/UnitArt/";
    static readonly string[] Kinds =
        { "Infantry", "Cavalry", "Archer", "Healer", "Wall", "Turret", "Tank", "Plane", "Jet", "Helicopter", "Dragon" };

    public static string Execute()
    {
        var log = new StringBuilder();
        System.IO.Directory.CreateDirectory(Dst);
        foreach (var kind in Kinds)
        {
            string file = Src + kind + ".png";
            if (!System.IO.File.Exists(file)) { log.AppendLine(kind + ": missing"); continue; }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(System.IO.File.ReadAllBytes(file));
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            int minX = w, minY = h, maxX = -1, maxY = -1, transparent = 0;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte a = px[y * w + x].a;
                if (a < 8) { transparent++; continue; }
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            if (maxX < 0) { log.AppendLine(kind + ": empty"); continue; }
            if (transparent < w * h / 20) { log.AppendLine(kind + ": WARNING background not transparent"); }

            const int margin = 4;
            minX = Mathf.Max(0, minX - margin); minY = Mathf.Max(0, minY - margin);
            maxX = Mathf.Min(w - 1, maxX + margin); maxY = Mathf.Min(h - 1, maxY + margin);
            int cw = maxX - minX + 1, ch = maxY - minY + 1;
            var cropped = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            cropped.SetPixels32(GetBlock(px, w, minX, minY, cw, ch));
            cropped.Apply();

            string path = Dst + kind + ".png";
            System.IO.File.WriteAllBytes(path, cropped.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(cropped);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 1024;
            imp.npotScale = TextureImporterNPOTScale.None; // keep the true aspect ratio
            imp.isReadable = true;                         // the game reads alpha to cut the sticker border
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            log.AppendLine($"{kind}: {cw}x{ch}");
        }
        return log.ToString();
    }

    static Color32[] GetBlock(Color32[] px, int w, int x0, int y0, int cw, int ch)
    {
        var block = new Color32[cw * ch];
        for (int y = 0; y < ch; y++)
            System.Array.Copy(px, (y0 + y) * w + x0, block, y * cw, cw);
        return block;
    }
}
