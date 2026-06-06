using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using Best.SocketIO;
using Best.HTTP.Proxies;
using System.Threading;
using DG.Tweening;
using Coffee.UISoftMask;
using System.Linq;
public class GameManager : MonoBehaviour
{
  [Header("Scripts")]
  [SerializeField] private SlotController slotManager;
  [SerializeField] private UIManager uIManager;
  [SerializeField] private SocketController socketController;
  [SerializeField] private AudioController audioController;
  [SerializeField] internal FreeSpinController freeSpinController;

  [Header("For Spins")]
  [SerializeField] private Button SlotStart_Button;
  [SerializeField] private Button StopSpin_Button;
  [SerializeField] private Button ToatlBetMinus_Button;
  [SerializeField] private Button TotalBetPlus_Button;
  [SerializeField] private TMP_Text totalBet_text;
  [SerializeField] private TMP_Text LineBet_Text;
  [SerializeField] private bool isSpinning;
  [SerializeField] private Button TurboON_Button;
  [SerializeField] private Button TurboOFF_Button;

  [Header("For Auto Spins")]
  [SerializeField] private Button AutoSpin_Button;
  [SerializeField] private Button AutoSpinStop_Button;
  [SerializeField] private GameObject AutoSpinGlow;
  [SerializeField] internal bool isAutoSpin;
  [SerializeField] private float autoSpinDelay = 1.5f;

  [Header("For Features")]
  [SerializeField] internal ImageAnimation ThreeInARow;
  [SerializeField] internal ImageAnimation FourInARow;
  [SerializeField] internal ImageAnimation FiveInARow;

  private double currentBalance;
  [SerializeField] private double currentTotalBet;
  [SerializeField] internal int betCounter = 0;


  private Coroutine autoSpinRoutine;
  private int _autoSpinRemaining;
  private bool _autoUntilFeature;
  [SerializeField] internal bool isFreeSpin;

  private bool initiated;
  [SerializeField] internal bool turboMode;
  [SerializeField] internal bool immediateStop;
  private Coroutine spinRoutine;


  void Start()
  {
    SetButton(SlotStart_Button, () => ExecuteSpin(), true);
    // --- OLD (Age of Gods template): panel-driven, click was a no-op because
    //     AutoSpinPanelController opened a count selection panel on hover/click-toggle.
    // SetButton(AutoSpin_Button, () => { }, true);
    SetButton(AutoSpin_Button, () => StartAutoSpin(-1), true);
    SetButton(AutoSpinStop_Button, () => StartCoroutine(StopAutoSpinCoroutine()));
    SetButton(ToatlBetMinus_Button, () => { OnBetChange(false); });
    SetButton(TotalBetPlus_Button, () => { OnBetChange(true); });
    SetButton(TurboON_Button, () => { ToggleTurboMode(); });
    SetButton(TurboOFF_Button, () => { ToggleTurboMode(); });
    SetButton(StopSpin_Button, () => StartCoroutine(StopSpin()));

    if (freeSpinController != null) freeSpinController.gameManager = this;

    socketController.OnInit = InitGame;
    uIManager.ToggleAudio = audioController.SetMuteAll;
    uIManager.playButtonAudio = (s) => audioController.Play(s);
    uIManager.OnExit = () => socketController.CloseSocket();
    uIManager.OnLowBalConfirm = () => ToggleButtonGrp(true);
    socketController.ShowDisconnectionPopup = uIManager.DisconnectionPopup;
  }

