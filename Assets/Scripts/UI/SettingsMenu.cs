using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Handles the Settings menu (both Main Menu and Pause Menu).
/// Automatically saves and loads values using PlayerPrefs.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [Header("Audio Mixer")]
    [Tooltip("The main audio mixer. Ensure it has exposed parameters: MasterVol, MusicVol, SFXVol.")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("UI Controls")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Slider sensitivitySlider;

    // Keys for PlayerPrefs
    private const string PREF_MASTER_VOL = "MasterVol";
    private const string PREF_MUSIC_VOL = "MusicVol";
    private const string PREF_SFX_VOL = "SFXVol";
    private const string PREF_FULLSCREEN = "Fullscreen";
    private const string PREF_SENSITIVITY = "Sensitivity";

    private void Start()
    {
        LoadSettings();
        
        // Add listeners
        if (masterSlider != null)
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
        
        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
            
        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
            
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
            
        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
    }

    private void LoadSettings()
    {
        // Default volumes: 1.0 (max). AudioMixer expects values that we will log10, so 0.0001 to 1 range for sliders is ideal.
        float masterVol = PlayerPrefs.GetFloat(PREF_MASTER_VOL, 1f);
        float musicVol = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, 1f);
        float sfxVol = PlayerPrefs.GetFloat(PREF_SFX_VOL, 1f);
        
        if (masterSlider != null) masterSlider.value = masterVol;
        if (musicSlider != null) musicSlider.value = musicVol;
        if (sfxSlider != null) sfxSlider.value = sfxVol;

        // Apply audio settings right away
        SetMasterVolume(masterVol);
        SetMusicVolume(musicVol);
        SetSFXVolume(sfxVol);

        // Fullscreen
        bool isFullscreen = PlayerPrefs.GetInt(PREF_FULLSCREEN, 1) == 1;
        if (fullscreenToggle != null) fullscreenToggle.isOn = isFullscreen;
        SetFullscreen(isFullscreen);

        // Sensitivity
        float sensitivity = PlayerPrefs.GetFloat(PREF_SENSITIVITY, 3f);
        if (sensitivitySlider != null) sensitivitySlider.value = sensitivity;
    }

    public void SetMasterVolume(float sliderValue)
    {
        // Convert linear 0.0001 - 1 to logarithmic -80dB - 0dB
        float dB = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;
        if (audioMixer != null)
            audioMixer.SetFloat("MasterVol", dB);
            
        PlayerPrefs.SetFloat(PREF_MASTER_VOL, sliderValue);
    }

    public void SetMusicVolume(float sliderValue)
    {
        float dB = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;
        if (audioMixer != null)
            audioMixer.SetFloat("MusicVol", dB);
            
        PlayerPrefs.SetFloat(PREF_MUSIC_VOL, sliderValue);
    }

    public void SetSFXVolume(float sliderValue)
    {
        float dB = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;
        if (audioMixer != null)
            audioMixer.SetFloat("SFXVol", dB);
            
        PlayerPrefs.SetFloat(PREF_SFX_VOL, sliderValue);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(PREF_FULLSCREEN, isFullscreen ? 1 : 0);
    }

    public void SetSensitivity(float sensitivity)
    {
        PlayerPrefs.SetFloat(PREF_SENSITIVITY, sensitivity);
        
        // Find CameraFollow in the scene and update it if it exists
        CameraFollow camFollow = FindObjectOfType<CameraFollow>();
        if (camFollow != null)
        {
            camFollow.UpdateSensitivity();
        }
    }
}
