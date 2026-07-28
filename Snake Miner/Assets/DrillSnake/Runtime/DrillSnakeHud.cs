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

        private Font _font;
        private Text _statsText;
        private Text _objectiveText;
        private Text _debugText;
        private Text _messageText;
        private Text _heatText;
        private RectTransform _heatFill;
        private GameObject _upgradePanel;
        private float _messageHideTime;
        private Action<DrillSnakeUpgradeType> _purchaseUpgrade;

        public void Build(Action<DrillSnakeUpgradeType> purchaseUpgrade)
        {
            _purchaseUpgrade = purchaseUpgrade;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
            BuildHeatBar();
            BuildMessage();
            BuildUpgradePanel();
        }

        public void UpdateState(
            int bankedCredits,
            int cargoCount,
            int cargoValue,
            float heat,
            float maximumHeat,
            int seed,
            bool slowTesting,
            bool heatFree,
            bool gridVisible,
            bool atRefinery,
            bool waitingToDepart,
            Func<DrillSnakeUpgradeType, int> getUpgradeLevel,
            Func<DrillSnakeUpgradeType, int> getUpgradeCost)
        {
            _statsText.text =
                $"BANKED CREDITS  <color=#55F1E4>{bankedCredits:N0}</color>\n" +
                $"CARGO SEGMENTS  <color=#FFE474>{cargoCount}</color>\n" +
                $"CARGO VALUE     <color=#FFE474>{cargoValue:N0}</color>\n" +
                $"LEVEL SEED      <color=#B9C9D6>{seed}</color>";

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

            _debugText.text =
                "<b>CONTROLS</b>\n" +
                "WASD / ARROWS  Turn\n" +
                "SPACE          Boost\n" +
                "R              Regenerate\n" +
                "1 / 2          Slow / Normal\n" +
                "G              Grid overlay\n" +
                "H              Heat-free mode" +
                debugFlags;

            var heatNormalized = maximumHeat <= 0f ? 0f : Mathf.Clamp01(heat / maximumHeat);
            _heatFill.anchorMax = new Vector2(heatNormalized, 1f);
            _heatText.text = $"HEAT  {Mathf.CeilToInt(heat)} / {Mathf.CeilToInt(maximumHeat)}";
            var heatImage = _heatFill.GetComponent<Image>();
            heatImage.color = Color.Lerp(
                new Color(0.08f, 0.82f, 0.72f),
                new Color(1f, 0.2f, 0.08f),
                Mathf.Pow(heatNormalized, 1.7f));

            _upgradePanel.SetActive(atRefinery);
            foreach (var pair in _upgradeButtons)
            {
                var type = pair.Key;
                var level = getUpgradeLevel(type);
                var cost = getUpgradeCost(type);
                pair.Value.interactable = bankedCredits >= cost;
                _upgradeLabels[type].text =
                    $"{UpgradeName(type)}  LV.{level}\n" +
                    $"<size=19>{UpgradeEffect(type)}  •  {cost:N0} CR</size>";
            }

            if (waitingToDepart && atRefinery && Time.time >= _messageHideTime)
            {
                _messageText.color = new Color(0.8f, 0.94f, 0.98f);
                _messageText.text = "CHOOSE A DIRECTION TO DEPART";
            }
        }

        public void ShowMessage(string message, Color color, float duration)
        {
            _messageText.color = color;
            _messageText.text = message;
            _messageHideTime = Time.time + duration;
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
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -26f),
                new Vector2(620f, 72f),
                new Vector2(0.5f, 1f));

            _objectiveText = CreateText(
                "Objective",
                panel.transform,
                25,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            Stretch(_objectiveText.rectTransform, 14f);
            _objectiveText.text =
                "MINE ORE AND RETURN TO THE REFINERY\n" +
                "<size=17><color=#86A4B4>Longer cargo trains are harder to bring home.</color></size>";
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
                new Vector2(22f, -22f),
                new Vector2(340f, 174f),
                new Vector2(0f, 1f));

            _statsText = CreateText(
                "Stats",
                panel.transform,
                22,
                TextAnchor.UpperLeft,
                FontStyle.Bold);
            Stretch(_statsText.rectTransform, 18f);
            _statsText.supportRichText = true;
            _statsText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void BuildDebugLegend()
        {
            var panel = CreatePanel(
                "Controls Panel",
                transform,
                new Color(0.025f, 0.04f, 0.055f, 0.86f));
            SetRect(
                panel.GetComponent<RectTransform>(),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-22f, -22f),
                new Vector2(330f, 235f),
                new Vector2(1f, 1f));

            _debugText = CreateText(
                "Controls",
                panel.transform,
                20,
                TextAnchor.UpperLeft,
                FontStyle.Normal);
            Stretch(_debugText.rectTransform, 18f);
            _debugText.supportRichText = true;
            _debugText.lineSpacing = 1.05f;
        }

        private void BuildHeatBar()
        {
            var panel = CreatePanel(
                "Heat Panel",
                transform,
                new Color(0.025f, 0.04f, 0.055f, 0.94f));
            SetRect(
                panel.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 24f),
                new Vector2(620f, 65f),
                new Vector2(0.5f, 0f));

            var background = CreatePanel(
                "Heat Background",
                panel.transform,
                new Color(0.08f, 0.1f, 0.12f, 1f));
            SetRect(
                background.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                Vector2.zero,
                new Vector2(-24f, -22f),
                new Vector2(0.5f, 0.5f));

            var fill = CreatePanel(
                "Heat Fill",
                background.transform,
                new Color(0.08f, 0.82f, 0.72f));
            _heatFill = fill.GetComponent<RectTransform>();
            _heatFill.anchorMin = Vector2.zero;
            _heatFill.anchorMax = new Vector2(0f, 1f);
            _heatFill.offsetMin = Vector2.zero;
            _heatFill.offsetMax = Vector2.zero;

            _heatText = CreateText(
                "Heat Label",
                panel.transform,
                20,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            Stretch(_heatText.rectTransform, 4f);
        }

        private void BuildMessage()
        {
            _messageText = CreateText(
                "Payoff and Failure Message",
                transform,
                31,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            SetRect(
                _messageText.rectTransform,
                new Vector2(0.5f, 0.72f),
                new Vector2(0.5f, 0.72f),
                Vector2.zero,
                new Vector2(900f, 110f),
                new Vector2(0.5f, 0.5f));

            var outline = _messageText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private void BuildUpgradePanel()
        {
            _upgradePanel = CreatePanel(
                "Refinery Upgrade Panel",
                transform,
                new Color(0.025f, 0.04f, 0.055f, 0.95f));
            SetRect(
                _upgradePanel.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(22f, 22f),
                new Vector2(390f, 390f),
                new Vector2(0f, 0f));

            var title = CreateText(
                "Upgrade Title",
                _upgradePanel.transform,
                25,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            SetRect(
                title.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -12f),
                new Vector2(-24f, 50f),
                new Vector2(0.5f, 1f));
            title.text = "REFINERY UPGRADES";

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
                new Color(0.12f, 0.26f, 0.3f, 1f));
            SetRect(
                buttonObject.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -70f - index * 75f),
                new Vector2(-30f, 62f),
                new Vector2(0.5f, 1f));

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = new Color(0.16f, 0.36f, 0.4f);
            colors.highlightedColor = new Color(0.2f, 0.58f, 0.58f);
            colors.pressedColor = new Color(0.08f, 0.8f, 0.72f);
            colors.disabledColor = new Color(0.12f, 0.14f, 0.16f, 0.72f);
            button.colors = colors;
            var capturedType = type;
            button.onClick.AddListener(() => _purchaseUpgrade?.Invoke(capturedType));

            var label = CreateText(
                $"{type} Label",
                buttonObject.transform,
                22,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            Stretch(label.rectTransform, 6f);
            label.supportRichText = true;

            _upgradeButtons[type] = button;
            _upgradeLabels[type] = label;
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
            text.color = new Color(0.92f, 0.96f, 0.98f);
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var image = panel.AddComponent<Image>();
            image.color = color;
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
                DrillSnakeUpgradeType.DrillMotor => "faster drilling",
                DrillSnakeUpgradeType.DriveSpeed => "faster movement",
                DrillSnakeUpgradeType.OreScanner => "+15% ore value",
                _ => string.Empty
            };
        }
    }
}