  private void SetButton(Button button, Action action, bool slotButton = false)
  {
    if (button == null) return;

    button.onClick.RemoveAllListeners();
    button.onClick.AddListener(() =>
    {
      uIManager.playButtonAudio?.Invoke("button");
      action?.Invoke();
    });
  }
  void InitGame()
  {
    if (!initiated)
    {
      initiated = true;
      betCounter = 0;
      currentTotalBet = socketController.InitLineBetData.bets[betCounter] * socketController.InitLineBetData.lines.Count;
      currentBalance = socketController.PlayerData.balance;
      uIManager.UpdatePlayerInfo();
      if (totalBet_text) totalBet_text.text = TextFormatter.FormatMoney(currentTotalBet);
      LineBet_Text.text = TextFormatter.FormatMoney(socketController.InitLineBetData.bets[betCounter]);
      UpdateBetButtonsInteractable();
      if (currentBalance < currentTotalBet)
      {
        ToggleButtonGrp(false);
        uIManager.LowBalPopup();
      }

      uIManager.PopulateSymbolsPayout(socketController.InitSymbolData);
    }
    else
    {
      uIManager.PopulateSymbolsPayout(socketController.InitSymbolData);
    }
    uIManager.RefreshDiamondPayoutTexts(currentTotalBet);
    uIManager.PopulateInfoPageDiamondPayouts();
  }

  void ExecuteSpin()
  {
    // Block re-entry: during auto / free spin AutoSpinRoutine and RunFreeSpins drive SpinRoutine
    // without populating the spinRoutine field, so the null-check below would otherwise let a stray
    // click launch a parallel SpinRoutine that fights the active one for the reel tweens.
    if (isAutoSpin || isFreeSpin || isSpinning) return;

    // If the win-animation sequence is mid-flight, treat the spin click as a skip so OneSpinFlow's
    // WaitWinAnimDone gate resolves promptly and we can launch the next spin.
    if (uIManager.winAnim != null && uIManager.winAnim.IsPlaying)
      uIManager.winAnim.Skip();

    if (spinRoutine != null)
    {
      StopCoroutine(spinRoutine);
      spinRoutine = null;
      isSpinning = false;
    }
    spinRoutine = StartCoroutine(SpinRoutine());
  }

  internal void StartAutoSpin(int count)
  {
    if (isAutoSpin || isFreeSpin || isSpinning) return;
    if (uIManager.winAnim != null && uIManager.winAnim.IsPlaying)
      uIManager.winAnim.Skip();
    _autoUntilFeature = (count < 0);
    _autoSpinRemaining = count;
    isAutoSpin = true;
    // --- OLD (Age of Gods): just hid the auto-spin button via SetActive
    // AutoSpin_Button.gameObject.SetActive(false);
    SetAutoSpinUI(true);
    autoSpinRoutine = StartCoroutine(AutoSpinRoutine());
  }

  // Called from OnSpin right after the result arrives. For auto-until-feature, flipping isAutoSpin
  // off here (instead of after SpinRoutine returns) lets SpinRoutine's end-of-routine
  // `if (!isAutoSpin && !isFreeSpin) ToggleButtonGrp(true)` re-enable the bottom bar — otherwise
  // a multiplier/wild trigger ends auto-spin with buttons still disabled.
  // TODO: reimplement against payload.freeSpins / triggeredFeatures (referenced removed field iswheeltrigger)
  // void HandleAutoUntilFeatureCutoff()
  // {
  //   if (!isAutoSpin || !_autoUntilFeature) return;
  //   var p = socketController.ResultData?.payload;
  //   if (p == null || !p.iswheeltrigger) return;
  //
  //   isAutoSpin = false;
  //   SetAutoSpinUI(false);
  // }

  void ToggleTurboMode()
  {
    audioController.Play("turbo");
    turboMode = !turboMode;
    if (turboMode)
    {
      TurboON_Button.gameObject.SetActive(false);
      TurboOFF_Button.gameObject.SetActive(true);
    }
    else
    {
      TurboOFF_Button.gameObject.SetActive(false);
      TurboON_Button.gameObject.SetActive(true);
    }
  }

