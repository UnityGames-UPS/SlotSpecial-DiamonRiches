using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using System.Linq;
using System;
using TMPro;
using Newtonsoft.Json;

public class SlotController : MonoBehaviour
{
  [SerializeField] internal AudioController audioController;
  [SerializeField] internal GameManager gameManager;

  [Header("Sprites")]
  [SerializeField] private Sprite[] iconImages;
  [SerializeField] private Sprite[] goldIconImages;

  [Header("Special Win Animations")]
  [SerializeField] private List<Sprite> wildBgAnimationSprites;
  [SerializeField] private List<Sprite> wildIconAnimationSprites;
  [SerializeField] private List<Sprite> ladyIconAnimationSprites;
  [SerializeField] private float wildBgAnimationSpeed = 5f;
  [SerializeField] private float wildIconAnimationSpeed = 5f;
  [SerializeField] private float ladyIconAnimationSpeed = 5f;

  internal List<Sprite> GetWildBgSprites() => wildBgAnimationSprites;
  internal List<Sprite> GetWildIconSprites() => wildIconAnimationSprites;
  internal List<Sprite> GetLadyIconSprites() => ladyIconAnimationSprites;
  internal float GetWildBgSpeed() => wildBgAnimationSpeed;
  internal float GetWildIconSpeed() => wildIconAnimationSpeed;
  internal float GetLadyIconSpeed() => ladyIconAnimationSpeed;

  [Header("Slot Images")]
  [SerializeField] internal List<SlotImage> slotMatrix;
  [SerializeField] private List<SlotImage> allMatrix;

  [Header("Animation Overlay")]
  [SerializeField] private RectTransform animationOverlayParent;
  // Full-slot black (alpha ~200) overlay. Enabled only for the looping win pass; animating
  // symbols are lifted to animationOverlayParent (above this) so they stay lit while the rest darken.
  [SerializeField] private GameObject darkOverlay;

  // Wild symbol id — when it is the middle symbol of a line we suppress that line's payout label.
  private const int WildId = 5;

  [Header("Slots Transforms")]
  [SerializeField] private RectTransform[] Slot_Transform;

  private List<Tweener> alltweens = new List<Tweener>();
  private Tweener WinTween = null;
  [SerializeField] private List<SlotIconView> animatingIcons = new List<SlotIconView>();
  [SerializeField] private float wildPerColumnDelay = 2f;
  [SerializeField] private float betweenLineDelay = 0.25f;
  private Coroutine WinLoopCorutine = null;
  private Coroutine scatterChainCoroutine = null;

  [Header("Symbol Win Animations")]
  [SerializeField] private List<SymbolWinAnim> symbolWinAnims = new List<SymbolWinAnim>();
  private Dictionary<int, SymbolWinAnim> _winAnimById;

  internal SymbolWinAnim GetWinAnim(int id)
    => (_winAnimById != null && _winAnimById.TryGetValue(id, out var a)) ? a : null;

  [Header("Free Spin Symbol Animation")]
  [SerializeField] private List<Sprite> freeSpinSymbolAnimSprites = new List<Sprite>();
  [SerializeField] private float freeSpinSymbolAnimSpeed = 5f;

  // Free-spin (scatter) symbol id. It does not contribute to win lines; it is animated on reel
  // stop, column by column. Free spins trigger when each of the first FreeSpinTriggerColumns
  // columns holds at least one of these symbols.
  private const int FreeSpinSymbolId = 6;
  private const int FreeSpinTriggerColumns = 3;

  // Set true by each reel's landing tween OnComplete so the scatter chain can fire a column's
  // animation the instant that reel lands (while reels to the right are still spinning).
  private bool[] _reelLanded;

  void Awake()
  {
    ShuffleMatrix();

    _winAnimById = new Dictionary<int, SymbolWinAnim>();
    foreach (var a in symbolWinAnims)
      if (a != null && !_winAnimById.ContainsKey(a.id)) _winAnimById[a.id] = a;
  }

  internal IEnumerator StartSpin()
  {
    StopWinLoop();
    SetDarkOverlay(false);
    KillAllTweens();
    ResetAllIcons();
    StopIconAnimation();

    List<Tween> initTweens = new();
    audioController.Play("spin_start");
    for (int i = 0; i < Slot_Transform.Length; i++)
    {
      initTweens.Add(InitializeTweening(Slot_Transform[i]));
    }
    yield return initTweens[0].WaitForCompletion();
    yield return initTweens[^1].WaitForCompletion();
  }

