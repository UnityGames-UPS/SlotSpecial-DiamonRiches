using System.Collections;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
public class UIManager : MonoBehaviour
{
  [SerializeField] private SocketController socketController;
  [SerializeField] private GameManager gameManager;

  [Header("Paytable Popup")]
  [SerializeField] internal Button paytable_Button;
  [SerializeField] private GameObject payTablePopup_Object;
  [SerializeField] private Button paytableExit_Button;
  [SerializeField] private Button paytableBgExit_Button;

  [Header("Paytable Texts")]
  [SerializeField] private SymbolPayoutTexts[] SymbolsTexts;

  [Header("Pagination")]
  int CurrentIndex = 0;
  [SerializeField] private GameObject[] paytableList;
  [SerializeField] private Image[] pageIndicators;
  [SerializeField] private Sprite indicatorOn;
  [SerializeField] private Sprite indicatorOff;

  [Header("Pagination - Drag")]
  [SerializeField] private float pageWidth = 1453.13f;
  [SerializeField] private float dragSnapThreshold = 0.25f;
  [SerializeField] private float edgeResistance = 0.35f;
  [SerializeField] private float snapDuration = 0.3f;
  private float _dragAccum;
  private int _neighborIndex = -1;
  private bool _dragAtEdge;

  [Header("Sound Toggle")]
  [SerializeField] private Button SoundToggle_button;
  [SerializeField] private Sprite soundON;
  [SerializeField] private Sprite soundOFF;
  private bool isSound = true;

  [Header("all Win Popup")]
  [SerializeField] private TMP_Text Win_Text;

  [Header("Win Animation - Text")]
  [SerializeField] private float scaleUpDuration = 0.8f;
  [SerializeField] private float normalLerpDuration = 3.0f;
  [SerializeField] private float bigWinLerpDuration = 3.5f;
  [SerializeField] private float superWinLerpDuration = 4.0f;
  [SerializeField] private float textMoveDuration = 0.4f;
  [SerializeField] private float postLerpHoldDuration = 2.0f;

  [Header("Win Animation - Thresholds")]
  [SerializeField] private float normalWinMultiplier = 5f;
  [SerializeField] private float bigWinMultiplier = 10f;
  [SerializeField] private float superWinMultiplier = 15f;

  [Header("Win Animation - Big Win")]
  [SerializeField] private RectTransform bigWinBgRect;
  [SerializeField] private RectTransform bigWinAnimRect;
  [SerializeField] private ImageAnimation bigWinAnim;
  [Range(0.1f, 0.9f)][SerializeField] private float bigWinTextMoveThreshold = 0.6f;
  [SerializeField] private float bigWinTextTargetY = -131f;

  [Header("Win Animation - Super Win")]
  [SerializeField] private RectTransform superWinAnimRect;
  [SerializeField] private ImageAnimation superWinBgAnim;
  [SerializeField] private ImageAnimation superWinCoinAnim;
  [Range(0.1f, 0.9f)][SerializeField] private float superWinTextMoveThreshold = 0.4f;
  [Range(0.1f, 0.9f)][SerializeField] private float superWinExtraThreshold = 0.65f;
  [SerializeField] private float superWinBgFadeIn = 0.25f;
  [SerializeField] private float superWinBgFadeOut = 0.35f;
  [SerializeField] private float superWinCoinFadeIn = 0.4f;
  [SerializeField] private float superWinCoinFadeOut = 0.35f;
  [Range(0.0f, 1.0f)][SerializeField] private float superWinBgProgressForCoin = 0.7f;
  private bool _coinLoopShown;
  private bool _coinEndPlayed;
  private Coroutine _coinWatcher;

  [Header("Win Animation - Debug")]
  [SerializeField] private bool enableDebugKeys = false;
  [SerializeField] private float debugBetAmount = 1.0f;


  [Header("low balance popup")]
  [SerializeField] private GameObject LowBalancePopup_Object;
  [SerializeField] private Button Close_Button;


