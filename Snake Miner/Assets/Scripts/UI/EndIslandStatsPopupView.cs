using System;
using System.Collections;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SSG_Core.Scripts.Localization;
using SSG_Core.Scripts.UI;
using SSG.Util;

[RequireComponent(typeof(Popup))]
public class EndIslandStatsPopupView : MonoBehaviour
{
	private const float CountUpDuration = 1.25f;
	private const float OptionalStatsRowSpacing = 70f;
	private const string TitleLocId = "ui_popup_title_end_island_stats";
	private const string NextButtonLocId = "ui_go_to_next_island";

	[SerializeField] private Sprite _fishCaughtIcon;
	[SerializeField] private Sprite _moneyEarnedIcon;
	[SerializeField] private Sprite _rodCastsIcon;
	[SerializeField] private Sprite _turtlePetsIcon;
	[SerializeField] private Sprite _penguinsKickedIcon;

	[SerializeField] private Image _fishCaughtIconImage;
	[SerializeField] private Image _moneyEarnedIconImage;
	[SerializeField] private Image _rodCastsIconImage;
	[SerializeField] private Image _turtlePetsIconImage;
	[SerializeField] private Image _penguinsKickedIconImage;
	
	[SerializeField] private TMP_Text _titleText;
	[SerializeField] private TMP_Text _fishCaughtValueText;
	[SerializeField] private TMP_Text _moneyEarnedValueText;
	[SerializeField] private TMP_Text _rodCastsValueText;
	[SerializeField] private TMP_Text _turtlePetsValueText;
	[SerializeField] private TMP_Text _penguinsKickedValueText;
	[SerializeField] private GameObject _turtlePetsRow;
	[SerializeField] private GameObject _penguinsKickedRow;
	[SerializeField] private RectTransform _statsTable;
	[SerializeField] private Button _nextButton;
	[SerializeField] private TMP_Text _nextButtonText;
	[SerializeField] private float _extraStatsRowTableYOffset = 52f;

	private Popup _popup;
	private Coroutine _countUpRoutine;

	public Popup Popup => _popup;
	public Button NextButton => _nextButton;

	public event Action NextIslandClicked;

	[Button]
	private void Awake()
	{
		_popup = GetComponent<Popup>();
		EnsureView();
		SetStats(0, 0, 0, 0, 0);
		if (_nextButton != null)
			_nextButton.onClick.AddListener(HandleNextButtonClicked);
	}

	private void OnDestroy()
	{
		if (_nextButton != null)
			_nextButton.onClick.RemoveListener(HandleNextButtonClicked);
	}

	public void ShowStats(IslandRunStatsManager.Snapshot snapshot, string buttonLabelLocId = NextButtonLocId)
	{
		PrepareStats(buttonLabelLocId);
		PlayStatsCountUp(snapshot);
	}

	public void PrepareStats(string buttonLabelLocId = NextButtonLocId)
	{
		EnsureView();
		if (!ValidateBindings())
			return;

		if (_nextButtonText != null)
			_nextButtonText.text = Localizer.GetText(string.IsNullOrWhiteSpace(buttonLabelLocId) ? NextButtonLocId : buttonLabelLocId);
		if (_nextButton != null)
			_nextButton.interactable = true;

		if (_countUpRoutine != null)
			StopCoroutine(_countUpRoutine);
		SetStats(0, 0, 0, 0, 0);
		_countUpRoutine = null;
	}

	public void PlayStatsCountUp(IslandRunStatsManager.Snapshot snapshot)
	{
		EnsureView();

		if (_countUpRoutine != null)
			StopCoroutine(_countUpRoutine);

		SetStats(0, 0, 0, 0, 0);
		LogIslandProgressStats(snapshot);
		if (!isActiveAndEnabled)
		{
			Debug.LogError($"{nameof(EndIslandStatsPopupView)} cannot count up because the popup view is not active and enabled.", this);
			SetStats(snapshot.FishCaught, snapshot.MoneyEarned, snapshot.RodCasts, snapshot.TurtlePets, snapshot.PenguinsKicked, snapshot.PenguinsKicked > 0);
			return;
		}

		Debug.Log(
			$"{nameof(EndIslandStatsPopupView)} starting count-up: Fish={snapshot.FishCaught}, Money={snapshot.MoneyEarned}, RodCasts={snapshot.RodCasts}, TurtlePets={snapshot.TurtlePets}, PenguinsKicked={snapshot.PenguinsKicked}.",
			this);
		_countUpRoutine = StartCoroutine(AnimateStatsCountUpRoutine(snapshot));
	}

	public void StopAnimations()
	{
		if (_countUpRoutine == null)
			return;

		StopCoroutine(_countUpRoutine);
		_countUpRoutine = null;
	}

	private IEnumerator AnimateStatsCountUpRoutine(IslandRunStatsManager.Snapshot snapshot)
	{
		var elapsed = 0f;
		while (elapsed < CountUpDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			var t = Mathf.Clamp01(elapsed / CountUpDuration);
			var easedT = Mathf.SmoothStep(0f, 1f, t);
			SetStats(
				Mathf.RoundToInt(snapshot.FishCaught * easedT),
				(long)System.Math.Round(snapshot.MoneyEarned * (double)easedT, System.MidpointRounding.AwayFromZero),
				Mathf.RoundToInt(snapshot.RodCasts * easedT),
				Mathf.RoundToInt(snapshot.TurtlePets * easedT),
				Mathf.RoundToInt(snapshot.PenguinsKicked * easedT),
				snapshot.PenguinsKicked > 0);
			yield return null;
		}

		SetStats(snapshot.FishCaught, snapshot.MoneyEarned, snapshot.RodCasts, snapshot.TurtlePets, snapshot.PenguinsKicked, snapshot.PenguinsKicked > 0);
		_countUpRoutine = null;
	}