  IEnumerator AutoSpinRoutine()
  {
    if (isSpinning)
    {
      if (spinRoutine != null)
      {
        StopCoroutine(spinRoutine);
        spinRoutine = null;
      }
      isSpinning = false;
    }

    while (isAutoSpin)
    {
      yield return SpinRoutine();

      if (isFreeSpin)
        yield break;

      // _autoUntilFeature cutoff is handled mid-spin in HandleAutoUntilFeatureCutoff (right after the
      // result arrives), so isAutoSpin will already be false here and the while-loop exits on its own.
      if (!_autoUntilFeature)
      {
        _autoSpinRemaining--;
        if (_autoSpinRemaining <= 0) break;
      }

      yield return new WaitForSeconds(autoSpinDelay);
    }

    // CLEAN EXIT (NO coroutine calls)
    isSpinning = false;
    isAutoSpin = false;

    // --- OLD (Age of Gods): only restored the auto-spin button
    // AutoSpin_Button.gameObject.SetActive(true);
    SetAutoSpinUI(false);
    ToggleButtonGrp(true);
  }

  private IEnumerator StopAutoSpinCoroutine()
  {
    isAutoSpin = false;

    // --- OLD (Age of Gods): swapped buttons via SetActive immediately
    // AutoSpin_Button.interactable = false;
    // AutoSpin_Button.gameObject.SetActive(true);

    // Lock stop button so repeat clicks no-op while the current spin finishes.
    if (AutoSpinStop_Button) AutoSpinStop_Button.interactable = false;

    yield return new WaitUntil(() => !isSpinning);

    SetAutoSpinUI(false);

    if (!uIManager.IsLowBalPopupOpen)
      ToggleButtonGrp(true);

    if (spinRoutine != null)
    {
      StopCoroutine(spinRoutine);
      spinRoutine = null;
    }

    if (autoSpinRoutine != null)
    {
      StopCoroutine(autoSpinRoutine);
      autoSpinRoutine = null;
    }
  }
  IEnumerator SpinRoutine()
  {
    ToggleButtonGrp(false);
    bool start = OnSpinStart();

    // // ===== CASE 1: Spin did not start (low balance etc.)
    if (!start)
    {
      spinRoutine = null;
      isSpinning = false;

      if (isAutoSpin)
      {
        StartCoroutine(StopAutoSpinCoroutine());
      }

      yield break;
    }

    yield return OneSpinFlow();

    // The trigger spin's OnSpinEnd flipped isFreeSpin and faded in the FS UI. Drive the awarded
    // spins inline so retriggers (which run inside OnSpinEnd and bump spinsRemaining) extend
    // the loop transparently.
    if (isFreeSpin)
      yield return RunFreeSpinLoop();

    if (!isAutoSpin && !isFreeSpin)
    {
      ToggleButtonGrp(true);
    }
    isSpinning = false;
    spinRoutine = null;
  }

  IEnumerator RunFreeSpinLoop()
  {
    while (freeSpinController.spinsRemaining > 0)
    {
      freeSpinController.BeforeSpin();
      freeSpinController.spinsRemaining--;

      // OneSpinFlow drives a full server spin → reels → diamond/lineWins. Retrigger inside
      // OnSpinEnd will RegisterAward, bumping spinsRemaining back up — the while-loop then
      // continues for the extra spins.
      yield return OneSpinFlow();

      freeSpinController.AccumulateWin(LastSpinWinAmount());

      if (freeSpinController.spinsRemaining > 0)
        yield return freeSpinController.InterSpinDelay();
    }

    // Final spin completed without retrigger. Its lineWins already ran one-shot (spinsRemaining
    // hit 0 inside that AnimateLineWins call — see SlotController.AnimateLineWins). End panel
    // overlays the looping diamond/lineWins behind it.
    isFreeSpin = false;
    yield return freeSpinController.ShowEndPanel(t => uIManager.SetPlayerCurrentWinning(t));
    audioController.Play("bg");
  }

  bool LastSpinTriggeredFreeSpins()
  {
    var triggered = socketController.ResultData?.payload?.triggeredFeatures;
    if (triggered == null) return false;
    foreach (var t in triggered)
      if (t != null && string.Equals(t.ToString(), "FREE_SPINS", StringComparison.OrdinalIgnoreCase))
        return true;
    return false;
  }