  [Header("disconnection popup")]
  [SerializeField] private GameObject DisconnectPopup_Object;
  [SerializeField] private Button CloseDisconnect_Button;

  [Header("Reconnection Popup")]
  [SerializeField] private GameObject ReconectionPopup_Object;

  [Header("Startup Popup")]
  [SerializeField] private GameObject StartupPanel;
  [SerializeField] private Button CloseStartupPanelBtn;

  [Header("Quit Popup")]
  [SerializeField] private GameObject QuitPopupObject;
  [SerializeField] private Button GameExit_Button;
  [SerializeField] private Button no_Button;
  [SerializeField] private Button yes_Button;

  private bool isExit = false;
  private const string HasSeenStartupKey = "hasSeenStartup";

  internal bool isWinAnimating;
  private Coroutine _winCoroutine;
  private List<Tween> _winTweens = new List<Tween>();
  private bool _bigWinActive;
  private bool _superWinActive;
  private double _currentWinAmount;
  private int _currentDecimalPlaces;
  private bool _winTextMoved;
  private float _winTextOriginalY;

  [Header("JS / Audio")]
  [SerializeField] private JSFunctCalls jsFunctCalls;
  [SerializeField] private AudioController audioController;

  [Header("player texts")]
  [SerializeField] private TMP_Text playerCurrentWinning;
  [SerializeField] private TMP_Text playerBalance;

  [Header("Diamonds Payout UI")]
  // 8 rows, index 0 = count 2, index 7 = count 9.
  [SerializeField] private ImageAnimation diamondPayoutOverlayShine;
  [SerializeField] private RectTransform[] diamondPayoutRowBg;
  [SerializeField] private ImageAnimation[] diamondPayoutRowWinAnim;
  [SerializeField] private RectTransform[] diamondPayoutRowWinAnimRect;
  [SerializeField] private TMP_Text[] diamondPayoutRowText;
  [SerializeField] private float diamondPayoutRowWinWidthDelta = 40f;
  private Vector2[] _diamondPayoutRowBgDefaultSize;
  private Vector2[] _diamondPayoutRowWinAnimDefaultSize;
  private int _diamondActiveRowIndex = -1;

  internal Action<bool> ToggleAudio;
  internal Action<string> playButtonAudio;

  internal Action OnExit;
  internal Action OnLowBalConfirm;

  private void Awake()
  {
    if (StartupPanel != null) StartupPanel.SetActive(false);

    if (jsFunctCalls != null)
      jsFunctCalls.RegisterVisibilityListener(gameObject.name);

    if (Win_Text != null)
    {
      Win_Text.transform.localScale = Vector3.zero;
      _winTextOriginalY = Win_Text.GetComponent<RectTransform>().anchoredPosition.y;
    }

    CacheDiamondPayoutDefaults();
  }

  private void CacheDiamondPayoutDefaults()
  {
    if (diamondPayoutRowBg != null)
    {
      _diamondPayoutRowBgDefaultSize = new Vector2[diamondPayoutRowBg.Length];
      for (int i = 0; i < diamondPayoutRowBg.Length; i++)
        if (diamondPayoutRowBg[i] != null)
          _diamondPayoutRowBgDefaultSize[i] = diamondPayoutRowBg[i].sizeDelta;
    }
    if (diamondPayoutRowWinAnimRect != null)
    {
      _diamondPayoutRowWinAnimDefaultSize = new Vector2[diamondPayoutRowWinAnimRect.Length];
      for (int i = 0; i < diamondPayoutRowWinAnimRect.Length; i++)
        if (diamondPayoutRowWinAnimRect[i] != null)
          _diamondPayoutRowWinAnimDefaultSize[i] = diamondPayoutRowWinAnimRect[i].sizeDelta;
    }
  }

