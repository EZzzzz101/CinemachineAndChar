using UnityEngine;

/// <summary>
/// 角色音效管理（单例）— Animation Event 接收端 + 代码直调
/// 挂在角色 GameObject 上，使用自身 AudioSource 替代 PlayClipAtPoint
/// </summary>
public class CharacterAudio : MonoBehaviour
{
    public static CharacterAudio Instance { get; private set; }

    [Header("脚步声")]
    [SerializeField] private AudioClip[] _footstepClips;
    [SerializeField] private AudioClip[] _footBackClips;
    [SerializeField] [Range(0, 1)] private float _footSpatialBlend = 0.5f;

    [Header("攻击音效")]
    [SerializeField] private AudioClip _attackWhoosh;
    [SerializeField] private AudioClip _attackHit;
    [SerializeField] private AudioClip _weaponBackSound;     // 收刀
    [SerializeField] private AudioClip _weaponEndSound;      // 入鞘
    [SerializeField] [Range(0, 1)] private float _atkSpatialBlend = 0.7f;

    [Header("闪避音效")]
    [SerializeField] private AudioClip _dashFrontSound;
    [SerializeField] private AudioClip _dashBackSound;
    [SerializeField] [Range(0, 1)] private float _dodgeSpatialBlend = 0.7f;

    [Header("受击")]
    [SerializeField] private AudioClip _hurtVoice;

    private AudioSource _audioSource;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ===== 动画事件接收方法 =====

    public void PlayFootSound()
    {
        PlayRandom(_footstepClips, _footSpatialBlend);
    }

    public void PlayFootBackSound()
    {
        PlayRandom(_footBackClips, _footSpatialBlend);
    }

    public void PlayWhooshSound()
    {
        PlayClip(_attackWhoosh, _atkSpatialBlend);
    }

    public void PlayHitSound()
    {
        PlayClip(_attackHit, _atkSpatialBlend);
    }

    public void PlayWeaponBackSound()
    {
        PlayClip(_weaponBackSound, _atkSpatialBlend);
    }

    public void PlayWeaponEndSound()
    {
        PlayClip(_weaponEndSound, _atkSpatialBlend);
    }

    /// <summary>
    /// Combo 每段攻击音效 — ComboNext() 代码直调
    /// </summary>
    public void PlayComboSound(AudioClip clip)
    {
        PlayClip(clip, _atkSpatialBlend);
    }

    public void PlayDodgeSound(AnimationEnterBehaviour.AnimationEnterState dashDir)
    {
        var clip = dashDir == AnimationEnterBehaviour.AnimationEnterState.DashFront
            ? _dashFrontSound : _dashBackSound;
        PlayClip(clip, _dodgeSpatialBlend);
    }

    public void PlayHurtVoice()
    {
        PlayClip(_hurtVoice, 0.5f);
    }

    // ===== 内部 =====

    private void PlayRandom(AudioClip[] clips, float spatialBlend)
    {
        if (clips == null || clips.Length == 0) return;
        PlayClip(clips[Random.Range(0, clips.Length)], spatialBlend);
    }

    private void PlayClip(AudioClip clip, float spatialBlend)
    {
        if (clip == null) return;
        _audioSource.spatialBlend = spatialBlend;
        _audioSource.PlayOneShot(clip);
    }
}
