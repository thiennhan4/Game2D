using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("Audio Sources")]
    public AudioSource effectAudioSource;
    public AudioSource musicAudioSource;
    [Header("Audio Clips")]
    public AudioClip Heal;
    public AudioClip coin;
    public AudioClip Health;
    public AudioClip Intro;
    public AudioClip jump;
    public AudioClip Run;
    public AudioClip WinGame;
    public AudioClip LoseGame;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (effectAudioSource != null && clip != null)
        {
            effectAudioSource.PlayOneShot(clip);
        }
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicAudioSource != null && clip != null)
        {
            musicAudioSource.clip = clip;
            musicAudioSource.loop = true;
            musicAudioSource.Play();
        }
    }
}