  private void Start()
  {
    // Set up each button with the appropriate action
    SetButton(yes_Button, CallOnExitFunction);
    SetButton(no_Button, () => { if (!isExit) QuitPopupObject.SetActive(false); });
    SetButton(GameExit_Button, () => { OpenPopup(QuitPopupObject); });
    SetButton(paytable_Button, () => { OpenPopup(payTablePopup_Object); });
    SetButton(paytableExit_Button, () => payTablePopup_Object.SetActive(false));
    SetButton(paytableBgExit_Button, () => payTablePopup_Object.SetActive(false));
    // SetButton(SoundToggle_button, ToggleSound);
    SetButton(CloseDisconnect_Button, CallOnExitFunction);
    SetButton(Close_Button, () => { LowBalancePopup_Object.SetActive(false); OnLowBalConfirm?.Invoke(); });

    if (CloseStartupPanelBtn) CloseStartupPanelBtn.onClick.RemoveAllListeners();
    if (CloseStartupPanelBtn) CloseStartupPanelBtn.onClick.AddListener(() =>
    {
      if (StartupPanel != null) StartupPanel.SetActive(false);
    });

    // Initialize other settings
    foreach (var page in paytableList)
    {
      RectTransform rt = page.transform as RectTransform;
      rt.anchoredPosition = new Vector2(0, rt.anchoredPosition.y);
    }
    paytableList[CurrentIndex = 0].SetActive(true);
    InitIndicators();
  }

  private void Update()
  {
    if (!enableDebugKeys) return;
    if (Input.GetKeyDown(KeyCode.Alpha1))
      TriggerWinAnimation(debugBetAmount * (normalWinMultiplier + 2f), debugBetAmount);
    if (Input.GetKeyDown(KeyCode.Alpha2))
      TriggerWinAnimation(debugBetAmount * (bigWinMultiplier + 2f), debugBetAmount);
    if (Input.GetKeyDown(KeyCode.Alpha3))
      TriggerWinAnimation(debugBetAmount * (superWinMultiplier + 5f), debugBetAmount);
    if (Input.GetKeyDown(KeyCode.Alpha4))
      ResetWinAnimation();
  }

  private void SetButton(Button button, Action action)
  {
    if (button == null)
    {
      Debug.LogError("Button is null");
      return;
    }

    button.onClick.RemoveAllListeners();
    button.onClick.AddListener(() =>
    {
      playButtonAudio?.Invoke("button");
      action?.Invoke();
    });
  }

  internal void UpdatePlayerInfo()
  {
    double winAmount = socketController.ResultData?.payload?.winAmount ?? 0.00;
    if(winAmount>0)
      playerCurrentWinning.text = TextFormatter.FormatSprite(winAmount, TextFormatter.GetSignificantDecimals(winAmount));
    else
     playerCurrentWinning.text = TextFormatter.FormatSprite(0, 2); 

    SetPlayerBalance(socketController.PlayerData.balance);
  }

  void ResetWinUIText()
  {
    playerCurrentWinning.text = TextFormatter.FormatSprite(0.00, 2);
  }

  internal void SetPlayerCurrentWinning(double value)
  {
    if (playerCurrentWinning != null)
      playerCurrentWinning.text = TextFormatter.FormatMoney(value);
  }

  internal void LowBalPopup()
  {
    OpenPopup(LowBalancePopup_Object);
  }

  internal bool IsLowBalPopupOpen => LowBalancePopup_Object != null && LowBalancePopup_Object.activeSelf;

  // TODO: rework to use Symbol.payout + features.diamondPayout (Symbol.multiplier is now always empty in Diamond Riches, so texts currently clear).
  internal void PopulateSymbolsPayout(UiData uiData)
  {
    // if (uiData == null || uiData.paylines.symbols == null)
    //   return;

    // foreach(var symbolText in SymbolsTexts)
    // {
    //   Symbol symbol = uiData.paylines.symbols.FirstOrDefault(s => s.name == symbolText.symbolName);
    //   if (symbol == null || symbol.multiplier == null || symbol.multiplier.Count == 0)
    //   {
    //     symbolText.symbolText.ForEach(t => t.text = "");
    //     continue;
    //   }

    //   int multiplierCount = symbol.multiplier.Count;
    //   for (int j = 0; j < multiplierCount; j++)
    //   {
    //     double payout = symbol.multiplier[j] * socketController.InitLineBetData.bets[gameManager.betCounter];
    //     string payoutText = $"{payout}";
    //     symbolText.symbolText[j].text = payoutText;
    //   }
    // }
  }

