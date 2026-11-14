using UnityEditor;

namespace Apollo.EditorTools
{
    /// <summary>
    /// Applies sensible import settings to Assets/Resources/EarthMap.* (equirectangular Earth map):
    /// up to 8192 px wide, mipmaps, repeat horizontally (longitude wraps) and clamp vertically (no pole seam).
    /// </summary>
    public sealed class EarthMapImportSettings : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/EarthMap")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 8192;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.wrapModeU = UnityEngine.TextureWrapMode.Repeat;
            importer.wrapModeV = UnityEngine.TextureWrapMode.Clamp;
        }
    }
}
