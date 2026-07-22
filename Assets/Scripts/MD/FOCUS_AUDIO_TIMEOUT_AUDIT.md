# Focus / Visibility / Audio-Mute / Timeout / OnError — Audit Runbook

> **Who this is for:** an LLM auditing ONE of our ~70 slot games. Every game shares this
> lifecycle logic but with different class/method names. Your job: verify each of the 5 checks
> below against the reference contract, report **PASS / FAIL / MISSING** with a `file:line`, and
> where it's FAIL or MISSING, paste the corrected implementation.
>
> The reference game is **Diamond Riches**. All reference code below is copied verbatim from it.

---

## How to use this doc

1. Read all 5 checks first so you understand how the pieces interlock (they cooperate — a
   correct `OnFocusChanged` is useless if the visibility listener was never registered).
2. For each check: locate the equivalent code in the target game, compare against **What to look
   for** and the **Reference implementation**, then run through **Common failure modes**.
3. Emit a verdict per check using the template, then fill in the **Final report** table at the end.
4. If a piece is MISSING or FAIL, output the exact code to add/fix, adapted to the target game's
   names (see the naming map).

### Naming map — match by ROLE, not exact name

Names vary across the 70+ games. Map by responsibility, not spelling:

| Role | Diamond Riches name | Other games may call it |
|---|---|---|
| Socket lifecycle manager | `SocketController` | `SocketIOManager`, `NetworkManager` |
| UI / popup manager | `UIManager` | `UiManager`, `UIController` |
| Audio manager | `AudioController` | `AudioManager`, `SoundManager` |
| JS interop wrapper | `JSFunctCalls` | `JSManager`, `WebGLBridge` |
| Disconnect popup call | `DisconnectionPopup()` | `OpenDisconnectPopup()`, `ShowDisconnect()` |
| User sound flag | `isSound` | `soundOn`, `audioEnabled` |
| Mute-all method | `SetMuteAll(bool)` | `SetMute(bool)`, `PauseAllAudio()`/`ResumeAudio()` |