  internal void PlayDiamondPayoutShineOverlay()
  {
    if (diamondPayoutOverlayShine == null) return;
    diamondPayoutOverlayShine.StopAnimation();
    diamondPayoutOverlayShine.doLoopAnimation = false;
    diamondPayoutOverlayShine.StartAnimation();
  }

  // Maps diamondCount (2..9) to row index (0..7) and plays the per-row glow looped, with the
  // row's bg + win-anim rects widened by diamondPayoutRowWinWidthDelta. StopDiamondPayoutRowWin
  // unwinds the resize.
  internal void PlayDiamondPayoutRowWin(int diamondCount)
  {
    int idx = diamondCount - 2;
    if (diamondPayoutRowWinAnim == null || idx < 0 || idx >= diamondPayoutRowWinAnim.Length) return;
    StopDiamondPayoutRowWin();
    _diamondActiveRowIndex = idx;

    if (diamondPayoutRowBg != null && idx < diamondPayoutRowBg.Length && diamondPayoutRowBg[idx] != null)
    {
      var rt = diamondPayoutRowBg[idx];
      rt.sizeDelta = _diamondPayoutRowBgDefaultSize[idx] + new Vector2(diamondPayoutRowWinWidthDelta, 0f);
    }
    if (diamondPayoutRowWinAnimRect != null && idx < diamondPayoutRowWinAnimRect.Length && diamondPayoutRowWinAnimRect[idx] != null)
    {
      var rt = diamondPayoutRowWinAnimRect[idx];
      rt.sizeDelta = _diamondPayoutRowWinAnimDefaultSize[idx] + new Vector2(diamondPayoutRowWinWidthDelta, 0f);
    }

    var anim = diamondPayoutRowWinAnim[idx];
    if (anim != null)
    {
      anim.StopAnimation();
      anim.doLoopAnimation = true;
      anim.gameObject.SetActive(true);
      anim.StartAnimation();
    }
  }

  internal void StopDiamondPayoutRowWin()
  {
    if (_diamondActiveRowIndex < 0) return;
    int idx = _diamondActiveRowIndex;

    if (diamondPayoutRowWinAnim != null && idx < diamondPayoutRowWinAnim.Length && diamondPayoutRowWinAnim[idx] != null)
    {
      var anim = diamondPayoutRowWinAnim[idx];
      anim.StopAnimation();
      anim.ResetToFirstFrame();
      anim.gameObject.SetActive(false);
    }
    if (diamondPayoutRowBg != null && idx < diamondPayoutRowBg.Length && diamondPayoutRowBg[idx] != null
        && _diamondPayoutRowBgDefaultSize != null && idx < _diamondPayoutRowBgDefaultSize.Length)
    {
      diamondPayoutRowBg[idx].sizeDelta = _diamondPayoutRowBgDefaultSize[idx];
    }
    if (diamondPayoutRowWinAnimRect != null && idx < diamondPayoutRowWinAnimRect.Length && diamondPayoutRowWinAnimRect[idx] != null
        && _diamondPayoutRowWinAnimDefaultSize != null && idx < _diamondPayoutRowWinAnimDefaultSize.Length)
    {
      diamondPayoutRowWinAnimRect[idx].sizeDelta = _diamondPayoutRowWinAnimDefaultSize[idx];
    }
    _diamondActiveRowIndex = -1;
  }

