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
public class GameManager : MonoBehaviour
{
  [Header("Scripts")]
  [SerializeField] private SlotController slotManager;
  [SerializeField] private UIManager uIManager;
  [SerializeField] private SocketController socketController;
  [SerializeField] private AudioController audioController;
  [SerializeField] private FreeSpinController freeSpinController;

  [Header("For Spins")]
  [SerializeField] private Button SlotStart_Button;
  [SerializeField] private Button StopSpin_Button;
  [SerializeField] private Button ToatlBetMinus_Button;
  [SerializeField] private Button TotalBetPlus_Button;
  [SerializeField] private TMP_Text totalBet_text;
  [SerializeField] private bool isSpinning;
  [SerializeField] private Button TurboON_Button;
  [SerializeField] private Button TurboOFF_Button;

  [Header("For Auto Spins")]
  [SerializeField] private Button AutoSpin_Button;
  [SerializeField] private Button AutoSpinStop_Button;
  [SerializeField] internal static bool isAutoSpin;

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
  [SerializeField] internal static bool isFreeSpin;

  private bool initiated;
  static internal bool turboMode;
  static internal bool immediateStop;
  private Coroutine spinRoutine;
  private bool autoSkipArmed;
  private bool autoSkipWinRequested;


  void Start()
  {
    SetButton(SlotStart_Button, () =>
    {
      if (autoSkipArmed)
      {
        autoSkipWinRequested = true;
        autoSkipArmed = false;
        SlotStart_Button.interactable = false;
      }
      else
      {
        ExecuteSpin();
      }
    }, true);
    // AutoSpin button does nothing on click in GameManager — the AutoSpinPanelController handles hover (desktop) and click-toggle (mobile/tablet) to drive the panel. Audio still plays via SetButton wrapper.
    SetButton(AutoSpin_Button, () => { }, true);
    SetButton(AutoSpinStop_Button, () => StartCoroutine(StopAutoSpinCoroutine()));
    SetButton(ToatlBetMinus_Button, () => { OnBetChange(false); });
    SetButton(TotalBetPlus_Button, () => { OnBetChange(true); });
    SetButton(TurboON_Button, () => { ToggleTurboMode(); });
    SetButton(TurboOFF_Button, () => { ToggleTurboMode(); });
    SetButton(StopSpin_Button, () => StartCoroutine(StopSpin()));


    slotManager.ShuffleMatrix();
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
  }

  void ExecuteSpin()
  {
    // Block re-entry: during auto / free spin AutoSpinRoutine and RunFreeSpins drive SpinRoutine
    // without populating the spinRoutine field, so the null-check below would otherwise let a stray
    // click launch a parallel SpinRoutine that fights the active one for the reel tweens.
    if (isAutoSpin || isFreeSpin || isSpinning) return;

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
    _autoUntilFeature = (count < 0);
    _autoSpinRemaining = count;
    isAutoSpin = true;
    AutoSpin_Button.gameObject.SetActive(false);
    autoSpinRoutine = StartCoroutine(AutoSpinRoutine());
  }

  // Called from OnSpin right after the result arrives. For auto-until-feature, flipping isAutoSpin
  // off here (instead of after SpinRoutine returns) lets SpinRoutine's end-of-routine
  // `if (!isAutoSpin && !isFreeSpin) ToggleButtonGrp(true)` re-enable the bottom bar — otherwise
  // a multiplier/wild trigger ends auto-spin with buttons still disabled.
  void HandleAutoUntilFeatureCutoff()
  {
    if (!isAutoSpin || !_autoUntilFeature) return;
    var p = socketController.ResultData?.payload;
    if (p == null || !p.iswheeltrigger) return;

    isAutoSpin = false;
    if (AutoSpin_Button != null) AutoSpin_Button.gameObject.SetActive(true);
  }

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

