using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using SSG_Core.Scripts.Audio;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.Input;
using SSG_Core.Scripts.Localization;
using SSG_Core.Scripts.Menu;
using SSG_Core.Scripts.Scene;
using SSG_Core.Scripts.UI;
using SSG_Core.Scripts.Util.Platform;
using SSG.Util;
using TMPro;
using UnityEngine;

public class SkillTreeManager : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private Transform _nodesParent;
    [SerializeField] private float _cellSize = 1f;

    [Header("Prefabs (stubbed)")]
    [SerializeField] private SkillTreeNodeView _nodePrefab;
    [SerializeField] private TMP_Text _currencyText;
    [SerializeField] private ButtonWithAction _backButton;
    [SerializeField] private ButtonWithAction _wishlistButton;
    [SerializeField] private GameObject _backButtonKeyboardBinding;
    [SerializeField] private GameObject _backButtonControllerBinding;
    [SerializeField] private SkillTreePowerupInfoPanel _powerupInfoPanel;
    [Header("Controller Navigation")]
    [SerializeField] private float _navigateDeadzone = 0.5f;
    [SerializeField] private float _navigateInitialRepeatDelay = 0.22f;
    [SerializeField] private float _navigateHeldRepeatDelay = 0.12f;
    [SerializeField, Range(0f, 180f)] private float _maxNavigateAngleDegrees = 45f;

    // Runtime
    private SkillTreeData _data;
    private readonly Dictionary<GridPos, SkillTreeNodeView> _spawned = new Dictionary<GridPos, SkillTreeNodeView>();
    private readonly HashSet<GridPos> _initiallyUnlockedPositions = new HashSet<GridPos>();
    private bool _hasPlayedEntryCurrencyCount;
    private Coroutine _entryCurrencyStartRoutine;
    private SkillTreeNodeView _selectedNode;
    private bool _isNavigateHeld;
    private float _nextNavigateTime;
    private bool _wasUsingController;

    public ButtonWithAction BackButton => _backButton;

    private void Start()
    {
        if (_backButton != null)
        {
            _backButton.OnClicked -= HandleBackClicked;
            _backButton.OnClicked += HandleBackClicked;
        }

        if (_wishlistButton != null)
        {
            _wishlistButton.OnClicked -= HandleWishlistClicked;
            _wishlistButton.OnClicked += HandleWishlistClicked;
        }

        RefreshBackButtonBindingVisibility(IsUsingController());
        HideBackButtonControlBindingForWebBuild();
        Build();
        RefreshCurrencyText();
        if (_entryCurrencyStartRoutine != null)
            StopCoroutine(_entryCurrencyStartRoutine);
        _entryCurrencyStartRoutine = StartCoroutine(BeginEntryCurrencyCountWhenReady());
    }

    private void Update()
    {
        UpdateControllerSelectionState();
        HandleControllerNavigation();
        HandleControllerSubmit();

        if (InputManager.InputActions.SkillTree.Pause.WasPressedThisFrame())
            GoToLevel1();

        if (InputManager.InputActions.SkillTree.Exit.WasPressedThisFrame())
            GoToLevel1();
    }

    private void OnDestroy()
    {
        if (_backButton != null)
            _backButton.OnClicked -= HandleBackClicked;

        if (_wishlistButton != null)
            _wishlistButton.OnClicked -= HandleWishlistClicked;

        if (_entryCurrencyStartRoutine != null)
            StopCoroutine(_entryCurrencyStartRoutine);
    }

    private void HideBackButtonControlBindingForWebBuild()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_backButtonControllerBinding != null)
            _backButtonControllerBinding.SetActive(false);
#endif
    }

    private void RefreshBackButtonBindingVisibility(bool isUsingController)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        isUsingController = false;
