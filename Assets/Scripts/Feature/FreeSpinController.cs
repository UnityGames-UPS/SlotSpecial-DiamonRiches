using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FreeSpinController : MonoBehaviour
{
  [Header("Panels (CanvasGroup)")]
  [SerializeField] private CanvasGroup startPanelCG;
  [SerializeField] private CanvasGroup uiPanelCG;
  [SerializeField] private CanvasGroup endPanelCG;

  [Header("Backgrounds")]
  [SerializeField] private GameObject freeSpinsBg;
  [SerializeField] private Image normalBgImage;
  private Image freeSpinsBgImage;

  [Header("Start Panel")]
  [SerializeField] private TMP_Text awardedCountText;
  [SerializeField] private Button startButton;

  [Header("UI Panel")]
  [SerializeField] private TMP_Text freeSpinsLeftText;
  [SerializeField] private TMP_Text totalWinText;
  [SerializeField] private Image gamesLeftLabelImage;
  [SerializeField] private Sprite gamesLeftPluralSprite;
  [SerializeField] private Sprite gameLeftSingularSprite;
  [SerializeField] private Button stopButton;
  [SerializeField] private Button skipButton;
  [SerializeField] private TMP_Text stopButtonCountText;
  [SerializeField] private TMP_Text skipButtonCountText;

  [Header("End Panel")]
  [SerializeField] private TMP_Text endTotalWinText;
  [SerializeField] private Button okButton;

  [Header("Timing")]
  [SerializeField] private float fadeDuration = 0.4f;
  [SerializeField] private float endPanelDelay = 1.0f;
  [SerializeField] private float interSpinDelay = 1.0f;

  private int _spinsRemaining;
  private double _totalWin;
  private bool _startClicked;
  private bool _skipRequested;
  private bool _okClicked;

  void Awake()
  {
    if (startButton != null)
    {
      startButton.onClick.RemoveAllListeners();
      startButton.onClick.AddListener(() => _startClicked = true);
    }
    if (stopButton != null)
    {
      stopButton.onClick.RemoveAllListeners();
      stopButton.onClick.AddListener(() =>
      {
        GameManager.immediateStop = true;
        stopButton.interactable = false;
      });
    }
    if (skipButton != null)
    {
      skipButton.onClick.RemoveAllListeners();
      skipButton.onClick.AddListener(() =>
      {
        _skipRequested = true;
        skipButton.interactable = false;
      });
    }
    if (okButton != null)
    {
      okButton.onClick.RemoveAllListeners();
      okButton.onClick.AddListener(() => _okClicked = true);
    }

    SnapHide(startPanelCG);
    SnapHide(uiPanelCG);
    SnapHide(endPanelCG);

    if (freeSpinsBg != null)
    {
      freeSpinsBgImage = freeSpinsBg.GetComponent<Image>();
      if (freeSpinsBgImage != null)
      {
        var c = freeSpinsBgImage.color; c.a = 0f; freeSpinsBgImage.color = c;
      }
    }
  }

  internal IEnumerator RunFreeSpins(int totalCount, Func<IEnumerator> spinFn, Func<double> getLastWinAmount,
    Func<bool> wasFreeSpinTrigger, Func<int> getRetriggerAward, Action<double> onOkBeforeFade, Action onAllDone)
  {
    _spinsRemaining = totalCount;
    _totalWin = 0;

    awardedCountText.text = TextFormatter.FormatSprite(totalCount, 0);
    _startClicked = false;
    yield return FadeIn(startPanelCG);
    yield return new WaitUntil(() => _startClicked);

    InitUiPanelTexts();
    Sequence enter = DOTween.Sequence();
    SetCgInteractable(startPanelCG, false);
    enter.Join(startPanelCG.DOFade(0f, fadeDuration));
    uiPanelCG.gameObject.SetActive(true);
    uiPanelCG.alpha = 0f;
    SetCgInteractable(uiPanelCG, false);
    enter.Join(uiPanelCG.DOFade(1f, fadeDuration));
    if (normalBgImage != null) enter.Join(normalBgImage.DOFade(0f, fadeDuration));
    if (freeSpinsBgImage != null) enter.Join(freeSpinsBgImage.DOFade(1f, fadeDuration));
    yield return enter.WaitForCompletion();
    startPanelCG.gameObject.SetActive(false);
    SetCgInteractable(uiPanelCG, true);

    yield return new WaitForSeconds(1f);

    while (_spinsRemaining > 0)
    {
      _spinsRemaining--;
      UpdateUiPanel();
      ConfigureSpinningButtons();

      yield return spinFn();

      if (getLastWinAmount != null) _totalWin += getLastWinAmount();
      totalWinText.text = TextFormatter.FormatMoney(_totalWin);

      if (wasFreeSpinTrigger != null && wasFreeSpinTrigger())
      {
        int extra = getRetriggerAward != null ? getRetriggerAward() : 0;
        if (extra > 0) yield return HandleRetrigger(extra);
      }

      if (_spinsRemaining > 0)
      {
        ConfigureDelayButtons();
        yield return WaitOrSkip(interSpinDelay);
      }
    }

    SetCgInteractable(uiPanelCG, false);
    if (stopButton != null) stopButton.interactable = false;
    if (skipButton != null) skipButton.interactable = false;

    yield return new WaitForSeconds(endPanelDelay);

    int decimals = TextFormatter.GetSignificantDecimals(_totalWin, 2);
    endTotalWinText.text = TextFormatter.FormatSprite(_totalWin, decimals);
    _okClicked = false;
    yield return FadeIn(endPanelCG);
    yield return new WaitUntil(() => _okClicked);

    SetCgInteractable(endPanelCG, false);

    onOkBeforeFade?.Invoke(_totalWin);

    Sequence exit = DOTween.Sequence();
    exit.Join(endPanelCG.DOFade(0f, fadeDuration));
    exit.Join(uiPanelCG.DOFade(0f, fadeDuration));
    if (normalBgImage != null) exit.Join(normalBgImage.DOFade(1f, fadeDuration));
    if (freeSpinsBgImage != null) exit.Join(freeSpinsBgImage.DOFade(0f, fadeDuration));
    yield return exit.WaitForCompletion();

    endPanelCG.gameObject.SetActive(false);
    uiPanelCG.gameObject.SetActive(false);

    onAllDone?.Invoke();
  }

  IEnumerator HandleRetrigger(int awardedThisSpin)
  {
    awardedCountText.text = TextFormatter.FormatSprite(awardedThisSpin, 0);
    _startClicked = false;
    yield return FadeIn(startPanelCG);
    yield return new WaitUntil(() => _startClicked);

    _spinsRemaining += awardedThisSpin;
    UpdateUiPanel();

    SetCgInteractable(startPanelCG, false);
    yield return startPanelCG.DOFade(0f, fadeDuration).WaitForCompletion();
    startPanelCG.gameObject.SetActive(false);
  }

  void InitUiPanelTexts()
  {
    _spinsRemaining = Mathf.Max(_spinsRemaining, 0);
    UpdateUiPanel();
    totalWinText.text = TextFormatter.FormatMoney(0);
  }

  void UpdateUiPanel()
  {
    freeSpinsLeftText.text = TextFormatter.FormatSprite(_spinsRemaining, 0);
    bool singular = _spinsRemaining <= 1;
    if (gamesLeftLabelImage != null)
    {
      gamesLeftLabelImage.sprite = singular ? gameLeftSingularSprite : gamesLeftPluralSprite;
    }
    string countStr = _spinsRemaining.ToString();
    if (stopButtonCountText != null) stopButtonCountText.text = countStr;
    if (skipButtonCountText != null) skipButtonCountText.text = countStr;
  }

  void ConfigureSpinningButtons()
  {
    if (stopButton != null)
    {
      stopButton.gameObject.SetActive(true);
      stopButton.interactable = true;
    }
    if (skipButton != null)
    {
      skipButton.gameObject.SetActive(false);
      skipButton.interactable = false;
    }
  }

  void ConfigureDelayButtons()
  {
    if (stopButton != null)
    {
      stopButton.gameObject.SetActive(false);
      stopButton.interactable = false;
    }
    if (skipButton != null)
    {
      skipButton.gameObject.SetActive(true);
      skipButton.interactable = true;
    }
  }

  internal void SetButtonsInteractable(bool stop, bool skip)
  {
    if (stopButton != null) stopButton.interactable = stop;
    if (skipButton != null) skipButton.interactable = skip;
  }

  internal bool ConsumeSkipRequest()
  {
    if (_skipRequested) { _skipRequested = false; return true; }
    return false;
  }

  internal void ArmSkipButton()
  {
    if (stopButton != null) { stopButton.gameObject.SetActive(false); stopButton.interactable = false; }
    if (skipButton != null) { skipButton.gameObject.SetActive(true); skipButton.interactable = true; }
  }

  internal void DisarmSkipButton()
  {
    if (skipButton != null) skipButton.interactable = false;
  }

  internal IEnumerator SkippableWait(float seconds)
  {
    yield return WaitOrSkip(seconds);
  }

  IEnumerator WaitOrSkip(float seconds)
  {
    _skipRequested = false;
    float elapsed = 0f;
    while (elapsed < seconds)
    {
      if (_skipRequested) yield break;
      elapsed += Time.deltaTime;
      yield return null;
    }
  }

  IEnumerator FadeIn(CanvasGroup cg)
  {
    cg.gameObject.SetActive(true);
    cg.alpha = 0f;
    SetCgInteractable(cg, false);
    yield return cg.DOFade(1f, fadeDuration).OnComplete(() => SetCgInteractable(cg, true)).WaitForCompletion();
  }

  void SetCgInteractable(CanvasGroup cg, bool on)
  {
    if (cg == null) return;
    cg.interactable = on;
    cg.blocksRaycasts = on;
  }

  void SnapHide(CanvasGroup cg)
  {
    if (cg == null) return;
    cg.alpha = 0f;
    SetCgInteractable(cg, false);
    cg.gameObject.SetActive(false);
  }
}