  internal void PopulateSlotMatrix(List<List<string>> resultData)
  {
    for (int i = 0; i < resultData.Count; i++)
    {
      for (int j = 0; j < resultData[i].Count; j++)
      {
        int id = int.Parse(resultData[i][j]);
        slotMatrix[j].slotImages[i].SetIcon(ID: id, image: iconImages[id]); // matrix is [row][col]; slotMatrix is column-major
      }
    }

    // --- Gold-position feature (Age of Gods) — commented out for later review.
    //     New Diamond Riches result has no goldenPositions in the payload.
    // if (goldPositions.Count > 0)
    // {
    //   if (goldPositions.Count > 1)
    //   {
    //     audioController.Play("thunder");
    //   }
    //   for (int i = 0; i < goldPositions.Count; i++)
    //   {
    //     int id = goldPositions[i].symbolId;
    //     for (int j = 0; j < goldPositions[i].positions.Count; j++)
    //     {
    //       int col = goldPositions[i].positions[j][1];
    //       int row = goldPositions[i].positions[j][0];
    //       slotMatrix[col].slotImages[row].SetGoldIcon(goldIconImages[id]);
    //     }
    //   }
    //   ToggleDarkFG(true, false);
    // }
  }

  // internal IEnumerator HideGoldIcons()
  // {
  //   for (int i = 0; i < slotMatrix.Count; i++)
  //   {
  //     for (int j = 0; j < slotMatrix[i].slotImages.Count; j++)
  //     {
  //       if (slotMatrix[i].slotImages[j].isGold)
  //         slotMatrix[i].slotImages[j].AnimateGoldIcon();
  //     }
  //   }
  //
  //   ToggleDarkFG(false, false);
  //   yield return new WaitForSecondsRealtime(1f);
  // }

  internal IEnumerator StopSpin(Action playFallAudio)
  {
    if (_reelLanded == null || _reelLanded.Length != Slot_Transform.Length)
      _reelLanded = new bool[Slot_Transform.Length];
    for (int i = 0; i < _reelLanded.Length; i++) _reelLanded[i] = false;

    // Runs alongside the reel-stop loop: animates each scatter the moment its reel lands.
    // Not awaited here — the win presentation waits on it instead, so the buttons can re-enable and
    // a spin click can interrupt it (StartSpin -> StopWinLoop stops this coroutine).
    scatterChainCoroutine = StartCoroutine(FreeSpinSymbolChain());

    for (int i = 0; i < Slot_Transform.Length; i++)
    {
      StopTweening(Slot_Transform[i], i);
      int landed = i;
      alltweens[i].OnComplete(() => { if (landed < _reelLanded.Length) _reelLanded[landed] = true; });

      if (!gameManager.immediateStop)
      {
        playFallAudio?.Invoke();
        // Interruptible inter-reel delay: the moment Stop is pressed (immediateStop -> true)
        // bail out of the wait so the remaining reels are stopped on the same frame.
        float wait = gameManager.turboMode ? 0.2f : 0.6f;
        float elapsed = 0f;
        while (elapsed < wait && !gameManager.immediateStop)
        {
          elapsed += Time.unscaledDeltaTime;
          yield return null;
        }
      }
    }

    yield return alltweens[^1].WaitForCompletion();
    KillAllTweens();
  }

  // Walks the first FreeSpinTriggerColumns columns left to right. For each, waits until that
  // reel has landed, then plays the scatter (id 6) animation if the column holds one. The chain
  // breaks the moment a column has no scatter — so a scatter that lands in a later column without
  // scatters in every preceding column is never animated (no free-spin trigger is possible).
  IEnumerator FreeSpinSymbolChain()
  {
    for (int col = 0; col < FreeSpinTriggerColumns && col < slotMatrix.Count; col++)
    {
      yield return new WaitUntil(() => _reelLanded != null && col < _reelLanded.Length && _reelLanded[col]);

      var scatter = GetColumnFreeSpinIcon(col);
      if (scatter == null)
        yield break; // chain broken: this column's last-row symbol isn't a scatter

      yield return scatter.PlayFreeSpinAnim(freeSpinSymbolAnimSprites, freeSpinSymbolAnimSpeed);
    }
  }