  int LastSpinFreeSpinAward() => socketController.ResultData?.payload?.freeSpins?.awarded ?? 0;
  double LastSpinWinAmount() => socketController.ResultData?.payload?.winAmount ?? 0;
  List<string> LastSpinScatterPositions() => socketController.ResultData?.payload?.freeSpins?.scatterPositions;

  IEnumerator OneSpinFlow()
  {
    immediateStop = false;
    if (isFreeSpin) uIManager.ResetWinAnimation();
    yield return OnSpin();
    // yield return new WaitForSecondsRealtime(0.5f);
    yield return OnSpinEnd();
    // Auto / free spin loops must wait for the win-animation sequence to fully reset before the
    // next spin can kick off. Skip() (called from ExecuteSpin / StartAutoSpin / OnSpinStart) makes
    // this resolve promptly.
    if(isAutoSpin || isFreeSpin) yield return uIManager.WaitWinAnimDone();
  }

  IEnumerator StopSpin()
  {
    if (immediateStop)
      yield break;
    immediateStop = true;
    StopSpin_Button.gameObject.SetActive(false);
    StopSpin_Button.interactable = false;
    yield return new WaitUntil(() => !isSpinning);
    immediateStop = false;
    StopSpin_Button.interactable = true;
  }

  bool OnSpinStart()
  {
    uIManager.ResetWinAnimation();
    // Tear down any leftover winning-row anim from the previous spin so a click mid-presentation
    // resets the row immediately (size/loop), then fire the one-shot shine across all rows.
    uIManager.StopDiamondPayoutRowWin();
    uIManager.PlayDiamondPayoutShineOverlay();

    if (currentBalance < currentTotalBet && !isFreeSpin)
    {
      uIManager.LowBalPopup();
      return false;
    }
    isSpinning = true;
    return true;
  }

  IEnumerator OnSpin()
  {
    if (!isFreeSpin)
      uIManager.SetPlayerBalance(socketController.PlayerData.balance - currentTotalBet);

    // Stop button is usable in manual, auto, AND free-spin modes (per spec: stop must remain
    // available to interrupt auto/free chains). Explicitly re-enable interactable — the previous
    // spin's StopSpin click flow flips it false on press and back true on release, so a click
    // that races with spin teardown can leave the next spin starting with interactable=false.
    immediateStop = false;
    StopSpin_Button.gameObject.SetActive(true);
    StopSpin_Button.interactable = true;

    yield return slotManager.StartSpin();

    socketController.AccumulateResult(betCounter);
    yield return new WaitUntil(() => socketController.isResultdone);

    slotManager.PopulateSlotMatrix(socketController.ResultData.matrix);

    int waitFor = 10;
    for (int i = 0; i < waitFor; i++)
    {
      if (immediateStop && i>7)
      {
        break;
      }

      yield return new WaitForSecondsRealtime(0.1f);
    }

    yield return slotManager.StopSpin(() => audioController.Play("spin_stop"));
    immediateStop = false;

    if (StopSpin_Button.gameObject.activeSelf)
      StopSpin_Button.gameObject.SetActive(false);
  }

