using UnityEngine;
using DG.Tweening;

public class HPBar : MonoBehaviour
{
    [SerializeField]
    private RectTransform fill;

    public void SetHP(float current, float max)
    {
        float percent = current / max;

        fill.DOKill();

        fill.DOScaleX(percent, 0.3f);
    }
}