  // If a column holds the free-spin (id 6) symbol in more than one row, only the lowest one
  // (highest row index) animates, so at most one free-spin animation plays per column.
  // Returns null when the column has no free-spin symbol.
  SlotIconView GetColumnFreeSpinIcon(int col)
  {
    if (col < 0 || col >= slotMatrix.Count) return null;
    var images = slotMatrix[col].slotImages;
    if (images == null) return null;
    SlotIconView last = null;
    for (int row = 0; row < images.Count; row++)
      if (images[row] != null && images[row].id == FreeSpinSymbolId) last = images[row];
    return last;
  }

  internal void ShuffleMatrix(bool ignoreResultMatrix = false)
  {
    HashSet<SlotIconView>[] resultSets = null;
    if (ignoreResultMatrix)
    {
      resultSets = new HashSet<SlotIconView>[slotMatrix.Count];
      for (int i = 0; i < slotMatrix.Count; i++)
        resultSets[i] = new HashSet<SlotIconView>(slotMatrix[i].slotImages);
    }

    for (int i = 0; i < allMatrix.Count; i++)
    {
      for (int j = 0; j < allMatrix[i].slotImages.Count; j++)
      {
        if (ignoreResultMatrix && i < resultSets.Length && resultSets[i].Contains(allMatrix[i].slotImages[j]))
          continue;
        int randomIndex = UnityEngine.Random.Range(0, iconImages.Length - 1);
        allMatrix[i].slotImages[j].SetIcon(ID: randomIndex, image: iconImages[randomIndex]);
      }
    }
  }

  void StopWinLoop()
  {
    if (WinLoopCorutine != null)
    {
      StopCoroutine(WinLoopCorutine);
      WinLoopCorutine = null;
    }
    if (scatterChainCoroutine != null)
    {
      StopCoroutine(scatterChainCoroutine);
      scatterChainCoroutine = null;
    }
  }

  internal void StopIconAnimation()
  {
    foreach (var item in animatingIcons)
    {
      item.StopAnim();
    }
    animatingIcons.Clear();
  }

  internal void ResetAllIcons()
  {
    foreach (var item in allMatrix)
    {
      foreach (var item1 in item.slotImages)
      {
        item1.Reset();
      }
    }
  }

  // Reel travel speed in local units/second. Durations are derived from this so
  // every move (intro, loop, stop) runs at a constant speed regardless of distance.
  [SerializeField] private float reelSpeed = 2857f;

  private const float SpinTopY = 1900f;
  private const float SpinBottomY = -1900f;
  private const float RestY = 145.402496f;

  // Duration needed to travel between two Y positions at reelSpeed.
  private float DurationFor(float fromY, float toY)
    => Mathf.Abs(toY - fromY) / Mathf.Max(reelSpeed, 0.0001f);

