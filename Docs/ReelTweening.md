# Reel Tweening: Init, Stop, Rest Positions & Speed

Portable reference for the reel-spin tweening system used in `SlotController.cs`. Written so it can be re-implemented in a different slot game project. Uses DOTween (`DG.Tweening`).

## Core constants

```csharp
[SerializeField] private float reelSpeed = 2857f;   // local units/second

private const float SpinTopY = 1900f;
private const float SpinBottomY = -1900f;
private const float RestY = 145.402496f;
```

- `SpinTopY` / `SpinBottomY` — the loop boundary positions a reel strip travels between while spinning.
- `RestY` — the landed/idle Y position where a reel sits showing its final symbols.
- `reelSpeed` — constant travel speed in local units/second. All three values (top, bottom, rest) are specific to this project's reel strip layout and symbol size — recalculate them for a different game's art/grid.

## Duration helper

Every tween's duration is derived from distance ÷ speed, so every move (intro, loop, stop) runs at the same constant speed regardless of how far it travels:

```csharp
private float DurationFor(float fromY, float toY)
  => Mathf.Abs(toY - fromY) / Mathf.Max(reelSpeed, 0.0001f);
```

## Init tween (start spin)

Called once per reel column when a spin starts. Builds a `Sequence`:
1. Tween from the reel's current Y down to `SpinBottomY` (linear) — this is the "wind-up" leaving the rest position.
2. On completion, snap the reel's position straight up to `SpinTopY` (no visible jump because it's off-screen), then start an **infinite looping** `Tweener` from `SpinTopY` → `SpinBottomY` (`Ease.Linear`, `SetLoops(-1, LoopType.Restart)`). This looping tween is stored in a list (`alltweens`) indexed by reel column, so it can be killed later.

```csharp
private Tween InitializeTweening(Transform slotTransform)
{
  Sequence seq = DOTween.Sequence();
  float startY = slotTransform.localPosition.y;
  seq.Append(slotTransform.DOLocalMoveY(SpinBottomY, DurationFor(startY, SpinBottomY)).SetEase(Ease.Linear));
  seq.AppendCallback(() =>
  {
    slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, SpinTopY);
    Tweener tweener = slotTransform.DOLocalMoveY(SpinBottomY, DurationFor(SpinTopY, SpinBottomY))
      .SetLoops(-1, LoopType.Restart)
      .SetEase(Ease.Linear);
    alltweens.Add(tweener);
  });
  return seq;
}
```

Usage (`StartSpin`): kill any existing tweens first, then init every reel column and await the first and last reel's intro tween completing before treating the spin as "started":

```csharp
KillAllTweens();
List<Tween> initTweens = new();
for (int i = 0; i < Slot_Transform.Length; i++)
  initTweens.Add(InitializeTweening(Slot_Transform[i]));
yield return initTweens[0].WaitForCompletion();
yield return initTweens[^1].WaitForCompletion();
```

## Stop tween (land a reel)

Called once per reel column, in order, to stop it and land it on its final symbols:

```csharp
private void StopTweening(Transform slotTransform, int index)
{
  alltweens[index].Kill();
  slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, SpinTopY);
  alltweens[index] = slotTransform.DOLocalMoveY(RestY, DurationFor(SpinTopY, RestY)).SetEase(Ease.OutBack, 0.9f);
}
```

- Kills the infinite loop tween.
- Snaps the reel to `SpinTopY` (so every reel lands from the same starting point, keeping landing duration/feel consistent regardless of where in the loop it was killed).
- Tweens down to `RestY` using `Ease.OutBack` (overshoot amount `0.9`) for a bounce-settle landing feel. This is the only non-linear easing in the whole system — it's what gives the reel stop its "weight."

## Kill all

```csharp
private void KillAllTweens()
{
  for (int i = 0; i < alltweens.Count; i++)
    alltweens[i].Kill();
  alltweens.Clear();
}
```

Called at the start of a spin (to clear any leftover tweens) and after all reels have landed.

## Stagger / stop sequencing

Reels aren't stopped simultaneously — each is stopped with an interruptible delay so they land left-to-right with a cascading feel:

```csharp
for (int i = 0; i < reelsToLand; i++)
{
  StopTweening(Slot_Transform[i], i);

  bool immediate = gameManager.immediateStop; // user pressed "stop" during turbo/skip
  if (!immediate)
  {
    float wait = gameManager.turboMode ? 0.2f : 0.6f;
    float elapsed = 0f;
    while (elapsed < wait && !gameManager.immediateStop)
    {
      elapsed += Time.unscaledDeltaTime;
      yield return null;
    }
  }
}
```

- Normal mode: 0.6s between each reel starting its stop tween.
- Turbo mode: 0.2s.
- `immediateStop`: skips all remaining delays so every remaining reel stops on the same frame (used when the player forces an instant stop).

After all reel-stop tweens complete, every reel is explicitly snapped to `RestY` as a safety net against any `Ease.OutBack` overshoot frame race:

```csharp
for (int i = 0; i < Slot_Transform.Length; i++)
  Slot_Transform[i].localPosition = new Vector2(Slot_Transform[i].localPosition.x, RestY);
KillAllTweens();
```

## Speed / acceleration model

There is **no acceleration/deceleration curve** — spin and loop movement is constant speed (`Ease.Linear`), driven entirely by `reelSpeed` (units/second). The only eased motion is the landing tween (`Ease.OutBack`), which is what reads as a "slow down and settle" even though the reel was moving at constant speed right up until it started that tween. Perceived deceleration between reels comes from the per-reel stagger delay (turbo vs. normal), not from changing `reelSpeed` itself.

## Porting checklist

To reuse this in a different slot game project:

1. Recalculate `SpinTopY`, `SpinBottomY`, and `RestY` for the new reel strip's symbol size/spacing and visible window — these three values are the only game-specific numbers, everything else is generic.
2. Tune `reelSpeed` (units/second) to the new project's desired spin feel; keep it constant since all durations derive from it.
3. Keep an `alltweens` list (or equivalent) indexed by reel column so each column's active tween can be killed independently.
4. Reuse `DurationFor`, `InitializeTweening`, `StopTweening`, and `KillAllTweens` largely as-is — they have no dependency on symbol content, only on `Transform.localPosition.y`.
5. Reuse the turbo/normal stagger delay pattern (`0.2f` / `0.6f`) and the `immediateStop` interrupt if the new game needs a "skip to instant stop" feature; otherwise these can be simplified to a plain fixed delay per reel.
