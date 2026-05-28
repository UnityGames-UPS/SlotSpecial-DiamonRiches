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
  [SerializeField] internal Image specialAnimImage;
  [SerializeField] internal ImageAnimation specialAnim;

  [Header("Win Iteration")]
  [SerializeField] private float winPulsePeak = 1.05f;
  [SerializeField] private float winPulseDuration = 0.7f;

  internal bool IsSpecialSymbol => id == 9 || id == 10;

  [Header("Debug")]
  [SerializeField] private bool previewAnimations = false;

  Tween iconAnim;
  private Transform _cachedParent;
  private int _cachedSiblingIndex;
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
    if (specialAnimImage != null)
    {
      _specialAnimDefaultSize = specialAnimImage.rectTransform.sizeDelta;
      _specialAnimDefaultAnchoredPos = specialAnimImage.rectTransform.anchoredPosition3D;
    }
    if (specialAnim != null)
    {
      _specialAnimDefaultSpeed = specialAnim.AnimationSpeed;
      specialAnim.StopAnimation();
      specialAnim.ResetToFirstFrame();
    }
    if (specialAnimImage != null) specialAnimImage.color = new Color(1f, 1f, 1f, 0f);
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
    transform.SetParent(_cachedParent, worldPositionStays: true);
    transform.SetSiblingIndex(_cachedSiblingIndex);
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

    if (specialAnim != null)
    {
      specialAnim.StopAnimation();
      specialAnim.ResetToFirstFrame();
      specialAnim.AnimationSpeed = _specialAnimDefaultSpeed;
    }
    if (specialAnimImage != null)
    {
      specialAnimImage.color = new Color(1f, 1f, 1f, 0f);
      specialAnimImage.rectTransform.sizeDelta = _specialAnimDefaultSize;
      specialAnimImage.rectTransform.anchoredPosition3D = _specialAnimDefaultAnchoredPos;
    }
    iconImage.color = new Color(1f, 1f, 1f, 1f);
    iconImage.enabled = true;

    // Reset transform
    iconImage.transform.localScale = Vector3.one;
  }

  internal IEnumerator PlayWinIteration(SlotController controller, Transform overlayParent, bool showWinLineText, double winAmount, bool isSyncedPass)
  {
    Lift(overlayParent);

    if (showWinLineText)
    {
      WinAmountText.gameObject.SetActive(true);
      WinAmountText.text = TextFormatter.FormatSprite(winAmount, TextFormatter.GetSignificantDecimals(winAmount));
    }

    borderAnimation.gameObject.SetActive(true);
    borderAnimation.doLoopAnimation = false;
    borderAnimation.delayBetweenLoop = 0f;
    // borderAnimation.StartAnimation();

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

  IEnumerator PlayOverlaySequence(List<Sprite> sprites, float speed)
  {
    if (specialAnim == null || specialAnimImage == null)
    {
      yield return new WaitForSecondsRealtime(2f);
      yield break;
    }
    iconImage.enabled = false; // hide the static icon rendering behind the overlay while it plays
    specialAnimImage.color = new Color(1f, 1f, 1f, 1f);
    specialAnim.AnimationSpeed = speed;
    specialAnim.holdAtLastFrame = false;
    specialAnim.PlaySequence(sprites, loop: false);
    yield return new WaitUntil(() => !specialAnim.isplaying);
    specialAnim.StopAnimation();
    specialAnim.ResetToFirstFrame();
    specialAnimImage.color = new Color(1f, 1f, 1f, 0f); // hide overlay between iterations
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
    if (specialAnim != null)
    {
      specialAnim.StopAnimation();
      specialAnim.ResetToFirstFrame();
      specialAnim.AnimationSpeed = _specialAnimDefaultSpeed;
    }
    if (specialAnimImage != null)
    {
      specialAnimImage.color = new Color(1f, 1f, 1f, 0f);
      specialAnimImage.rectTransform.sizeDelta = _specialAnimDefaultSize;
      specialAnimImage.rectTransform.anchoredPosition3D = _specialAnimDefaultAnchoredPos;
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
    if (specialAnim != null)
    {
      specialAnim.StopAnimation();
      specialAnim.ResetToFirstFrame();
      specialAnim.AnimationSpeed = _specialAnimDefaultSpeed;
    }
    if (specialAnimImage != null)
    {
      specialAnimImage.color = new Color(1f, 1f, 1f, 0f);
      specialAnimImage.rectTransform.sizeDelta = _specialAnimDefaultSize;
      specialAnimImage.rectTransform.anchoredPosition3D = _specialAnimDefaultAnchoredPos;
    }
    iconImage.color = new Color(1f, 1f, 1f, 1f);
    iconImage.enabled = true;
  }
}
