using UnityEngine;

namespace Apollo.Shared
{
    /// <summary>Runtime helpers for placeholder visuals: unlit materials, a procedural Earth texture and a UV sphere mesh.</summary>
    public static class ViewFactory
    {
        public static Material Unlit(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader);
            material.color = color;
            return material;
        }

        public static Material EarthMaterial()
        {
            Material material = Unlit(Color.white);
            material.mainTexture = EarthTexture();
            return material;
        }

        /// <summary>Unit-diameter UV sphere (radius 0.5), north pole on +Y. Scale the transform by the desired diameter.</summary>
        public static Mesh CreateSphereMesh(int longitudeSegments, int latitudeSegments)
        {
            int lon = longitudeSegments, lat = latitudeSegments;
            var vertices = new Vector3[(lon + 1) * (lat + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[lon * lat * 6];

            for (int j = 0; j <= lat; j++)
            {
                float v = (float)j / lat;
                float phi = (v - 0.5f) * Mathf.PI;
                for (int i = 0; i <= lon; i++)
                {
                    float u = (float)i / lon;
                    float theta = u * 2f * Mathf.PI;
                    int index = j * (lon + 1) + i;
                    vertices[index] = new Vector3(Mathf.Cos(phi) * Mathf.Cos(theta), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(theta)) * 0.5f;
                    uvs[index] = new Vector2(u, v);
                }
            }

            int t = 0;
            for (int j = 0; j < lat; j++)
            {
                for (int i = 0; i < lon; i++)
                {
                    int a = j * (lon + 1) + i, b = a + 1, c = a + lon + 1, d = c + 1;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = d;
                    triangles[t++] = a; triangles[t++] = d; triangles[t++] = b;
                }
            }

            var mesh = new Mesh { name = "UVSphere", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material, float diameter, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * diameter;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        // Procedural placeholder Earth (noise continents). No real map data: for orientation and motion cues only.
        private static Texture2D EarthTexture()
        {
            const int width = 512, height = 256;
            var pixels = new Color[width * height];
            var ocean = new Color(0.05f, 0.2f, 0.5f);
            var deepOcean = new Color(0.02f, 0.1f, 0.35f);
            var green = new Color(0.2f, 0.5f, 0.2f);
            var brown = new Color(0.5f, 0.42f, 0.28f);

            for (int y = 0; y < height; y++)
            {
                float lat = ((y + 0.5f) / height - 0.5f) * Mathf.PI;
                float cl = Mathf.Cos(lat), sl = Mathf.Sin(lat);
                for (int x = 0; x < width; x++)
                {
                    float lon = (x + 0.5f) / width * 2f * Mathf.PI;
                    float px = cl * Mathf.Cos(lon), py = sl, pz = cl * Mathf.Sin(lon);
                    float n = Noise3(px, py, pz, 2f) * 0.65f + Noise3(px, py, pz, 5f) * 0.35f;

                    Color c;
                    if (Mathf.Abs(lat) > 1.25f) c = Color.white;
                    else if (n > 0.52f) c = Color.Lerp(green, brown, Mathf.InverseLerp(0.52f, 0.7f, n));
                    else c = Color.Lerp(deepOcean, ocean, Mathf.InverseLerp(0.3f, 0.52f, n));
                    pixels[y * width + x] = c;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true);
            texture.wrapModeU = TextureWrapMode.Repeat;
            texture.wrapModeV = TextureWrapMode.Clamp;
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        // Seamless pseudo-3D noise (a function of the 3D point, so there is no seam at the longitude wrap).
        private static float Noise3(float x, float y, float z, float s)
        {
            return (Mathf.PerlinNoise(x * s + 11.1f, y * s + 7.3f)
                  + Mathf.PerlinNoise(y * s + 3.7f, z * s + 19.9f)
                  + Mathf.PerlinNoise(z * s + 5.5f, x * s + 13.3f)) / 3f;
        }
    }
}
