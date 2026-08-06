using UnityEngine;

/// <summary>
/// 角色音效管理（单例）— Animation Event 接收端 + 代码直调
/// 挂在角色 GameObject 上，数据从 CharacterDataSO 读取
/// </summary>
public class PlayerAudio : UnitAudio
{

    [SerializeField] private CharacterDataSO _data;

    // ===== 动画事件接收方法 =====

    public void PlayFootSound()
    {
        PlayRandom(_data.audioData.footstepClips, _data.audioData.footSpatialBlend);
    }

    public void PlayFootBackSound()
    {
        PlayRandom(_data.audioData.footBackClips, _data.audioData.footSpatialBlend);
    }

    public void PlayWhooshSound()
    {
        PlayClip(_data.audioData.attackWhoosh, _data.audioData.atkSpatialBlend);
    }

    /// <summary>
    /// 打击命中音 — ATK() 代码直调，clip 来自 ComboStepData.hitSound
    /// </summary>
    public void PlayHitSound(AudioClip clip)
    {
        PlayClip(clip, _data.audioData.atkSpatialBlend);
    }

    public void PlayWeaponBackSound()
    {
        PlayClip(_data.audioData.weaponBackSound, _data.audioData.atkSpatialBlend);
    }

    public void PlayWeaponEndSound()
    {
        PlayClip(_data.audioData.weaponEndSound, _data.audioData.atkSpatialBlend);
    }

    /// <summary>
    /// Combo 每段攻击音效 — ComboNext() 代码直调
    /// </summary>
    public void PlayComboSound(AudioClip clip)
    {
        PlayClip(clip, _data.audioData.atkSpatialBlend);
    }

    /// <summary>
    /// Combo 角色喊声 — ComboNext() 代码直调，随机
    /// </summary>
    public void PlayComboVoice(AudioClip[] clips)
    {
        PlayRandom(clips, 0.5f);
    }

    public void PlayDodgeSound(AnimationEnterBehaviour.AnimationEnterState dashDir)
    {
        var d = _data.audioData;
        var clip = dashDir == AnimationEnterBehaviour.AnimationEnterState.DashFront
            ? d.dashFrontSound : d.dashBackSound;
        PlayClip(clip, d.dodgeSpatialBlend);
    }

    public void PlayHurtVoice()
    {
        PlayRandom(_data.audioData.hurtVoiceClips, 0.5f);
    }
}
