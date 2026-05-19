# Wheel Feature Implementation Notes

## Files

- `Assets/Scripts/Feature/WheelView.cs` — per-wheel spin/stop logic
- `Assets/Scripts/Feature/WheelController.cs` — orchestrates which wheel plays and when
- `Assets/Scripts/Feature/WheelStoper.cs` — stationary trigger zone (the "needle") that detects when a segment passes

## Architecture

```
WheelController.PlayWheel(WheelBonus)
  → activeWheel.SpinTheWheel()          — starts infinite fast spin
  → WaitForSeconds(4f)                  — dramatic pause
  → StartCoroutine(activeWheel.StopWheel())
      → EnableOnlyTargetCollider()
      → WaitUntil(capturedStopAngle >= 0)   — waits for needle trigger
      → Phase 1: 2-rotation slow-down
      → Phase 2: crawl to exact stop angle
  → WaitForSeconds(4f)
  → ResetWheelsPanel()
```

## WheelView Inspector Fields

| Field | Purpose |
|---|---|
| `isStatic` | If true, wheel spins as ambient decoration on Start using `staticRotationDuration` (unrelated to game spin) |
| `staticRotationDuration` | Duration per rotation for the ambient static spin |
| `degreesPerSecond` | **Single source of truth for game spin speed.** Both the fast spin and stop phases derive their durations from this |
| `finalCrawlFactor` | Phase 2 start speed as a fraction of `degreesPerSecond` (default 0.15). Lower = longer crawl |
| `targetIndex` | Set by `WheelController` before `StopWheel()` is called — the `WheelItem.index` that should land on the needle |
| `wheelItems` | Array of all segments. Each has `type`, `index`, `value`, and a `Collider2D` reference |

## Spin Speed Math

All durations are derived from `degreesPerSecond` so changing one value keeps everything in sync:

```
SpinTheWheel:   duration = 360 / degreesPerSecond

Phase 1 (OutSine over 720°):
  OutSine initial velocity = (π/2) × degrees / duration
  → duration = (π/2) × 720 / degreesPerSecond
  Starts at degreesPerSecond, decelerates to ~0 over 2 rotations

Phase 2 (OutQuart over finalDelta°):
  OutQuart initial velocity = 4 × degrees / duration
  → duration = 4 × finalDelta / (degreesPerSecond × finalCrawlFactor)
  Crawls slowly to the exact captured stop angle
```

## Stop Sequence (WheelView.StopWheel)

1. `capturedStopAngle = -1f` (reset)
2. `EnableOnlyTargetCollider()` — the fast spin keeps running untouched
3. `WaitUntil(() => capturedStopAngle >= 0f)` — yields until WheelStoper fires
4. `DOKill(false)` — freeze wheel at current position without snapping
5. **Phase 1**: tween `−720°` with `Ease.OutSine` over derived duration
6. Recalculate `finalDelta`:
   - `cwDelta = (currentZ − capturedStopAngle + 360) % 360`
   - `finalDelta = cwDelta < 1 ? 360 : (360 − cwDelta)`
7. **Phase 2**: tween `−finalDelta` with `Ease.OutQuart` over derived duration
8. Wheel is now stopped exactly at `capturedStopAngle`

## WheelStoper (Needle Trigger)

```csharp
void OnTriggerEnter2D(Collider2D other) {
    WheelView wheelView = other.GetComponentInParent<WheelView>();
    foreach item in wheelView.wheelItems:
        if item.collider == other:           // Collider2D == Collider2D (correct)
            hitAngle = wheelView.transform.localEulerAngles.z
            wheelView.OnSegmentHit(hitAngle)
}
```

Key: `item.collider == other` compares two `Collider2D` references. Previously this was `item.collider == other.gameObject` (Collider2D vs GameObject) which is always false — the wheel never stopped.

## WheelItem

Defined as a `[Serializable]` class at the bottom of `WheelView.cs`:

```csharp
public class WheelItem {
    public string type;   // e.g. "FREESPIN", "MULTIPLIER"
    public int index;     // position index (0..N-1), used to match targetIndex
    public double value;  // payout value
    public Collider2D collider; // the segment's collider, assigned in Inspector
}
```

## WheelController Flow

```csharp
// Finds the WheelItem whose type+value matches the server result
int FindTargetIndex(WheelView wheel, WheelBonus bonus)

// Before calling StopWheel:
activeWheel.targetIndex = FindTargetIndex(activeWheel, wheelBonus);

// WheelController.WheelRotationDuration is passed to SpinTheWheel()
// but WheelView ignores it — speed is entirely controlled by degreesPerSecond
```

## Known Issues / Tuning

- If the wrong segment lands on the needle: the `index` values assigned in Inspector may not match the physical segment positions. Verify that `WheelItem.index` for each segment matches the segment's visual order on the wheel.
- If phase 2 crawl is too long: increase `finalCrawlFactor` (e.g. 0.25)
- If phase 2 crawl is too short/abrupt: decrease `finalCrawlFactor` (e.g. 0.08)
- Static wheels (UI decorations) use `staticRotationDuration` and are unaffected by `degreesPerSecond`
