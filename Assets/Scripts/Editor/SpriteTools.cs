using System.IO;
using Factory.Presentation;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Factory.EditorTools
{
    /// <summary>Menu Factory > Sprites: seed the PNGs from the code drawings and build the atlas.</summary>
    public static class SpriteTools
    {
        public const string AtlasPath = "Assets/Resources/SpriteAtlas.spriteatlasv2";

        [MenuItem("Factory/Sprites/Exportar sprites gerados (só os que faltam)")]
        private static void ExportMissingMenu() => Debug.Log($"[SpriteTools] {Export(overwrite: false)} PNG(s) criados em {SpriteImportRules.Folder}");

        [MenuItem("Factory/Sprites/Restaurar todos os sprites originais (sobrescreve)")]
        private static void ExportAllMenu()
        {
            if (!EditorUtility.DisplayDialog("Restaurar sprites",
                    "Sobrescrever todos os PNGs de " + SpriteImportRules.Folder + " pelos desenhos originais do código?",
                    "Sobrescrever", "Cancelar")) return;
            Debug.Log($"[SpriteTools] {Export(overwrite: true)} PNG(s) restaurados");
        }

        [MenuItem("Factory/Sprites/Criar ou atualizar Sprite Atlas")]
        private static void AtlasMenu() => CreateOrUpdateAtlas();

        /// <summary>Writes the code drawing of each art sprite as a PNG. Returns how many were written.</summary>
        public static int Export(bool overwrite)
        {
            Directory.CreateDirectory(SpriteImportRules.Folder);
            int written = 0;
            foreach (var (fileName, draw) in SpriteLibrary.Art)
            {
                string path = $"{SpriteImportRules.Folder}/{fileName}.png";
                if (!overwrite && File.Exists(path)) continue;
                File.WriteAllBytes(path, SpriteLibrary.EncodePng(draw()));
                written++;
            }
            AssetDatabase.Refresh();
            return written;
        }

        /// <summary>
        /// Packs every sprite in the folder into one texture, so the ground, buildings and items
        /// share a texture and batch together. Settings keep pixels crisp and unbled.
        /// </summary>
        public static void CreateOrUpdateAtlas()
        {
            if (!File.Exists(AtlasPath))
            {
                var asset = new SpriteAtlasAsset();
                asset.Add(new Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(SpriteImportRules.Folder) });
                SpriteAtlasAsset.Save(asset, AtlasPath);
                AssetDatabase.Refresh();
            }

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.includeInBuild = true;
            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                padding = 2,
                enableRotation = false,
                enableTightPacking = false,
                enableAlphaDilation = false,
                blockOffset = 1,
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                filterMode = FilterMode.Point,
                generateMipMaps = false,
                readable = false,
                sRGB = true,
            };
            var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.maxTextureSize = 512;
            importer.SetPlatformSettings(platform);
            importer.SaveAndReimport();
            Debug.Log("[SpriteTools] Atlas pronto: " + AtlasPath);
        }
    }
}
