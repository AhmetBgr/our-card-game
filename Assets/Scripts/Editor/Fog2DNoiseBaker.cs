using System.IO;
using UnityEditor;
using UnityEngine;

// Bakes the tileable noise texture that Custom/2D/Fog2D samples instead of computing noise per pixel.
// Every channel is seamless tiling fBm Perlin noise, stretched to the full 0..1 range:
//   R = puff detail   (8 cells per tile, 4 octaves)  -- DETAIL_CELLS in the shader
//   G, B = swirl field (4 cells per tile, 3 octaves, independent seeds)
//   A = fog banks     (4 cells per tile, 2 octaves)  -- BANK_CELLS in the shader
public static class Fog2DNoiseBaker
{
    public const string TexturePath = "Assets/Textures/Fog2DNoise.png";
    private const int Size = 256;

    [MenuItem("Tools/Fog2D/Bake Noise Texture")]
    public static void Bake()
    {
        float[] r = Channel(8, 4, 11);
        float[] g = Channel(4, 3, 23);
        float[] b = Channel(4, 3, 37);
        float[] a = Channel(4, 2, 53);

        var pixels = new Color32[Size * Size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(ToByte(r[i]), ToByte(g[i]), ToByte(b[i]), ToByte(a[i]));

        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(pixels);
        texture.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
        File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        // Block compression turns smooth gradients into visible steps; 256x256 RGBA32 is ~350 KB with mips.
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        Debug.Log($"Fog2D noise baked to {TexturePath}");
    }

    private static float[] Channel(int baseCells, int octaves, int seed)
    {
        var values = new float[Size * Size];
        float min = float.MaxValue, max = float.MinValue;

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float sum = 0f, amplitude = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                int cells = baseCells << o;
                sum += amplitude * Perlin(x * cells / (float)Size, y * cells / (float)Size, cells, seed + o * 101);
                norm += amplitude;
                amplitude *= 0.5f;
            }

            float v = sum / norm;
            values[y * Size + x] = v;
            if (v < min) min = v;
            if (v > max) max = v;
        }

        float range = Mathf.Max(max - min, 1e-6f);
        for (int i = 0; i < values.Length; i++) values[i] = (values[i] - min) / range;
        return values;
    }

    // Gradient noise whose lattice wraps every `period` cells, so the texture tiles seamlessly.
    private static float Perlin(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        float u = Fade(fx), w = Fade(fy);

        float n00 = Gradient(x0, y0, period, seed, fx, fy);
        float n10 = Gradient(x0 + 1, y0, period, seed, fx - 1f, fy);
        float n01 = Gradient(x0, y0 + 1, period, seed, fx, fy - 1f);
        float n11 = Gradient(x0 + 1, y0 + 1, period, seed, fx - 1f, fy - 1f);
        return Mathf.Lerp(Mathf.Lerp(n00, n10, u), Mathf.Lerp(n01, n11, u), w);
    }

    private static float Gradient(int ix, int iy, int period, int seed, float dx, float dy)
    {
        ix = (ix % period + period) % period;
        iy = (iy % period + period) % period;
        float angle = Hash(ix, iy, seed) / 4294967296f * Mathf.PI * 2f;
        return Mathf.Cos(angle) * dx + Mathf.Sin(angle) * dy;
    }

    private static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
}