	private void SetStats(int fishCaught, long moneyEarned, int rodCasts, int turtlePets, int penguinsKicked, bool showPenguinsKicked = false)
	{
		EnsureView();

		var showTurtlePets = SaveUtil.IsSaveDataReady && SaveUtil.SaveData.HasCaughtTurtlePet;
		_fishCaughtValueText.text = fishCaught.ToString();
		_moneyEarnedValueText.text = string.Format(Localizer.GetText("ui_currency_amount_format"), CurrencyFormatter.GetNumberShortText(moneyEarned));
		_rodCastsValueText.text = rodCasts.ToString();
		_turtlePetsValueText.text = turtlePets.ToString();
		if (_penguinsKickedValueText != null)
			_penguinsKickedValueText.text = penguinsKicked.ToString();
		if (_turtlePetsRow != null)
			_turtlePetsRow.SetActive(showTurtlePets);
		if (_penguinsKickedRow != null)
			_penguinsKickedRow.SetActive(showPenguinsKicked);
		RefreshPenguinsKickedRowPosition(showTurtlePets);
		RefreshStatsTablePosition((showTurtlePets ? 1 : 0) + (showPenguinsKicked ? 1 : 0));
	}

	private void HandleNextButtonClicked()
	{
		NextIslandClicked?.Invoke();
	}

	private void LogIslandProgressStats(IslandRunStatsManager.Snapshot snapshot)
	{
		var upgradePurchaseTime = snapshot.PurchasedUpgradeThisIsland
			? FormatSeconds(snapshot.TimeToLastUpgradePurchaseSeconds)
			: "N/A";
		Debug.Log(
			$"{nameof(EndIslandStatsPopupView)} island progress stats: TotalIslandTime={FormatSeconds(snapshot.TotalIslandTimeSeconds)}, TotalSessionTime={FormatSeconds(snapshot.TotalSessionTimeSeconds)}, TimeToLastUpgradePurchase={upgradePurchaseTime}, AllAvailableUpgradesPurchased={snapshot.AllAvailableUpgradesPurchased}, AvailableUnpurchasedUpgradeCount={snapshot.AvailableUnpurchasedUpgradeCount}.",
			this);
	}

	private static string FormatSeconds(float seconds)
	{
		var timeSpan = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
		return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds / 10:D2}";
	}

	private void EnsureView()
	{
		if (_titleText == null)
			return;

		_titleText.text = Localizer.GetText(TitleLocId);
		ApplyStatIcons();
	}

	private void ApplyStatIcons()
	{
		if (_fishCaughtIconImage != null && _fishCaughtIcon != null)
			_fishCaughtIconImage.sprite = _fishCaughtIcon;
		if (_moneyEarnedIconImage != null && _moneyEarnedIcon != null)
			_moneyEarnedIconImage.sprite = _moneyEarnedIcon;
		if (_rodCastsIconImage != null && _rodCastsIcon != null)
			_rodCastsIconImage.sprite = _rodCastsIcon;
		if (_turtlePetsIconImage != null && _turtlePetsIcon != null)
			_turtlePetsIconImage.sprite = _turtlePetsIcon;
		if (_penguinsKickedIconImage != null && _penguinsKickedIcon != null)
			_penguinsKickedIconImage.sprite = _penguinsKickedIcon;
	}

	private void RefreshPenguinsKickedRowPosition(bool showTurtlePets)
	{
		if (_penguinsKickedRow == null || _turtlePetsRow == null)
			return;

		if (_penguinsKickedRow.transform is RectTransform rowRect && _turtlePetsRow.transform is RectTransform templateRect)
			rowRect.anchoredPosition = templateRect.anchoredPosition + (showTurtlePets ? Vector2.down * OptionalStatsRowSpacing : Vector2.zero);
	}

	private void RefreshStatsTablePosition(int extraStatsRowCount)
	{
		if (_statsTable == null)
			return;

		var anchoredPosition = _statsTable.anchoredPosition;
		anchoredPosition.y = -52f + Mathf.Max(0, extraStatsRowCount) * _extraStatsRowTableYOffset;
		_statsTable.anchoredPosition = anchoredPosition;
	}

	private bool ValidateBindings()
	{
		var isValid = true;
		if (_titleText == null)
			isValid = LogMissingBinding("Title Text");
		if (_nextButton == null)
			isValid = LogMissingBinding("Next Island Button") && isValid;
		if (_nextButtonText == null)
			isValid = LogMissingBinding("Next Island Button text") && isValid;
		if (_fishCaughtValueText == null)
			isValid = LogMissingBinding("Fish Caught Value") && isValid;
		if (_moneyEarnedValueText == null)
			isValid = LogMissingBinding("Money Earned Value") && isValid;
		if (_rodCastsValueText == null)
			isValid = LogMissingBinding("Rod Casts Value") && isValid;
		if (_turtlePetsValueText == null)
			isValid = LogMissingBinding("Turtle Pets Value") && isValid;
		if (_turtlePetsRow == null)
			isValid = LogMissingBinding("Turtle Pets Row") && isValid;
		if (_turtlePetsIconImage == null)
			isValid = LogMissingBinding("Turtle Pets Icon Image") && isValid;
		if (_penguinsKickedValueText == null)
			isValid = LogMissingBinding("Penguins Kicked Value") && isValid;
		if (_penguinsKickedRow == null)
			isValid = LogMissingBinding("Penguins Kicked Row") && isValid;

		return true; // just log
	}

	private bool LogMissingBinding(string bindingName)
	{
		Debug.LogError($"{nameof(EndIslandStatsPopupView)} is missing required binding '{bindingName}'. Check Popup_EndIslandStats prefab and _Base scene overrides.", this);
		return false;
	}

}