  IEnumerator OnSpinEnd()
  {
    // audioController.Stop("spin_stop");
    
    uIManager.UpdatePlayerInfo();

    // Free-spin trigger / retrigger gate: must run BEFORE diamond / lineWins presentation so the
    // user sees the centered triggered animation + start panel before any wins resolve. If this
    // is the initial trigger (isFreeSpin currently false), we also fade in the FS background/UI
    // here, on the entry spin only.
    if (LastSpinTriggeredFreeSpins())
    {
      // Auto-spin must turn off the moment FS triggers (per spec).
      if (isAutoSpin)
      {
        isAutoSpin = false;
        SetAutoSpinUI(false);
        if (autoSpinRoutine != null) { StopCoroutine(autoSpinRoutine); autoSpinRoutine = null; }
      }

      int awarded = LastSpinFreeSpinAward();
      bool isEntry = !isFreeSpin;
      if (isEntry) freeSpinController.BeginSession();

      // Centered triggered sequence: third play coincides with the Start panel fade-in.
      Coroutine startPanelIn = null;
      yield return slotManager.PlayFreeSpinTriggeredSequence(
        LastSpinScatterPositions(),
        onThirdPlayStart: () =>
        {
          startPanelIn = StartCoroutine(freeSpinController.PlayStartPanelIn(awarded));
        }
      );
      if (startPanelIn != null) yield return startPanelIn;
      yield return freeSpinController.WaitStartPanelOk();

      freeSpinController.RegisterAward(awarded);

      if (isEntry)
      {
        isFreeSpin = true;
        yield return freeSpinController.FadeInFreeSpinUi();
        audioController.Play("FP");
      }
    }

    if (socketController.ResultData.payload.winAmount > 0)
      uIManager.TriggerWinAnimation(socketController.ResultData.payload.winAmount, currentTotalBet);

    // Diamond feature: server always sends diamondCount + diamondPositions (even for the
    // single-diamond idle case).
    var feats = socketController.ResultData.payload.featureWins;
    bool diamondTrigger = feats != null && feats.diamondPositions != null && feats.diamondCount >= 2;
    bool diamondIdle = feats != null && feats.diamondPositions != null && feats.diamondCount == 1;

    if (diamondTrigger) uIManager.PlayDiamondPayoutRowWin(feats.diamondCount);

    // Matrix diamond animation:
    //   - auto-spin: single non-looped cycle yielded in parallel with the line-wins synced pass;
    //     the icons reset themselves on completion. If the user stops auto mid-cycle, we re-arm
    //     the looping animation below so the trigger stays visible.
    //   - manual / free: existing forever-loop, torn down on next StartSpin's StopIconAnimation.
    Coroutine diamondCycle = null;
    if (diamondTrigger)
    {
      if (isAutoSpin) diamondCycle = StartCoroutine(slotManager.PlayDiamondTriggeredCycle(feats.diamondPositions));
      else slotManager.StartDiamondTriggered(feats.diamondPositions);
    }
    else if (diamondIdle
             && SlotController.TryParseDiamondPos(feats.diamondPositions[0], out int idleRow, out int idleCol))
    {
      StartCoroutine(slotManager.PlayDiamondIdle(idleRow, idleCol));
    }

    if (socketController.ResultData.payload.lineWins.Count > 0)
    {
      // "win" SFX now fires with the win-line animations (after the scatter animations), inside
      // SlotController's win presentation.
      yield return slotManager.AnimateLineWins(socketController.ResultData.payload.lineWins);
    }

    if (diamondCycle != null) yield return diamondCycle;

    // Auto-spin was stopped during the parallel cycles: re-arm the looping diamond animation so
    // the matrix matches what a manual-spin trigger would have shown.
    if (diamondTrigger && !isAutoSpin && !isFreeSpin)
      slotManager.StartDiamondTriggered(feats.diamondPositions);

    // bool autoContinued = isFreeSpin || isAutoSpin || LastSpinWasWildTrigger() || LastSpinWasFreeSpinTrigger();
    // if (autoContinued)
    //   yield return WaitWinAnimOrSkip();
  }

  IEnumerator TriggerFeature(int count, List<Vector2Int> streak)
  {
    ResetInARowAnimations(animate: false);
    ImageAnimation feature = null;

    if (count == 3) feature = ThreeInARow;
    else if (count == 4) feature = FourInARow;
    else if (count == 5) feature = FiveInARow;

    if (feature == null)
    {
      Debug.LogError("Invalid feature count: " + count);
      yield break;
    }

    Vector3 finalPosition;

    if (count % 2 == 1) // 3 or 5
    {
      // Exact center
      Vector2Int centerPos = streak[count / 2];
      finalPosition = GetSlotTransform(centerPos.x, centerPos.y).position;
    }
    else // 4 in a row
    {
      // Between middle two slots
      Vector2Int leftCenter = streak[(count / 2) - 1];
      Vector2Int rightCenter = streak[count / 2];

      Transform leftT = GetSlotTransform(leftCenter.x, leftCenter.y);
      Transform rightT = GetSlotTransform(rightCenter.x, rightCenter.y);

      finalPosition = (leftT.position + rightT.position) / 2f;
    }
    audioController.Play("inarow");
    feature.transform.position = finalPosition;
    feature.gameObject.SetActive(true);
    feature.StartAnimation();
    yield return feature.rendererDelegate.DOFade(1, 0.5f).WaitForCompletion();
    yield return new WaitForSeconds(1.8f);
  }

