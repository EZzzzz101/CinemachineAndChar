using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 锁定指示器 — 在 Canvas 上跟随锁定目标的屏幕位置
/// </summary>
public class LockOnUI : MonoBehaviour
{
    [SerializeField] private Image _indicator;
    [SerializeField] private RectTransform _canvasRect;
    [SerializeField] private Camera   _uiCamera;

    void Start()
    {
        if (_indicator  == null) _indicator  = GetComponent<Image>();
        if (_canvasRect == null) _canvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        if (_uiCamera   == null) _uiCamera   = Camera.main;

        if (_indicator != null) _indicator.enabled = false;

        Debug.Log($"[LockOnUI] 初始化: indicator={_indicator != null} canvas={_canvasRect != null} cam={_uiCamera != null}");

        if (LockOnManager.HasInstance)
            LockOnManager.Instance.OnLockOnChanged += OnLockOnChanged;
    }

    void Update()
    {
        var target = LockOnManager.Instance?.CurrentTarget;
        if (target == null)
        {
            _indicator.enabled = false;
            return;
        }

        Vector3 screenPos = _uiCamera.WorldToScreenPoint(target.GetLockOnPosition());

        // 目标在屏幕后方时隐藏
        if (screenPos.z < 0f)
        {
            _indicator.enabled = false;
            return;
        }

        // 屏幕坐标 → Canvas 局部坐标
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, (Vector2)screenPos, null, out Vector2 localPos
        );

        _indicator.rectTransform.anchoredPosition = localPos;
        _indicator.enabled = true;
    }

    void OnLockOnChanged(LockOnTarget target)
    {
        Debug.Log($"[LockOnUI] OnLockOnChanged: {(target != null ? target.name : "null")}");
        if (_indicator != null) _indicator.enabled = (target != null);
    }

    void OnDestroy()
    {
        if (LockOnManager.HasInstance)
            LockOnManager.Instance.OnLockOnChanged -= OnLockOnChanged;
    }
}
