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
        private const float CameraPitchDegrees = 64f;
        private const float CameraBaseOrthographicSize = 7.5f;
        private const float CameraSizePerCargoSegment = 0.055f;
        private const float CameraMaximumOrthographicSize = 11.25f;
        private static readonly Vector3 CameraFollowOffset =
            new(0f, 28f, -13.7f);

        [Header("Prototype")]
        [SerializeField] private int levelSeed = 240628;
        [SerializeField]
        private DrillSnakeLayoutPreset layoutPreset =
            DrillSnakeLayoutPreset.MediumCrystalCaverns;
        [SerializeField]
        private DrillSnakeArtMode artMode =
            DrillSnakeArtMode.ProceduralCel;
        [SerializeField] private DrillSnakeTuning tuning = new();

        private readonly DrillSnakeSession _session = new();
        private readonly int[] _upgradeLevels = new int[4];

        private DrillSnakeSimulation _simulation;
        private DrillSnakeWorldView _worldView;
        private DrillSnakeHud _hud;
        private Camera _camera;
        private float _cameraZoomVelocity;
        private Vector3 _cameraLead;
        private Vector3 _cameraLeadVelocity;
        private float _nextMoveTime;
        private float _nextTurretTime;
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
            if (_simulation != null)
            {
                _simulation.AdvanceTime(Time.deltaTime);
                _worldView?.SetDrillPowerActive(_simulation.DrillActive);
                _worldView?.SetHeatTint(
                    tuning.GetHeatRatio(_simulation.Heat));
            }

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

            if (!_busy && _simulation != null && Time.time >= _nextTurretTime)
            {
                FireTurret();
            }

            UpdateHud();
        }

        private void LateUpdate()
        {
            UpdateCameraFollow();
        }

        private void BuildPresentation()
        {
            _camera = EnsureCamera();
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
            _worldView.BuildWorld(map, artMode);
            _worldView.SyncSnake(_simulation, 0f);
            _worldView.SyncCollectibles(_simulation);
            SnapCameraToSnake();
            _nextMoveTime = Time.time;
            _nextTurretTime = Time.time + 0.25f;

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
                _slowTesting,
                _simulation.Heat);
            if (result.ChangedTerrain)
            {
                _worldView.RemoveDrilledCell(result.Cell);
            }

            if (result.Outcome == DrillSnakeStepOutcome.CollectedOre)
            {
                _worldView.PlayOreCollection(
                    result.CollectedPickupCell,
                    result.OreType);
            }

            _worldView.SyncCollectibles(_simulation);
            if (result.SpawnedPickups.Count > 0)
            {
                _worldView.PlayOreScatter(
                    result.Cell,
                    result.SpawnedPickups);
            }

            if (result.Outcome == DrillSnakeStepOutcome.Blocked)
            {
                _expeditionMoving = false;
                _simulation.ClearDirectionBuffer();
                _hud.ShowMessage(
                    "PATH BLOCKED  •  FIND A ROUTE OR DRILL CHARGE",
                    new Color(1f, 0.58f, 0.16f),
                    1.1f);
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

            if (result.Outcome == DrillSnakeStepOutcome.CollectedDrillPowerup)
            {
                _hud.ShowMessage(
                    $"DRILL CHARGE ACTIVE  •  " +
                    $"{tuning.DrillPowerupDuration:0} SECONDS",
                    new Color(1f, 0.58f, 0.08f),
                    1.4f);
            }

            if (result.Outcome == DrillSnakeStepOutcome.Drilled)
            {
                _hud.ShowMessage(
                    result.OreType == DrillSnakeOreType.None
                        ? "DRILL CHARGE  •  BLOCK DESTROYED"
                        : "DRILL CHARGE  •  ORE SHATTERED",
                    new Color(1f, 0.68f, 0.12f),
                    0.65f);
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

        private void FireTurret()
        {
            var result = _simulation.TryFireTurret(
                tuning,
                GetUpgradeLevel(DrillSnakeUpgradeType.OreScanner));
            if (!result.Fired)
            {
                _nextTurretTime = Time.time + 0.12f;
                return;
            }

            _nextTurretTime = Time.time + tuning.TurretFireInterval;
            _worldView.PlayTurretShot(
                result,
                tuning.ProjectileTravelSeconds,
                tuning.ProjectileSize);
            if (result.Destroyed)
            {
                _worldView.RemoveDrilledCell(result.Target);
                _worldView.SyncCollectibles(_simulation);
                _worldView.PlayOreScatter(
                    result.Target,
                    result.SpawnedPickups);
                _hud.ShowMessage(
                    $"{OreName(result.OreType)} ORE SHATTERED  •  " +
                    $"{result.SpawnedPickups.Count} FRAGMENTS",
                    OreColor(result.OreType),
                    0.8f);
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
            if (!_expeditionMoving)
            {
                // While stopped, a direction is a departure command rather
                // than a buffered turn. TrySetDirection intentionally accepts
                // the current forward direction, accepts either 90-degree
                // direction, and rejects only the immediate reverse.
                if (!_simulation.TrySetDirection(direction))
                {
                    return;
                }

                _expeditionMoving = true;
                _nextMoveTime = Time.time;
                return;
            }

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

            if (keyboard.tKey.wasPressedThisFrame)
            {
                ToggleArtMode();
            }
        }

        private void ToggleArtMode()
        {
            artMode = artMode == DrillSnakeArtMode.IllustratedPng
                ? DrillSnakeArtMode.ProceduralCel
                : DrillSnakeArtMode.IllustratedPng;
            _worldView.BuildWorld(_simulation.Map, artMode);
            _worldView.SyncSnake(_simulation, 0f);
            _worldView.SyncCollectibles(_simulation);
            _hud.SetArtMode(artMode);
            _hud.ShowMessage(
                artMode == DrillSnakeArtMode.ProceduralCel
                    ? "ART MODE  •  PROCEDURAL CEL"
                    : "ART MODE  •  ILLUSTRATED PNG",
                new Color(0.42f, 0.92f, 1f),
                1.4f);
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
                tuning.GetHeatSpeedBonus(_simulation.Heat),
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
                artMode,
                _simulation.DrillPowerRemaining,
                _simulation.IsAtRefinery,
                !_expeditionMoving && !_busy,
                GetUpgradeLevel,
                GetUpgradeCost);
        }

        private static Camera EnsureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.orthographic = true;
            camera.orthographicSize = CameraBaseOrthographicSize;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.01f, 0.012f);
            camera.transform.rotation = Quaternion.Euler(
                CameraPitchDegrees,
                0f,
                0f);
            return camera;
        }

        private void SnapCameraToSnake()
        {
            if (_camera == null || _simulation == null)
            {
                return;
            }

            _cameraZoomVelocity = 0f;
            _cameraLead = Vector3.zero;
            _cameraLeadVelocity = Vector3.zero;
            _camera.transform.position = GetCameraTarget();
            _camera.orthographicSize = GetDesiredCameraSize();
        }

        private void UpdateCameraFollow()
        {
            if (_camera == null || _simulation == null)
            {
                return;
            }

            var deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            var desiredLead = _expeditionMoving
                ? new Vector3(
                    _simulation.Direction.x,
                    0f,
                    _simulation.Direction.y) * 1.35f
                : Vector3.zero;
            _cameraLead = Vector3.SmoothDamp(
                _cameraLead,
                desiredLead,
                ref _cameraLeadVelocity,
                0.34f,
                4f,
                deltaTime);

            // The visual head already follows a continuous, constant-speed
            // path. Lock the camera to that path so a second damping layer
            // cannot turn cell boundaries into visible catch-up pulses.
            _camera.transform.position = GetCameraTarget();
            _camera.orthographicSize = Mathf.SmoothDamp(
                _camera.orthographicSize,
                GetDesiredCameraSize(),
                ref _cameraZoomVelocity,
                0.32f,
                4f,
                deltaTime);
        }

        private float GetDesiredCameraSize()
        {
            var cargoSegments = _simulation == null
                ? 0
                : Mathf.Max(
                    0,
                    _simulation.Segments.Count -
                    DrillSnakeSimulation.MinimumSegmentCount);
            return Mathf.Min(
                CameraMaximumOrthographicSize,
                CameraBaseOrthographicSize +
                cargoSegments * CameraSizePerCargoSegment);
        }

        private Vector3 GetCameraTarget()
        {
            var world = _worldView != null &&
                        _worldView.TryGetHeadVisualPosition(out var visualPosition)
                ? visualPosition
                : DrillSnakeWorldView.GridToWorld(_simulation.Head);
            var focus = new Vector3(
                world.x + _cameraLead.x,
                0f,
                world.z + _cameraLead.z);
            return focus + CameraFollowOffset;
        }

        private static void EnsureLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.26f, 0.29f, 0.33f);
            RenderSettings.ambientEquatorColor = new Color(0.14f, 0.14f, 0.145f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.032f, 0.03f);

            if (FindFirstObjectByType<Light>() != null)
            {
                return;
            }

            var lightObject = new GameObject("Excavation Key Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.78f, 0.84f, 0.92f);
            light.intensity = 1.18f;
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
                DrillSnakeOreType.Common => new Color(1f, 0.47f, 0.08f),
                DrillSnakeOreType.Rare => new Color(0.22f, 0.7f, 1f),
                DrillSnakeOreType.VeryRare => new Color(0.98f, 0.26f, 0.8f),
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