      yield return new WaitForSeconds(0.5f);
    }

    // CLEAN EXIT (NO coroutine calls)
    isSpinning = false;
    isAutoSpin = false;

    AutoSpin_Button.gameObject.SetActive(true);
    ToggleButtonGrp(true);
  }

  private IEnumerator StopAutoSpinCoroutine()
  {
    isAutoSpin = false;
    AutoSpin_Button.interactable = false;
    AutoSpin_Button.gameObject.SetActive(true);
    yield return new WaitUntil(() => !isSpinning);

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

    // ===== CASE 1: Spin did not start (low balance etc.)
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

    // Chain wild-triggered spins; each chained result can itself trigger another wild
    // (wildFeaturePending > 0) or land a free-spin trigger, so re-check after every spin.
    while (true)
    {
      if (!isFreeSpin && LastSpinWasFreeSpinTrigger())
      {
        int awarded = LastSpinFreeSpinAward();
        isFreeSpin = true;
        isAutoSpin = false;
        if (autoSpinRoutine != null) { StopCoroutine(autoSpinRoutine); autoSpinRoutine = null; }
        AutoSpin_Button.gameObject.SetActive(true);

        audioController.Play("FP");

        yield return freeSpinController.RunFreeSpins(
          awarded,
          OneSpinFlow,
          LastSpinWinAmount,
          LastSpinWasFreeSpinTrigger,
          LastSpinFreeSpinAward,
          uIManager.SetPlayerCurrentWinning,
          OnFreeSpinsComplete
        );
        break;
      }

      if (!isAutoSpin && !isFreeSpin && LastSpinWasWildTrigger())
      {
        if (!OnSpinStart()) break;
        yield return OneSpinFlow();
        continue;
      }

      break;
    }

    if (!isAutoSpin && !isFreeSpin)
    {
      ToggleButtonGrp(true);
    }
    isSpinning = false;
    spinRoutine = null;
  }

  IEnumerator OneSpinFlow()
  {
    immediateStop = false;
    if (isFreeSpin) uIManager.ResetWinAnimation();
    yield return OnSpin();
    yield return OnSpinEnd();
  }

  internal bool LastSpinWasFreeSpinTrigger()
  {
    var p = socketController.ResultData?.payload;
    if (p == null) return false;
    if (!p.iswheeltrigger) return false;
    return p.wheelBonus != null && string.Equals(p.wheelBonus.featureType, "freeSpin", System.StringComparison.OrdinalIgnoreCase);
  }

  internal bool LastSpinWasWildTrigger()
  {
    var p = socketController.ResultData?.payload;
    if (p == null) return false;
    if (!p.iswheeltrigger) return false;
    if (p.wheelBonus == null) return false;
    if (!string.Equals(p.wheelBonus.featureType, "wild", System.StringComparison.OrdinalIgnoreCase)) return false;
    return p.wildFeaturePending > 0;
  }

  int LastSpinFreeSpinAward()
  {
    return socketController.ResultData?.payload?.wheelBonus?.featureValue ?? 0;
  }

  double LastSpinWinAmount()
  {
    return socketController.ResultData?.payload?.winAmount ?? 0;
  }

  void OnFreeSpinsComplete()
  {
    isFreeSpin = false;
    audioController.Play("bg");
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

    if (!isFreeSpin && !LastSpinWasWildTrigger())
      StopSpin_Button.gameObject.SetActive(true);
    yield return slotManager.StartSpin();

    socketController.AccumulateResult(betCounter);
    yield return new WaitUntil(() => socketController.isResultdone);

    HandleAutoUntilFeatureCutoff();

    slotManager.PopulateSlotMatrix(socketController.ResultData.matrix, socketController.ResultData.payload.goldenPositions);

    var wildPositions = socketController.ResultData.payload.wildPositions;
    bool hasWild = wildPositions != null && wildPositions.Count > 0;
    if (hasWild)
    {
      if (StopSpin_Button.gameObject.activeSelf)
        StopSpin_Button.gameObject.SetActive(false);
      if (isFreeSpin && freeSpinController != null)
        freeSpinController.SetButtonsInteractable(false, false);


      if (!isFreeSpin)
        StopSpin_Button.gameObject.SetActive(true);
    }

    int waitFor = 4;
    for (int i = 0; i < waitFor; i++)
    {
      if (immediateStop)
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
    yield return null;
    // audioController.Stop("spin_stop");

    // if (socketController.ResultData.payload.goldenPositions.Count > 0)
    // {
    //   if (isFreeSpin && freeSpinController != null)
    //     freeSpinController.SetButtonsInteractable(false, false);

    //   if (socketController.ResultData.payload.iswheeltrigger)
    //   {
    //     var gs = socketController.ResultData.payload.wheelBonus.goldenSymbols;
    //     if (gs != null && gs.count >= 3)
    //     {
    //       var streak = gs.positions.ConvertAll(col => new Vector2Int(gs.row, col));
    //       yield return new WaitForSecondsRealtime(1f);
    //       yield return TriggerFeature(gs.count, streak);
    //     }
    //     else
    //     {
    //       Debug.LogError("Invalid golden symbols data from wheel bonus");
    //       yield break;
    //     }

    //     yield return wheelController.PlayWheel(socketController.ResultData.payload.wheelBonus);

    //     ResetInARowAnimations();
    //     yield return new WaitForSecondsRealtime(0.5f);
    //     yield return slotManager.HideGoldIcons();
    //   }
    //   else
    //   {
    //     yield return slotManager.HideGoldIcons();
    //   }
    // }

    // slotManager.ResetWildFeatureIcons();

    // uIManager.UpdatePlayerInfo();

    // if (socketController.ResultData.payload.winAmount > 0)
    // {
      
    //   uIManager.TriggerWinAnimation(socketController.ResultData.payload.winAmount, currentTotalBet);
    // }

    // if (socketController.ResultData.payload.lineWins.Count > 0)
    // {
    //   audioController.Play("win");
    //   if (isFreeSpin && freeSpinController != null)
    //     freeSpinController.SetButtonsInteractable(false, true);
    //   yield return slotManager.AnimateLineWins(socketController.ResultData.payload.lineWins);
    // }

    // bool autoContinued = isFreeSpin || isAutoSpin || LastSpinWasWildTrigger() || LastSpinWasFreeSpinTrigger();
    // if (autoContinued)
    //   yield return WaitWinAnimOrSkip();
  }

  IEnumerator WaitWinAnimOrSkip()
  {
    if (!uIManager.isWinAnimating)
    {
      // Skip click may have carried over from line-wins SkippableWait; consume so it doesn't leak into next phase.
      CheckWinSkip();
      yield break;
    }

    if (CheckWinSkip())
    {
      uIManager.ResetWinAnimation();
      yield break;
    }

    if (isFreeSpin && freeSpinController != null)
      freeSpinController.ArmSkipButton();
    else
      ArmAutoSkip();

    while (uIManager.isWinAnimating)
    {
      if (CheckWinSkip())
      {
        uIManager.ResetWinAnimation();
        break;
      }
      yield return null;
    }

    DisarmWinSkip();
  }

  bool CheckWinSkip()
  {
    if (isFreeSpin && freeSpinController != null && freeSpinController.ConsumeSkipRequest()) return true;
    if (!isFreeSpin && autoSkipWinRequested) { autoSkipWinRequested = false; return true; }
    return false;
  }

  void ArmAutoSkip()
  {
    autoSkipArmed = true;
    autoSkipWinRequested = false;
    if (SlotStart_Button) SlotStart_Button.interactable = true;
  }

  void DisarmWinSkip()
  {
    autoSkipArmed = false;
    if (isFreeSpin && freeSpinController != null) freeSpinController.DisarmSkipButton();
    // Wild-auto path: ExecuteSpin disabled SlotStart_Button via ToggleButtonGrp(false); re-disable after the gate so the user can't accidentally interrupt the auto-continued second wild spin.
    if (!isFreeSpin && !isAutoSpin && SlotStart_Button) SlotStart_Button.interactable = false;
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
      if (betCounter < lastIndex) betCounter++;
    }
    else
    {
      if (betCounter > 0) betCounter--;
    }

    currentTotalBet = socketController.InitLineBetData.bets[betCounter] * socketController.InitLineBetData.lines.Count;
    if (totalBet_text) totalBet_text.text = TextFormatter.FormatMoney(currentTotalBet);
    UpdateBetButtonsInteractable();
    uIManager.PopulateSymbolsPayout(socketController.InitSymbolData);
  }

  void UpdateBetButtonsInteractable()
  {
    if (socketController == null || socketController.InitLineBetData == null || socketController.InitLineBetData.bets == null) return;
    int lastIndex = socketController.InitLineBetData.bets.Count - 1;
    if (ToatlBetMinus_Button) ToatlBetMinus_Button.interactable = betCounter > 0;
    if (TotalBetPlus_Button) TotalBetPlus_Button.interactable = betCounter < lastIndex;
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
