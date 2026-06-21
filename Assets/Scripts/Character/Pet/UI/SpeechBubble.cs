using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 头顶对话气泡 — 挂在角色/Bangboo 上即可，自动创建，大小自适应文字
/// </summary>
public class SpeechBubble : MonoBehaviour
{
    [Header("外观")]
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private int _fontSize = 14;
    [SerializeField] private float _maxWidth = 250f;    // 超过此宽度换行
    [SerializeField] private float _offsetY = 2.5f;
    [SerializeField] private float _autoHideDelay = 3f;
    [Range(0.5f, 20f)]
    [SerializeField] private float _rotationSmooth = 5f;

    private Canvas _canvas;
    private TMP_Text _text;
    private Image _background;
    private RectTransform _bgRect;
    private Coroutine _hideCoroutine;
    private Transform _cameraTransform;

    public event Action OnAutoHidden;

    void Awake()
    {
        CreateBubble();
        _canvas.enabled = false;
    }

    void Start()
    {
        if (Camera.main != null) _cameraTransform = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (!_canvas.enabled || _cameraTransform == null) return;

        Vector3 toCamera = _canvas.transform.position - _cameraTransform.position;
        toCamera.y = 0;
        if (toCamera.magnitude > 0.01f)
        {
            _canvas.transform.rotation = Quaternion.Slerp(
                _canvas.transform.rotation,
                Quaternion.LookRotation(toCamera),
                Time.deltaTime * _rotationSmooth
            );
        }
    }

    private void CreateBubble()
    {
        var canvasGo = new GameObject("BubbleCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = new Vector3(0, _offsetY, 0);
        canvasGo.transform.localScale = Vector3.one * 0.01f;

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = Camera.main;
        canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // ── 背景 ──
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        _background = bgGo.AddComponent<Image>();
        _background.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        _bgRect = bgGo.GetComponent<RectTransform>();
        _bgRect.sizeDelta = new Vector2(60, 40); // 初始最小尺寸

        // ── 文字 ──
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(bgGo.transform, false);
        _text = textGo.AddComponent<TextMeshProUGUI>();
        if (_font != null) _text.font = _font;
        _text.fontSize = _fontSize;
        _text.color = Color.white;
        _text.alignment = TextAlignmentOptions.Center;
        _text.enableWordWrapping = true;

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 5);
        textRect.offsetMax = new Vector2(-10, -5);

        // ContentSizeFitter 让文字框自适应内容大小
        var fitter = textGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // LayoutElement 限制最大宽度，超过则换行
        var layout = textGo.AddComponent<LayoutElement>();
        layout.preferredWidth = _maxWidth;
    }

    public void Show(string message)
    {
        if (_text != null) _text.text = message;
        if (_canvas != null) _canvas.enabled = true;

        // 等一帧让 ContentSizeFitter 算出实际大小，再更新背景
        StartCoroutine(UpdateBackgroundSize());

        if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
        _hideCoroutine = StartCoroutine(AutoHideRoutine());
    }

    /// <summary>等布局更新后让背景跟上文字大小</summary>
    private IEnumerator UpdateBackgroundSize()
    {
        yield return null; // 等一帧
        if (_text == null || _bgRect == null) yield break;

        float w = _text.preferredWidth + 20;
        float h = _text.preferredHeight + 10;
        _bgRect.sizeDelta = new Vector2(w, h);
    }

    public void Hide()
    {
        if (_canvas != null) _canvas.enabled = false;
        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }
    }

    private IEnumerator AutoHideRoutine()
    {
        yield return new WaitForSeconds(_autoHideDelay);
        Hide();
        OnAutoHidden?.Invoke();
    }
}
