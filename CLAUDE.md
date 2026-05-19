# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## CRITICAL RULES

NEVER EXPLORE UNITY SCENE FILES (.unity) OR SCENE HIERARCHIES. ALWAYS WORK EXCLUSIVELY THROUGH C# SCRIPTS. ALL UNDERSTANDING OF OBJECT RELATIONSHIPS, COMPONENT REFERENCES, AND STRUCTURE MUST BE DERIVED FROM SCRIPTS ONLY.

## Coding Conventions

Prefer `internal` over `public` for C# members. The project lives in a single assembly, so `internal` is sufficient for anything not requiring cross-assembly access. Reserve `public` for members that genuinely need external visibility.

## Project Overview

**Age of Gods** is a Unity WebGL slot machine game (Unity 6000.3.9f1). It connects to a real-time backend via Socket.IO and is embedded in a React Native / web host via JavaScript bridge calls (`JSFunctCalls`).

## Build & Development

This is a Unity project — there is no CLI build command. Open the project in **Unity 6000.3.9f1** and use:
- **Build**: File → Build Settings → WebGL → Build
- **Play in Editor**: Use the Play button; `testToken` in `SocketController` is used in editor instead of the JS-injected auth token
- **Test socket locally**: Set `TestSocketURI` in `SocketController.cs` to your dev server URL

## Architecture

### Core Flow

```
SocketController (network) → GameManager (orchestration) → SlotController + UIManager
```

1. **`SocketController`** — Socket.IO connection lifecycle, auth token injection (WebGL gets token from host via `JSFunctCalls`, editor uses `testToken`). Listens on `game:init` and `result` events. Parses JSON into `Root` model via Newtonsoft.Json.
2. **`GameManager`** — Central coordinator. Owns all coroutine-based spin state machines: `SpinRoutine`, `FreeSpinRoutine`, `AutoSpinRoutine`. Calls into `SlotController`, `UIManager`, and `AudioController`.
3. **`SlotController`** — Manages the 5-reel slot matrix (a `List<SlotImage>`, each with `List<SlotIconView>`). Handles DOTween-based reel spinning, win line animations, golden icon overlays, and the wheel bonus popup.
4. **`UIManager`** — All popups (paytable, settings, free spin, disconnection, win, low balance). Single `currentPopup` tracker. Audio toggle wired via `Action<bool, string> ToggleAudio`.
5. **`AudioController`** — Multiple `AudioSource` channels: bg, button, win/lose, spin stop.

### Spin Lifecycle

`ExecuteSpin()` → `SpinRoutine()` coroutine:
1. `OnSpinStart()` — guard checks, button disable
2. `OnSpin()` — starts reel tweens, emits `request` to backend, awaits `isResultdone`, populates matrix, stops reels
3. `OnSpinEnd()` — line win animations, golden icon logic, wheel trigger check
4. Post-spin branches: free spin trigger → `FreeSpinRoutine`; wheel trigger → `SlotController.PlayWheel`; normal end → re-enable buttons

### WebGL ↔ Host Bridge

`JSFunctCalls` wraps `[DllImport("__Internal")]` P/Invoke calls for WebGL only:
- Host calls `ReceiveAuthToken(json)` to inject socket URL + token before connection
- Game sends `SendPostMessage(message)` for lifecycle events: `"OnEnter"`, `"OnExit"`, `"session_expired"`, `"error"`

### Wheel Bonus Feature

Three wheel sizes (small/medium/large) defined by `WheelBonus.wheelType`. `WheelView` uses collider-based stopping: spins freely via DOTween, then enables only the winning `WheelItem`'s `Collider2D` and waits for `OnSegmentHit`.

### Data Models (all in `SocketController.cs`)

- `Root` — top-level server response with `id` discriminator (`"initData"` or `"ResultData"`)
- `Payload` — spin result: `lineWins`, `goldenPositions`, `wheelBonus`, free spin state, `iswheeltrigger`
- `GameData` — bet list, line definitions
- `WheelBonus` — `wheelType`, `featureType` (`"freeSpin"` or `"multiplier"`), `featureValue`

### Slot Matrix Layout

- `slotMatrix`: 5 columns × N rows (N = 3 + level, where level 0–4 unlocks more rows)
- `allMatrix`: wider matrix used for initial shuffle display
- `WildMatrix`: overlay layer for wild symbol animations
- Level/ways display: level 0 = 243 ways, level 1 = 1024, level 2 = 3125, level 3 = 7776, level 4 = 16807

### Key Static State

- `GameManager.immediateStop` — set by stop-spin button to skip inter-reel delays
- `GameManager.winAnimComplete` — set by `UIManager` when win popup closes

## Custom Sprite Font (TMP Rich Text) — TODO: apply to other numeric text displays

The `numbers_gold` TMP sprite asset maps digits and punctuation to sprite indices:
- Digits `0`–`9` → `<sprite=0>` through `<sprite=9>`
- Full stop (`.`) → `<sprite=10>`
- Comma (`,`) → `<sprite=11>`

Use `SlotIconView.FormatWinAmount(double)` as the reference implementation when converting numeric values to this sprite-tag format. Other displays (e.g. total win, balance) may need the same treatment.

## Dependencies

- **DOTween** — all animation tweens (`DG.Tweening`)
- **Best.SocketIO** (Best HTTP) — Socket.IO client
- **Newtonsoft.Json** (Unity package) — JSON deserialization for server responses
- **TextMeshPro** — all UI text
- **Unity UI Extensions** (`com.unity.uiextensions`) — extended UI components
