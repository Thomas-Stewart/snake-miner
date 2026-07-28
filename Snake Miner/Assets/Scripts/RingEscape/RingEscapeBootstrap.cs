using UnityEngine;
using UnityEngine.SceneManagement;
using SSG_Core.Scripts.Util;

namespace BallBounce.RingEscape
{
    /// <summary>
    /// Keeps the procedural simulation self-contained: opening the Game scene is
    /// enough to run it, without requiring a prefab or hand-authored scene setup.
    /// </summary>
    internal static class RingEscapeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= CreateSimulationForGameScene;
            SceneManager.sceneLoaded += CreateSimulationForGameScene;
        }

        private static void CreateSimulationForGameScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Game")
            {
                return;
            }

            if (Object.FindFirstObjectByType<CheatManager>() == null)
            {
                var cheatObject = new GameObject("Cheat Manager");
                cheatObject.AddComponent<CheatManager>();
            }

            if (Object.FindFirstObjectByType<RingEscapeSimulation>() == null)
            {
                var simulationObject = new GameObject("Ring Escape Simulation");
                SceneManager.MoveGameObjectToScene(simulationObject, scene);
                simulationObject.AddComponent<RingEscapeSimulation>();
            }
        }
    }
}