  internal void RefreshDiamondPayoutTexts(double totalBet = 0)
  {
    if (diamondPayoutRowText == null) return;
    var payout = socketController?.InitData?.features?.diamondPayout;
    var bets = socketController?.InitLineBetData?.bets;
    if (payout == null || bets == null || gameManager == null) return;
    if (gameManager.betCounter < 0 || gameManager.betCounter >= bets.Count) return;
    double lineBet = bets[gameManager.betCounter];

    for (int i = 0; i < diamondPayoutRowText.Length; i++)
    {
      if (diamondPayoutRowText[i] == null) continue;
      int count = i + 2;
      if (!payout.TryGetValue(count, out int mult)) { diamondPayoutRowText[i].text = ""; continue; }
      diamondPayoutRowText[i].text = TextFormatter.FormatMoney(mult * totalBet);
    }
  }

  private void CallOnExitFunction()
  {
    isExit = true;
    // OnExit?.Invoke();
    StartCoroutine(socketController.CloseSocket());
  }

  private void OpenPopup(GameObject Popup)
  {
    if (Popup) Popup.SetActive(true);
  }

  private void ClosePopup(GameObject Popup)
  {
    if (DisconnectPopup_Object.activeSelf)
    {
      Debug.LogError("Disconnect popup is active, cant open Popup: " + Popup.name);
      return;
    } 
    if (Popup) Popup.SetActive(false);
  }

  private void InitIndicators()
  {
    for (int i = 0; i < pageIndicators.Length; i++)
      pageIndicators[i].sprite = i == 0 ? indicatorOn : indicatorOff;
  }

  internal void OnDragBegin()
  {
    DOTween.Kill(paytableList[CurrentIndex].transform as RectTransform);
    if (_neighborIndex >= 0 && _neighborIndex < paytableList.Length)
      DOTween.Kill(paytableList[_neighborIndex].transform as RectTransform);
    _dragAccum = 0f;
    _neighborIndex = -1;
    _dragAtEdge = false;
  }

  internal void OnDragDelta(float deltaX)
  {
    _dragAccum += deltaX;

    bool atLeftEdge = CurrentIndex == 0 && _dragAccum > 0f;
    bool atRightEdge = CurrentIndex == paytableList.Length - 1 && _dragAccum < 0f;
    _dragAtEdge = atLeftEdge || atRightEdge;

    RectTransform current = paytableList[CurrentIndex].transform as RectTransform;

    if (_dragAtEdge)
    {
      if (_neighborIndex != -1)
      {
        RectTransform old = paytableList[_neighborIndex].transform as RectTransform;
        old.anchoredPosition = new Vector2(0, old.anchoredPosition.y);
        paytableList[_neighborIndex].SetActive(false);
        _neighborIndex = -1;
      }
      float resisted = _dragAccum * edgeResistance;
      current.anchoredPosition = new Vector2(resisted, current.anchoredPosition.y);
      return;
    }

    int wantNeighbor = _dragAccum > 0f ? CurrentIndex - 1 : CurrentIndex + 1;
    if (wantNeighbor < 0 || wantNeighbor >= paytableList.Length) return;

    if (wantNeighbor != _neighborIndex)
    {
      if (_neighborIndex != -1)
      {
        RectTransform old = paytableList[_neighborIndex].transform as RectTransform;
        old.anchoredPosition = new Vector2(0, old.anchoredPosition.y);
        paytableList[_neighborIndex].SetActive(false);
      }
      _neighborIndex = wantNeighbor;
      paytableList[_neighborIndex].SetActive(true);
    }

    RectTransform neighbor = paytableList[_neighborIndex].transform as RectTransform;
    float neighborOffset = _dragAccum > 0f ? -pageWidth : pageWidth;
    current.anchoredPosition = new Vector2(_dragAccum, current.anchoredPosition.y);
    neighbor.anchoredPosition = new Vector2(_dragAccum + neighborOffset, neighbor.anchoredPosition.y);
  }

