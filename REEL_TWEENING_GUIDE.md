# Reel Spin / Stop Tweening — Implementation Guide

This document explains how the slot reel **spin and stop** animation is built using
DOTween, so it can be reproduced in another slot game. It covers:

- The Y-position + speed model (spin-top, spin-bottom, rest positions).
- `InitializeTweening` — the intro drop into an infinite spin loop.
- `StopTweening` — killing the loop and landing the reel with an OutBack bounce.
- How the caller **kicks off** spinning and **waits** for the intro/landing to finish.
- The staggered, interruptible per-reel stop sequence (turbo / immediate-stop).

> All animation uses **DOTween** (`using DG.Tweening;`). Each reel column is a single
> `RectTransform` that is moved only on its local **Y** axis. The child symbols ride
> along with it.

---

## 1. The position + speed model

A reel is one tall column `RectTransform`. There are three meaningful Y positions:

```csharp
private const float SpinTopY    = 1900f;        // just above the visible window (spawn point)
private const float SpinBottomY = -1900f;       // just below the visible window (exit point)
private const float RestY       = 145.402496f;  // final resting Y where results are shown
```

Instead of hard-coding durations, everything runs at a **constant reel speed** and the
duration for any move is derived from the distance. This keeps intro, loop, and stop all
moving at the same visual speed regardless of how far they travel.

```csharp
// Reel travel speed in local units/second.
[SerializeField] private float reelSpeed = 2857f;

// Duration needed to travel between two Y positions at reelSpeed.
private float DurationFor(float fromY, float toY)
    => Mathf.Abs(toY - fromY) / Mathf.Max(reelSpeed, 0.0001f);
```

The whole illusion is: move the column **top → bottom** on a loop (symbols scroll past the
window), then when stopping, teleport to the top one last time and ease down to `RestY`
with an overshoot bounce.

---

## 2. Tween bookkeeping

Every reel's *currently active* tween is stored by index in one list. This is the key data
structure — the intro sequence appends the loop tween, and `StopTweening` **replaces** the
entry at that index with the landing tween so the caller can wait on `alltweens[i]`.

```csharp
private List<Tweener> alltweens = new List<Tweener>();

[SerializeField] private RectTransform[] Slot_Transform; // one per reel column
```

---

## 3. `InitializeTweening` — intro drop → infinite loop

Called once per reel at the start of a spin. It returns a `Tween` (the intro `Sequence`)
so the caller can wait until the intro finishes. Its final callback starts the **infinite
looping** spin tween and registers it in `alltweens`.

```csharp
private Tween InitializeTweening(Transform slotTransform)
{
    Sequence seq = DOTween.Sequence();
    float startY = slotTransform.localPosition.y;

    // 1) Drop from wherever it is down to the bottom (one-time intro slide-out).
    seq.Append(slotTransform.DOLocalMoveY(SpinBottomY, DurationFor(startY, SpinBottomY))
        .SetEase(Ease.Linear));

    // 2) The instant that finishes, teleport to the top and start the infinite loop.
    seq.AppendCallback(() =>
    {
        slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, SpinTopY);

        Tweener tweener = slotTransform
            .DOLocalMoveY(SpinBottomY, DurationFor(SpinTopY, SpinBottomY))
            .SetLoops(-1, LoopType.Restart)   // infinite loop: top -> bottom, snap back to top, repeat
            .SetEase(Ease.Linear);            // constant speed = seamless scroll

        alltweens.Add(tweener);               // register so StopTweening/KillAllTweens can find it
    });

    return seq; // caller waits on this to know the intro is done
}
```

Notes:
- `LoopType.Restart` makes it jump back to `SpinTopY` at the end of each loop, so the reel
  appears to scroll endlessly downward.
- `Ease.Linear` on the loop is important — any other ease would make the scroll pulse.
- The loop tween is added to `alltweens` **inside** the callback, i.e. only after the intro
  completes. So `alltweens` is populated in reel order as each intro finishes.

---

## 4. `StopTweening` — land the reel with a bounce

