using UnityEditor;
using UnityEngine;

namespace Factory.EditorTools
{
    /// <summary>
    /// Any PNG placed in Assets/Resources/Sprites is imported as crisp pixel art automatically:
    /// 16 pixels per tile, Point filter, no compression, no mipmaps, centered pivot.
    /// To change a graphic, just overwrite its PNG; no Inspector work needed.
    /// </summary>
    public sealed class SpriteImportRules : AssetPostprocessor
    {
        public const string Folder = "Assets/Resources/Sprites";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder + "/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);
        }
    }
}
