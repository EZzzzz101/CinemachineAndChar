using System;
using TMPro;
using UnityEngine;

public class GamePanel : UIView
{
    public HPBar playerHp;
    public HPBar bossHp;

    public TMP_Text playerHPText;

    [Header("伤害数字")]
    [Tooltip("已挂 DamageText 组件的跳字模板；留空自动从子物体查找")]
    [SerializeField] private DamageText damageTextPrefab;

    // EventBus 无参版 Subscribe 每次会包新委托、无法退订，这里存住包装委托用泛型版订阅
    private Action<object> _onEnemyDied;

    private Canvas _canvas;
    private UIObjectPool<DamageText> _damagePool;

    protected override void Awake()
    {
        base.Awake();
        _onEnemyDied = _ => OnEnemyDied();
        _canvas = GetComponentInParent<Canvas>();

        if (damageTextPrefab == null)
            damageTextPrefab = GetComponentInChildren<DamageText>(true);

        if (damageTextPrefab != null)
        {
            _damagePool = new UIObjectPool<DamageText>(damageTextPrefab, transform, 10, 50);
            damageTextPrefab.gameObject.SetActive(false);   // 模板本身不显示，只用来克隆
        }
        else
        {
            Debug.LogWarning("[GamePanel] 未找到 DamageText 模板，伤害跳字不会生成");
        }
    }

    private void OnEnable()
    {
        EventBus.Subscribe<HPData>(
            GameEvents.HPChanged,
            OnHPChanged
        );

        EventBus.Subscribe<HPData>(
            GameEvents.HPTextChanged,
            OnHPTextChanged
        );

        EventBus.Subscribe<DamageData>(
            GameEvents.HitLanded,
            OnHitLanded
        );

        EventBus.Subscribe<object>(
            GameEvents.EnemyDied,
            _onEnemyDied
        );
    }


    private void OnDisable()
    {
        EventBus.Unsubscribe<HPData>(
            GameEvents.HPChanged,
            OnHPChanged
        );

        EventBus.Unsubscribe<HPData>(
            GameEvents.HPTextChanged,
            OnHPTextChanged
        );

        EventBus.Unsubscribe<DamageData>(
            GameEvents.HitLanded,
            OnHitLanded
        );

        EventBus.Unsubscribe<object>(
            GameEvents.EnemyDied,
            _onEnemyDied
        );
    }

    /// <summary>怪物死亡 → 弹出胜利结算面板（UI 只订阅，不反向调用战斗模块）</summary>
    private void OnEnemyDied()
    {
        UIManager.Instance.Open<WinView>();
    }

    /// <summary>命中事件 → 在命中点生成伤害数字（暴击=图标+数字，普通=纯数字）</summary>
    private void OnHitLanded(DamageData data)
    {
        if (_damagePool == null || _canvas == null) return;

        DamageText damageText = _damagePool.Get();
        if (!PositionAt(damageText.transform as RectTransform, data.hitPoint))
        {
            _damagePool.Return(damageText);   // 目标在相机背后等无法显示的情况，直接回收
            return;
        }
        damageText.Show(data, () => _damagePool.Return(damageText));
    }

    /// <summary>把世界命中点转到 Canvas 本地坐标（兼容 ScreenSpace-Overlay）；返回是否成功定位</summary>
    private bool PositionAt(RectTransform rt, Vector3 worldPoint)
    {
        if (rt == null) return false;

        // 世界坐标 → 屏幕坐标（Overlay 画布需要主相机投影，不能直接用世界坐标）
        Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? Camera.main : _canvas.worldCamera;
        if (cam == null) return false;

        Vector3 screenPoint = cam.WorldToScreenPoint(worldPoint);
        if (screenPoint.z < 0f) return false;   // 目标在相机背后

        // 屏幕坐标 → Canvas 本地坐标（Overlay 传 null，Camera 模式传画布相机）
        Camera convCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, screenPoint, convCam, out Vector2 local))
        {
            rt.anchoredPosition = local;
            return true;
        }
        return false;
    }

    private void OnHPChanged(HPData data)
    {
        //玩家
        if(data.id==1)
            playerHp.SetHP(data.current,data.max);
        //敌人
        if(data.id==100)
            bossHp.SetHP(data.current,data.max);
    }

    private void OnHPTextChanged(HPData data)
    {
        //玩家
        if (data.id == 1)
        {
            playerHPText.text = $"{(int)data.current}/{(int)data.max}";
        }

    }
}
