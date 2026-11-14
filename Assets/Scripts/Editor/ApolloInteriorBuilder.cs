using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apollo.EditorTools
{
    /// <summary>
    /// Builds the blockout spacecraft interior from primitives, loosely inspired by the Apollo Command Module cabin:
    /// off-white painted panels, dark instrument panel with switches and indicator lights, three couches facing it,
    /// overhead light panels and ducts, a side hatch-style round door and a framed window.
    /// Interior volume: x -4..4 (8 m, +X faces the instrument panel), y 0..3, z -2..2. Walls sit outside this volume.
    /// </summary>
    internal static class ApolloInteriorBuilder
    {
        private static System.Random random;

        public static GameObject Build()
        {
            random = new System.Random(7);
            var root = new GameObject("Spacecraft");

            Material hull = Mat("Hull", new Color(0.80f, 0.79f, 0.74f), 0f, 0.25f);
            Material cabinet = Mat("Cabinet", new Color(0.52f, 0.54f, 0.56f), 0.2f, 0.3f);
            Material darkPanel = Mat("PanelDark", new Color(0.09f, 0.09f, 0.10f), 0f, 0.3f);
            Material floorMat = Mat("Floor", new Color(0.20f, 0.21f, 0.23f), 0f, 0.3f);
            Material metal = Mat("Metal", new Color(0.62f, 0.64f, 0.67f), 0.9f, 0.55f);
            Material couch = Mat("Couch", new Color(0.70f, 0.67f, 0.58f), 0f, 0.1f);
            Material trim = Mat("Trim", new Color(0.42f, 0.42f, 0.43f), 0f, 0.2f);
            Material orange = Mat("Orange", new Color(0.88f, 0.42f, 0.08f), 0f, 0.25f);
            Material switchOff = Mat("SwitchOff", new Color(0.30f, 0.30f, 0.32f), 0.3f, 0.4f);
            Material lightPanel = Emissive("LightPanel", new Color(1f, 0.95f, 0.85f), 2.2f);
            Material ledGreen = Emissive("LedGreen", new Color(0.2f, 1f, 0.3f), 3f);
            Material ledAmber = Emissive("LedAmber", new Color(1f, 0.65f, 0.1f), 3f);
            Material ledRed = Emissive("LedRed", new Color(1f, 0.15f, 0.1f), 3f);
            Material ledWhite = Emissive("LedWhite", new Color(0.9f, 0.92f, 1f), 2.5f);
            Material[] indicators = { switchOff, switchOff, switchOff, switchOff, switchOff, switchOff, ledGreen, ledGreen, ledAmber, ledRed, ledWhite };

            BuildShell(root, hull, floorMat);
            BuildWindowFrame(root, metal);
            BuildWallPanelling(root, trim);
            BuildInstrumentPanel(root, darkPanel, metal, ledWhite, indicators);
            BuildCouches(root, couch, metal, trim);
            BuildSideBays(root, cabinet, darkPanel, orange, indicators);
            BuildHatch(root, metal, orange, darkPanel);
            BuildOverhead(root, metal, lightPanel);
            BuildFloorDetail(root, orange);
            BuildLights(root);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.17f, 0.19f);
            return root;
        }

        // --- structure --------------------------------------------------------------------------------------------

        private static void BuildShell(GameObject root, Material hull, Material floor)
        {
            Box(root, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(8.4f, 0.2f, 4.4f), floor);
            Box(root, "Ceiling", new Vector3(0f, 3.1f, 0f), new Vector3(8.4f, 0.2f, 4.4f), hull);
            Box(root, "WallRight", new Vector3(0f, 1.5f, 2.1f), new Vector3(8.4f, 3f, 0.2f), hull);
            Box(root, "WallFront", new Vector3(4.1f, 1.5f, 0f), new Vector3(0.2f, 3f, 4f), hull);
            Box(root, "WallBack", new Vector3(-4.1f, 1.5f, 0f), new Vector3(0.2f, 3f, 4f), hull);

            // Left wall (z = -2.1) with a 2 m x 1.2 m window opening (x -1..1, y 1.1..2.3). Open hole, no glass yet.
            // This wall faces Earth (nadir) in the fixed V1 attitude.
            Box(root, "WallLeft_A", new Vector3(-2.6f, 1.5f, -2.1f), new Vector3(3.2f, 3f, 0.2f), hull);
            Box(root, "WallLeft_B", new Vector3(2.6f, 1.5f, -2.1f), new Vector3(3.2f, 3f, 0.2f), hull);
            Box(root, "WallLeft_Below", new Vector3(0f, 0.55f, -2.1f), new Vector3(2f, 1.1f, 0.2f), hull);
            Box(root, "WallLeft_Above", new Vector3(0f, 2.65f, -2.1f), new Vector3(2f, 0.7f, 0.2f), hull);
        }

        private static void BuildWindowFrame(GameObject root, Material metal)
        {
            Decor(root, "WindowFrame_Top", new Vector3(0f, 2.3f, -1.95f), new Vector3(2.24f, 0.12f, 0.14f), metal);
            Decor(root, "WindowFrame_Bottom", new Vector3(0f, 1.1f, -1.95f), new Vector3(2.24f, 0.12f, 0.14f), metal);
            Decor(root, "WindowFrame_Left", new Vector3(-1f, 1.7f, -1.95f), new Vector3(0.12f, 1.3f, 0.14f), metal);
            Decor(root, "WindowFrame_Right", new Vector3(1f, 1.7f, -1.95f), new Vector3(0.12f, 1.3f, 0.14f), metal);
        }

        private static void BuildWallPanelling(GameObject root, Material trim)
        {
            // Raised seams on the right wall (inner face z = 2.0) and back wall (inner face x = -4.0).
            foreach (float y in new[] { 1.0f, 2.0f })
            {
                Decor(root, "SeamRightH", new Vector3(0f, y, 1.985f), new Vector3(8f, 0.04f, 0.03f), trim);
                Decor(root, "SeamBackH", new Vector3(-3.985f, y, 0f), new Vector3(0.03f, 0.04f, 4f), trim);
            }
            for (float x = -3.5f; x <= 3.51f; x += 1f)
                Decor(root, "SeamRightV", new Vector3(x, 1.5f, 1.985f), new Vector3(0.04f, 3f, 0.03f), trim);
            for (float z = -1.5f; z <= 1.51f; z += 1f)
                Decor(root, "SeamBackV", new Vector3(-3.985f, 1.5f, z), new Vector3(0.03f, 3f, 0.04f), trim);
        }

        // --- main instrument panel (front wall, +X) ------------------------------------------------------------------

        private static void BuildInstrumentPanel(GameObject root, Material dark, Material metal, Material gaugeFace, Material[] indicators)
        {
            Box(root, "ConsoleBase", new Vector3(3.7f, 0.45f, 0f), new Vector3(0.5f, 0.9f, 3.6f), dark);
            Decor(root, "ConsoleShelf", new Vector3(3.62f, 0.92f, 0f), new Vector3(0.66f, 0.06f, 3.6f), metal);
            Box(root, "InstrumentPanel", new Vector3(3.92f, 1.5f, 0f), new Vector3(0.1f, 1.2f, 3.6f), dark);

            // Switch and indicator grid on the panel face (x = 3.865).
            for (int row = 0; row < 6; row++)
            {
                for (int col = 0; col < 22; col++)
                {
                    float y = 1.0f + row * 0.15f;
                    float z = -1.54f + col * 0.147f;
                    Material m = indicators[random.Next(indicators.Length)];
                    Decor(root, "Switch", new Vector3(3.86f, y, z), new Vector3(0.03f, 0.06f, 0.05f), m);
                }
            }

            // Round gauges above the grid, axis along X.
            for (int i = 0; i < 5; i++)
            {
                var gauge = Cylinder(root, "Gauge", new Vector3(3.855f, 1.97f, -1.2f + i * 0.6f), new Vector3(0.2f, 0.015f, 0.2f), gaugeFace);
                gauge.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        // --- couches ------------------------------------------------------------------------------------------------------

        private static void BuildCouches(GameObject root, Material couch, Material metal, Material trim)
        {
            foreach (float z in new[] { -0.8f, 0f, 0.8f })
            {
                Box(root, "CouchBase", new Vector3(0.9f, 0.15f, z), new Vector3(0.7f, 0.3f, 0.5f), metal);
                Box(root, "CouchSeat", new Vector3(0.9f, 0.45f, z), new Vector3(0.8f, 0.2f, 0.62f), couch);

                var back = Box(root, "CouchBack", new Vector3(0.5f, 0.95f, z), new Vector3(0.14f, 0.75f, 0.62f), couch);
                back.transform.localRotation = Quaternion.Euler(0f, 0f, 10f); // positive Z rotation tips the top away from the console

                var head = Decor(root, "CouchHeadrest", new Vector3(0.44f, 1.42f, z), new Vector3(0.12f, 0.25f, 0.4f), couch);
                head.transform.localRotation = Quaternion.Euler(0f, 0f, 10f);

                Decor(root, "CouchArmL", new Vector3(0.95f, 0.65f, z - 0.34f), new Vector3(0.55f, 0.08f, 0.06f), trim);
                Decor(root, "CouchArmR", new Vector3(0.95f, 0.65f, z + 0.34f), new Vector3(0.55f, 0.08f, 0.06f), trim);
            }
        }

        // --- side equipment bays ----------------------------------------------------------------------------------------

        private static void BuildSideBays(GameObject root, Material cabinet, Material dark, Material orange, Material[] indicators)
        {
            // Lower cabinets, thin so a 0.7 m aisle remains between them and the couches.
            Box(root, "BayRightLower", new Vector3(1.2f, 0.45f, 1.91f), new Vector3(2.4f, 0.9f, 0.18f), cabinet);
            Box(root, "BayLeftLower", new Vector3(0f, 0.45f, -1.91f), new Vector3(2.4f, 0.9f, 0.18f), cabinet);
            Box(root, "BayBackRightLower", new Vector3(-2.2f, 0.45f, 1.91f), new Vector3(2.4f, 0.9f, 0.18f), cabinet);

            foreach (float x in new[] { 0.5f, 1.2f, 1.9f })
                Decor(root, "Handle", new Vector3(x, 0.7f, 1.81f), new Vector3(0.3f, 0.04f, 0.03f), orange);

            // Upper dark panel with a row of indicators on the right wall.
            Decor(root, "PanelRightUpper", new Vector3(1.2f, 1.9f, 1.94f), new Vector3(2.4f, 0.9f, 0.08f), dark);
            for (int i = 0; i < 12; i++)
            {
                Material m = indicators[random.Next(indicators.Length)];
                Decor(root, "BayLed", new Vector3(0.2f + i * 0.18f, 2.2f, 1.89f), new Vector3(0.06f, 0.06f, 0.03f), m);
            }
        }

        // --- hatch, overhead, floor -------------------------------------------------------------------------------------

        private static void BuildHatch(GameObject root, Material metal, Material orange, Material dark)
        {
            var ring = Cylinder(root, "HatchRing", new Vector3(-3.93f, 1.3f, 0f), new Vector3(1.2f, 0.03f, 1.2f), orange);
            ring.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var door = Cylinder(root, "HatchDoor", new Vector3(-3.91f, 1.3f, 0f), new Vector3(1.0f, 0.04f, 1.0f), metal);
            door.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var porthole = Cylinder(root, "HatchWindow", new Vector3(-3.885f, 1.45f, 0f), new Vector3(0.28f, 0.02f, 0.28f), dark);
            porthole.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Decor(root, "HatchHandle", new Vector3(-3.87f, 1.05f, 0f), new Vector3(0.05f, 0.08f, 0.5f), orange);
        }

        private static void BuildOverhead(GameObject root, Material metal, Material lightPanel)
        {
            foreach (float x in new[] { -2.5f, 0f, 2.5f })
                Decor(root, "CeilingLight", new Vector3(x, 2.98f, 0f), new Vector3(1.2f, 0.04f, 0.5f), lightPanel);

            foreach (float z in new[] { -1.6f, 1.6f })
            {
                var duct = Cylinder(root, "Duct", new Vector3(0f, 2.85f, z), new Vector3(0.16f, 3.9f, 0.16f), metal);
                duct.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // cylinder axis along X
            }
        }

        private static void BuildFloorDetail(GameObject root, Material orange)
        {
            foreach (float z in new[] { -1.5f, 1.5f })
                Decor(root, "FloorStripe", new Vector3(-0.2f, 0.005f, z), new Vector3(7.2f, 0.01f, 0.06f), orange);
        }

        private static void BuildLights(GameObject root)
        {
            foreach (float x in new[] { -2.5f, 0f, 2.5f })
                AddLight(root, new Vector3(x, 2.7f, 0f), new Color(1f, 0.93f, 0.8f), 6f, 2.2f);
            AddLight(root, new Vector3(3.3f, 1.5f, 0f), new Color(0.4f, 1f, 0.6f), 3f, 1.2f); // soft glow from the instrument panel
        }

        // --- helpers --------------------------------------------------------------------------------------------------------

        /// <summary>Solid box (keeps its collider).</summary>
        private static GameObject Box(GameObject parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent.transform, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            return cube;
        }

        /// <summary>Visual-only box (collider removed so the player is never snagged by detail).</summary>
        private static GameObject Decor(GameObject parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = Box(parent, name, position, scale, material);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            return cube;
        }

        /// <summary>Visual-only cylinder. Unit cylinder is 2 m tall along Y, so scale.y is half the length.</summary>
        private static GameObject Cylinder(GameObject parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent.transform, false);
            cylinder.transform.localPosition = position;
            cylinder.transform.localScale = scale;
            cylinder.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            return cylinder;
        }

        private static void AddLight(GameObject parent, Vector3 position, Color color, float range, float intensity)
        {
            var lightObject = new GameObject("InteriorLight");
            lightObject.transform.SetParent(parent.transform, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
        }

        private static Material Mat(string name, Color color, float metallic, float smoothness)
        {
            Material material = LoadOrCreate(name);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Emissive(string name, Color color, float intensity)
        {
            Material material = LoadOrCreate(name);
            material.SetColor("_BaseColor", color * 0.2f);
            material.SetFloat("_Smoothness", 0.2f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * intensity);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreate(string name)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            string path = $"Assets/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
