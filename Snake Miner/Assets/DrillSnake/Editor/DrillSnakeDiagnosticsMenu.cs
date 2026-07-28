using UnityEditor;
using UnityEngine;

namespace DrillSnake.Editor
{
    public static class DrillSnakeDiagnosticsMenu
    {
        [MenuItem("Tools/Drill Snake/Run Long-Snake Diagnostics")]
        public static void RunLongSnakeDiagnostics()
        {
            var presets = new[]
            {
                DrillSnakeLayoutPreset.EasyOpenQuarry,
                DrillSnakeLayoutPreset.MediumCrystalCaverns,
                DrillSnakeLayoutPreset.HardMagmaFissures
            };

            foreach (var preset in presets)
            {
                var map = DrillSnakeMap.Generate(240628, preset);
                var report = DrillSnakeDesignDiagnostics.Analyze(map);
                Debug.Log(report.ToConsoleString());
            }
        }
    }
}