  Transform GetSlotTransform(int row, int col)
  {
    return slotManager.slotMatrix[col].slotImages[row].transform;
  }

  void SetAutoSpinUI(bool autoActive)
  {
    if (AutoSpin_Button) AutoSpin_Button.gameObject.SetActive(!autoActive);
    if (AutoSpinStop_Button)
    {
      AutoSpinStop_Button.gameObject.SetActive(autoActive);
      AutoSpinStop_Button.interactable = autoActive;
    }
    if (AutoSpinGlow) AutoSpinGlow.SetActive(autoActive);
  }

  internal void ToggleButtonGrp(bool toggle)
  {
    if (SlotStart_Button) SlotStart_Button.interactable = toggle;
    if (AutoSpin_Button) AutoSpin_Button.interactable = toggle;
    if (toggle)
    {
      UpdateBetButtonsInteractable();
    }
    else
    {
      if (ToatlBetMinus_Button) ToatlBetMinus_Button.interactable = false;
      if (TotalBetPlus_Button) TotalBetPlus_Button.interactable = false;
    }
    uIManager.paytable_Button.interactable = toggle;
    TurboON_Button.interactable = toggle;
    TurboOFF_Button.interactable = toggle;
  }

  private void OnBetChange(bool inc)
  {
    if (audioController) audioController.Play("bet_change");

    int lastIndex = socketController.InitLineBetData.bets.Count - 1;
    if (inc)
    {
      betCounter = (betCounter >= lastIndex) ? 0 : betCounter + 1;
    }
    else
    {
      betCounter = (betCounter <= 0) ? lastIndex : betCounter - 1;
    }

    currentTotalBet = socketController.InitLineBetData.bets[betCounter] * socketController.InitLineBetData.lines.Count;
    if (totalBet_text) totalBet_text.text = TextFormatter.FormatMoney(currentTotalBet);
    LineBet_Text.text = TextFormatter.FormatMoney(socketController.InitLineBetData.bets[betCounter]);
    UpdateBetButtonsInteractable();
    uIManager.PopulateSymbolsPayout(socketController.InitSymbolData);
    uIManager.RefreshDiamondPayoutTexts(currentTotalBet);
  }

  void UpdateBetButtonsInteractable()
  {
    if (socketController == null || socketController.InitLineBetData == null || socketController.InitLineBetData.bets == null) return;
    if (ToatlBetMinus_Button) ToatlBetMinus_Button.interactable = true;
    if (TotalBetPlus_Button) TotalBetPlus_Button.interactable = true;
  }

  void ResetInARowAnimations(bool animate = true)
  {
    if (animate)
    {
      ThreeInARow.rendererDelegate.DOFade(0, 0.5f).OnComplete(() =>
      {
        ThreeInARow.StopAnimation();
        ThreeInARow.gameObject.SetActive(false);
      });

      FourInARow.rendererDelegate.DOFade(0, 0.5f).OnComplete(() =>
      {
        FourInARow.StopAnimation();
        FourInARow.gameObject.SetActive(false);
      });

      FiveInARow.rendererDelegate.DOFade(0, 0.5f).OnComplete(() =>
      {
        FiveInARow.StopAnimation();
        FiveInARow.gameObject.SetActive(false);
      });
    }
    else
    {
      ThreeInARow.StopAnimation();
      ThreeInARow.gameObject.SetActive(false);
      FourInARow.StopAnimation();
      FourInARow.gameObject.SetActive(false);
      FiveInARow.StopAnimation();
      FiveInARow.gameObject.SetActive(false);
    }
  }
}
