using UnityEngine;

public class UnitAudio : MonoBehaviour
{
    protected AudioSource _audioSource;


    protected virtual void Awake()
    {
        _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }


    public void PlayClip(AudioClip clip, float spatialBlend, float volume = 1f)
    {
        if (clip == null)
            return;

        _audioSource.volume = volume;
        _audioSource.spatialBlend = spatialBlend;
        _audioSource.PlayOneShot(clip);
    }


    public void PlayRandom(AudioClip[] clips, float spatialBlend, float volume = 1f)
    {
        if (clips == null || clips.Length == 0)
            return;

        PlayClip(
            clips[Random.Range(0, clips.Length)],
            spatialBlend,
            volume
        );
    }
}