To stop a reel, kill its infinite loop, teleport once more to the top, then ease down to
`RestY` with an `OutBack` overshoot (the little bounce/settle at the end). The landing tween
**replaces** the loop tween at the same index in `alltweens`, so callers can `WaitForCompletion`
on it.

```csharp
private void StopTweening(Transform slotTransform, int index)
{
    alltweens[index].Kill();  // stop the infinite loop

    // Teleport to the top so the landing slide always covers the full window (consistent feel).
    slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, SpinTopY);

    // Replace the loop entry with the landing tween so callers can await alltweens[index].
    alltweens[index] = slotTransform
        .DOLocalMoveY(RestY, DurationFor(SpinTopY, RestY))
        .SetEase(Ease.OutBack, 0.9f);  // overshoot then settle onto RestY
}
```

`Ease.OutBack` with an overshoot of `0.9f` gives the reel a satisfying land-and-settle. The
second argument is the overshoot amount — tune it for more/less bounce.

```csharp
private void KillAllTweens()
{
    for (int i = 0; i < alltweens.Count; i++)
        alltweens[i].Kill();
    alltweens.Clear();
}
```

---

## 5. Starting a spin — kick off + wait for intro

`StartSpin` cleans up leftover state from the previous spin, plays the loop SFX, then starts
the intro tween on every reel and **waits for the first and last** intro to complete before
returning. (Waiting for `[0]` and `[^1]` is enough since all intros run at the same speed.)

```csharp
internal IEnumerator StartSpin()
{
    // Tear down anything the previous spin left running before starting fresh.
    StopWinLoop();
    StopIconAnimation();
    SetDarkOverlay(false);
    ResetExtraColumn();
    KillAllTweens();     // clears alltweens so intro can repopulate it cleanly
    ResetAllIcons();

    List<Tween> initTweens = new();
    audioController.Play("spinning");
    for (int i = 0; i < Slot_Transform.Length; i++)
        initTweens.Add(InitializeTweening(Slot_Transform[i]));

    // Wait until the intro slides finish; by now each reel's infinite loop is running.
    yield return initTweens[0].WaitForCompletion();
    yield return initTweens[^1].WaitForCompletion();
}
```

`WaitForCompletion()` is the DOTween idiom for "yield in a coroutine until this tween is
done." That is the whole "how we wait for it" pattern — return the tween/sequence from the
method that creates it, and `yield return tween.WaitForCompletion()` at the call site.

### Between start and stop: fill in the results

After `StartSpin` returns (reels are looping), the caller fetches the server result and
writes the final symbols into the matrix **while the reels are still spinning**, so when
they land the correct symbols are already in place:

```csharp
yield return slotManager.StartSpin();          // reels now looping
// ... await server result ...
slotManager.PopulateSlotMatrix(result.matrix); // set final symbols under the spinning reel
// ... optional minimum-spin delay ...
yield return slotManager.StopSpin(() => audioController.Play("reelstop"));
```

`PopulateSlotMatrix` just calls `SetIcon` on each visible cell — the symbols are children of
the still-moving column, so they're offscreen until the landing tween brings them to `RestY`.

---

## 6. Stopping the reels — staggered, interruptible

`StopSpin` stops reels left-to-right with a delay between each (the classic cascade), then
waits for all of them to land. The delay is **interruptible**: if the player presses Stop
(or turbo is on), `immediateStop` flips true and the remaining reels all stop on the same
frame.

