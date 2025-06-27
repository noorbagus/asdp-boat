using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject howToPlayPanel;
    [SerializeField] private GameObject creditsPanel;
    
    [Header("Settings Options")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Toggle useBluetoothToggle;
    
    [Header("Audio")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip buttonSound;
    
    private void Start()
    {
        // Show main menu, hide others
        ShowMainMenu();
        
        // Initialize settings if we have them
        InitializeSettings();
        
        // Set audio sources if not assigned
        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
        }
        
        if (sfxSource == null && sfxSource != musicSource)
        {
            // Create a new audio source for SFX if needed
            GameObject sfxObject = new GameObject("SFX_Source");
            sfxObject.transform.parent = transform;
            sfxSource = sfxObject.AddComponent<AudioSource>();
        }
    }
    
    private void InitializeSettings()
    {
        // Load saved settings
        float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.75f);
        float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        int qualityLevel = PlayerPrefs.GetInt("QualityLevel", 2);
        bool useBluetooth = PlayerPrefs.GetInt("UseBluetooth", 1) == 1;
        
        // Apply to UI elements if they exist
        if (musicVolumeSlider != null) musicVolumeSlider.value = musicVolume;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfxVolume;
        if (fullscreenToggle != null) fullscreenToggle.isOn = fullscreen;
        if (qualityDropdown != null) qualityDropdown.value = qualityLevel;
        if (useBluetoothToggle != null) useBluetoothToggle.isOn = useBluetooth;
        
        // Apply settings to game
        ApplyAudioSettings();
        ApplyDisplaySettings();
    }
    
    // Menu Navigation
    
    public void ShowMainMenu()
    {
        PlayButtonSound();
        ToggleMenuPanel(mainMenuPanel);
    }
    
    public void ShowSettings()
    {
        PlayButtonSound();
        ToggleMenuPanel(settingsPanel);
    }
    
    public void ShowHowToPlay()
    {
        PlayButtonSound();
        ToggleMenuPanel(howToPlayPanel);
    }
    
    public void ShowCredits()
    {
        PlayButtonSound();
        ToggleMenuPanel(creditsPanel);
    }
    
    private void ToggleMenuPanel(GameObject activePanel)
    {
        // Hide all panels
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        
        // Show the active one
        if (activePanel != null)
        {
            activePanel.SetActive(true);
        }
    }
    
    // Game Actions
    
    public void StartGame()
    {
        PlayButtonSound();
        
        // Load first level scene
        SceneManager.LoadScene("Level1");
    }
    
    public void QuitGame()
    {
        PlayButtonSound();
        
        // Quit application
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    
    // Settings Handlers
    
    public void OnMusicVolumeChanged(float volume)
    {
        PlayerPrefs.SetFloat("MusicVolume", volume);
        ApplyAudioSettings();
    }
    
    public void OnSFXVolumeChanged(float volume)
    {
        PlayerPrefs.SetFloat("SFXVolume", volume);
        ApplyAudioSettings();
    }
    
    public void OnFullscreenToggled(bool isFullscreen)
    {
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        ApplyDisplaySettings();
    }
    
    public void OnQualityChanged(int qualityIndex)
    {
        PlayerPrefs.SetInt("QualityLevel", qualityIndex);
        QualitySettings.SetQualityLevel(qualityIndex);
    }
    
    public void OnBluetoothToggled(bool useBluetooth)
    {
        PlayerPrefs.SetInt("UseBluetooth", useBluetooth ? 1 : 0);
    }
    
    public void ApplyAudioSettings()
    {
        float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.75f);
        float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
        
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }
        
        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
        }
    }
    
    public void ApplyDisplaySettings()
    {
        bool isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        
        Screen.fullScreen = isFullscreen;
    }
    
    public void ResetSettings()
    {
        // Reset to defaults
        PlayerPrefs.SetFloat("MusicVolume", 0.75f);
        PlayerPrefs.SetFloat("SFXVolume", 1.0f);
        PlayerPrefs.SetInt("Fullscreen", 1);
        PlayerPrefs.SetInt("QualityLevel", 2);
        PlayerPrefs.SetInt("UseBluetooth", 1);
        
        // Reload the UI
        InitializeSettings();
    }
    
    // Audio
    
    private void PlayButtonSound()
    {
        if (sfxSource != null && buttonSound != null)
        {
            sfxSource.PlayOneShot(buttonSound);
        }
    }
}