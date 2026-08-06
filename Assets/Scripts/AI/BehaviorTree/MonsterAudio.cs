using UnityEngine;

/// <summary>
/// 角色音效管理（单例）— Animation Event 接收端 + 代码直调
/// 挂在角色 GameObject 上，数据从 CharacterDataSO 读取
/// </summary>
public class MonsterAudio : UnitAudio
{

    //(弃置项)
    [SerializeField] private CharacterDataSO _data;

    [SerializeField] private MonsterAttackConfigSO _attackConfigSO;
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
    /// 命中音 — BTAttack 命中结算时调用，clip 来自 MonsterAttackStepData.hitSound
    /// </summary>
    public void PlayHitSound(AudioClip clip, float volume, float spatialBlend)
    {
        PlayClip(clip, spatialBlend, volume);
    }

    /// <summary>
    /// 挥空音 — BTAttack 每个命中点触发时调用（不论是否打中），clip 来自 MonsterAttackStepData.swingSound
    /// </summary>
    public void PlaySwingSound(AudioClip clip, float volume, float spatialBlend)
    {
        PlayClip(clip, spatialBlend, volume);
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
        PlayClip(clip, 0.5f);
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