  internal void OnDragEnd()
  {
    RectTransform current = paytableList[CurrentIndex].transform as RectTransform;

    if (_dragAtEdge || _neighborIndex == -1)
    {
      current.DOAnchorPosX(0f, snapDuration).SetEase(Ease.OutCubic);
      _dragAccum = 0f;
      _dragAtEdge = false;
      return;
    }

    RectTransform neighbor = paytableList[_neighborIndex].transform as RectTransform;
    bool commit = Mathf.Abs(_dragAccum) >= dragSnapThreshold * pageWidth;

    if (commit)
    {
      int prevIndex = CurrentIndex;
      int nextIndex = _neighborIndex;
      float currentEndX = _dragAccum > 0f ? pageWidth : -pageWidth;

      if (prevIndex < pageIndicators.Length) pageIndicators[prevIndex].sprite = indicatorOff;
      if (nextIndex < pageIndicators.Length) pageIndicators[nextIndex].sprite = indicatorOn;
      CurrentIndex = nextIndex;

      DOTween.Sequence()
        .Append(current.DOAnchorPosX(currentEndX, snapDuration).SetEase(Ease.OutCubic))
        .Join(neighbor.DOAnchorPosX(0f, snapDuration).SetEase(Ease.OutCubic))
        .OnComplete(() =>
        {
          paytableList[prevIndex].SetActive(false);
          current.anchoredPosition = new Vector2(0, current.anchoredPosition.y);
        });
    }
    else
    {
      int oldNeighbor = _neighborIndex;
      float neighborEndX = _dragAccum > 0f ? -pageWidth : pageWidth;
      DOTween.Sequence()
        .Append(current.DOAnchorPosX(0f, snapDuration).SetEase(Ease.OutCubic))
        .Join(neighbor.DOAnchorPosX(neighborEndX, snapDuration).SetEase(Ease.OutCubic))
        .OnComplete(() =>
        {
          paytableList[oldNeighbor].SetActive(false);
          neighbor.anchoredPosition = new Vector2(0, neighbor.anchoredPosition.y);
        });
    }

    _dragAccum = 0f;
    _neighborIndex = -1;
    _dragAtEdge = false;
  }
  internal void CheckAndClosePopup()
  {
    if (ReconectionPopup_Object.activeInHierarchy)
    {
      ClosePopup(ReconectionPopup_Object);
    }
  }
  internal void ReconnectionPopup()
  {
    OpenPopup(ReconectionPopup_Object);
  }

  internal void SetPlayerBalance(double amount)
  {
    string formatted = TextFormatter.FormatMoney(amount);
    if (playerBalance != null) playerBalance.text = formatted;
  }

  internal void TriggerWinAnimation(double winAmount, double betAmount)
  {
    if (winAmount < betAmount * normalWinMultiplier) return;

    SnapResetWinAnimation();
    _bigWinActive = _superWinActive = _winTextMoved = false;

    bool isBigWin = winAmount >= betAmount * bigWinMultiplier && winAmount < betAmount * superWinMultiplier;
    bool isSuperWin = winAmount >= betAmount * superWinMultiplier;

    _winCoroutine = StartCoroutine(WinAnimationCoroutine(winAmount, isBigWin, isSuperWin));
  }

  internal void ResetWinAnimation()
  {
    ResetWinUIText();
    bool wasLerping = _winCoroutine != null;

    if (_winCoroutine != null) { StopCoroutine(_winCoroutine); _winCoroutine = null; }
    if (_coinWatcher != null) { StopCoroutine(_coinWatcher); _coinWatcher = null; }
    foreach (var t in _winTweens) t?.Kill();
    _winTweens.Clear();

    if (wasLerping && Win_Text != null && _currentWinAmount > 0)
    {
      Win_Text.text = TextFormatter.FormatSprite(_currentWinAmount, _currentDecimalPlaces);
      Win_Text.transform.localScale = Vector3.one;
    }

    ScaleOutObject(Win_Text.transform);
    ScaleOutObject(bigWinBgRect);
    ScaleOutObject(bigWinAnimRect);
    ScaleOutObject(superWinAnimRect);

    if (superWinBgAnim != null)
    {
      var bg = superWinBgAnim;
      _winTweens.Add(bg.FadeAlpha(0f, superWinBgFadeOut).OnComplete(() => { bg.StopAnimation(); bg.ResetToFirstFrame(); }));
    }
    if (_coinLoopShown) TriggerCoinEndAndSelfFade();

    if (Win_Text != null)
    {
      RectTransform winRT = Win_Text.GetComponent<RectTransform>();
      winRT.DOAnchorPosY(_winTextOriginalY, 0.4f).SetEase(Ease.InBack);
    }

    _bigWinActive = _superWinActive = _winTextMoved = false;
    _coinLoopShown = _coinEndPlayed = false;
    _currentWinAmount = 0;
    isWinAnimating = false;
  }

