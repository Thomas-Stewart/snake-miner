using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;

namespace DrillSnake
{
    [DisallowMultipleComponent]
    public sealed class DrillSnakeController : MonoBehaviour
    {
        [Header("Prototype")]
        [SerializeField] private int levelSeed = 240628;
        [SerializeField]
        private DrillSnakeLayoutPreset layoutPreset =
            DrillSnakeLayoutPreset.MediumCrystalCaverns;
        [SerializeField] private DrillSnakeTuning tuning = new();

        private readonly DrillSnakeSession _session = new();
        private readonly int[] _upgradeLevels = new int[4];

        private DrillSnakeSimulation _simulation;
        private DrillSnakeWorldView _worldView;
        private DrillSnakeHud _hud;
        private float _nextMoveTime;
        private bool _expeditionMoving;
        private bool _busy;
        private bool _slowTesting;
        private bool _heatFree;

        public DrillSnakeSimulation Simulation => _simulation;

        public int BankedCredits => _session.BankedCredits;

        private void Awake()
        {
            if (FindObjectsByType<DrillSnakeController>(FindObjectsSortMode.None).Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            Application.targetFrameRate = 120;
            BuildPresentation();
            GenerateLevel(levelSeed, false);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                HandleDebugInput(keyboard);
                if (!_busy)
                {
                    HandleDirectionInput(keyboard);
                    if (keyboard.spaceKey.wasPressedThisFrame)
                    {
                        _expeditionMoving = true;
                    }
                }
            }

            if (!_busy && _expeditionMoving && Time.time >= _nextMoveTime)
            {
                MoveOneCell(keyboard != null && keyboard.spaceKey.isPressed);
            }

            UpdateHud();
        }

        private void BuildPresentation()
        {
            EnsureCamera();
            EnsureLighting();
            EnsureEventSystem();

            var viewObject = new GameObject("Drill Snake World View");
            viewObject.transform.SetParent(transform, false);
            _worldView = viewObject.AddComponent<DrillSnakeWorldView>();

            var hudObject = new GameObject("Drill Snake HUD");
            hudObject.transform.SetParent(transform, false);
            _hud = hudObject.AddComponent<DrillSnakeHud>();
            _hud.Build(TryPurchaseUpgrade);
        }

        private void GenerateLevel(int seed, bool announce)
        {
            StopAllCoroutines();
            _busy = false;
            _expeditionMoving = false;
            var map = DrillSnakeMap.Generate(seed, layoutPreset);
            levelSeed = map.Seed;
            _simulation = new DrillSnakeSimulation(map);
            _worldView.BuildWorld(map);
            _worldView.SyncSnake(_simulation, 0f);
            _nextMoveTime = Time.time;

            if (announce)
            {
                _hud.ShowMessage(
                    $"{map.Settings.DisplayName}\n" +
                    $"REQUESTED {map.RequestedSeed}  •  ACCEPTED {map.Seed}  •  " +
                    $"TRY {map.GenerationAttempt}",
                    new Color(0.35f, 0.95f, 0.88f),
                    2f);
            }
        }

        private void MoveOneCell(bool boosting)
        {
            var result = _simulation.Step(
                tuning,
                GetUpgradeLevel(DrillSnakeUpgradeType.OreScanner),
                GetUpgradeLevel(DrillSnakeUpgradeType.Cooling),
                boosting,
                _heatFree);

            var interval = tuning.GetMoveInterval(
                GetUpgradeLevel(DrillSnakeUpgradeType.DriveSpeed),
                boosting,
                _slowTesting);
            if (result.ChangedTerrain)
            {
                interval += tuning.GetDrillDelay(
                    GetUpgradeLevel(DrillSnakeUpgradeType.DrillMotor));
                _worldView.RemoveDrilledCell(result.Cell);
            }

            _worldView.SyncSnake(_simulation, interval);
            _nextMoveTime = Time.time + interval;

            if (result.Outcome == DrillSnakeStepOutcome.CollectedOre)
            {
                _hud.ShowMessage(
                    $"+1 {OreName(result.OreType)} CARGO  •  {result.OreValue} CR",
                    OreColor(result.OreType),
                    0.85f);
            }

            if (result.Failed)
            {
                StartCoroutine(FailureSequence(result.Outcome));
                return;
            }

            if (result.Outcome == DrillSnakeStepOutcome.Docked &&
                _simulation.CargoCount > 0)
            {
                StartCoroutine(BankingSequence());
            }
        }

