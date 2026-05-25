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
  [SerializeField] internal Image goldenIconImage;
  [SerializeField] internal ImageAnimation bgImage;
  [SerializeField] internal Image goldenBgImage;
  [SerializeField] internal Image Dark;
  [SerializeField] internal bool isGold;
  [SerializeField] internal ImageAnimation borderAnimation;
  [SerializeField] internal ImageAnimation activeanimation;
  [SerializeField] private GameObject WinAmountBox;
  [SerializeField] private TMP_Text WinAmountText;
  [SerializeField] private Image DarkMaskImage;
  [SerializeField] private Sprite SpecialSymbolMaskSprite;

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
  internal void SetGoldIcon(Sprite image)
  {
    isGold = true;
    goldenBgImage.gameObject.SetActive(true);
    if (id == 10) return; // visual handled by WildIconView overlay layer
    goldenIconImage.color = Color.white;
    goldenIconImage.sprite = image;
    goldenIconImage.rectTransform.sizeDelta = id switch
    {
      9 => new Vector2(225f, 195f),
      _ => new Vector2(175f, 150f)
    };
    goldenIconImage.gameObject.SetActive(true);
    AnimateDarkImage(false);
  }

  internal void AnimateGoldIcon(bool show = false)
  {
    goldenIconImage.DOFade(show ? 1 : 0, 0.5f);
  }

  internal void AnimateDarkImage(bool show = false)
  {
    float targetAlpha = show ? 215f / 255f : 0f;
    
    Dark.DOFade(targetAlpha, 0.5f);
    if (id == 9)
    {
      DarkMaskImage.rectTransform.sizeDelta = new Vector2(225f, 195f);
      DarkMaskImage.sprite = SpecialSymbolMaskSprite;
    }
    else
    {
      DarkMaskImage.rectTransform.sizeDelta = new Vector2(175f, 150f);
      DarkMaskImage.sprite = null;
    }
  } 

  internal void Reset()
  {
    Drop();

    // Kill any running tween
    iconAnim?.Kill();
    iconAnim = null;

    // Reset gold state
    if (isGold)
    {
      AnimateGoldIcon(true);
      DOVirtual.DelayedCall(0.7f, () =>
      {
        goldenBgImage.gameObject.SetActive(false);
        goldenIconImage.gameObject.SetActive(false);
      });
      isGold = false;
    }
    else
    {
      goldenIconImage.color = Color.white;
      goldenBgImage.gameObject.SetActive(false);
      goldenIconImage.gameObject.SetActive(false);
    }

    // Reset visuals
    borderAnimation.StopAnimation();
    borderAnimation.doLoopAnimation = false;
    borderAnimation.gameObject.SetActive(false);
    // Instant dark clear: AnimateDarkImage uses a 0.5s DOFade, which would otherwise
    // overlap the next reel-spin tween and visually black out the reels.
    Dark.DOKill();
    var dc = Dark.color;
    Dark.color = new Color(dc.r, dc.g, dc.b, 0f);
    if (WinAmountBox.activeSelf)
    {
      WinAmountBox.SetActive(false);
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

    // Reset transform
    iconImage.transform.localScale = Vector3.one;
  }

  internal IEnumerator PlayWinIteration(SlotController controller, Transform overlayParent, bool showWinLineText, double winAmount)
  {
    Lift(overlayParent);
    AnimateDarkImage(false);

    if (showWinLineText)
    {
      WinAmountBox.SetActive(true);
      WinAmountText.text = TextFormatter.FormatSprite(winAmount, TextFormatter.GetSignificantDecimals(winAmount));
    }

    bgImage.StopAnimation();
    bgImage.doLoopAnimation = false;
    bgImage.delayBetweenLoop = 0f;

    borderAnimation.gameObject.SetActive(true);
    borderAnimation.doLoopAnimation = false;
    borderAnimation.delayBetweenLoop = 0f;
    borderAnimation.StopAnimation();

    if (IsSpecialSymbol)
    {
      borderAnimation.StartAnimation();

      iconImage.color = new Color(1f, 1f, 1f, 0f);

      var iconSeq = id == 10 ? controller.GetWildIconSprites() : controller.GetLadyIconSprites();
      if (specialAnim != null && iconSeq != null && iconSeq.Count > 0)
      {
        if (id == 9)
        {
          specialAnimImage.rectTransform.sizeDelta = LadySpecialAnimSize;
          specialAnimImage.rectTransform.anchoredPosition3D = LadySpecialAnimPos;
          specialAnim.AnimationSpeed = controller.GetLadyIconSpeed();
        }
        else
        {
          specialAnimImage.rectTransform.sizeDelta = WildSpecialAnimSize;
          specialAnimImage.rectTransform.anchoredPosition3D = WildSpecialAnimPos;
          specialAnim.AnimationSpeed = controller.GetWildIconSpeed();
        }
        specialAnimImage.color = new Color(1f, 1f, 1f, 1f);
        specialAnim.delayBetweenLoop = 0f;
        specialAnim.PlaySequence(iconSeq, loop: false);
      }

      if (id == 10)
      {
        var wildBg = controller.GetWildBgSprites();
        if (wildBg != null && wildBg.Count > 0)
        {
          var bgRt = bgImage.GetComponent<RectTransform>();
          if (bgRt != null)
          {
            bgRt.sizeDelta = WildBgSize;
            bgRt.anchoredPosition3D = WildBgPos;
          }
          bgImage.AnimationSpeed = controller.GetWildBgSpeed();
          bgImage.PlaySequence(wildBg, loop: false);
        }
      }
      else
      {
        if (!isGold) bgImage.StartAnimation();
      }
    }
    else if (!isGold)
    {
      bgImage.StartAnimation();
    }

    iconAnim?.Kill();
    iconImage.transform.localScale = Vector3.one;
    float halfPulse = winPulseDuration * 0.5f;
    Sequence pulseSeq = DOTween.Sequence();
    pulseSeq.Append(iconImage.transform.DOScale(Vector3.one * winPulsePeak, halfPulse).SetEase(Ease.OutSine));
    pulseSeq.Append(iconImage.transform.DOScale(Vector3.one, halfPulse).SetEase(Ease.InSine));
    pulseSeq.OnKill(() => iconAnim = null);
    iconAnim = pulseSeq;

    yield return new WaitUntil(() =>
    {
      bool pulseDone = pulseSeq == null || !pulseSeq.IsActive() || !pulseSeq.IsPlaying();
      bool bgDone = isGold || !bgImage.isplaying;
      bool specialDone = !IsSpecialSymbol || specialAnim == null || !specialAnim.isplaying;
      bool borderDone = !IsSpecialSymbol || !borderAnimation.isplaying;
      return pulseDone && bgDone && specialDone && borderDone;
    });
  }

  internal void ResetLineAnim()
  {
    Drop();
    iconAnim?.Kill();
    iconAnim = null;
    iconImage.transform.localScale = Vector3.one;
    AnimateDarkImage(true);
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
    if (WinAmountBox.activeSelf)
    {
      WinAmountBox.SetActive(false);
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
  }
}