```csharp
internal IEnumerator StopSpin(Action playFallAudio)
{
    // Stop each reel in turn.
    for (int i = 0; i < Slot_Transform.Length; i++)
    {
        StopTweening(Slot_Transform[i], i);   // replaces alltweens[i] with the landing tween

        bool immediate = gameManager.immediateStop;

        // Reel-stop SFX: always on reel 0; per-reel only while not immediate-stopping.
        if (i == 0 || !immediate)
            playFallAudio?.Invoke();

        if (!immediate)
        {
            // Interruptible inter-reel delay. The moment Stop is pressed (immediateStop -> true)
            // we bail out of the wait so the remaining reels stop on the same frame.
            float wait = gameManager.turboMode ? 0.2f : 0.6f;
            float elapsed = 0f;
            while (elapsed < wait && !gameManager.immediateStop)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    // Wait for every landing tween to finish.
    for (int i = 0; i < Slot_Transform.Length; i++)
        yield return alltweens[i].WaitForCompletion();

    // Snap to exactly RestY so downstream logic reads a stable final position regardless of
    // any OutBack overshoot frame race, then clear the tween list.
    for (int i = 0; i < Slot_Transform.Length; i++)
        Slot_Transform[i].localPosition =
            new Vector2(Slot_Transform[i].localPosition.x, RestY);
    KillAllTweens();

    Canvas.ForceUpdateCanvases(); // flush layout before anything samples world positions
    yield return null;
}
```

Key ideas:
- **`Time.unscaledDeltaTime`** is used for the manual delay loop so pausing / time-scale
  changes don't affect reel timing.
- The **immediate-stop / turbo** path skips the stagger: turbo sets `immediateStop = true`
  before `StopSpin` runs, so the very first iteration sees `immediate == true`, plays no
  inter-reel delay, and all reels land together.
- After landing, **snap each column to exactly `RestY`**. The `OutBack` overshoot means the
  transform might be mid-bounce on the frame you read it; snapping guarantees a clean final
  position for whatever presents the win (lifting symbols, reading world positions, etc.).

### The caller side

The stop button just sets a flag; the spin coroutine polls it. This is the whole
interruption mechanism:

```csharp
// Wired to the Stop button:
IEnumerator StopSpin()
{
    if (immediateStop) yield break;
    immediateStop = true;                       // <- reels bail out of their inter-reel delay
    StopSpin_Button.gameObject.SetActive(false);
    yield return new WaitUntil(() => !isSpinning);
    immediateStop = false;
}
```

---

## 7. Flow summary

```
OnSpin (caller coroutine)
  ├─ StartSpin()                       // clean up, start intro tweens, wait intro done -> reels loop
  ├─ await server result
  ├─ PopulateSlotMatrix(result)        // set final symbols under the spinning reels
  ├─ (turbo? immediateStop = true)     // optional minimum spin delay
  └─ StopSpin(reelStopSfx)             // stagger-stop (interruptible), wait all land, snap to RestY
```

Per reel, internally:

```
InitializeTweening:  currentY --Linear--> SpinBottomY, then teleport SpinTopY --Linear loop(-1)--> SpinBottomY
StopTweening:        Kill loop, teleport SpinTopY, --OutBack--> RestY   (replaces alltweens[i])
StopSpin end:        snap localPosition.y = RestY, KillAllTweens
```

---

## 8. Adapting to another game — checklist

1. **Set the three Y constants** for your reel geometry:
   - `SpinTopY` = just above the visible window (spawn),
   - `SpinBottomY` = just below it (exit),
   - `RestY` = the final resting Y of the column.
2. **Tune `reelSpeed`** (local units/second) for the feel you want; durations follow
   automatically from `DurationFor`.
3. Give each reel column its own `RectTransform` in `Slot_Transform[]`, with symbols as
   children so they scroll with the column.
4. Keep the **`alltweens` list indexed by reel** — the intro appends the loop tween and
   `StopTweening` swaps in the landing tween at the same index; callers await `alltweens[i]`.
5. **Populate final symbols after `StartSpin` returns and before `StopSpin`**, while reels
   are still looping.
6. For interruptible stops, expose an **`immediateStop` flag** the stop button sets and the
   stop loop polls; use `Time.unscaledDeltaTime` for the inter-reel delay.
7. Always **snap to `RestY` and `KillAllTweens()`** after landing so downstream code reads a
   stable position.
8. Tune the land feel via `Ease.OutBack`'s overshoot argument (here `0.9f`); the loop must
   stay `Ease.Linear` with `LoopType.Restart` for a seamless scroll.

> Dependency: **DOTween** (`DG.Tweening`). The waiting pattern throughout is
> `yield return tween.WaitForCompletion();` inside coroutines.
```