        private IEnumerator BankingSequence()
        {
            _busy = true;
            _expeditionMoving = false;
            _simulation.ClearDirectionBuffer();

            var payoff = _session.BankCargo(_simulation);
            var segmentCount = _simulation.CargoCount;
            _hud.ShowMessage(
                $"REFINERY PAYOUT  +{payoff:N0} CREDITS\n" +
                $"CONSUMING {segmentCount} CARGO SEGMENTS",
                new Color(0.25f, 1f, 0.72f),
                Mathf.Max(1.6f, segmentCount * tuning.BankSegmentSeconds + 0.5f));

            yield return new WaitForSeconds(0.12f);
            while (_simulation.CargoCount > 0)
            {
                yield return _worldView.AnimateTailConsumption(tuning.BankSegmentSeconds);
                _simulation.ConsumeTailCargo();
                _worldView.SyncSnake(_simulation, 0f);
            }

            _simulation.ResetHeat();
            _busy = false;
            _nextMoveTime = Time.time;
        }

        private IEnumerator FailureSequence(DrillSnakeStepOutcome outcome)
        {
            _busy = true;
            _expeditionMoving = false;
            _simulation.ClearDirectionBuffer();

            var lostCount = _simulation.CargoCount;
            var lostValue = _simulation.CargoValue;
            var reason = outcome switch
            {
                DrillSnakeStepOutcome.BodyCollision => "COLLIDED WITH YOUR OWN TRAIN",
                DrillSnakeStepOutcome.BedrockCollision => "DRILL SHATTERED ON BEDROCK",
                DrillSnakeStepOutcome.Overheated => "DRILL CORE OVERHEATED",
                _ => "EXPEDITION FAILED"
            };
            _hud.ShowMessage(
                $"{reason}\nLOST {lostCount} CARGO  •  {lostValue:N0} UNBANKED CREDITS",
                new Color(1f, 0.24f, 0.16f),
                2.2f);

            yield return new WaitForSeconds(1.15f);
            _session.ResolveFailedExpedition(_simulation);
            _worldView.SyncSnake(_simulation, 0f);
            _busy = false;
            _nextMoveTime = Time.time;
        }

        private void HandleDirectionInput(Keyboard keyboard)
        {
            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            {
                QueueDirection(Vector2Int.up);
            }

            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            {
                QueueDirection(Vector2Int.right);
            }

            if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            {
                QueueDirection(Vector2Int.down);
            }

            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
            {
                QueueDirection(Vector2Int.left);
            }
        }

        private void QueueDirection(Vector2Int direction)
        {
            if (!_simulation.QueueDirection(direction))
            {
                return;
            }

            _expeditionMoving = true;
        }

        private void HandleDebugInput(Keyboard keyboard)
        {
            if (keyboard.f1Key.wasPressedThisFrame)
            {
                SelectPreset(DrillSnakeLayoutPreset.EasyOpenQuarry);
            }

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                SelectPreset(DrillSnakeLayoutPreset.MediumCrystalCaverns);
            }

            if (keyboard.f3Key.wasPressedThisFrame)
            {
                SelectPreset(DrillSnakeLayoutPreset.HardMagmaFissures);
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                GenerateLevel(_simulation.Map.RequestedSeed, true);
            }

            if (keyboard.nKey.wasPressedThisFrame)
            {
                GenerateLevel(unchecked(_simulation.Map.RequestedSeed + 1), true);
            }

            if (keyboard.vKey.wasPressedThisFrame)
            {
                _worldView.ToggleLevelDesignOverlay();
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                _slowTesting = true;
                _hud.ShowMessage("SLOW TEST MODE", new Color(1f, 0.85f, 0.25f), 1f);
            }

            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                _slowTesting = false;
                _hud.ShowMessage("NORMAL MOVEMENT", new Color(0.5f, 0.9f, 1f), 1f);
            }

            if (keyboard.gKey.wasPressedThisFrame)
            {
                _worldView.ToggleGrid();
            }

