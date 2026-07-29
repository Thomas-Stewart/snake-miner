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

        [MenuItem("Tools/Drill Snake/Validate Runtime Presentation")]
        public static void ValidateRuntimePresentation()
        {
            var modes = new[]
            {
                DrillSnakeArtMode.IllustratedPng,
                DrillSnakeArtMode.ProceduralCel
            };
            foreach (var mode in modes)
            {
                var root = new GameObject($"Drill Snake {mode} Smoke Test");
                try
                {
                    var map = DrillSnakeMap.Generate(240628);
                    var simulation = new DrillSnakeSimulation(map);
                    var view = root.AddComponent<DrillSnakeWorldView>();
                    view.BuildWorld(map, mode);
                    view.SyncSnake(simulation, 0f);
                    view.SyncCollectibles(simulation);
                    view.SetDrillPowerActive(true);
                    if (!view.TryGetHeadVisualPosition(out _))
                    {
                        throw new System.InvalidOperationException(
                            $"{mode} did not create a head visual.");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }

            ValidateProceduralCelShader();
            Debug.Log(
                "Drill Snake PNG and Procedural Cel runtime presentations " +
                "built successfully.");
        }
    }
}
