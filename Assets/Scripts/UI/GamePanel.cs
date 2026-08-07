using TMPro;

public class GamePanel : UIView
{
    public HPBar playerHp;
    public HPBar bossHp;

    public TMP_Text playerHPText;

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