#endif
        if (_backButtonKeyboardBinding != null && _backButtonKeyboardBinding.activeSelf == isUsingController)
            _backButtonKeyboardBinding.SetActive(!isUsingController);
        if (_backButtonControllerBinding != null && _backButtonControllerBinding.activeSelf != isUsingController)
            _backButtonControllerBinding.SetActive(isUsingController);
    }

    [Button]
    public void Build()
    {
        Clear();
        SkillTreeNodeView.ResetSharedConnectionStates();

        _data = GameDataManager.Instance.SkillTreeData;
        CacheInitiallyUnlockedPositions();

        if (_data.Upgrades == null || _data.Upgrades.Length == 0)
        {
            Debug.LogWarning("SkillTreeManager: Config parsed but no upgrades found.");
            return;
        }

        StartCoroutine(BuildNodesRtn());
    }

    private void Clear()
    {
        foreach (var kvp in _spawned)
        {
            if (kvp.Value != null)
            {
                kvp.Value.OnRequestPurchase -= HandlePurchaseRequest;
                Destroy(kvp.Value.gameObject);
            }
        }

        _spawned.Clear();
        _initiallyUnlockedPositions.Clear();
        _data = default;
    }

    private void CacheInitiallyUnlockedPositions()
    {
        _initiallyUnlockedPositions.Clear();

        if (_data.Upgrades == null)
            return;

        for (var i = 0; i < _data.Upgrades.Length; i++)
        {
            var upgrade = _data.Upgrades[i];
            if (SaveUtil.IsUpgradeUnlocked(upgrade.gridPos))
                _initiallyUnlockedPositions.Add(upgrade.gridPos);
        }
    }

    private IEnumerator BuildNodesRtn()
    {
        if (_nodePrefab == null)
        {
            Debug.LogError("SkillTreeManager: Node prefab not assigned.");
            yield break;
        }

        var upgrades = _data.Upgrades.OrderBy(u => Mathf.Abs(u.gridPos.x) + Mathf.Abs(u.gridPos.y)).ToArray();
        for (var i = 0; i < upgrades.Length; i++)
        {
            var u = upgrades[i];
            var pos = u.gridPos;

            var world = GridToWorld(pos);
            var view = Instantiate(_nodePrefab, world, Quaternion.identity, _nodesParent);
            SetLayerRecursively(view.gameObject, _nodesParent != null ? _nodesParent.gameObject.layer : view.gameObject.layer);

            view.Initialize(u, i);
            view.SetWasUnlockedBeforeSession(_initiallyUnlockedPositions.Contains(pos));
            view.OnRequestPurchase += HandlePurchaseRequest;

            _spawned[pos] = view;
        }

        RefreshAllNodeStates();
        SelectEntryNode();
    }

    private void HandlePurchaseRequest(SkillTreeNodeView node)
    {
        if (!SkillTreeNodeRules.IsUnlockable(node.Data))
            return;

        var availableMoney = SaveUtil.SaveData.CashMoney;
        if (GameConfigParser.TryGetFloat(node.Data.varsJson, "cost", out var cost))
        {
            if (availableMoney >= cost)
            {
                availableMoney -= (int)cost;
                SaveUtil.SaveData.CashMoney = availableMoney;
                SaveUtil.SaveUpgrade(node.Data.gridPos);
                IslandRunStatsManager.RecordUpgradePurchased();
                if (GameDataManager.Instance != null)
                    GameDataManager.Instance.NotifyRuntimeModsChanged();
                MusicManager.Instance.PlayStinger(StingerEvent.SkillTreeNodeUnlock);
                node.PlayUnlockAnim();
                TryPlayPowerupLegendHighlight(node.Data.type);
                RefreshCurrencyText();
                RefreshAllNodeStates();
            }
        }
        else
        {
            Debug.LogError("Unable to find cost for upgrade " + node.Data.type);
        }
    }

    private void TryPlayPowerupLegendHighlight(string upgradeType)
    {
        if (!IsPowerupUnlockType(upgradeType))
            return;

        if (_powerupInfoPanel == null)
            _powerupInfoPanel = FindAnyObjectByType<SkillTreePowerupInfoPanel>(FindObjectsInactive.Include);

        if (_powerupInfoPanel != null)
            _powerupInfoPanel.PlayHighlightSequence();
    }

    private static bool IsPowerupUnlockType(string upgradeType)
    {
        return GameMods.IsUnlockKey(upgradeType);
    }

    private void BuildConnections()
    {
        for (var i = 0; i < _data.Upgrades.Length; i++)
        {
            var u = _data.Upgrades[i];
            var from = u.gridPos;

            if (!_spawned.TryGetValue(from, out var fromView) || fromView == null)
                continue;

            var conns = u.connections;
            if (conns == null || conns.Length == 0)
                continue;

            for (var c = 0; c < conns.Length; c++)
            {
                var to = conns[c];
                if (!_spawned.TryGetValue(to, out var toView) || toView == null)
                    continue;

                fromView.AddConnection(toView);
            }
        }
    }

    private Vector3 GridToWorld(GridPos p)
    {
        return new Vector3(p.x * _cellSize, p.y * _cellSize, 0);
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        var t = go.transform;
        for (var i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }

    // Optional runtime lookup
    public bool TryGetNodeView(int x, int y, out SkillTreeNodeView view)
    {
        return _spawned.TryGetValue(new GridPos(x, y), out view);
    }

    public void RefreshAllVisuals()
    {
        RefreshCurrencyText();
        RefreshAllNodeStates();
    }

    private void RefreshCurrencyText()
    {
        if (_currencyText == null)
            return;

        if (!SaveUtil.IsSaveDataReady)
        {
            _currencyText.text = string.Format(Localizer.GetText("ui_currency_amount_format"), CurrencyFormatter.GetNumberShortText(0L));
            return;
        }

        _currencyText.text = string.Format(Localizer.GetText("ui_currency_amount_format"), CurrencyFormatter.GetNumberShortText(SaveUtil.SaveData.CashMoney));
    }

    private void TryStartEntryCurrencyCount()
    {
        if (_hasPlayedEntryCurrencyCount || _currencyText == null || !SaveUtil.IsSaveDataReady)
            return;

        _hasPlayedEntryCurrencyCount = true;
        RefreshCurrencyText();
    }

    private IEnumerator BeginEntryCurrencyCountWhenReady()
    {
        yield return new WaitUntil(() =>
            _currencyText != null &&
            SaveUtil.IsSaveDataReady &&
            CoreGameManager.Instance != null &&
            !CoreGameManager.Instance.IsLoadingScreenShowing);

        TryStartEntryCurrencyCount();
        _entryCurrencyStartRoutine = null;
    }

    private void RefreshAllNodeStates()
    {
        foreach (var node in _spawned.Values)
        {
            if (node == null)
                continue;

            node.gameObject.SetActive(ShouldShowNode(node.Data));
        }

        foreach (var node in _spawned.Values)
        {
            if (node != null && node.gameObject.activeInHierarchy)
                node.RefreshVisuals();
        }

        RebuildConnections();
        EnsureSelectedNodeIsValid();
    }

    private void HandleControllerNavigation()
    {
        if (!IsUsingController())
            return;

        var navigateAction = InputManager.InputActions.SkillTree.NavigateSkillTree;
        if (!navigateAction.enabled)
            return;

        var input = navigateAction.ReadValue<Vector2>();
        if (input.sqrMagnitude < _navigateDeadzone * _navigateDeadzone)
        {
            _isNavigateHeld = false;
            _nextNavigateTime = 0f;
            return;
        }

        if (Time.unscaledTime < _nextNavigateTime)
            return;

        EnsureSelectedNodeIsValid();
        if (_selectedNode == null)
        {
            SelectBestDefaultNode();
            ScheduleNextNavigationRepeat();
            return;
        }

        var nextNode = FindBestAdjacentNode(input);
        if (nextNode != null)
            SetSelectedNode(nextNode);

        ScheduleNextNavigationRepeat();
    }

    private void HandleControllerSubmit()
    {
        if (!IsUsingController())
            return;

        if (!InputManager.InputActions.SkillTree.SelectSkillTree.WasPressedThisFrame())
            return;

        EnsureSelectedNodeIsValid();
        if (_selectedNode == null)
        {
            SelectBestDefaultNode();
            return;
        }

        _selectedNode.TryPurchase();
    }

    private void ScheduleNextNavigationRepeat()
    {
        _nextNavigateTime = Time.unscaledTime + (_isNavigateHeld ? _navigateHeldRepeatDelay : _navigateInitialRepeatDelay);
        _isNavigateHeld = true;
    }

    private void EnsureSelectedNodeIsValid()
    {
        if (IsNodeSelectable(_selectedNode))
            return;

        SelectBestDefaultNode();
    }

    private void SelectBestDefaultNode()
    {
        var nextNode = _spawned.Values
            .Where(IsNodeSelectable)
            .OrderByDescending(node => node.State == SkillTreeNodeView.SkillTreeNodeState.Unlockable)
            .ThenBy(node => Mathf.Abs(node.Data.gridPos.x) + Mathf.Abs(node.Data.gridPos.y))
            .FirstOrDefault();

        SetSelectedNode(nextNode);
    }

    private void SelectEntryNode()
    {
        var lastPurchasedNode = GetLastPurchasedNode();
        if (lastPurchasedNode == null)
        {
            SelectBestDefaultNode();
            return;
        }

        SetSelectedNode(lastPurchasedNode);
        CenterCameraOn(lastPurchasedNode);
    }

    private SkillTreeNodeView GetLastPurchasedNode()
    {
        if (!SaveUtil.IsSaveDataReady || SaveUtil.SaveData.SavedUpgrades == null)
            return null;

        for (var i = SaveUtil.SaveData.SavedUpgrades.Count - 1; i >= 0; i--)
        {
            var savedUpgrade = SaveUtil.SaveData.SavedUpgrades[i];
            if (!savedUpgrade.Exists)
                continue;

            if (_spawned.TryGetValue(savedUpgrade.Coords, out var node) && IsNodeSelectable(node))
                return node;
        }

        return null;
    }

    private void CenterCameraOn(SkillTreeNodeView node)
    {
        if (node == null)
            return;

        var skillTreeCamera = FindFirstObjectByType<SkillTreeCamera>();
        if (skillTreeCamera != null)
            skillTreeCamera.CenterOn(node.transform.position);
    }

    private void SetSelectedNode(SkillTreeNodeView node)
    {
        if (_selectedNode == node)
            return;

        if (_selectedNode != null)
            _selectedNode.SetControllerSelected(false);

        _selectedNode = node;

        if (_selectedNode != null)
        {
            var isUsingController = IsUsingController();
            _selectedNode.SetControllerSelected(isUsingController);
            if (isUsingController)
                FocusSelectedNode();
        }
    }

    private void UpdateControllerSelectionState()
    {
        var isUsingController = IsUsingController();
        RefreshBackButtonBindingVisibility(isUsingController);
        if (_selectedNode != null)
            _selectedNode.SetControllerSelected(isUsingController);

        if (isUsingController && !_wasUsingController)
        {
            ClearMouseHoverStateForAllNodes();
            FocusSelectedNode();
        }

        _wasUsingController = isUsingController;
    }

    private void ClearMouseHoverStateForAllNodes()
    {
        foreach (var node in _spawned.Values)
        {
            if (node != null)
                node.ClearMouseHoverState();
        }
    }

    private void FocusSelectedNode()
    {
        if (_selectedNode == null)
            return;

        var skillTreeCamera = FindFirstObjectByType<SkillTreeCamera>();
        if (skillTreeCamera != null)
            skillTreeCamera.FocusOn(_selectedNode.transform.position);
    }

    private static bool IsUsingController()
    {
        return ControllerHelper.Instance != null && ControllerHelper.Instance.IsMostRecentControlTypeAController;
    }

    private SkillTreeNodeView FindBestAdjacentNode(Vector2 inputDirection)
    {
        if (_selectedNode == null)
            return null;

        var direction = inputDirection.normalized;
        var minAllowedAlignment = Mathf.Cos(_maxNavigateAngleDegrees * Mathf.Deg2Rad);
        SkillTreeNodeView bestNode = null;
        var bestAlignment = float.NegativeInfinity;
        var bestDistance = float.MaxValue;
        var currentPos = (Vector2)_selectedNode.transform.position;

        foreach (var node in GetConnectedSelectableNodes(_selectedNode))
        {
            var offset = (Vector2)node.transform.position - currentPos;
            var distance = offset.magnitude;
            if (distance <= Mathf.Epsilon)
                continue;

            var alignment = Vector2.Dot(direction, offset / distance);
            if (alignment < minAllowedAlignment)
                continue;

            if (alignment < bestAlignment)
                continue;

            if (Mathf.Approximately(alignment, bestAlignment) && distance >= bestDistance)
                continue;

            bestAlignment = alignment;
            bestDistance = distance;
            bestNode = node;
        }

        return bestNode;
    }

    private IEnumerable<SkillTreeNodeView> GetConnectedSelectableNodes(SkillTreeNodeView node)
    {
        if (node == null)
            yield break;

        var yielded = new HashSet<GridPos>();
        var outgoingConnections = node.Data.connections;
        if (outgoingConnections != null)
        {
            for (var i = 0; i < outgoingConnections.Length; i++)
            {
                if (!_spawned.TryGetValue(outgoingConnections[i], out var connectedNode) || !IsNodeSelectable(connectedNode))
                    continue;

                if (yielded.Add(connectedNode.Data.gridPos))
                    yield return connectedNode;
            }
        }

        foreach (var candidate in _spawned.Values)
        {
            if (!IsNodeSelectable(candidate) || candidate == node)
                continue;

            var candidateConnections = candidate.Data.connections;
            if (candidateConnections == null)
                continue;

            for (var i = 0; i < candidateConnections.Length; i++)
            {
                if (candidateConnections[i].x != node.Data.gridPos.x || candidateConnections[i].y != node.Data.gridPos.y)
                    continue;

                if (yielded.Add(candidate.Data.gridPos))
                    yield return candidate;
                break;
            }
        }
    }

    private static bool IsNodeSelectable(SkillTreeNodeView node)
    {
        return node != null && node.gameObject.activeInHierarchy;
    }

    private void RebuildConnections()
    {
        foreach (var node in _spawned.Values)
        {
            if (node != null)
                node.ClearConnections();
        }

        BuildConnections();
    }

    private static bool ShouldShowNode(UpgradeNode node)
    {
        if (SaveUtil.IsUpgradeUnlocked(node.gridPos))
            return true;

        if (node.gridPos.x == 0 && node.gridPos.y == 0)
            return true; // Keep the root visible as the tree entry point.

        var conns = node.connections;
        if (conns == null || conns.Length == 0)
            return false;

        for (var i = 0; i < conns.Length; i++)
        {
            if (SaveUtil.IsUpgradeUnlocked(conns[i]))
                return true;
        }

        return false;
    }

    private void HandleBackClicked(BaseButton _)
    {
        GoToLevel1();
    }

    private static void HandleWishlistClicked(BaseButton _)
    {
        SteamStoreUrl.Open("https://store.steampowered.com/");
    }

    private void GoToLevel1()
    {
        if (SkillTreePopupController.IsPopupOpenOrTransitioning)
        {
            SkillTreePopupController.CloseSkillTreePopup();
            return;
        }

        CoreGameManager.Instance.GoToScene(SceneNames.Game);
    }
}
