using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DrillSnake
{
    public sealed class DrillSnakeHud : MonoBehaviour
    {
        private readonly Dictionary<DrillSnakeUpgradeType, Button> _upgradeButtons = new();
        private readonly Dictionary<DrillSnakeUpgradeType, Text> _upgradeLabels = new();
        private readonly Dictionary<DrillSnakeUpgradeType, Sprite> _upgradeIcons = new();
        private readonly Dictionary<DrillSnakeUpgradeType, Image> _upgradeIconImages = new();

        private Font _font;
        private Text _bankedText;
        private Text _cargoText;
        private Text _heatText;
        private Text _objectiveText;
        private Text _debugText;
        private Text _messageText;
        private Text _drillPowerText;
        private GameObject _debugPanel;
        private GameObject _upgradePanel;
        private float _messageHideTime;
        private Action<DrillSnakeUpgradeType> _purchaseUpgrade;

        public void Build(Action<DrillSnakeUpgradeType> purchaseUpgrade)
        {
            _purchaseUpgrade = purchaseUpgrade;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateUpgradeSprites();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            BuildObjective();
            BuildStats();
            BuildDebugLegend();
            BuildMessage();
            BuildDrillPowerStatus();
            BuildUpgradePanel();
        }

        public void UpdateState(
            int bankedCredits,
            int cargoCount,
            int cargoValue,
            float heat,
            float heatSpeedBonus,
            int requestedSeed,
            int acceptedSeed,
            string presetName,
            int generationAttempt,
            DrillSnakeValidationReport validationReport,
            int rejectedFailureCount,
            bool slowTesting,
            bool heatFree,
            bool gridVisible,
            bool levelDesignOverlayVisible,
            DrillSnakeArtMode artMode,
            float drillPowerRemaining,
            bool atRefinery,
            bool waitingToDepart,
            Func<DrillSnakeUpgradeType, int> getUpgradeLevel,
            Func<DrillSnakeUpgradeType, int> getUpgradeCost)
        {
            _bankedText.text =
                "<size=16><color=#AEB4B8>SCRAP</color></size>\n" +
                $"<size=28>{bankedCredits:N0}</size>\n" +
                "<size=13><color=#8C9499>BANKED</color></size>";
            _cargoText.text =
                "<size=16><color=#AEB4B8>CARGO</color></size>\n" +
                $"<size=28>{cargoCount}</size>\n" +
                $"<size=13><color=#F4B844>{cargoValue:N0} VALUE</color></size>";
            _heatText.text =
                "<size=16><color=#AEB4B8>HEAT</color></size>\n" +
                $"<size=28>{Mathf.CeilToInt(heat)}</size>\n" +
                $"<size=13><color=#F4B844>+{Mathf.RoundToInt(heatSpeedBonus * 100f)}% SPEED</color></size>";

            var debugFlags = string.Empty;
            if (slowTesting)
            {
                debugFlags += "\n<color=#FFE474>SLOW TEST MODE</color>";
            }

            if (heatFree)
            {
                debugFlags += "\n<color=#55F1E4>HEAT-FREE MODE</color>";
            }

            if (gridVisible)
            {
                debugFlags += "\n<color=#55F1E4>GRID VISIBLE</color>";
            }

            if (levelDesignOverlayVisible)
            {
                debugFlags +=
                    "\n<color=#55F1E4>VALIDATION OVERLAY</color>" +
                    "\n<color=#67FF73>GREEN</color> common  " +
                    "<color=#4A9FFF>BLUE</color> rare  " +
                    "<color=#FF3BEA>MAGENTA</color> very rare" +
                    "\n<color=#24DFFF>CYAN</color> safe long  " +
                    "<color=#FF761B>ORANGE</color> risky short";
            }

            var validationText = validationReport == null
                ? "VALIDATION PENDING"
                : validationReport.Summary;
            if (rejectedFailureCount > 0)
            {
                validationText += $"  •  {rejectedFailureCount} REJECTED FINDING(S)";
            }

            _debugText.text =
                "<b>CONTROLS</b>\n" +
                "WASD / ARROWS  Turn\n" +
                "SPACE          Boost\n" +
                "TURRET         Auto-targets nearby ore\n" +
                "DRILL CHARGE   10s contact destruction\n" +
                "F1 / F2 / F3  Easy / Medium / Hard\n" +
                "N              New seed\n" +
                "R              Reset active seed\n" +
                "V              Validation overlay\n" +
                "1 / 2          Slow / Normal\n" +
                "G              Grid overlay\n" +
                "H              Heat-free mode\n" +
                "T              PNG / Cel art\n" +
                $"<color=#86DCEB>ART  {ArtModeName(artMode)}</color>\n" +
                $"<color=#86A4B4>{validationText}</color>" +
                debugFlags;
            _debugPanel.SetActive(
                levelDesignOverlayVisible ||
                gridVisible ||
                slowTesting ||
                heatFree);

            _drillPowerText.gameObject.SetActive(drillPowerRemaining > 0f);
            _drillPowerText.text =
                $"DRILL CHARGE  {drillPowerRemaining:0.0}s\n" +
                "<size=14>CONTACT DESTROYS ALL BLOCKS</size>";

            // Hidden during this loop pass. The retained backing data makes a
            // redesigned progression layer easy to restore later.
            _upgradePanel.SetActive(false);
            SetArtMode(artMode);
            foreach (var pair in _upgradeButtons)
            {
                var type = pair.Key;
                var level = getUpgradeLevel(type);
                var cost = getUpgradeCost(type);
                pair.Value.interactable = bankedCredits >= cost;
                _upgradeLabels[type].text =
                    $"{UpgradeName(type)}  <size=15><color=#949CA1>LV.{level}</color></size>\n" +
                    $"<size=17><color=#C2C6C8>{UpgradeEffect(type)}</color></size>\n" +
                    $"<size=20><color=#F4B844>SCRAP  {cost:N0}</color></size>";
            }

            if (waitingToDepart && atRefinery && Time.time >= _messageHideTime)
            {
                _messageText.color = new Color(0.8f, 0.94f, 0.98f);
                _messageText.text = "CHOOSE A DIRECTION";
            }
        }

        public void ShowMessage(string message, Color color, float duration)
        {
            _messageText.color = color;
            _messageText.text = message;
            _messageHideTime = Time.time + duration;
        }

        public void SetArtMode(DrillSnakeArtMode artMode)
        {
            var showPngIcons = artMode == DrillSnakeArtMode.IllustratedPng;
            foreach (var pair in _upgradeIconImages)
            {
                pair.Value.gameObject.SetActive(showPngIcons);
            }

            foreach (var pair in _upgradeLabels)
            {
                SetRect(
                    pair.Value.rectTransform,
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(showPngIcons ? 120f : 24f, 0f),
                    new Vector2(showPngIcons ? 164f : 260f, -20f),
                    new Vector2(0f, 0.5f));
            }
        }

        private void Update()
        {
            if (_messageText != null &&
                _messageHideTime > 0f &&
                Time.time >= _messageHideTime)
            {
                _messageText.text = string.Empty;
                _messageHideTime = 0f;
            }
        }

        private void BuildObjective()
        {
            var panel = CreatePanel(
                "Objective Panel",
                transform,
                new Color(0.025f, 0.04f, 0.055f, 0.91f));
            SetRect(
                panel.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-16f, -16f),
                new Vector2(550f, 86f),
                new Vector2(1f, 1f));

            _objectiveText = CreateText(
                "Objective",
                panel.transform,
                23,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            Stretch(_objectiveText.rectTransform, 16f);
            _objectiveText.text =
                "<color=#F2F0E9>MINE ORE AND RETURN TO THE REFINERY</color>\n" +
                "<size=15><color=#A9ADB0>Turret shatters ore. Collect fragments and bring the train home.</color></size>";
        }

        private void BuildStats()
        {
            var panel = CreatePanel(
                "Stats Panel",
                transform,
                new Color(0.025f, 0.04f, 0.055f, 0.9f));
            SetRect(
                panel.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(16f, -16f),
                new Vector2(660f, 92f),
                new Vector2(0f, 1f));

            _bankedText = CreateStatBlock("Banked Scrap", panel.transform, 0);
            _cargoText = CreateStatBlock("Cargo", panel.transform, 1);
            _heatText = CreateStatBlock("Heat", panel.transform, 2);

            for (var separatorIndex = 1; separatorIndex <= 2; separatorIndex++)
            {
                var separator = CreateFlatPanel(
                    $"Resource Separator {separatorIndex}",
                    panel.transform,
                    new Color(0.33f, 0.36f, 0.38f, 0.7f));
                SetRect(
                    separator.GetComponent<RectTransform>(),
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(separatorIndex * 220f, 0f),
                    new Vector2(2f, 68f),
                    new Vector2(0.5f, 0.5f));
            }
        }

        private void BuildDebugLegend()
        {
            _debugPanel = CreatePanel(
                "Controls Panel",
                transform,
                new Color(0.025f, 0.04f, 0.055f, 0.86f));
            SetRect(
                _debugPanel.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-16f, -116f),
                new Vector2(390f, 300f),
                new Vector2(1f, 1f));

            _debugText = CreateText(
                "Controls",
                _debugPanel.transform,
                16,
                TextAnchor.UpperLeft,
                FontStyle.Normal);
            Stretch(_debugText.rectTransform, 20f);
            _debugText.supportRichText = true;
            _debugText.lineSpacing = 1.05f;
            _debugPanel.SetActive(false);
        }

        private void BuildMessage()
        {
            _messageText = CreateText(
                "Payoff and Failure Message",
                transform,
                22,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            SetRect(
                _messageText.rectTransform,
                new Vector2(0.5f, 0.64f),
                new Vector2(0.5f, 0.64f),
                Vector2.zero,
                new Vector2(680f, 72f),
                new Vector2(0.5f, 0.5f));

            var outline = _messageText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private void BuildDrillPowerStatus()
        {
            _drillPowerText = CreateText(
                "Drill Powerup Status",
                transform,
                21,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            SetRect(
                _drillPowerText.rectTransform,
                new Vector2(0.5f, 0.08f),
                new Vector2(0.5f, 0.08f),
                Vector2.zero,
                new Vector2(420f, 62f),
                new Vector2(0.5f, 0.5f));
            _drillPowerText.color = new Color(1f, 0.6f, 0.08f);
            var outline = _drillPowerText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.025f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            _drillPowerText.gameObject.SetActive(false);
        }

        private void BuildUpgradePanel()
        {
            _upgradePanel = CreatePanel(
                "Refinery Upgrade Panel",
                transform,
                new Color(0.025f, 0.04f, 0.055f, 0.95f));
            SetRect(
                _upgradePanel.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 14f),
                new Vector2(1280f, 188f),
                new Vector2(0.5f, 0f));

            var title = CreateText(
                "Upgrade Title",
                _upgradePanel.transform,
                16,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            SetRect(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -6f),
                new Vector2(-24f, 26f),
                new Vector2(0.5f, 1f));
            title.text = "<color=#A9ADB0>REFINERY UPGRADES</color>";

            var types = new[]
            {
                DrillSnakeUpgradeType.Cooling,
                DrillSnakeUpgradeType.DrillMotor,
                DrillSnakeUpgradeType.DriveSpeed,
                DrillSnakeUpgradeType.OreScanner
            };
            for (var i = 0; i < types.Length; i++)
            {
                CreateUpgradeButton(types[i], i);
            }
        }

        private void CreateUpgradeButton(DrillSnakeUpgradeType type, int index)
        {
            var buttonObject = CreatePanel(
                $"{type} Button",
                _upgradePanel.transform,
                new Color(0.055f, 0.062f, 0.068f, 0.98f));
            SetRect(
                buttonObject.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(18f + index * 311f, 14f),
                new Vector2(300f, 144f),
                new Vector2(0f, 0f));

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.82f, 0.55f);
            colors.pressedColor = new Color(0.92f, 0.62f, 0.28f);
            colors.disabledColor = new Color(0.46f, 0.48f, 0.5f, 0.9f);
            button.colors = colors;
            var capturedType = type;
            button.onClick.AddListener(() => _purchaseUpgrade?.Invoke(capturedType));

            var iconObject = new GameObject($"{type} Icon", typeof(RectTransform));
            iconObject.transform.SetParent(buttonObject.transform, false);
            var icon = iconObject.AddComponent<Image>();
            icon.sprite = _upgradeIcons[type];
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            SetRect(
                icon.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(14f, 0f),
                new Vector2(96f, 96f),
                new Vector2(0f, 0.5f));

            var label = CreateText(
                $"{type} Label",
                buttonObject.transform,
                20,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            SetRect(
                label.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(120f, 0f),
                new Vector2(164f, -20f),
                new Vector2(0f, 0.5f));
            label.supportRichText = true;

            _upgradeButtons[type] = button;
            _upgradeLabels[type] = label;
            _upgradeIconImages[type] = icon;
        }

        private Text CreateText(
            string name,
            Transform parent,
            int size,
            TextAnchor alignment,
            FontStyle style)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = new Color(0.93f, 0.93f, 0.9f);
            text.raycastTarget = false;
            return text;
        }

        private Text CreateStatBlock(string name, Transform parent, int index)
        {
            var text = CreateText(
                name,
                parent,
                22,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            SetRect(
                text.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(22f + index * 220f, 0f),
                new Vector2(180f, -12f),
                new Vector2(0f, 0.5f));
            text.supportRichText = true;
            text.lineSpacing = 0.82f;
            return text;
        }

        private GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var image = panel.AddComponent<Image>();
            image.color = color;
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.38f, 0.4f, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);

            return panel;
        }

        private void CreateUpgradeSprites()
        {
            var atlas = Resources.Load<Texture2D>("Art/DrillSnakeUpgradeAtlas");
            _upgradeIcons[DrillSnakeUpgradeType.Cooling] =
                CreateAtlasSprite(atlas, 0, 1, "Cooling Upgrade Icon");
            _upgradeIcons[DrillSnakeUpgradeType.DrillMotor] =
                CreateAtlasSprite(atlas, 1, 1, "Drill Motor Upgrade Icon");
            _upgradeIcons[DrillSnakeUpgradeType.DriveSpeed] =
                CreateAtlasSprite(atlas, 0, 0, "Drive Speed Upgrade Icon");
            _upgradeIcons[DrillSnakeUpgradeType.OreScanner] =
                CreateAtlasSprite(atlas, 1, 0, "Ore Scanner Upgrade Icon");
        }

        private static Sprite CreateAtlasSprite(
            Texture2D atlas,
            int column,
            int row,
            string name)
        {
            if (atlas == null)
            {
                return null;
            }

            atlas.wrapMode = TextureWrapMode.Clamp;
            atlas.filterMode = FilterMode.Bilinear;
            var width = atlas.width / 2;
            var height = atlas.height / 2;
            var sprite = Sprite.Create(
                atlas,
                new Rect(column * width, row * height, width, height),
                new Vector2(0.5f, 0.5f),
                500f,
                2u,
                SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        private static GameObject CreateFlatPanel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            panel.AddComponent<Image>().color = color;
            return panel;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static string UpgradeName(DrillSnakeUpgradeType type)
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

        private static string UpgradeEffect(DrillSnakeUpgradeType type)
        {
            return type switch
            {
                DrillSnakeUpgradeType.Cooling => "+18 max heat",
                DrillSnakeUpgradeType.DrillMotor => "stronger impacts",
                DrillSnakeUpgradeType.DriveSpeed => "faster movement",
                DrillSnakeUpgradeType.OreScanner => "+15% ore value",
                _ => string.Empty
            };
        }

        private static string ArtModeName(DrillSnakeArtMode mode)
        {
            return mode == DrillSnakeArtMode.ProceduralCel
                ? "PROCEDURAL CEL"
                : "ILLUSTRATED PNG";
        }
    }
}