            if (keyboard.hKey.wasPressedThisFrame)
            {
                _heatFree = !_heatFree;
                _hud.ShowMessage(
                    _heatFree ? "HEAT-FREE TESTING ENABLED" : "HEAT-FREE TESTING DISABLED",
                    _heatFree
                        ? new Color(0.35f, 1f, 0.85f)
                        : new Color(1f, 0.7f, 0.25f),
                    1.2f);
            }
        }

        private void SelectPreset(DrillSnakeLayoutPreset preset)
        {
            layoutPreset = preset;
            GenerateLevel(_simulation.Map.RequestedSeed, true);
        }

        private void TryPurchaseUpgrade(DrillSnakeUpgradeType type)
        {
            if (_busy || _simulation == null || !_simulation.IsAtRefinery)
            {
                return;
            }

            var level = GetUpgradeLevel(type);
            var cost = tuning.GetUpgradeCost(type, level);
            if (_session.BankedCredits < cost)
            {
                _hud.ShowMessage(
                    $"NEED {cost - _session.BankedCredits:N0} MORE CREDITS",
                    new Color(1f, 0.55f, 0.25f),
                    1.1f);
                return;
            }

            if (!_session.TrySpendCredits(cost))
            {
                return;
            }

            _upgradeLevels[(int)type]++;
            _hud.ShowMessage(
                $"{UpgradeDisplayName(type)} UPGRADED TO LV.{level + 1}",
                new Color(0.35f, 1f, 0.72f),
                1.4f);
        }

        private int GetUpgradeLevel(DrillSnakeUpgradeType type)
        {
            return _upgradeLevels[(int)type];
        }

        private int GetUpgradeCost(DrillSnakeUpgradeType type)
        {
            return tuning.GetUpgradeCost(type, GetUpgradeLevel(type));
        }

        private void UpdateHud()
        {
            if (_simulation == null || _hud == null)
            {
                return;
            }

            _hud.UpdateState(
                _session.BankedCredits,
                _simulation.CargoCount,
                _simulation.CargoValue,
                _simulation.Heat,
                tuning.GetMaximumHeat(GetUpgradeLevel(DrillSnakeUpgradeType.Cooling)),
                _simulation.Map.RequestedSeed,
                levelSeed,
                _simulation.Map.Settings.DisplayName,
                _simulation.Map.GenerationAttempt,
                _simulation.Map.ValidationReport,
                _simulation.Map.RejectedFailures.Count,
                _slowTesting,
                _heatFree,
                _worldView.GridVisible,
                _worldView.LevelDesignOverlayVisible,
                _simulation.IsAtRefinery,
                !_expeditionMoving && !_busy,
                GetUpgradeLevel,
                GetUpgradeCost);
        }

        private static void EnsureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.orthographic = true;
            camera.orthographicSize = 27.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.02f, 0.028f);
            camera.transform.position = new Vector3(0f, 38f, -17f);
            camera.transform.rotation = Quaternion.Euler(66f, 0f, 0f);
        }

        private static void EnsureLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.24f, 0.3f, 0.36f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.15f, 0.18f);
            RenderSettings.ambientGroundColor = new Color(0.025f, 0.03f, 0.04f);

            if (FindFirstObjectByType<Light>() != null)
            {
                return;
            }

            var lightObject = new GameObject("Excavation Key Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.92f, 0.96f, 1f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventObject = new GameObject("Event System");
            eventObject.AddComponent<EventSystem>();
            var inputModule = eventObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        private static string OreName(DrillSnakeOreType oreType)
        {
            return oreType switch
            {
                DrillSnakeOreType.Common => "COMMON",
                DrillSnakeOreType.Rare => "RARE",
                DrillSnakeOreType.VeryRare => "VERY RARE",
                _ => string.Empty
            };
        }

        private static Color OreColor(DrillSnakeOreType oreType)
        {
            return oreType switch
            {
                DrillSnakeOreType.Common => new Color(0.2f, 0.95f, 0.42f),
                DrillSnakeOreType.Rare => new Color(0.24f, 0.62f, 1f),
                DrillSnakeOreType.VeryRare => new Color(0.95f, 0.3f, 1f),
                _ => Color.white
            };
        }

        private static string UpgradeDisplayName(DrillSnakeUpgradeType type)
        {
            return type switch
            {
                DrillSnakeUpgradeType.Cooling => "COOLING",
                DrillSnakeUpgradeType.DrillMotor => "DRILL MOTOR",
                DrillSnakeUpgradeType.DriveSpeed => "DRIVE SPEED",
                DrillSnakeUpgradeType.OreScanner => "ORE SCANNER",
                _ => type.ToString().ToUpperInvariant()
            };
        }

        private void OnDrawGizmos()
        {
            if (_simulation == null)
            {
                return;
            }

            Gizmos.color = new Color(0.1f, 0.75f, 0.78f, 0.22f);
            for (var i = 0; i <= _simulation.Map.Width; i++)
            {
                var offset = i - _simulation.Map.Width * 0.5f;
                Gizmos.DrawLine(
                    new Vector3(offset, 0.02f, -22.5f),
                    new Vector3(offset, 0.02f, 22.5f));
                Gizmos.DrawLine(
                    new Vector3(-22.5f, 0.02f, offset),
                    new Vector3(22.5f, 0.02f, offset));
            }

            Gizmos.color = Color.cyan;
            foreach (var dock in _simulation.Map.Docks)
            {
                Gizmos.DrawWireCube(
                    DrillSnakeWorldView.GridToWorld(dock, 0.4f),
                    new Vector3(0.9f, 0.8f, 0.9f));
            }

            for (var i = 0; i < _simulation.Segments.Count; i++)
            {
                Gizmos.color = i == 0
                    ? new Color(1f, 0.5f, 0.05f)
                    : i < DrillSnakeSimulation.MinimumSegmentCount
                        ? new Color(0.2f, 0.65f, 0.75f)
                        : new Color(0.9f, 0.25f, 1f);
                Gizmos.DrawSphere(
                    DrillSnakeWorldView.GridToWorld(_simulation.Segments[i], 1.2f),
                    0.18f);
            }
        }
    }
}
