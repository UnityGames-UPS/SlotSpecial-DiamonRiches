using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotIconView : MonoBehaviour
{
  [Header("required fields")]
  [SerializeField] internal int pos;
  [SerializeField] internal int id = -1;
  [SerializeField] internal Image iconImage;
  [SerializeField] internal ImageAnimation bgImage;
  [SerializeField] internal ImageAnimation borderAnimation;
  [SerializeField] internal ImageAnimation activeanimation;
  [SerializeField] private TMP_Text WinAmountText;

  [Header("Special Symbol Animation Overlay")]
  [SerializeField] internal Image AnimLayerImage;
  [SerializeField] internal ImageAnimation AnimLayerIA;

  [Header("Win Iteration")]
  [SerializeField] private float winPulsePeak = 1.05f;
  [SerializeField] private float winPulseDuration = 0.7f;

  internal bool IsSpecialSymbol => id == 9 || id == 10;

  [Header("Debug")]
  [SerializeField] private bool previewAnimations = false;

  Tween iconAnim;
  private Transform _cachedParent;
  private int _cachedSiblingIndex;
  private Vector3 _cachedLocalPosition;
  private Vector2 _specialAnimDefaultSize;
  private Vector3 _specialAnimDefaultAnchoredPos;
  private Vector2 _bgDefaultSize;
  private Vector3 _bgDefaultAnchoredPos;
  private float _bgDefaultSpeed;
  private float _specialAnimDefaultSpeed;

  static readonly Vector2 LadySpecialAnimSize = new Vector2(350.1554f, 285.9313f);
  static readonly Vector3 LadySpecialAnimPos = new Vector3(0f, 11.0395f, 0f);
  static readonly Vector2 WildSpecialAnimSize = new Vector2(320f, 320f);
  static readonly Vector3 WildSpecialAnimPos = Vector3.zero;
  static readonly Vector2 WildBgSize = new Vector2(320f, 328.472f);
  static readonly Vector3 WildBgPos = Vector3.zero;

  void Awake()
  {
    _cachedParent = transform.parent;
    _cachedSiblingIndex = transform.GetSiblingIndex();
    _cachedLocalPosition = transform.localPosition;
    if (AnimLayerImage != null)
    {
      _specialAnimDefaultSize = AnimLayerImage.rectTransform.sizeDelta;
      _specialAnimDefaultAnchoredPos = AnimLayerImage.rectTransform.anchoredPosition3D;
    }
    if (AnimLayerIA != null)
    {
      _specialAnimDefaultSpeed = AnimLayerIA.AnimationSpeed;
      AnimLayerIA.StopAnimation();
      AnimLayerIA.ResetToFirstFrame();
    }
    if (AnimLayerImage != null) AnimLayerImage.color = new Color(1f, 1f, 1f, 0f);
    if (bgImage != null)
    {
      _bgDefaultSpeed = bgImage.AnimationSpeed;
      var bgRt = bgImage.GetComponent<RectTransform>();
      if (bgRt != null)
      {
        _bgDefaultSize = bgRt.sizeDelta;
        _bgDefaultAnchoredPos = bgRt.anchoredPosition3D;
      }
    }
    if(borderAnimation.gameObject.activeInHierarchy)
      borderAnimation.gameObject.SetActive(false);
  }

  void Start()
  {
    // if (!previewAnimations) return;

    // bgImage.gameObject.SetActive(true);
    // bgImage.StartAnimation();

    // borderAnimation.gameObject.SetActive(true);
    // borderAnimation.StartAnimation();
  }

  internal void Lift(Transform overlayParent)
  {
    transform.SetParent(overlayParent, worldPositionStays: true);
  }

  internal void Drop()
  {
    // worldPositionStays:false + explicit localPosition restore: the symbol's intended slot Y is
    // the prefab local Y captured at Awake, not whatever world position the overlay parent held
    // at the moment of Drop. World-stays drift was leaving symbols at wrong Y when a new spin
    // started mid win-loop.
    transform.SetParent(_cachedParent, worldPositionStays: false);
    transform.SetSiblingIndex(_cachedSiblingIndex);
    transform.localPosition = _cachedLocalPosition;
  }

  internal void SetIcon(Sprite image, int ID)
  {
    iconImage.sprite = image;
    id = ID;
  }

  internal void Reset()
  {
    Drop();

    // Kill any running tween
    iconAnim?.Kill();
    iconAnim = null;

    // Reset visuals
    borderAnimation.StopAnimation();
    borderAnimation.doLoopAnimation = false;
    borderAnimation.gameObject.SetActive(false);
    if (WinAmountText.gameObject.activeSelf)
    {
      WinAmountText.gameObject.SetActive(false);
      WinAmountText.text = "";
    }

    // Stop animations safely
    bgImage.StopAnimation();
    bgImage.ResetToFirstFrame();
    bgImage.doLoopAnimation = false;
    bgImage.AnimationSpeed = _bgDefaultSpeed;
    {
      var bgRt = bgImage.GetComponent<RectTransform>();
      if (bgRt != null)
      {
        bgRt.sizeDelta = _bgDefaultSize;
        bgRt.anchoredPosition3D = _bgDefaultAnchoredPos;
      }
    }
    activeanimation.StopAnimation();

    if (AnimLayerIA != null)
    {
      AnimLayerIA.StopAnimation();
      AnimLayerIA.ResetToFirstFrame();
      AnimLayerIA.AnimationSpeed = _specialAnimDefaultSpeed;
    }
    if (AnimLayerImage != null)
    {
      AnimLayerImage.color = new Color(1f, 1f, 1f, 0f);
      AnimLayerImage.rectTransform.sizeDelta = _specialAnimDefaultSize;
      AnimLayerImage.rectTransform.anchoredPosition3D = _specialAnimDefaultAnchoredPos;
    }
    iconImage.color = new Color(1f, 1f, 1f, 1f);
    iconImage.enabled = true;

    // Reset transform
    iconImage.transform.localScale = Vector3.one;
  }

  internal IEnumerator PlayWinIteration(SlotController controller, Transform overlayParent, bool showWinLineText, double winAmount, bool isSyncedPass)
  {
    controller.RegisterAnimatingIcon(this);
    Lift(overlayParent);

    if (showWinLineText)
    {
      WinAmountText.gameObject.SetActive(true);
      WinAmountText.text = TextFormatter.FormatSprite(winAmount, TextFormatter.GetSignificantDecimals(winAmount));
    }

    borderAnimation.gameObject.SetActive(true);
    borderAnimation.doLoopAnimation = true;
    borderAnimation.delayBetweenLoop = 0f;
    borderAnimation.StartAnimation();

    // iconAnim?.Kill();
    // iconImage.transform.localScale = Vector3.one;
    // float halfPulse = winPulseDuration * 0.5f;
    // Sequence pulseSeq = DOTween.Sequence();
    // pulseSeq.Append(iconImage.transform.DOScale(Vector3.one * winPulsePeak, halfPulse).SetEase(Ease.OutSine));
    // pulseSeq.Append(iconImage.transform.DOScale(Vector3.one, halfPulse).SetEase(Ease.InSine));
    // pulseSeq.OnKill(() => iconAnim = null);
    // iconAnim = pulseSeq;

    // yield return new WaitUntil(() =>
    // {
    //   // bool pulseDone = pulseSeq == null || !pulseSeq.IsActive() || !pulseSeq.IsPlaying();
    //   // bool bgDone = !bgImage.isplaying;
    //   // bool specialDone = !IsSpecialSymbol || specialAnim == null || !specialAnim.isplaying;
    //   bool borderDone = !IsSpecialSymbol || !borderAnimation.isplaying;
    //   return borderDone; //&& bgDone && specialDone && borderDone;
    // });

    var winAnim = controller.GetWinAnim(id);

    if (isSyncedPass)
    {
      if (winAnim != null && winAnim.syncedSprites != null && winAnim.syncedSprites.Count > 0)
        yield return PlayOverlaySequence(winAnim.syncedSprites, winAnim.syncedSpeed);
      else
        yield return new WaitForSecondsRealtime(2f); // fallback: ids without sequences yet (2,3,…)
    }
    else // loop pass
    {
      if (winAnim != null && winAnim.loopUsesPulse)
        yield return PlayPulse();                          // gold bars id 0,1
      else if (winAnim != null && winAnim.loopSprites != null && winAnim.loopSprites.Count > 0)
        yield return PlayOverlaySequence(winAnim.loopSprites, winAnim.loopSpeed);
      else
        yield return new WaitForSecondsRealtime(2f);       // fallback
    }
  }

  // Free-spin (scatter) symbol animation, played in place on reel stop (no lift / no dark overlay).
  internal IEnumerator PlayFreeSpinAnim(List<Sprite> sprites, float speed)
  {
    if (sprites == null || sprites.Count == 0) yield break;
    yield return PlayOverlaySequence(sprites, speed);
  }

  // Single-diamond idle: one-shot overlay at default AnimLayer size. Lifts to overlayParent so
  // it stays above siblings, drops back when finished. Non-blocking from the caller's perspective —
  // they StartCoroutine without yielding.
  internal IEnumerator PlayDiamondIdleAnim(SlotController controller, Transform overlayParent, List<Sprite> sprites, float speed)
  {
    if (sprites == null || sprites.Count == 0) yield break;
    controller.RegisterAnimatingIcon(this);
    Lift(overlayParent);
    yield return PlayOverlaySequence(sprites, speed);
    Drop();
  }

  // Multi-diamond trigger: looped overlay at +100 width/height. Yields forever — caller fires it
  // and forgets; teardown happens via StopAnim/Reset on next spin (which restores
  // _specialAnimDefaultSize and Drops the icon).
  internal IEnumerator PlayDiamondTriggeredLoop(SlotController controller, Transform overlayParent, List<Sprite> sprites, float speed)
  {
    if (sprites == null || sprites.Count == 0 || AnimLayerIA == null || AnimLayerImage == null) yield break;
    controller.RegisterAnimatingIcon(this);
    Lift(overlayParent);
    AnimLayerImage.rectTransform.sizeDelta = _specialAnimDefaultSize + new Vector2(100f, 100f);
    iconImage.enabled = false;
    AnimLayerImage.color = new Color(1f, 1f, 1f, 1f);
    AnimLayerIA.AnimationSpeed = speed;
    AnimLayerIA.holdAtLastFrame = false;
    AnimLayerIA.gameObject.SetActive(true);
    AnimLayerIA.PlaySequence(sprites, loop: true);
    // Keep coroutine alive so the icon stays registered in animatingIcons until StopIconAnimation
    // is called on the next spin (which invokes StopAnim and unwinds the +100 size).
    while (AnimLayerIA != null && AnimLayerIA.isplaying) yield return null;
  }

  IEnumerator PlayOverlaySequence(List<Sprite> sprites, float speed)
  {
    if (AnimLayerIA == null || AnimLayerImage == null)
    {
      yield return new WaitForSecondsRealtime(2f);
      yield break;
    }
    iconImage.enabled = false; // hide the static icon rendering behind the overlay while it plays
    AnimLayerImage.color = new Color(1f, 1f, 1f, 1f);
    AnimLayerIA.AnimationSpeed = speed;
    AnimLayerIA.holdAtLastFrame = false;
    AnimLayerIA.gameObject.SetActive(true);
    AnimLayerIA.PlaySequence(sprites, loop: false);
    yield return new WaitUntil(() => !AnimLayerIA.isplaying);
    AnimLayerIA.StopAnimation();
    AnimLayerIA.ResetToFirstFrame();
    AnimLayerImage.color = new Color(1f, 1f, 1f, 0f); // hide overlay between iterations
    AnimLayerIA.gameObject.SetActive(false);
    iconImage.enabled = true;
  }

  IEnumerator PlayPulse()
  {
    iconAnim?.Kill();
    iconImage.transform.localScale = Vector3.one;
    float halfPulse = winPulseDuration * 0.5f;
    Sequence pulseSeq = DOTween.Sequence();
    pulseSeq.Append(iconImage.transform.DOScale(Vector3.one * winPulsePeak, halfPulse).SetEase(Ease.OutSine));
    pulseSeq.Append(iconImage.transform.DOScale(Vector3.one, halfPulse).SetEase(Ease.InSine));
    pulseSeq.OnKill(() => iconAnim = null);
    iconAnim = pulseSeq;
    yield return pulseSeq.WaitForCompletion();
  }

  internal void ResetLineAnim()
  {
    Drop();
    iconAnim?.Kill();
    iconAnim = null;
    iconImage.transform.localScale = Vector3.one;
    borderAnimation.doLoopAnimation = false;
    borderAnimation.StopAnimation();
    borderAnimation.gameObject.SetActive(false);
    bgImage.StopAnimation();
    bgImage.ResetToFirstFrame();
    bgImage.doLoopAnimation = false;
    bgImage.AnimationSpeed = _bgDefaultSpeed;
    {
      var bgRt = bgImage.GetComponent<RectTransform>();
      if (bgRt != null)
      {
        bgRt.sizeDelta = _bgDefaultSize;
        bgRt.anchoredPosition3D = _bgDefaultAnchoredPos;
      }
    }
    if (AnimLayerIA != null)
    {
      AnimLayerIA.StopAnimation();
      AnimLayerIA.ResetToFirstFrame();
      AnimLayerIA.AnimationSpeed = _specialAnimDefaultSpeed;
    }
    if (AnimLayerImage != null)
    {
      AnimLayerImage.color = new Color(1f, 1f, 1f, 0f);
      AnimLayerImage.rectTransform.sizeDelta = _specialAnimDefaultSize;
      AnimLayerImage.rectTransform.anchoredPosition3D = _specialAnimDefaultAnchoredPos;
    }
    iconImage.color = new Color(1f, 1f, 1f, 1f);
    iconImage.enabled = true;
    if (WinAmountText.gameObject.activeSelf)
    {
      WinAmountText.gameObject.SetActive(false);
      WinAmountText.text = "";
    }
  }

  internal void StopAnim()
  {
    Drop();
    iconAnim?.Kill();
    iconImage.transform.localScale = Vector3.one;
    borderAnimation.StopAnimation();
    borderAnimation.gameObject.SetActive(false);
    bgImage.StopAnimation();
    bgImage.ResetToFirstFrame();
    bgImage.AnimationSpeed = _bgDefaultSpeed;
    {
      var bgRt = bgImage.GetComponent<RectTransform>();
      if (bgRt != null)
      {
        bgRt.sizeDelta = _bgDefaultSize;
        bgRt.anchoredPosition3D = _bgDefaultAnchoredPos;
      }
    }
    activeanimation.StopAnimation();
    if (AnimLayerIA != null)
    {
      AnimLayerIA.StopAnimation();
      AnimLayerIA.ResetToFirstFrame();
      AnimLayerIA.AnimationSpeed = _specialAnimDefaultSpeed;
    }
    if (AnimLayerImage != null)
    {
      AnimLayerImage.color = new Color(1f, 1f, 1f, 0f);
      AnimLayerImage.rectTransform.sizeDelta = _specialAnimDefaultSize;
      AnimLayerImage.rectTransform.anchoredPosition3D = _specialAnimDefaultAnchoredPos;
    }
    iconImage.color = new Color(1f, 1f, 1f, 1f);
    iconImage.enabled = true;
  }
}