If a game splits mute into `PauseAllAudio()` / `ResumeAudio()` instead of a single
`SetMuteAll(bool)`, that is acceptable **only if** resume still respects the user's sound setting
(see Check 3's invariant).

### The 5 checks at a glance

1. **Visibility listener registration** — `.jslib` + `RegisterVisibilityListener` wrapper, called from `Awake`.
2. **`OnFocusChanged` callback** — `public`, routes to audio + socket.
3. **Audio mute/unmute honoring the runtime sound setting** — blur mutes, focus restores to user's choice.
4. **60-second background timeout** — closes the socket if the player stays away too long.
5. **`OnError(Error err)`** — session-expired vs generic error handling.
6. **Instant JS-side mute** — `.jslib` suspends the WebAudio context on blur so audio stops immediately.

---

## Check 1 — Visibility listener registration (JS `.jslib` + wrapper + Awake call)

### What to look for
Three linked pieces must all exist:
- A `RegisterVisibilityChangeListener` function inside the game's `CustomJsLib.jslib`
  (`mergeInto(LibraryManager.library, { ... })` block). *Note: `.jslib` is not a `.cs` file — if
  you cannot open it, report it as "unverified, ask a human to confirm the .jslib function exists".*
- A `[DllImport("__Internal")]` + `internal void RegisterVisibilityListener(string)` wrapper in
  the JS-interop script, guarded by `#if UNITY_WEBGL && !UNITY_EDITOR`.
- Exactly one call `jsFunctCalls.RegisterVisibilityListener(gameObject.name)` in the UI manager's
  `Awake()`. **The `OnFocusChanged` receiver (Check 2) MUST be a component on that same
  GameObject** — the JS layer calls `SendMessage(gameObjectName, 'OnFocusChanged', value)`.

### Reference implementation

`JSFunctCalls.cs`:
```csharp
[DllImport("__Internal")] private static extern void RegisterVisibilityChangeListener(string gameObjectName);

internal void RegisterVisibilityListener(string gameObjectName)
{
#if UNITY_WEBGL && !UNITY_EDITOR
    Debug.Log($"[JS] Registering visibility change listener on '{gameObjectName}'");
    RegisterVisibilityChangeListener(gameObjectName);
#else
    Debug.Log("[JS] Visibility listener not registered (editor mode)");
#endif
}
```

`UIManager.Awake()`:
```csharp
if (jsFunctCalls != null)
    jsFunctCalls.RegisterVisibilityListener(gameObject.name);
```

The `.jslib` function (contract is fixed — callback name `OnFocusChanged`, values `'1'`=focused /
`'0'`=blurred; do not change them):
```js
RegisterVisibilityChangeListener: function(gameObjectNamePtr) {
  var gameObjectName = UTF8ToString(gameObjectNamePtr);

  // See Check 6 — instant JS-side mute. Kills audio before Unity's throttled loop.
  function setUnityAudioSuspended(suspended) {
      try {
          var wa = (typeof WEBAudio !== 'undefined') ? WEBAudio
                 : (typeof Module !== 'undefined' && Module.WEBAudio) ? Module.WEBAudio
                 : null;
          if (!wa || !wa.audioContext) return;
          if (suspended) {
              if (wa.audioContext.state === 'running') wa.audioContext.suspend();
          } else {
              if (wa.audioContext.state === 'suspended') wa.audioContext.resume();
          }
      } catch (err) { console.warn('[JS] Unity audio suspend/resume failed:', err); }
  }

  function sendFocusToUnity(focused) {
      setUnityAudioSuspended(!focused);
      try {
          var value = focused ? '1' : '0';
          if (typeof SendMessage === 'function') {
              SendMessage(gameObjectName, 'OnFocusChanged', value);
          } else if (typeof unityInstance !== 'undefined' && unityInstance && unityInstance.SendMessage) {
              unityInstance.SendMessage(gameObjectName, 'OnFocusChanged', value);
          }
      } catch (err) {
          console.error('[JS] Error sending focus message to Unity:', err);
      }
  }

  window._unityVisibilityCallback = function() {
      var hidden = document.hidden || document.webkitHidden;
      sendFocusToUnity(!hidden);
  };
  window._unityWindowBlurCallback  = function() { sendFocusToUnity(false); };
  window._unityWindowFocusCallback = function() { sendFocusToUnity(true); };

  // Remove before re-adding to avoid duplicates
  document.removeEventListener('visibilitychange',       window._unityVisibilityCallback);
  document.removeEventListener('webkitvisibilitychange', window._unityVisibilityCallback);
  window.removeEventListener('blur',  window._unityWindowBlurCallback);
  window.removeEventListener('focus', window._unityWindowFocusCallback);

  document.addEventListener('visibilitychange',       window._unityVisibilityCallback);
  document.addEventListener('webkitvisibilitychange', window._unityVisibilityCallback);
  window.addEventListener('blur',  window._unityWindowBlurCallback);
  window.addEventListener('focus', window._unityWindowFocusCallback);
},
```

### Common failure modes
- `RegisterVisibilityListener` never called from `Awake()` → listener never wired; focus changes never reach Unity.
- Registered on a GameObject whose name differs from where `OnFocusChanged` lives → `SendMessage` silently no-ops.
- DllImport / call not guarded by `#if UNITY_WEBGL && !UNITY_EDITOR` → editor/native build fails to compile or link.
- `.jslib` function missing entirely while the C# wrapper exists → runtime "function not found".

---

## Check 2 — `OnFocusChanged(string value)` callback

### What to look for
A **`public`** method named exactly `OnFocusChanged(string value)` on the same GameObject passed
in Check 1. It must (a) parse `"1"` → focused, (b) drive audio, (c) notify the socket manager.

### Reference implementation
`UIManager.cs`:
```csharp
public void OnFocusChanged(string value)
{
    bool focused = value == "1";
    Debug.Log("UNITY FOCUS CHANGED: " + value + " (focused: " + focused + ")");
    audioController?.SetMuteAll(focused ? !isSound : true);
    socketController?.HandleFocusChange(focused);
}
```

### Common failure modes
- Declared `internal`/`private` instead of **`public`** → Unity's `SendMessage` cannot invoke it (this is the single most common bug). Reserve `internal` for everything else per project convention, but this method must be `public`.
- Method renamed → JS contract expects the literal name `OnFocusChanged`.
- Audio muted unconditionally on focus (`SetMuteAll(false)` on focus) instead of `!isSound` → un-mutes a game the user chose to keep silent (see Check 3 invariant).
- Missing the `socketController?.HandleFocusChange(focused)` call → audio toggles but the 60s timeout (Check 4) never arms.

---

## Check 3 — Audio mute/unmute honoring the runtime sound setting

### What to look for
Two paths mute audio, and **both must restore to the user's chosen setting on focus**, never
force-unmute:
- **WebGL path** — via `OnFocusChanged` → `SetMuteAll(focused ? !isSound : true)` (Check 2).
- **Editor/native path** — `AudioController.OnApplicationFocus(bool)` mutes on blur, restores to
  `userMuted` on focus.

And the user's sound toggle must feed `userMuted`:
- `isSound` is the user's runtime flag; `SetSound(bool)` updates it and calls `ToggleAudio?.Invoke(!isSound)`.
- `GameManager` wires `uIManager.ToggleAudio = audioController.SetMuteAll`.
- `SetMuteAll(bool mute)` stores `userMuted = mute` before muting sources.

### The invariant (verify this explicitly)
> **Regaining focus must never un-mute a game the user muted.**
> On focus, the WebGL path restores to `!isSound` and the native path restores to `userMuted` —
> both equal the user's current choice. On blur, both force `mute = true` regardless of setting.

### Reference implementation
`AudioController.cs`:
```csharp
internal void SetMuteAll(bool mute)
{
    userMuted = mute;
    foreach (var entry in entries) entry.source.mute = mute;
}

private void OnApplicationFocus(bool focus)
{
    foreach (var entry in entries)
    {
        entry.source.mute = focus ? userMuted : true;
    }
}
```

`UIManager.cs` (user toggle):
```csharp
private void SetSound(bool soundOn)
{
    isSound = soundOn;
    // SetMuteAll(true) mutes; isSound==true means audio plays, so invoke with !isSound.
    ToggleAudio?.Invoke(!isSound);
    ApplySoundButtonVisibility();
}
```

`GameManager.cs` (wiring):
```csharp
uIManager.ToggleAudio = audioController.SetMuteAll;
```

### Common failure modes
- `OnApplicationFocus` restores with `false` (hard un-mute) instead of `userMuted` → breaks the invariant.
- `SetMuteAll` mutes sources but forgets to store `userMuted` → the native focus path later restores to a stale value.
- `ToggleAudio` never wired in the manager (`GameManager`/bootstrap) → the sound button does nothing.
- Polarity inversion: passing `isSound` instead of `!isSound` to a *mute* method → button is backwards.
- Game has no `OnApplicationFocus` at all → editor/native builds don't mute on blur (WebGL still works via `OnFocusChanged`; flag as partial FAIL if native/editor focus muting is expected).

---

## Check 4 — 60-second background timeout

### What to look for
On the socket manager: fields, a `HandleFocusChange(bool)` entry point (called from Check 2), and
a `FocusTimeoutCheck` coroutine that closes the socket after `maxBackgroundTime` (60s) away.

### Reference implementation
`SocketController.cs` — fields:
```csharp
private bool hasFocus = true;
private float focusLostTime = 0f;
private Coroutine focusCheckRoutine;
private float maxBackgroundTime = 60f;
private bool isExiting = false;
private bool isBeingDestroyed = false;
```

`HandleFocusChange`:
```csharp
internal void HandleFocusChange(bool focus)
{
    hasFocus = focus;

    if (!focus)
    {
        focusLostTime = Time.time;
        if (focusCheckRoutine == null && !isExiting && !isBeingDestroyed)
            focusCheckRoutine = StartCoroutine(FocusTimeoutCheck());
    }
    else
    {
        if (focusCheckRoutine != null)
        {
            StopCoroutine(focusCheckRoutine);
            focusCheckRoutine = null;
        }
    }
}
```

`FocusTimeoutCheck`:
```csharp
private IEnumerator FocusTimeoutCheck()
{
    while (!hasFocus && !isExiting && !isBeingDestroyed)
    {
        if (Time.time - focusLostTime >= maxBackgroundTime)
        {
            Debug.LogWarning("[SOCKET] Background timeout — closing connection");
            isConnected = false;
            ResetPingRoutine();

            if (manager != null)
            {
                try { manager.Close(); }
                catch (Exception e) { Debug.LogWarning($"[SOCKET] Focus close error: {e.Message}"); }
            }

            UiManager.DisconnectionPopup();
            focusCheckRoutine = null;
            yield break;
        }

        yield return new WaitForSecondsRealtime(1f);
    }

    focusCheckRoutine = null;
}
```

> `ResetPingRoutine()` / `isBeingDestroyed` (set in `OnDestroy`) / `isExiting` (set when closing)
> are Diamond Riches names — map to the target game's ping-stop and lifecycle guards. If the game
> has no ping routine, drop that line.

### Common failure modes
- `WaitForSecondsRealtime` replaced with `WaitForSeconds` → timer stalls when the tab is backgrounded (`Time.timeScale`/frame ticks may pause), so the 60s never elapses. **Must be Realtime.**
- No guard `focusCheckRoutine == null` before `StartCoroutine` → duplicate coroutines on rapid blur/focus.
- Routine not stopped on regained focus → socket closes even though the player came back in time.
- `manager.Close()` not wrapped in try/catch → an exception during teardown leaks the coroutine handle.
- Ping/heartbeat routine not stopped on timeout → phantom reconnection attempts after disconnect.
- Missing `isExiting`/`isBeingDestroyed` guards → coroutine runs during scene teardown and touches destroyed objects.

---

## Check 5 — `OnError(Error err)`

### What to look for
An error handler registered on the socket and differentiating **session-expired** from generic
errors, with WebGL-guarded messages to the JS host.

### Reference implementation
`SocketController.cs` — registration (in socket setup):
```csharp
GameSocket.On<Error>(SocketIOEventTypes.Error, OnError);
```

Handler:
```csharp
private void OnError(Error err)
{
    Debug.LogError("[ERROR] Socket error: " + err);
    if (!string.IsNullOrEmpty(err.message) && err.message.Contains("Session expired"))
    {
        Debug.LogWarning("Session expired detected");
        OnDisconnected();
#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager.SendCustomMessage("session_expired");
#endif
    }
    else
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager.SendCustomMessage("error");
#endif
    }
}
```

### Common failure modes
- `OnError` never registered via `GameSocket.On<Error>(SocketIOEventTypes.Error, OnError)` → server errors silently ignored.
- No `"Session expired"` branch → expired sessions don't notify the host (`session_expired`) and the player sees a generic error.
- `SendCustomMessage` calls not wrapped in `#if UNITY_WEBGL && !UNITY_EDITOR` → editor/native build breaks.
- Session-expired branch omits `OnDisconnected()` → UI never shows the disconnect state.
- Null `err.message` not guarded → `NullReferenceException` on `.Contains`.

---

## Check 6 — Instant JS-side mute (WebAudio suspend in `.jslib`)

### What to look for
Inside `RegisterVisibilityChangeListener` in the game's `CustomJsLib.jslib`, the focus dispatcher
(`sendFocusToUnity`) must suspend/resume Unity's WebAudio context **directly in JS**, as its first
action, before the `SendMessage` handoff.

> **Why this exists:** a hidden browser tab or a backgrounded ReactNativeWebView throttles Unity's
> main loop (rAF pauses; timers clamp to ~1s+). If muting only travels through
> `SendMessage → C# OnFocusChanged → SetMuteAll`, the audio keeps playing for ~3s until the
> throttled loop processes it. Also, Unity's native `OnApplicationFocus` does **not** fire inside a
> WebView, so the C# native path can't cover APK. Suspending the AudioContext in the JS event
> handler stops sound instantly on every platform. This is not a `.cs` file — if you can't open it,
> mark UNVERIFIED and ask a human to confirm.

### Reference implementation
`CustomJsLib.jslib` — helper + first line of the dispatcher:
```js
function setUnityAudioSuspended(suspended) {
    try {
        var wa = (typeof WEBAudio !== 'undefined') ? WEBAudio
               : (typeof Module !== 'undefined' && Module.WEBAudio) ? Module.WEBAudio
               : null;
        if (!wa || !wa.audioContext) return;
        if (suspended) {
            if (wa.audioContext.state === 'running') wa.audioContext.suspend();
        } else {
            if (wa.audioContext.state === 'suspended') wa.audioContext.resume();
        }
    } catch (err) { console.warn('[JS] Unity audio suspend/resume failed:', err); }
}

function sendFocusToUnity(focused) {
    setUnityAudioSuspended(!focused);   // <-- instant; runs before Unity's throttled loop
    // ... existing SendMessage(gameObjectName, 'OnFocusChanged', focused ? '1' : '0') ...
}
```

### Why it's safe with the user's sound setting
`suspend()`/`resume()` only pause/unpause the whole context — they never touch the per-source
`.mute` flags Unity manages (Check 3). A game the user muted stays muted after `resume()`. The late
C# `OnFocusChanged` then reasserts the correct mute state; consistent, just no longer audible during
the gap. **No C# change is required for this check** — it is purely additive in the `.jslib`.

### Common failure modes
- `sendFocusToUnity` only calls `SendMessage` (no `setUnityAudioSuspended`) → 3s of audio on tab switch / APK background (the original bug this check exists to catch).
- `suspend`/`resume` polarity swapped (`setUnityAudioSuspended(focused)`) → mutes on return, plays on leave.
- Missing the `Module.WEBAudio` fallback and `WEBAudio` isn't global in that build → helper silently no-ops; confirm the audio actually stops in a real build.
- No `state === 'running'` / `'suspended'` guard → redundant suspend/resume calls (harmless, but noisy; guards preferred).

---

## Verdict template (use per check)

```
Check N — <name>
Status: PASS | FAIL | MISSING | UNVERIFIED
Location: <file>:<line>   (or "not found")
Notes: <what matched / what's wrong>
Fix (if FAIL/MISSING): <adapted code block>
```

## Final report

| # | Check | Status | Location | Action taken |
|---|---|---|---|---|
| 1 | Visibility listener registration (.jslib + wrapper + Awake) | | | |
| 2 | `OnFocusChanged` public callback | | | |
| 3 | Audio mute/unmute honors user sound setting (both paths + invariant) | | | |
| 4 | 60s background timeout (`WaitForSecondsRealtime`) | | | |
| 5 | `OnError` (session-expired vs generic) | | | |
| 6 | Instant JS-side mute (WebAudio suspend in `.jslib`) | | | |

---

*Reference: [FEATURE_PORTING_GUIDE.md](FEATURE_PORTING_GUIDE.md) has additional step-by-step porting
detail for the browser-focus feature. This doc is the verification/audit counterpart and additionally
covers the audio/sound-setting interaction and the `OnError` handler.*