  #region TweeningCode
  private Tween InitializeTweening(Transform slotTransform)
  {
    Sequence seq = DOTween.Sequence();
    float startY = slotTransform.localPosition.y;
    seq.Append(slotTransform.DOLocalMoveY(SpinBottomY, DurationFor(startY, SpinBottomY)).SetEase(Ease.Linear));
    seq.AppendCallback(() =>
    {
      slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, SpinTopY);
      // ShuffleMatrix(ignoreResultMatrix: true);
      Tweener tweener = slotTransform.DOLocalMoveY(SpinBottomY, DurationFor(SpinTopY, SpinBottomY))
        .SetLoops(-1, LoopType.Restart)
        .SetEase(Ease.Linear);
      alltweens.Add(tweener);
    });
    return seq;
  }

  private void StopTweening(Transform slotTransform, int index)
  {
    alltweens[index].Kill();
    slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, SpinTopY);
    alltweens[index] = slotTransform.DOLocalMoveY(RestY, DurationFor(SpinTopY, RestY)).SetEase(Ease.Linear);
  }

  private void KillAllTweens()
  {
    for (int i = 0; i < alltweens.Count; i++)
    {
      alltweens[i].Kill();
    }
    alltweens.Clear();
  }
  #endregion

  // Parses a "row,col" position string from the server lineWins payload.
  static bool TryParsePosition(string pos, out int row, out int col)
  {
    row = col = 0;
    var parts = pos?.Split(',');
    if (parts == null || parts.Length != 2) return false;
    return int.TryParse(parts[0], out row) && int.TryParse(parts[1], out col);
  }

  // Yields until the scatter (free-spin symbol) animation chain started in StopSpin has finished,
  // so the win-line presentation always runs after the scatter animations.
  internal IEnumerator WaitForScatterChain()
  {
    if (scatterChainCoroutine != null) yield return scatterChainCoroutine;
  }

  internal IEnumerator AnimateLineWins(List<LineWin> lineWins)
  {
    bool autoContinued = gameManager.isAutoSpin || gameManager.isFreeSpin;

    if (autoContinued)
    {
      // Auto/free spin chains the next spin, so block on the synced pass to keep the win visible,
      // then stop (no infinite loop). Wait for the scatter animations first.
      yield return WaitForScatterChain();
      audioController.Play("win");
      yield return PlaySyncedPass(lineWins, showPayouts: lineWins.Count == 1);
      foreach (var lineWin in lineWins) StopAnimateLineWin(lineWin);
      yield break;
    }

    // Manual spin: run the whole presentation (synced pass -> loop) in the background so the caller
    // returns immediately and the bottom-bar buttons are re-enabled while the synced pass is still
    // playing. The user can skip / start the next spin at any point; the next StartSpin kills this
    // via StopWinLoop().
    WinLoopCorutine = StartCoroutine(WinPresentation(lineWins));
    yield break;
  }

  // Manual-spin win presentation: wait for the scatter animations, then the synced first pass
  // (no darkening), then an indefinite loop.
  IEnumerator WinPresentation(List<LineWin> lineWins)
  {
    yield return WaitForScatterChain();

    bool singleLine = lineWins.Count == 1;

    audioController.Play("win");

    // Synchronized first pass shows every winning line with no darkening. Payouts shown only
    // when there is exactly one winning line.
    yield return PlaySyncedPass(lineWins, showPayouts: singleLine);

    if (singleLine)
    {
      // Icons stay on; loop the per-iteration animations indefinitely with the line payout still visible.
      yield return SingleLineLoop(lineWins[0]);
    }
    else
    {
      // Darken the whole slot for the multi-line looping pass; animating symbols lift above the overlay.
      SetDarkOverlay(true);
      foreach (var lineWin in lineWins) StopAnimateLineWin(lineWin);
      yield return PerLineLoop(lineWins);
    }
  }

  IEnumerator PlaySyncedPass(List<LineWin> lineWins, bool showPayouts)
  {
    // Payout label sits on the middle symbol of each winning line.
    var midPerLine = new Dictionary<(int col, int row), double>();
    if (showPayouts)
    {
      foreach (var lineWin in lineWins)
      {
        int c = lineWin.positions.Count;
        if (c == 0) continue;
        if (!TryParsePosition(lineWin.positions[c / 2], out int midRow, out int midCol)) continue;
        if (midCol < 0 || midCol >= slotMatrix.Count) continue;
        if (midRow < 0 || midRow >= slotMatrix[midCol].slotImages.Count) continue;
        // Skip the payout label when the middle symbol is a wild.
        if (slotMatrix[midCol].slotImages[midRow].id == WildId) continue;
        midPerLine[(midCol, midRow)] = lineWin.payout;
      }
    }

    var seen = new HashSet<(int, int)>();
    var running = new List<Coroutine>();
    foreach (var lineWin in lineWins)
    {
      for (int i = 0; i < lineWin.positions.Count; i++)
      {
        if (!TryParsePosition(lineWin.positions[i], out int row, out int col)) continue;
        if (col < 0 || col >= slotMatrix.Count) continue;
        if (row < 0 || row >= slotMatrix[col].slotImages.Count) continue;
        if (!seen.Add((col, row))) continue;

        bool show = midPerLine.TryGetValue((col, row), out double payout);
        running.Add(StartCoroutine(slotMatrix[col].slotImages[row].PlayWinIteration(this, animationOverlayParent, show, show ? payout : 0, isSyncedPass: true)));
      }
    }
    foreach (var co in running)
      if (co != null) yield return co;
  }

  IEnumerator SingleLineLoop(LineWin lineWin)
  {
    while (true)
    {
      yield return new WaitForSecondsRealtime(betweenLineDelay);

      var running = new List<Coroutine>();
      int count = lineWin.positions.Count;
      int midIndex = count / 2;
      for (int i = 0; i < count; i++)
      {
        if (!TryParsePosition(lineWin.positions[i], out int row, out int col)) continue;
        if (col < 0 || col >= slotMatrix.Count) continue;
        if (row < 0 || row >= slotMatrix[col].slotImages.Count) continue;
        bool show = i == midIndex && slotMatrix[col].slotImages[row].id != WildId;
        audioController.Play("blink");
        running.Add(StartCoroutine(slotMatrix[col].slotImages[row].PlayWinIteration(this, animationOverlayParent, show, show ? lineWin.payout : 0, isSyncedPass: false)));
      }
      foreach (var co in running)
        if (co != null) yield return co;
    }
  }

  IEnumerator PerLineLoop(List<LineWin> lineWins)
  {
    while (true)
    {
      foreach (var lineWin in lineWins)
      {
        var running = new List<Coroutine>();
        int count = lineWin.positions.Count;
        int midIndex = count / 2;
        for (int i = 0; i < count; i++)
        {
          if (!TryParsePosition(lineWin.positions[i], out int row, out int col)) continue;
          if (col < 0 || col >= slotMatrix.Count) continue;
          if (row < 0 || row >= slotMatrix[col].slotImages.Count) continue;
          bool show = i == midIndex && slotMatrix[col].slotImages[row].id != WildId;
          double payout = show ? lineWin.payout : 0;
          audioController.Play("blink");
          running.Add(StartCoroutine(slotMatrix[col].slotImages[row].PlayWinIteration(this, animationOverlayParent, show, payout, isSyncedPass: false)));
        }
        foreach (var c in running)
          if (c != null) yield return c;

        yield return new WaitForSecondsRealtime(betweenLineDelay);
        StopAnimateLineWin(lineWin);
      }
    }
  }

  void StopAnimateLineWin(LineWin lineWins)
  {
    for (int i = 0; i < lineWins.positions.Count; i++)
    {
      if (!TryParsePosition(lineWins.positions[i], out int row, out int col)) continue;
      if (col < 0 || col >= slotMatrix.Count) continue;
      if (row < 0 || row >= slotMatrix[col].slotImages.Count) continue;

      slotMatrix[col].slotImages[row].ResetLineAnim();
    }
  }

  // internal void SetGoldenDarkActive(bool isTrue = false)
  // {
  //   for (int i = 0; i < slotMatrix.Count; i++)
  //   {
  //     for (int j = 0; j < slotMatrix[i].slotImages.Count; j++)
  //     {
  //       if (slotMatrix[i].slotImages[j].isGold)
  //       {
  //         slotMatrix[i].slotImages[j].Dark.SetActive(isTrue);
  //         slotMatrix[i].slotImages[j].Darkest.SetActive(isTrue);
  //       }
  //     }
  //   }
  // }

  internal void SetDarkOverlay(bool state)
  {
    if (darkOverlay != null) darkOverlay.SetActive(state);
  }

  // internal void SetWildPosOff()
  // {
  //   for (int i = 0; i < WildMatrix.Count; i++)
  //   {
  //     for (int j = 0; j < WildMatrix[i].slotImages.Count; j++)
  //     {
  //       WildMatrix[i].slotImages[j].StopAnimation();
  //       WildMatrix[i].slotImages[j].gameObject.SetActive(false);
  //     }
  //   }
  // }

  // internal void ResetAllAnim()
  // {
  //   ToggleDarkFG(false, true);
  // }
}

[Serializable]
public class SlotImage
{
  public List<SlotIconView> slotImages = new List<SlotIconView>(10);
}

[Serializable]
public class SymbolWinAnim
{
  public int id;
  public List<Sprite> syncedSprites = new List<Sprite>(); // first synced pass frames
  public float syncedSpeed = 5f;
  public List<Sprite> loopSprites = new List<Sprite>();    // single/multiline loop pass frames
  public float loopSpeed = 5f;
  public bool loopUsesPulse = false;                       // gold bars (id 0,1): pulse instead of frames on loop
}
