using UnityEngine;
using TMPro;
using DG.Tweening;

public struct DamageData
{
    public float damage;

    public bool isCritical;
}

public class DamageText : MonoBehaviour
{
    public TMP_Text text;
    public GameObject critIcon;

    public void Show(DamageData data)
    {
        text.text = data.damage.ToString();


        if(data.isCritical)
        {
            critIcon.SetActive(true);

            text.color = Color.red;

            transform.localScale = Vector3.zero;

            transform.DOScale(1.3f,0.15f)
                .SetEase(Ease.OutBack);
        }
        else
        {
            critIcon.SetActive(false);

            text.color = Color.white;
        }


        PlayAnimation();
    }


    void PlayAnimation()
    {
        transform.DOMoveY(
            transform.position.y + 1.5f,
            1f
        );


        text.DOFade(0,1f)
            .OnComplete(()=>Destroy(gameObject));
    }
}