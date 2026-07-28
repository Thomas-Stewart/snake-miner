using UnityEngine;
using UnityEngine.SceneManagement;

namespace DrillSnake
{
    internal static class DrillSnakeBootstrap
    {
        private const string PrototypeSceneName = "DrillSnakePrototype";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= EnsurePrototypeRuntime;
            SceneManager.sceneLoaded += EnsurePrototypeRuntime;
        }

        private static void EnsurePrototypeRuntime(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != PrototypeSceneName ||
                Object.FindFirstObjectByType<DrillSnakeController>() != null)
            {
                return;
            }

            var runtime = new GameObject("Drill Snake Runtime");
            SceneManager.MoveGameObjectToScene(runtime, scene);
            runtime.AddComponent<DrillSnakeController>();
        }
    }
}
