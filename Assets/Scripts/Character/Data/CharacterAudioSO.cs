using UnityEngine;

/// <summary>
/// 角色音效数据（ScriptableObject）
/// </summary>
[CreateAssetMenu(fileName = "CharacterAudio", menuName = "Character/CharacterAudio")]
public class CharacterAudioSO : ScriptableObject
{
    [Header("脚步声")]
    [Header("前进脚步声")]
    public AudioClip[] footstepClips;
    [Header("收脚声")]
    public AudioClip[] footBackClips;
    [Range(0, 1)] public float footSpatialBlend = 0.5f;

    [Header("攻击音效")]
    public AudioClip attackWhoosh;          // 挥砍破空
    public AudioClip attackHit;             // 打击
    public AudioClip weaponBackSound;       // 收刀
    public AudioClip weaponEndSound;        // 入鞘
    [Range(0, 1)] public float atkSpatialBlend = 0.7f;

    [Header("闪避音效")]
    public AudioClip dashFrontSound;        // 前闪
    public AudioClip dashBackSound;         // 后闪
    [Range(0, 1)] public float dodgeSpatialBlend = 0.7f;

    [Header("受击")]
    public AudioClip hurtVoice;
}
