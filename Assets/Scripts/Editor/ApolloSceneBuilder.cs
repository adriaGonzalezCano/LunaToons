using Apollo.Shared;
using Apollo.Spacecraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Apollo.EditorTools
{
    /// <summary>Menu "Apollo > Build V1 Scene": generates a blockout spacecraft interior, player and simulation host, and saves Assets/Scenes/V1.unity.</summary>
    public static class ApolloSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/V1.unity";

        [MenuItem("Apollo/Build V1 Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var hostObject = new GameObject("SimulationHost");
            var host = hostObject.AddComponent<SimulationHost>();
            var overlay = hostObject.AddComponent<SimulationDebugOverlay>();
            new SerializedObject(overlay).FindProperty("host").objectReferenceValue = host;
            new SerializedObject(overlay).ApplyModifiedPropertiesWithoutUndo();

            var spacecraft = new GameObject("Spacecraft");
            Material hull = MakeMaterial("Hull", new Color(0.75f, 0.77f, 0.8f));
            Material floor = MakeMaterial("Floor", new Color(0.3f, 0.32f, 0.36f));
            Material panel = MakeMaterial("Panel", new Color(0.15f, 0.45f, 0.6f));

            // Interior volume: 8 m long (x), 3 m high (y), 4 m wide (z). Walls are 0.2 m thick, outside the volume.
            Block(spacecraft, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(8.4f, 0.2f, 4.4f), floor);
            Block(spacecraft, "Ceiling", new Vector3(0f, 3.1f, 0f), new Vector3(8.4f, 0.2f, 4.4f), hull);
            Block(spacecraft, "WallLeft", new Vector3(0f, 1.5f, -2.1f), new Vector3(8.4f, 3f, 0.2f), hull);
            Block(spacecraft, "WallRight", new Vector3(0f, 1.5f, 2.1f), new Vector3(8.4f, 3f, 0.2f), hull);
            Block(spacecraft, "WallFront", new Vector3(4.1f, 1.5f, 0f), new Vector3(0.2f, 3f, 4f), hull);
            Block(spacecraft, "WallBack", new Vector3(-4.1f, 1.5f, 0f), new Vector3(0.2f, 3f, 4f), hull);

            // Placeholder consoles so the room reads as one coherent space.
            Block(spacecraft, "ConsoleFront", new Vector3(3.5f, 0.6f, 0f), new Vector3(0.6f, 1.2f, 2.4f), panel);
            Block(spacecraft, "ConsoleLeft", new Vector3(0f, 0.5f, -1.7f), new Vector3(2.5f, 1f, 0.4f), panel);
            Block(spacecraft, "ConsoleRight", new Vector3(0f, 0.5f, 1.7f), new Vector3(2.5f, 1f, 0.4f), panel);

            AddLight(spacecraft, new Vector3(-2f, 2.6f, 0f));
            AddLight(spacecraft, new Vector3(2f, 2.6f, 0f));

            var player = new GameObject("Player");
            player.transform.position = new Vector3(-2.5f, 0.1f, 0f);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            player.AddComponent<FirstPersonController>();

            var cameraObject = new GameObject("PlayerCamera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Apollo: built and saved {ScenePath}");
        }

        private static void Block(GameObject parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent.transform, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void AddLight(GameObject parent, Vector3 position)
        {
            var lightObject = new GameObject("InteriorLight");
            lightObject.transform.SetParent(parent.transform, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 8f;
            light.intensity = 3f;
        }

        private static Material MakeMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            string path = $"Assets/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