  private void SnapResetWinAnimation()
  {
    if (_winCoroutine != null) { StopCoroutine(_winCoroutine); _winCoroutine = null; }
    if (_coinWatcher != null) { StopCoroutine(_coinWatcher); _coinWatcher = null; }
    foreach (var t in _winTweens) t?.Kill();
    _winTweens.Clear();
    isWinAnimating = false;

    if (Win_Text != null)
    {
      Win_Text.transform.localScale = Vector3.zero;
      RectTransform winRT = Win_Text.GetComponent<RectTransform>();
      winRT.anchoredPosition = new Vector2(winRT.anchoredPosition.x, _winTextOriginalY);
    }
    if (bigWinBgRect) bigWinBgRect.localScale = Vector3.zero;
    if (bigWinAnimRect) bigWinAnimRect.localScale = Vector3.zero;
    if (bigWinAnim) bigWinAnim.StopAnimation();
    if (superWinAnimRect) superWinAnimRect.localScale = Vector3.zero;
    if (superWinBgAnim) { superWinBgAnim.StopAnimation(); superWinBgAnim.SetAlpha(0f); superWinBgAnim.ResetToFirstFrame(); }
    if (superWinCoinAnim) { superWinCoinAnim.StopAnimation(); superWinCoinAnim.SetAlpha(0f); superWinCoinAnim.ResetToFirstFrame(); }

    _bigWinActive = _superWinActive = _winTextMoved = false;
    _coinLoopShown = _coinEndPlayed = false;
  }

  private IEnumerator WinAnimationCoroutine(double winAmount, bool isBigWin, bool isSuperWin)
  {
    float moveThreshold = isSuperWin ? superWinTextMoveThreshold
                        : isBigWin ? bigWinTextMoveThreshold
                        : float.MaxValue;

    float duration = isSuperWin ? superWinLerpDuration
                   : isBigWin ? bigWinLerpDuration
                   : normalLerpDuration;

    int decimalPlaces = TextFormatter.GetSignificantDecimals(winAmount, 2);
    _currentWinAmount = winAmount;
    _currentDecimalPlaces = decimalPlaces;
    isWinAnimating = true;

    _winTweens.Add(Win_Text.transform.DOScale(Vector3.one, scaleUpDuration).SetEase(Ease.OutBack));

    float elapsed = 0f;
    while (elapsed < duration)
    {
      elapsed += Time.deltaTime;
      float t = Mathf.Clamp01(elapsed / duration);

      Win_Text.text = TextFormatter.FormatSprite((double)t * winAmount, decimalPlaces);

      if ((isBigWin || isSuperWin) && !_winTextMoved && t >= moveThreshold)
      {
        audioController.Play("bigwin");
        _winTextMoved = true;
        StartMoveWinTextDown();
      }

      if (isSuperWin && !_superWinActive && t >= superWinExtraThreshold)
      {
        _superWinActive = true;
        ScaleInObject(superWinAnimRect);
        if (superWinBgAnim != null)
        {
          superWinBgAnim.SetAlpha(0f);
          superWinBgAnim.StartAnimation();
          _winTweens.Add(superWinBgAnim.FadeAlpha(1f, superWinBgFadeIn));
          _coinWatcher = StartCoroutine(WatchBgShowCoin());
        }
      }

      yield return null;
    }

    Win_Text.text = TextFormatter.FormatSprite(winAmount, decimalPlaces);

    yield return new WaitForSeconds(postLerpHoldDuration);

    ScaleOutObject(Win_Text.transform);
    if (_bigWinActive || _superWinActive)
    {
      ScaleOutObject(bigWinBgRect);
      ScaleOutObject(bigWinAnimRect);
    }
    if (_superWinActive)
    {
      ScaleOutObject(superWinAnimRect);
      if (superWinBgAnim != null)
      {
        var bg = superWinBgAnim;
        _winTweens.Add(bg.FadeAlpha(0f, superWinBgFadeOut).OnComplete(() => { bg.StopAnimation(); bg.ResetToFirstFrame(); }));
      }
      TriggerCoinEndAndSelfFade();
    }

    _winCoroutine = null;
    isWinAnimating = false;
  }

