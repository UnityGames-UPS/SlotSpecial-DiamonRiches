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

  [Header("Slots Transforms")]
  [SerializeField] private RectTransform[] Slot_Transform;

  private List<Tweener> alltweens = new List<Tweener>();
  private Tweener WinTween = null;
  [SerializeField] private List<SlotIconView> animatingIcons = new List<SlotIconView>();
  [SerializeField] private float wildPerColumnDelay = 2f;
  [SerializeField] private float betweenLineDelay = 0.25f;
  private Coroutine WinLoopCorutine = null;

  void Awake()
  {
    ShuffleMatrix();
  }

  internal IEnumerator StartSpin()
  {
    StopWinLoop();
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

  internal void PopulateSlotMatrix(List<List<string>> resultData, List<GoldenPositions> goldPositions)
  {
    for (int i = 0; i < resultData.Count; i++)
    {
      for (int j = 0; j < resultData[i].Count; j++)
      {
        slotMatrix[j].slotImages[i].SetIcon(ID: int.Parse(resultData[i][j]), image: iconImages[int.Parse(resultData[i][j])]);
      }
    }
    if (goldPositions.Count > 0)
    {
      if (goldPositions.Count > 1)
      {
        audioController.Play("thunder");
      }
      for (int i = 0; i < goldPositions.Count; i++)
      {
        int id = goldPositions[i].symbolId;
        for (int j = 0; j < goldPositions[i].positions.Count; j++)
        {
          int col = goldPositions[i].positions[j][1];
          int row = goldPositions[i].positions[j][0];
          if (id == 10)
          {
            slotMatrix[col].slotImages[row].SetGoldIcon(goldIconImages[id]); // sets isGold only (returns early visually)
          }
          else
          {
            slotMatrix[col].slotImages[row].SetGoldIcon(goldIconImages[id]);
          }
        }
      }
      ToggleDarkFG(true, false);
    }
  }

  internal IEnumerator HideGoldIcons()
  {
    for (int i = 0; i < slotMatrix.Count; i++)
    {
      for (int j = 0; j < slotMatrix[i].slotImages.Count; j++)
      {
        if (slotMatrix[i].slotImages[j].isGold)
          slotMatrix[i].slotImages[j].AnimateGoldIcon();
      }
    }

    ToggleDarkFG(false, false);
    yield return new WaitForSecondsRealtime(1f);
  }

  internal IEnumerator StopSpin(Action playFallAudio)
  {
    for (int i = 0; i < Slot_Transform.Length; i++)
    {
      StopTweening(Slot_Transform[i], i);

      if (!GameManager.immediateStop)
      {
        playFallAudio?.Invoke();
        yield return new WaitForSecondsRealtime(GameManager.turboMode ? 0.2f : 0.6f);
      }
    }

    yield return alltweens[^1].WaitForCompletion();
    KillAllTweens();
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

  private const float SpinTopY = 1500f;
  private const float SpinBottomY = -1500f;
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

  internal IEnumerator AnimateLineWins(List<LineWin> lineWins)
  {
    ToggleDarkFG(true, true);
    bool singleLine = lineWins.Count == 1;

    // Synchronized first pass. Payouts shown only when there is exactly one winning line.
    yield return PlaySyncedPass(lineWins, showPayouts: singleLine);

    bool autoContinued = GameManager.isAutoSpin || GameManager.isFreeSpin
      || gameManager.LastSpinWasWildTrigger() || gameManager.LastSpinWasFreeSpinTrigger();

    if (autoContinued)
    {
      foreach (var lineWin in lineWins) StopAnimateLineWin(lineWin);
      yield break;
    }

    if (singleLine)
    {
      // Icons stay on; loop the per-iteration animations indefinitely with the line payout still visible.
      WinLoopCorutine = StartCoroutine(SingleLineLoop(lineWins[0]));
    }
    else
    {
      foreach (var lineWin in lineWins) StopAnimateLineWin(lineWin);
      WinLoopCorutine = StartCoroutine(PerLineLoop(lineWins));
    }
  }

  IEnumerator PlaySyncedPass(List<LineWin> lineWins, bool showPayouts)
  {
    var lastPerLine = new Dictionary<(int col, int row), double>();
    if (showPayouts)
    {
      foreach (var lineWin in lineWins)
      {
        int c = lineWin.positions.Count;
        if (c == 0) continue;
        var last = lineWin.positions[c - 1];
        lastPerLine[(last.position[1], last.position[0])] = lineWin.payout;
      }
    }

    var seen = new HashSet<(int, int)>();
    var running = new List<Coroutine>();
    foreach (var lineWin in lineWins)
    {
      for (int i = 0; i < lineWin.positions.Count; i++)
      {
        int col = lineWin.positions[i].position[1];
        int row = lineWin.positions[i].position[0];
        if (col < 0 || col >= slotMatrix.Count) continue;
        if (row < 0 || row >= slotMatrix[col].slotImages.Count) continue;
        if (!seen.Add((col, row))) continue;

        bool show = lastPerLine.TryGetValue((col, row), out double payout);
        running.Add(StartCoroutine(slotMatrix[col].slotImages[row].PlayWinIteration(this, animationOverlayParent, show, show ? payout : 0)));
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
      int lastIndex = count - 1;
      for (int i = 0; i < count; i++)
      {
        int col = lineWin.positions[i].position[1];
        int row = lineWin.positions[i].position[0];
        if (col < 0 || col >= slotMatrix.Count) continue;
        if (row < 0 || row >= slotMatrix[col].slotImages.Count) continue;
        bool show = i == lastIndex;
        audioController.Play("blink");
        running.Add(StartCoroutine(slotMatrix[col].slotImages[row].PlayWinIteration(this, animationOverlayParent, show, show ? lineWin.payout : 0)));
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
        int lastIndex = count - 1;
        for (int i = 0; i < count; i++)
        {
          int col = lineWin.positions[i].position[1];
          int row = lineWin.positions[i].position[0];
          if (col < 0 || col >= slotMatrix.Count) continue;
          if (row < 0 || row >= slotMatrix[col].slotImages.Count) continue;
          bool show = i == lastIndex;
          double payout = show ? lineWin.payout : 0;
          audioController.Play("blink");
          running.Add(StartCoroutine(slotMatrix[col].slotImages[row].PlayWinIteration(this, animationOverlayParent, show, payout)));
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
    int count = Mathf.Min(lineWins.positions.Count, lineWins.pattern.Count);

    for (int i = 0; i < count; i++)
    {
      int col = lineWins.positions[i].position[1];
      int row = lineWins.positions[i].position[0];

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

  internal void ToggleDarkFG(bool state = false, bool forAll = true)
  {
    for (int i = 0; i < allMatrix.Count; i++)
    {
      for (int j = 0; j < allMatrix[i].slotImages.Count; j++)
      {
        if (forAll)
        {
          allMatrix[i].slotImages[j].AnimateDarkImage(state);
        }
        else
        {
          if (!allMatrix[i].slotImages[j].isGold)
          {
            allMatrix[i].slotImages[j].AnimateDarkImage(state);
          }
        }
      }
    }
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
