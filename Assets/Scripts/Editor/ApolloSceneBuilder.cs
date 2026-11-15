using Apollo.Core;
using Apollo.MissionControl;
using Apollo.Shared;
using Apollo.Spacecraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Apollo.EditorTools
{
    /// <summary>
    /// Menu "Apollo > Build V1 Scene": regenerates Assets/Scenes/V1.unity (overwrites it) with the simulation host,
    /// the spacecraft interior, the external Earth view, the player and two Mission Control rigs
    /// (1: 3D orbit view, 2: flat map with ground track).
    /// </summary>
    public static class ApolloSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/V1.unity";

        [MenuItem("Apollo/Build V1 Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Simulation and global systems (always active).
            var systems = new GameObject("SimulationHost");
            var host = systems.AddComponent<SimulationHost>();
            SetRef(systems.AddComponent<SimulationTimeControls>(), "host", host);
            SetRef(systems.AddComponent<DebugImpulseControls>(), "host", host);
            var switcher = systems.AddComponent<EnvironmentSwitcher>();

            ApolloInteriorBuilder.Build();

            var externalView = new GameObject("ExternalView").AddComponent<ExternalView>();
            SetRef(externalView, "host", host);

            GameObject player = BuildPlayer();
            GameObject orbitRig = BuildOrbitRig(host);
            GameObject mapRig = BuildMapRig(host);

            SetRefs(switcher, "rigs", player, orbitRig, mapRig);
            orbitRig.SetActive(false);
            mapRig.SetActive(false);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Apollo: built and saved {ScenePath}");
        }

        private static GameObject BuildPlayer()
        {
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
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 20000f; // the scaled Earth and stars extend to ~14000 units
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();
            return player;
        }

        // Control Center 1: 3D orbit view. Placed far from the interior and beyond the player camera's far plane.
        private static GameObject BuildOrbitRig(SimulationHost host)
        {
            var rig = new GameObject("ControlCenter1_OrbitRig");
            rig.transform.position = new Vector3(100000f, 0f, 0f);
            SetRef(rig.AddComponent<OrbitView>(), "host", host);
            SetRef(rig.AddComponent<TelemetryPanel>(), "host", host);

            var cameraObject = new GameObject("OrbitCamera");
            cameraObject.transform.SetParent(rig.transform, false);
            cameraObject.AddComponent<OrbitCamera>(); // positions itself around the rig origin
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 2000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();
            return rig;
        }

        // Control Center 2: flat map with ground track, seen by an orthographic camera.
        private static GameObject BuildMapRig(SimulationHost host)
        {
            var rig = new GameObject("ControlCenter2_MapRig");
            rig.transform.position = new Vector3(200000f, 0f, 0f);
            SetRef(rig.AddComponent<GroundTrackView>(), "host", host);
            var telemetry = rig.AddComponent<TelemetryPanel>();
            SetRef(telemetry, "host", host);
            SetBool(telemetry, "compact", true);

            var cameraObject = new GameObject("MapCamera");
            cameraObject.transform.SetParent(rig.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -100f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 60f; // refined every frame by GroundTrackView
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.03f, 0.05f);
            cameraObject.AddComponent<AudioListener>();
            return rig;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRefs(Object target, string field, params Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty array = serialized.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
