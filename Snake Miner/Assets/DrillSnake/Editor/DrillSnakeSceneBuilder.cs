using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DrillSnake.Editor
{
    public static class DrillSnakeSceneBuilder
    {
        public const string PrototypeScenePath =
            "Assets/DrillSnake/Scenes/DrillSnakePrototype.unity";

        [MenuItem("Tools/Drill Snake/Build Prototype Scene")]
        public static void BuildPrototypeScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrototypeScenePath));

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var runtime = new GameObject("Drill Snake Runtime");
            runtime.AddComponent<DrillSnakeController>();
            SceneManager.MoveGameObjectToScene(runtime, scene);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, PrototypeScenePath))
            {
                throw new System.InvalidOperationException(
                    $"Could not save {PrototypeScenePath}.");
            }

            AddPrototypeToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = runtime;
            Debug.Log(
                $"Drill Snake prototype scene built at {PrototypeScenePath}. " +
                "Enter Play Mode to begin.");
        }

        public static void BuildPrototypeSceneForBatch()
        {
            BuildPrototypeScene();
            ValidatePrototype();
        }

        [MenuItem("Tools/Drill Snake/Validate Prototype")]
        public static void ValidatePrototype()
        {
            if (!File.Exists(PrototypeScenePath))
            {
                throw new FileNotFoundException(
                    "Build the Drill Snake prototype scene first.",
                    PrototypeScenePath);
            }

            var map = DrillSnakeMap.Generate(240628);
            if (map.Width != 45 ||
                map.Height != 45 ||
                map.Docks.Count != 4 ||
                map.Docks.Any(dock =>
                    map.GetCell(dock) != DrillSnakeCellType.RefineryDock) ||
                map.CountCells(DrillSnakeCellType.CommonOre) < 20 ||
                map.CountCells(DrillSnakeCellType.RareOre) < 20 ||
                map.CountCells(DrillSnakeCellType.VeryRareOre) < 12)
            {
                throw new System.InvalidOperationException(
                    "The deterministic prototype map failed its content validation.");
            }

            var buildScene = EditorBuildSettings.scenes.FirstOrDefault(
                item => item.path == PrototypeScenePath && item.enabled);
            if (buildScene == null)
            {
                throw new System.InvalidOperationException(
                    "The Drill Snake prototype scene is not enabled in Build Settings.");
            }

            Debug.Log(
                "Drill Snake validation passed: scene, build settings, four docks, " +
                "and all ore tiers are present.");
        }

        private static void AddPrototypeToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.path != PrototypeScenePath)
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(PrototypeScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
