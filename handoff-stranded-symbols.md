# Handoff — stranded gold-bar symbols after mid-pulse spin click

## Repo

- Project: `/Users/dev-mehul/SlotSpecial-DiamondRiches` (Unity, Diamond Riches slot client)
- Branch: `dev-meh`
- Only C# scripts are in-scope per CLAUDE.md (no scene / prefab / asset edits).

## Bug

When a winning line involves **id 0 (GoldA) or id 1 (GoldB)** — the only symbols configured with `loopUsesPulse = true` in `SymbolWinAnim` — and the user clicks Spin **mid-pulse during the win loop**, the next spin shows:

1. Some of the previously-winning line's symbols **stranded in the scene view at a random ±~1000 Y offset** below the slot panel (visible in scene view, off-camera in game view).
2. Those stranded symbols still display the **previous spin's sprite ids**, not the new result — i.e. `PopulateSlotMatrix` → `SetIcon` did not update them.
3. The line presentation appears to keep playing on those offset positions even when the new server result has no `lineWins`.

Reproduces only with id 0 / id 1 pulse animations. Non-pulse symbols (which run frame-sequence overlays via `PlayOverlaySequence`) don't trigger it.

Example server payloads (see screenshots referenced in chat):
- Spin A: `matrix=[[1,7,4],[1,1,6],[3,1,1]]`, line at `["0,0","1,1","2,2"]` symbol GoldB (id 1), diamondPositions `["0,1"]` (diamondCount 1 → triggers `PlayDiamondIdle`).
- Spin B (clicked mid-pulse of A's loop): `matrix=[[2,3,4],[1,2,2],[2,6,7]]`, `lineWins=[]` — but 2 of A's gold-bar icons remain stranded.

## Relevant files

- `Assets/Scripts/Base/SlotController.cs` — `StartSpin` teardown order (101), `StopWinLoop` (271), `StopIconAnimation` (297), `ResetAllIcons` (306), `RegisterAnimatingIcon` (292), `animatingIcons` list (53), `PlaySyncedPass` / `SingleLineLoop` / `PerLineLoop` (444 / 482 / 505), `PlayDiamondIdle` (546), `StartDiamondTriggered` (556).
- `Assets/Scripts/Base/SlotIconView.cs` — `Lift` (117), `Drop` (132), `ForceRestoreToRest` (158), `Reset` (171), `StopAnim` (368), `ResetLineAnim` (322), `PlayWinIteration` (191), `PlayPulse` (311), `PlayOverlaySequence` (290), `PlayDiamondIdleAnim` (261), `PlayDiamondTriggeredLoop` (273), `SetIcon` (171). `_restParent / _restSiblingIndex / _restLocalPosition` (Awake snapshot). `_cachedParent / _cachedSiblingIndex / _cachedLocalPosition` (Lift snapshot).
- `Assets/Scripts/Base/GameManager.cs:514` — `StartCoroutine(slotManager.PlayDiamondIdle(idleRow, idleCol))` fires diamond idle on diamondCount==1.

## What's already in place (committed to working tree, not git-committed)

1. `Lift` caches `_cachedParent` / sibling index / `localPosition` at lift time (was Awake originally). Re-Lift while parent == overlay is a no-op (early return). On the **first** lift while `_cachedParent` is null, the pre-lift state is snapshotted.
2. `Drop` restores from `_cachedParent`, clears the cache. Falls back to `ForceRestoreToRest` if the icon is not on `_restParent` but has no lift cache.
3. **Awake-time rest snapshot** added (`_restParent`, `_restSiblingIndex`, `_restLocalPosition`). Separate from the Lift/Drop pair — used as the "go home" baseline.
4. `ForceRestoreToRest()` helper — unconditionally reparents to `_restParent`, sets sibling index + localPosition, clears `_cachedParent`. Used by `Reset()` and `StopAnim()`.
5. `StartSpin` teardown reordered to: `StopWinLoop → StopIconAnimation → SetDarkOverlay(false) → KillAllTweens → ResetAllIcons → InitializeTweening`. Every Drop / ForceRestoreToRest path runs before the reel tween kicks off.

**None of this fixed the bug.** The stranded gold-bar symbols still appear on next spin when click happens mid-pulse.

## Investigation summary

- `slotMatrix` and `allMatrix` are SerializeField — references are static at runtime. No `Instantiate` in `SlotController` / `SlotIconView`. No code reassigns `slotMatrix[c].slotImages[r]`.
- `SetParent` calls in the whole project are only in `Lift` / `Drop` / `ForceRestoreToRest` (confirmed via grep).
- `PlayPulse` only animates `iconImage.transform.localScale` — no `localPosition` writes. Scale alone cannot produce ±1000 Y offset. `iconAnim?.Kill()` is called by every teardown path (Reset, StopAnim, ResetLineAnim) and `localScale` is explicitly set back to `Vector3.one`.
- `TriggerFeature` (the in-a-row effect that writes `feature.transform.position = slotIcon.transform.position`) is commented out in `GameManager` — not active.
- `ImageAnimation` doesn't touch transform position.
- The user observed `animatingIcons` had **4 entries during the win loop** when there should be 3 — 3 line icons on the animation overlay (lifted) and **1 extra icon (the diamond) sitting at its rest/cached position**. The diamond got there via `PlayDiamondIdleAnim` → `RegisterAnimatingIcon` → `Lift` → play overlay → `Drop` (self-drop at end). After the self-drop, the icon is back at rest but **still in `animatingIcons`** until the next `StopIconAnimation` clears the list.
- Sprite mismatch on the stranded symbols (showing the OLD ids) is the load-bearing clue. If `PopulateSlotMatrix` ran `SetIcon` on `slotMatrix[col].slotImages[row]`, the sprite would have updated. So either (a) the stranded GameObjects are not the same `SlotIconView` references currently in `slotMatrix`, or (b) something is overriding/duplicating them.

## Next steps for the new convo

### 1. Audit `animatingIcons` lifecycle — primary suspect

Strong signal from the user's observation:

> "once id 0 and 1 goes to animating icons and we do the next spin those 3 symbols always come back to animating icons even when result data shows there is no lines on those positions and how the positions are offseted."

This says the **gold-bar icons re-enter `animatingIcons` on subsequent spins even when the new result has no winning lines involving those positions.** That's not possible from `PlayWinIteration` alone (it only runs for `lineWin.positions`). Suspects:

- A coroutine started for the previous spin's win presentation is still alive after `StopCoroutine(WinLoopCorutine)` — `StopCoroutine` only stops the top-level coroutine, not children started via `StartCoroutine` inside `PlaySyncedPass` / `SingleLineLoop` / `PerLineLoop`. Each of those inner `PlayWinIteration` coroutines calls `controller.RegisterAnimatingIcon(this)` at its start. If one is mid-`PlayPulse` when `WinLoopCorutine` is killed, it survives, completes its `yield return pulseSeq.WaitForCompletion()` (resolves because pulse is killed elsewhere), and exits — but it already registered. That alone wouldn't cause re-entry on the *next* spin though.
- More likely: `animatingIcons` is **never cleared between win presentations**. It's only cleared in `StopIconAnimation` (called from `StartSpin`). But if a `PlayWinIteration` coroutine from spin N's loop is still alive when spin N+1 starts, it would register the same icon again into the freshly-cleared list. **Verify**: does anything start `PlayWinIteration` (or any path that calls `RegisterAnimatingIcon`) *after* `StopIconAnimation` runs in `StartSpin`? The most likely answer is the surviving child coroutines from the previous loop, which haven't yet hit their `yield return pulseSeq.WaitForCompletion()` resolution.

**Required fix sketch**:
- Track child coroutines started inside `PlaySyncedPass` / `SingleLineLoop` / `PerLineLoop` (the `running` lists) **as fields on `SlotController`**, and `StopCoroutine` each of them in `StopWinLoop` before draining `_activeWinLines`. Right now those `running` lists are locals — killing the parent doesn't kill them.
- After stopping the child coroutines, then drain `_activeWinLines` and clear `animatingIcons`.

### 2. Clean up `animatingIcons` when an animation completes naturally

> "the diamond idle animation goes into animating icons but should be cleaned as soon as the animation completes no?"

Yes. Today `animatingIcons` only ever shrinks in `StopIconAnimation` (which `Clear()`s the whole list on next spin start). Anything that self-completes (like `PlayDiamondIdleAnim` → `Drop()`) leaves a stale entry behind.

**Required fix**: add an `UnregisterAnimatingIcon(SlotIconView icon)` on `SlotController` that removes from the list. Call it at the natural end of:

- `PlayDiamondIdleAnim` after its `Drop()` (SlotIconView.cs:269).
- `PlayWinIteration` — at end of method (only meaningful for the `autoContinued` path in `AnimateLineWins` and for synced pass; the manual win loop's `PlayWinIteration` is iterated forever so it never naturally ends). Cleanest place: have whoever calls `Lift` also be responsible for `Unregister` after the matching `Drop`. Or: simpler — in `Drop()` and `ForceRestoreToRest()`, call back into `SlotController.UnregisterAnimatingIcon(this)`. The icon view doesn't hold a controller ref though, so plumb the controller into `Lift` or store a controller ref on the view.

This stops `animatingIcons` from carrying stale references across iterations of the same win loop and across spins.

### 3. Stranded symbols are likely the symptom of (1)

Hypothesis: when spin N+1's `StopIconAnimation` runs, the surviving inner `PlayWinIteration` coroutines from spin N's loop **re-register the same line icons into the freshly-cleared `animatingIcons`** (and possibly re-Lift them — though `Lift` early-returns if already on overlay, the cache may already be cleared by `ForceRestoreToRest`, so a fresh Lift re-caches with the wrong parent if the icon got reparented in the interim). The icons stay on overlay → reel tween moves the column down → icons don't follow → next spin's `SetIcon` *does* update them (they're still in slotMatrix), but their transform is stuck on overlayParent.

To validate this hypothesis quickly, re-add the diagnostic logs that were in place (see prior commit history of `SlotIconView.cs` / `SlotController.cs` — they printed `[SlotIcon LIFT]`, `[SlotIcon DROP]`, `[SlotIcon FORCE_REST]`, `[SlotIcon SET_ICON]`, `[SpinDbg]` markers, and a slotMatrix dump after `ResetAllIcons`). Run the repro and check:

- Does `RegisterAnimatingIcon` fire **after** `[SpinDbg] after StopIconAnimation`? If yes, surviving child coroutines are the culprit.
- Does a `LIFT` log fire for a line icon **after** `[SpinDbg] after ResetAllIcons`? That's a smoking-gun re-Lift from a zombie coroutine.

### 4. After validating, the fix

1. Promote the `running` lists in `PlaySyncedPass`, `SingleLineLoop`, `PerLineLoop` to a single field `_activeWinIterCoroutines` (or similar) on `SlotController`.
2. In `StopWinLoop`, after `StopCoroutine(WinLoopCorutine)`, iterate `_activeWinIterCoroutines` and `StopCoroutine` each, then clear the list.
3. Add `UnregisterAnimatingIcon` and call it from `PlayDiamondIdleAnim` (post-Drop) — and ideally from a guaranteed-to-run cleanup at the end of `PlayWinIteration` for the autoContinued and synced-pass paths.

## Verification once fixed

1. Spin to a line containing only id 0 or id 1 (gold bars), let the loop run a couple of iterations, click Spin mid-pulse. Inspect scene view: no stranded symbols below the slot panel.
2. Inspect `animatingIcons` (it's `[SerializeField] internal` so visible in the inspector). Mid-loop should show exactly N entries (where N = unique line positions + diamond if any). After diamond idle finishes naturally, the diamond should no longer appear in the list.
3. Spin to a mixed-id line (e.g. one of the line symbols is wild/lady id, others are gold), repeat the mid-pulse click. Verify no stranded symbols.
4. Multi-line win that includes a gold-only line, run `PerLineLoop`, click Spin mid-line. No stranded symbols.

## Notes / gotchas

- Don't touch scenes / prefabs / .asset / .meta / .anim — CLAUDE.md scripts-only rule.
- `iconImage` shares the SlotIconView GameObject vs being a child — can't be verified without the prefab, but `PlayPulse` only does `DOScale` so it's not the root cause either way.
- DOTween: `Sequence.Kill()` is synchronous; `pulseSeq.OnKill` runs in the Kill call. `WaitForCompletion` resolves when the tween is no longer active. So killed-mid-tween doesn't hang the coroutine.
- `_restParent` may be null if the icon is a root GameObject at Awake (rare for UI). `ForceRestoreToRest` no-ops in that case — worth checking via the diagnostic logs.
- `animationOverlayParent` is the world-position destination of `Lift`; the visible "stranded" symbols are sitting at that overlay's world position with a localPosition that depends on when the world-position-preserving SetParent sampled.
