using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class DamageText : MonoBehaviour
{
    public TMP_Text text;
    public GameObject critIcon;

    [Header("颜色")]
    public Color normalColor = Color.white;
    public Color critColor = Color.red;

    [Header("暴击图标")]
    [Tooltip("图标与数字之间的间距")]
    public float critIconGap = 2f;

    private RectTransform _critIconRt;
    private Image _critIconImage;

    private void Awake()
    {
        _critIconRt = critIcon != null ? critIcon.transform as RectTransform : null;
        _critIconImage = critIcon != null ? critIcon.GetComponent<Image>() : null;

        // 图标与文字绑定：挂到 Text 下面，随文字一起移动/缩放
        if (_critIconRt != null && text != null && _critIconRt.parent != text.transform)
            _critIconRt.SetParent(text.transform, false);
    }

    /// <summary>
    /// 显示一条伤害数字：暴击 = 图标 + 数字组合，普通 = 纯数字。
    /// 兼容对象池复用：开始时重置缩放/颜色/透明度并 Kill 残留补间；
    /// 动画结束后回调 onComplete（如返回对象池），不传则自动销毁。
    /// </summary>
    public void Show(DamageData data, Action onComplete = null)
    {
        transform.DOKill();
        text.DOKill();
        if (_critIconImage != null) _critIconImage.DOKill();
        if (_critIconRt != null) _critIconRt.DOKill();

        text.text = data.damage.ToString();
        critIcon.SetActive(data.isCritical);
        PositionCritIcon();

        if (data.isCritical)
        {
            text.color = critColor;
            transform.localScale = Vector3.zero;
            transform.DOScale(1.3f, 0.15f)
                .SetEase(Ease.OutBack);

            // 图标与数字同步：出现即全显（自身小弹跳更醒目），结束时一起淡出
            if (_critIconImage != null)
            {
                _critIconImage.color = new Color(_critIconImage.color.r, _critIconImage.color.g, _critIconImage.color.b, 1f);
            }
            if (_critIconRt != null)
            {
                _critIconRt.localScale = Vector3.one * 0.6f;
                _critIconRt.DOScale(1f, 0.15f).SetEase(Ease.OutBack);
            }
        }
        else
        {
            text.color = normalColor;
            transform.localScale = Vector3.one;
            if (_critIconRt != null)
                _critIconRt.localScale = Vector3.one;
            if (_critIconImage != null)
                _critIconImage.color = new Color(_critIconImage.color.r, _critIconImage.color.g, _critIconImage.color.b, 1f);   // 重置，防复用时残留透明
        }

        // 上浮 + 淡出
        transform.DOMoveY(transform.position.y + 1.5f, 1f);
        if (_critIconImage != null)
            _critIconImage.DOFade(0f, 1f);
        text.DOFade(0f, 1f).OnComplete(() =>
        {
            if (onComplete != null) onComplete();
            else Destroy(gameObject);
        });
    }

    /// <summary>把图标贴到数字左侧：以首个字符实际位置为锚点，与文本对齐方式无关</summary>
    private void PositionCritIcon()
    {
        if (_critIconRt == null || text == null) return;

        text.ForceMeshUpdate();
        if (text.textInfo.characterCount == 0) return;

        var first = text.textInfo.characterInfo[0];
        float leftX = first.bottomLeft.x;                                   // 数字最左（文本本地坐标）
        float centerY = (first.topLeft.y + first.bottomLeft.y) * 0.5f;      // 数字垂直中心
        float halfIcon = _critIconRt.rect.width * 0.5f;

        _critIconRt.anchoredPosition = new Vector2(
            leftX - halfIcon - critIconGap,
            centerY
        );
    }
}
