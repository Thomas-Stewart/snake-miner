using UnityEditor;
using UnityEditor.Rendering;
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

        [MenuItem("Tools/Drill Snake/Validate Procedural Cel Shader")]
        public static void ValidateProceduralCelShader()
        {
            var shader = Resources.Load<Shader>(
                "Shaders/DrillSnakeProceduralCel");
            if (shader == null)
            {
                throw new System.InvalidOperationException(
                    "Drill Snake procedural cel shader was not found.");
            }

            var messages = ShaderUtil.GetShaderMessages(shader);
            var errorCount = 0;
            foreach (var message in messages)
            {
                var formatted =
                    $"{message.severity}: {message.message} " +
                    $"({message.platform}, line {message.line})";
                if (message.severity == ShaderCompilerMessageSeverity.Error)
                {
                    errorCount++;
                    Debug.LogError(formatted, shader);
                }
                else
                {
                    Debug.LogWarning(formatted, shader);
                }
            }

            if (errorCount > 0 || ShaderUtil.ShaderHasError(shader))
            {
                throw new System.InvalidOperationException(
                    $"Procedural cel shader has {errorCount} compiler error(s).");
            }

            Debug.Log(
                $"Drill Snake procedural cel shader compiled without errors " +
                $"({messages.Length} compiler message(s)).",
                shader);
        }
    }
}