  private IEnumerator WatchBgShowCoin()
  {
    while (superWinBgAnim != null && superWinBgAnim.Progress < superWinBgProgressForCoin)
      yield return null;
    _coinWatcher = null;
    if (_coinLoopShown || superWinCoinAnim == null) yield break;
    _coinLoopShown = true;
    superWinCoinAnim.SetAlpha(0f);
    superWinCoinAnim.StartAnimation();
    _winTweens.Add(superWinCoinAnim.FadeAlpha(1f, superWinCoinFadeIn));
  }

  private void TriggerCoinEndAndSelfFade()
  {
    if (superWinCoinAnim == null || _coinEndPlayed) return;
    _coinEndPlayed = true;
    var coin = superWinCoinAnim;
    coin.PlayEndSequence();
    float endDuration = coin.GetEndSequenceDuration();
    _winTweens.Add(DOVirtual.DelayedCall(endDuration, () =>
    {
      if (coin == null) return;
      coin.FadeAlpha(0f, superWinCoinFadeOut).OnComplete(() => { coin.StopAnimation(); coin.ResetToFirstFrame(); });
    }));
  }

  private void StartMoveWinTextDown()
  {
    RectTransform rt = Win_Text.GetComponent<RectTransform>();
    Tween move = rt.DOAnchorPosY(bigWinTextTargetY, textMoveDuration)
      .SetEase(Ease.Linear)
      .OnComplete(() =>
      {
        _bigWinActive = true;
        bigWinAnim.StartAnimation();
        ScaleInObject(bigWinBgRect);
        ScaleInObject(bigWinAnimRect);
        // TODO: Add ImageAnimation references for bigWin when assets are ready
      });
    _winTweens.Add(move);
  }

  private void ScaleInObject(RectTransform rt)
  {
    if (rt == null) return;
    rt.localScale = Vector3.zero;
    _winTweens.Add(rt.DOScale(Vector3.one, scaleUpDuration).SetEase(Ease.OutBack));
  }

  private void ScaleOutObject(Transform tr)
  {
    if (tr == null || tr.localScale == Vector3.zero) return;
    _winTweens.Add(tr.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InBack));
  }

  internal void DisconnectionPopup()
  {
    if (!isExit)
    {
      OpenPopup(DisconnectPopup_Object);
    }
  }

  public void OnFocusChanged(string value)
  {
    bool focused = value == "1";
    Debug.Log("UNITY FOCUS CHANGED: " + value + " (focused: " + focused + ")");
    audioController?.SetMuteAll(!focused);
    socketController?.HandleFocusChange(focused);
  }

  private void ToggleSound()
  {
    isSound = !isSound;
    if (isSound)
    {
      SoundToggle_button.image.sprite = soundOFF;
      ToggleAudio?.Invoke(false);
    }
    else
    {
      SoundToggle_button.image.sprite = soundON;
      ToggleAudio?.Invoke(true);
    }
  }

}

[Serializable]
public class SymbolPayoutTexts
{
  public string symbolName;
  public List<TMP_Text> symbolText;
}
