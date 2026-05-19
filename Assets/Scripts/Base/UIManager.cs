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

  [Header("Paytable Texts")]
  [SerializeField] private SymbolPayoutTexts[] SymbolsTexts;

  [Header("Pagination")]
  int CurrentIndex = 0;
  [SerializeField] private GameObject[] paytableList;
  [SerializeField] private Button RightBtn;
  [SerializeField] private Button LeftBtn;
  [SerializeField] private Image[] pageIndicators;
  [SerializeField] private Sprite indicatorOn;
  [SerializeField] private Sprite indicatorOff;

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
  }

  private void Start()
  {
    // Set up each button with the appropriate action
    SetButton(yes_Button, CallOnExitFunction);
    SetButton(no_Button, () => { if (!isExit) QuitPopupObject.SetActive(false); });
    SetButton(GameExit_Button, () => { OpenPopup(QuitPopupObject); });
    SetButton(paytable_Button, () => { OpenPopup(payTablePopup_Object); });
    SetButton(paytableExit_Button, () => payTablePopup_Object.SetActive(false));

    SetButton(SoundToggle_button, ToggleSound);

    SetButton(LeftBtn, () => Slide(WrappedIndex(CurrentIndex - 1)));
    SetButton(RightBtn, () => Slide(WrappedIndex(CurrentIndex + 1)));
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
    double winAmount = socketController.ResultData?.payload?.winAmount ?? 0;
    playerCurrentWinning.text = TextFormatter.FormatMoney(winAmount);

    SetPlayerBalance(socketController.PlayerData.balance);
  }

  void ResetWinUIText()
  {
    playerCurrentWinning.text = TextFormatter.FormatMoney(0);
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

  internal void PopulateSymbolsPayout(UiData uiData)
  {
    if (uiData == null || uiData.paylines.symbols == null)
      return;

    foreach(var symbolText in SymbolsTexts)
    {
      Symbol symbol = uiData.paylines.symbols.FirstOrDefault(s => s.name == symbolText.symbolName);
      if (symbol == null || symbol.multiplier == null || symbol.multiplier.Count == 0)
      {
        symbolText.symbolText.ForEach(t => t.text = "");
        continue;
      }

      int multiplierCount = symbol.multiplier.Count;
      for (int j = 0; j < multiplierCount; j++)
      {
        double payout = symbol.multiplier[j] * socketController.InitLineBetData.bets[gameManager.betCounter];
        string payoutText = $"{payout}";
        symbolText.symbolText[j].text = payoutText;
      }
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
    {
      pageIndicators[i].sprite = i == 0 ? indicatorOn : indicatorOff;
      Button btn = pageIndicators[i].GetComponent<Button>();
      if (btn != null)
      {
        int captured = i;
        SetButton(btn, () => SnapToPage(captured));
      }
    }
  }

  private int WrappedIndex(int i)
  {
    if (i > paytableList.Length - 1) return 0;
    if (i < 0) return paytableList.Length - 1;
    return i;
  }

  private void SnapToPage(int nextIndex)
  {
    if (nextIndex == CurrentIndex) return;
    if (nextIndex < 0 || nextIndex > paytableList.Length - 1) return;

    DOTween.Kill(paytableList[CurrentIndex].transform as RectTransform);
    DOTween.Kill(paytableList[nextIndex].transform as RectTransform);

    int prevIndex = CurrentIndex;
    CurrentIndex = nextIndex;

    RectTransform prevRT = paytableList[prevIndex].transform as RectTransform;
    RectTransform nextRT = paytableList[nextIndex].transform as RectTransform;

    prevRT.anchoredPosition = new Vector2(0, prevRT.anchoredPosition.y);
    paytableList[prevIndex].SetActive(false);

    nextRT.anchoredPosition = new Vector2(0, nextRT.anchoredPosition.y);
    paytableList[nextIndex].SetActive(true);

    if (prevIndex < pageIndicators.Length) pageIndicators[prevIndex].sprite = indicatorOff;
    if (nextIndex < pageIndicators.Length) pageIndicators[nextIndex].sprite = indicatorOn;

    LeftBtn.interactable = true;
    RightBtn.interactable = true;
  }

  private void Slide(int nextIndex)
  {
    if (nextIndex == CurrentIndex) return;
    if (nextIndex < 0 || nextIndex > paytableList.Length - 1) return;

    bool inc = nextIndex > CurrentIndex;

    RectTransform current = paytableList[CurrentIndex].transform as RectTransform;
    RectTransform next = paytableList[nextIndex].transform as RectTransform;

    float incomingStartX = inc ? 2340f : -2340f;
    float outgoingEndX = inc ? -2340f : 2340f;

    next.anchoredPosition = new Vector2(incomingStartX, next.anchoredPosition.y);
    paytableList[nextIndex].SetActive(true);

    LeftBtn.interactable = false;
    RightBtn.interactable = false;

    int prevIndex = CurrentIndex;
    CurrentIndex = nextIndex;

    if (prevIndex < pageIndicators.Length) pageIndicators[prevIndex].sprite = indicatorOff;
    if (nextIndex < pageIndicators.Length) pageIndicators[nextIndex].sprite = indicatorOn;

    DOTween.Sequence()
      .Append(current.DOAnchorPosX(outgoingEndX, 0.4f).SetEase(Ease.InOutCubic))
      .Join(next.DOAnchorPosX(0, 0.4f).SetEase(Ease.InOutCubic))
      .OnComplete(() =>
      {
        paytableList[prevIndex].SetActive(false);
        current.anchoredPosition = new Vector2(0, current.anchoredPosition.y);
        LeftBtn.interactable = true;
        RightBtn.interactable = true;
      });
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
