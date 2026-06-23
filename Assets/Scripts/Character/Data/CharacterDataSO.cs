using UnityEngine;

/// <summary>
/// 角色数据总表（ScriptableObject）— 所有配置集中管理
/// CharacterAudio / CharacterVFX 从这里读，不再自己拖字段
/// </summary>
[CreateAssetMenu(fileName = "CharacterData", menuName = "Character/CharacterData")]
public class CharacterDataSO : ScriptableObject
{
    [Header("连招")]
    public ComboConfigSO comboConfig;

    [Header("音效")]
    public CharacterAudioSO audioData;
}
