using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    [System.Serializable]
    public class SoundEffect
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)]
        public float volume = 1f;
        [Range(0.5f, 1.5f)]
        public float pitch = 1f;
        public bool loop = false;
        
        [HideInInspector]
        public AudioSource source;
    }
    
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private int sfxSourcesCount = 5;
    
    [Header("Sound Effects")]
    [SerializeField] private SoundEffect[] soundEffects;
    [SerializeField] private AudioClip[] paddleSounds;
    [SerializeField] private AudioClip[] collisionSounds;
    [SerializeField] private AudioClip[] treasureSounds;
    
    [Header("Music")]
    [SerializeField] private AudioClip mainMenuMusic;
    [SerializeField] private AudioClip gameplayMusic;
    
    private List<AudioSource> sfxSources = new List<AudioSource>();
    private Dictionary<string, SoundEffect> soundDictionary = new Dictionary<string, SoundEffect>();
    
    // Singleton pattern
    public static AudioManager Instance { get; private set; }
    
    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializeAudio();
    }
    
    private void InitializeAudio()
    {
        // Create music source if not assigned
        if (musicSource == null)
        {
            GameObject musicObj = new GameObject("Music_Source");
            musicObj.transform.parent = transform;
            musicSource = musicObj.AddComponent<AudioSource>();
            musicSource.loop = true;
        }
        
        // Create SFX sources
        for (int i = 0; i < sfxSourcesCount; i++)
        {
            GameObject sfxObj = new GameObject("SFX_Source_" + i);
            sfxObj.transform.parent = transform;
            AudioSource source = sfxObj.AddComponent<AudioSource>();
            sfxSources.Add(source);
        }
        
        // Build sound dictionary
        foreach (SoundEffect sound in soundEffects)
        {
            sound.source = null;
            soundDictionary[sound.name] = sound;
        }
        
        // Apply saved volume settings
        SetMusicVolume(PlayerPrefs.GetFloat("MusicVolume", 0.75f));
        SetSFXVolume(PlayerPrefs.GetFloat("SFXVolume", 1.0f));
    }
    
    // Play a sound by name
    public void PlaySound(string name)
    {
        if (soundDictionary.TryGetValue(name, out SoundEffect sound))
        {
            PlaySoundEffect(sound);
        }
        else
        {
            Debug.LogWarning("Sound not found: " + name);
        }
    }
    
    // Play random paddle sound
    public void PlayPaddleSound()
    {
        if (paddleSounds.Length > 0)
        {
            AudioClip clip = paddleSounds[Random.Range(0, paddleSounds.Length)];
            PlayOneShot(clip, 0.7f);
        }
    }
    
    // Play random collision sound
    public void PlayCollisionSound()
    {
        if (collisionSounds.Length > 0)
        {
            AudioClip clip = collisionSounds[Random.Range(0, collisionSounds.Length)];
            PlayOneShot(clip, 1.0f);
        }
    }
    
    // Play random treasure sound
    public void PlayTreasureSound()
    {
        if (treasureSounds.Length > 0)
        {
            AudioClip clip = treasureSounds[Random.Range(0, treasureSounds.Length)];
            PlayOneShot(clip, 0.8f);
        }
    }
    
    // General method to play any clip
    public void PlayOneShot(AudioClip clip, float volume = 1.0f)
    {
        if (clip == null) return;
        
        // Find an available audio source
        AudioSource source = GetAvailableSFXSource();
        if (source != null)
        {
            source.PlayOneShot(clip, volume);
        }
    }
    
    // Play a specific sound effect
    private void PlaySoundEffect(SoundEffect sound)
    {
        AudioSource source = GetAvailableSFXSource();
        if (source != null)
        {
            source.clip = sound.clip;
            source.volume = sound.volume;
            source.pitch = sound.pitch;
            source.loop = sound.loop;
            source.Play();
            
            // Store reference if looping (for stopping later)
            if (sound.loop)
            {
                sound.source = source;
            }
        }
    }
    
    // Find an available SFX source
    private AudioSource GetAvailableSFXSource()
    {
        // Look for an inactive source first
        foreach (AudioSource source in sfxSources)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }
        
        // If all are in use, use the oldest one
        return sfxSources[0];
    }
    
    // Stop a looping sound
    public void StopSound(string name)
    {
        if (soundDictionary.TryGetValue(name, out SoundEffect sound))
        {
            if (sound.source != null && sound.source.isPlaying)
            {
                sound.source.Stop();
                sound.source = null;
            }
        }
    }
    
    // Play music
    public void PlayMusic(AudioClip music, float fadeTime = 1.0f)
    {
        if (music == null) return;
        
        // If already playing this music, skip
        if (musicSource.clip == music && musicSource.isPlaying)
            return;
            
        // Stop current music and play new
        StartCoroutine(FadeMusicCoroutine(music, fadeTime));
    }
    
    // Play main menu music
    public void PlayMainMenuMusic()
    {
        PlayMusic(mainMenuMusic);
    }
    
    // Play gameplay music
    public void PlayGameplayMusic()
    {
        PlayMusic(gameplayMusic);
    }
    
    // Fade between music tracks
    private System.Collections.IEnumerator FadeMusicCoroutine(AudioClip newClip, float fadeTime)
    {
        float startVolume = musicSource.volume;
        
        // Fade out
        if (musicSource.isPlaying)
        {
            float t = 0;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0, t / fadeTime);
                yield return null;
            }
        }
        
        // Change clip and play
        musicSource.clip = newClip;
        musicSource.Play();
        
        // Fade in
        float t2 = 0;
        while (t2 < fadeTime)
        {
            t2 += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0, startVolume, t2 / fadeTime);
            yield return null;
        }
    }
    
    // Volume control methods
    public void SetMusicVolume(float volume)
    {
        if (musicSource != null)
        {
            musicSource.volume = volume;
        }
    }
    
    public void SetSFXVolume(float volume)
    {
        foreach (AudioSource source in sfxSources)
        {
            source.volume = volume;
        }
    }
}