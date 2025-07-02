using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InputSettingsManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private TMP_Dropdown inputModeDropdown;
    [SerializeField] private GameObject cameraSettingsGroup;
    
    [Header("Camera Settings")]
    [SerializeField] private TMP_Dropdown cameraDropdown;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityValue;
    [SerializeField] private Slider debounceSlider;
    [SerializeField] private TextMeshProUGUI debounceValue;
    [SerializeField] private Toggle shoulderFallbackToggle;
    [SerializeField] private Toggle showPreviewToggle;
    [SerializeField] private Toggle showPoseToggle;
    
    [Header("Preview Settings")]
    [SerializeField] private Slider previewSizeSlider;
    [SerializeField] private Slider previewAlphaSlider;
    [SerializeField] private TMP_Dropdown previewPositionDropdown;
    
    [Header("Status Display")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Image connectionIndicator;
    [SerializeField] private Button testCameraButton;
    [SerializeField] private Button calibrateButton;
    
    // References
    private CameraBodyTracker cameraTracker;
    private CameraPreviewUI previewUI;
    private BoatController boatController;
    
    // Settings data
    private CameraInputSettings currentSettings;
    
    [System.Serializable]
    public class CameraInputSettings
    {
        public int selectedCamera = 0;
        public float sensitivity = 0.05f;
        public float debounceTime = 0.3f;
        public bool enableShoulderFallback = true;
        public bool showPreview = true;
        public bool showPoseOverlay = true;
        public float previewSize = 1f;
        public float previewAlpha = 0.8f;
        public int previewPosition = 0; // 0=top-right, 1=top-left, 2=bottom-right, 3=bottom-left
    }
    
    private void Start()
    {
        InitializeComponents();
        LoadSettings();
        SetupUI();
    }
    
    private void InitializeComponents()
    {
        cameraTracker = FindObjectOfType<CameraBodyTracker>();
        previewUI = FindObjectOfType<CameraPreviewUI>();
        boatController = FindObjectOfType<BoatController>();
        
        currentSettings = new CameraInputSettings();
    }
    
    private void LoadSettings()
    {
        currentSettings.selectedCamera = PlayerPrefs.GetInt("CameraIndex", 0);
        currentSettings.sensitivity = PlayerPrefs.GetFloat("CameraSensitivity", 0.05f);
        currentSettings.debounceTime = PlayerPrefs.GetFloat("CameraDebounce", 0.3f);
        currentSettings.enableShoulderFallback = PlayerPrefs.GetInt("ShoulderFallback", 1) == 1;
        currentSettings.showPreview = PlayerPrefs.GetInt("ShowPreview", 1) == 1;
        currentSettings.showPoseOverlay = PlayerPrefs.GetInt("ShowPoseOverlay", 1) == 1;
        currentSettings.previewSize = PlayerPrefs.GetFloat("PreviewSize", 1f);
        currentSettings.previewAlpha = PlayerPrefs.GetFloat("PreviewAlpha", 0.8f);
        currentSettings.previewPosition = PlayerPrefs.GetInt("PreviewPosition", 0);
    }
    
    private void SaveSettings()
    {
        PlayerPrefs.SetInt("CameraIndex", currentSettings.selectedCamera);
        PlayerPrefs.SetFloat("CameraSensitivity", currentSettings.sensitivity);
        PlayerPrefs.SetFloat("CameraDebounce", currentSettings.debounceTime);
        PlayerPrefs.SetInt("ShoulderFallback", currentSettings.enableShoulderFallback ? 1 : 0);
        PlayerPrefs.SetInt("ShowPreview", currentSettings.showPreview ? 1 : 0);
        PlayerPrefs.SetInt("ShowPoseOverlay", currentSettings.showPoseOverlay ? 1 : 0);
        PlayerPrefs.SetFloat("PreviewSize", currentSettings.previewSize);
        PlayerPrefs.SetFloat("PreviewAlpha", currentSettings.previewAlpha);
        PlayerPrefs.SetInt("PreviewPosition", currentSettings.previewPosition);
        PlayerPrefs.Save();
    }
    
    private void SetupUI()
    {
        SetupInputModeDropdown();
        SetupCameraDropdown();
        SetupSliders();
        SetupToggles();
        SetupButtons();
        UpdateUIFromSettings();
        
        // Initially hide camera settings if not in camera mode
        UpdateCameraSettingsVisibility();
    }
    
    private void SetupInputModeDropdown()
    {
        if (inputModeDropdown == null) return;
        
        inputModeDropdown.ClearOptions();
        inputModeDropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "Keyboard",
            "Bluetooth Sensor", 
            "Direct Control",
            "Camera Body Tracking"
        });
        
        inputModeDropdown.onValueChanged.AddListener(OnInputModeChanged);
        
        // Set current mode
        if (boatController != null)
        {
            inputModeDropdown.value = (int)boatController.GetInputMode();
        }
    }
    
    private void SetupCameraDropdown()
    {
        if (cameraDropdown == null || cameraTracker == null) return;
        
        cameraDropdown.ClearOptions();
        var cameras = cameraTracker.GetAvailableCameras();
        
        if (cameras != null && cameras.Length > 0)
        {
            var options = new System.Collections.Generic.List<string>();
            foreach (string camera in cameras)
            {
                options.Add(camera);
            }
            cameraDropdown.AddOptions(options);
        }
        else
        {
            cameraDropdown.AddOptions(new System.Collections.Generic.List<string> { "No cameras found" });
        }
        
        cameraDropdown.onValueChanged.AddListener(OnCameraChanged);
    }
    
    private void SetupSliders()
    {
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = 0.01f;
            sensitivitySlider.maxValue = 0.2f;
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }
        
        if (debounceSlider != null)
        {
            debounceSlider.minValue = 0.1f;
            debounceSlider.maxValue = 2f;
            debounceSlider.onValueChanged.AddListener(OnDebounceChanged);
        }
        
        if (previewSizeSlider != null)
        {
            previewSizeSlider.minValue = 0.5f;
            previewSizeSlider.maxValue = 2f;
            previewSizeSlider.onValueChanged.AddListener(OnPreviewSizeChanged);
        }
        
        if (previewAlphaSlider != null)
        {
            previewAlphaSlider.minValue = 0.2f;
            previewAlphaSlider.maxValue = 1f;
            previewAlphaSlider.onValueChanged.AddListener(OnPreviewAlphaChanged);
        }
    }
    
    private void SetupToggles()
    {
        if (shoulderFallbackToggle != null)
        {
            shoulderFallbackToggle.onValueChanged.AddListener(OnShoulderFallbackChanged);
        }
        
        if (showPreviewToggle != null)
        {
            showPreviewToggle.onValueChanged.AddListener(OnShowPreviewChanged);
        }
        
        if (showPoseToggle != null)
        {
            showPoseToggle.onValueChanged.AddListener(OnShowPoseChanged);
        }
    }
    
    private void SetupButtons()
    {
        if (testCameraButton != null)
        {
            testCameraButton.onClick.AddListener(TestCamera);
        }
        
        if (calibrateButton != null)
        {
            calibrateButton.onClick.AddListener(CalibrateCamera);
        }
    }
    
    private void UpdateUIFromSettings()
    {
        if (cameraDropdown != null)
            cameraDropdown.value = currentSettings.selectedCamera;
        
        if (sensitivitySlider != null)
            sensitivitySlider.value = currentSettings.sensitivity;
        
        if (debounceSlider != null)
            debounceSlider.value = currentSettings.debounceTime;
        
        if (shoulderFallbackToggle != null)
            shoulderFallbackToggle.isOn = currentSettings.enableShoulderFallback;
        
        if (showPreviewToggle != null)
            showPreviewToggle.isOn = currentSettings.showPreview;
        
        if (showPoseToggle != null)
            showPoseToggle.isOn = currentSettings.showPoseOverlay;
        
        if (previewSizeSlider != null)
            previewSizeSlider.value = currentSettings.previewSize;
        
        if (previewAlphaSlider != null)
            previewAlphaSlider.value = currentSettings.previewAlpha;
        
        if (previewPositionDropdown != null)
            previewPositionDropdown.value = currentSettings.previewPosition;
        
        UpdateValueDisplays();
    }
    
    private void UpdateValueDisplays()
    {
        if (sensitivityValue != null)
            sensitivityValue.text = currentSettings.sensitivity.ToString("F3");
        
        if (debounceValue != null)
            debounceValue.text = currentSettings.debounceTime.ToString("F1") + "s";
    }
    
    private void UpdateCameraSettingsVisibility()
    {
        bool showCameraSettings = inputModeDropdown != null && 
                                 inputModeDropdown.value == 3; // Camera Body Tracking
        
        if (cameraSettingsGroup != null)
        {
            cameraSettingsGroup.SetActive(showCameraSettings);
        }
    }
    
    private void Update()
    {
        UpdateStatus();
    }
    
    private void UpdateStatus()
    {
        if (statusText == null || cameraTracker == null) return;
        
        string status = "Camera: ";
        
        if (cameraTracker.IsInitialized())
        {
            if (cameraTracker.IsCameraActive())
            {
                status += cameraTracker.HasPoseDetection() ? "Active - Pose Detected" : "Active - Searching";
                
                if (connectionIndicator != null)
                    connectionIndicator.color = Color.green;
            }
            else
            {
                status += "Inactive";
                if (connectionIndicator != null)
                    connectionIndicator.color = Color.yellow;
            }
        }
        else
        {
            status += "Not Initialized";
            if (connectionIndicator != null)
                connectionIndicator.color = Color.red;
        }
        
        statusText.text = status;
    }
    
    // Event handlers
    private void OnInputModeChanged(int index)
    {
        if (boatController != null)
        {
            boatController.SetInputMode((BoatController.InputMode)index);
        }
        
        UpdateCameraSettingsVisibility();
    }
    
    private void OnCameraChanged(int index)
    {
        currentSettings.selectedCamera = index;
        
        if (cameraTracker != null)
        {
            cameraTracker.SwitchCamera(index);
        }
        
        SaveSettings();
    }
    
    private void OnSensitivityChanged(float value)
    {
        currentSettings.sensitivity = value;
        
        if (cameraTracker != null)
        {
            cameraTracker.SetDetectionThreshold(value);
        }
        
        UpdateValueDisplays();
        SaveSettings();
    }
    
    private void OnDebounceChanged(float value)
    {
        currentSettings.debounceTime = value;
        
        if (cameraTracker != null)
        {
            cameraTracker.SetDebounceTime(value);
        }
        
        UpdateValueDisplays();
        SaveSettings();
    }
    
    private void OnShoulderFallbackChanged(bool enabled)
    {
        currentSettings.enableShoulderFallback = enabled;
        // Apply to camera tracker if it has this setting
        SaveSettings();
    }
    
    private void OnShowPreviewChanged(bool enabled)
    {
        currentSettings.showPreview = enabled;
        
        if (previewUI != null)
        {
            previewUI.SetVisible(enabled);
        }
        
        SaveSettings();
    }
    
    private void OnShowPoseChanged(bool enabled)
    {
        currentSettings.showPoseOverlay = enabled;
        
        if (previewUI != null)
        {
            previewUI.TogglePoseOverlay();
        }
        
        SaveSettings();
    }
    
    private void OnPreviewSizeChanged(float value)
    {
        currentSettings.previewSize = value;
        
        if (previewUI != null)
        {
            Vector2 baseSize = new Vector2(200, 150);
            previewUI.SetPreviewSize(baseSize * value);
        }
        
        SaveSettings();
    }
    
    private void OnPreviewAlphaChanged(float value)
    {
        currentSettings.previewAlpha = value;
        
        if (previewUI != null)
        {
            previewUI.SetPreviewAlpha(value);
        }
        
        SaveSettings();
    }
    
    // Button actions
    private void TestCamera()
    {
        if (cameraTracker != null)
        {
            if (cameraTracker.IsCameraActive())
            {
                cameraTracker.StopTracking();
            }
            else
            {
                cameraTracker.StartTracking();
            }
        }
    }
    
    private void CalibrateCamera()
    {
        // Placeholder for calibration routine
        Debug.Log("Camera calibration started");
        
        if (statusText != null)
        {
            statusText.text = "Calibrating... Stand in neutral position";
        }
        
        // Start calibration coroutine
        StartCoroutine(CalibrationRoutine());
    }
    
    private System.Collections.IEnumerator CalibrationRoutine()
    {
        yield return new WaitForSeconds(3f);
        
        if (statusText != null)
        {
            statusText.text = "Calibration complete";
        }
        
        yield return new WaitForSeconds(2f);
        
        // Reset status display
        UpdateStatus();
    }
    
    // Public methods
    public void ShowSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }
    
    public void HideSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }
    
    public void ToggleSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(!settingsPanel.activeSelf);
        }
    }
    
    public void ResetToDefaults()
    {
        currentSettings = new CameraInputSettings();
        UpdateUIFromSettings();
        ApplyAllSettings();
        SaveSettings();
    }
    
    private void ApplyAllSettings()
    {
        if (cameraTracker != null)
        {
            cameraTracker.SwitchCamera(currentSettings.selectedCamera);
            cameraTracker.SetDetectionThreshold(currentSettings.sensitivity);
            cameraTracker.SetDebounceTime(currentSettings.debounceTime);
        }
        
        if (previewUI != null)
        {
            previewUI.SetVisible(currentSettings.showPreview);
            previewUI.SetPreviewAlpha(currentSettings.previewAlpha);
            Vector2 baseSize = new Vector2(200, 150);
            previewUI.SetPreviewSize(baseSize * currentSettings.previewSize);
        }
    }
    
    public CameraInputSettings GetCurrentSettings()
    {
        return currentSettings;
    }
    
    public void ApplySettings(CameraInputSettings settings)
    {
        currentSettings = settings;
        UpdateUIFromSettings();
        ApplyAllSettings();
        SaveSettings();
    }
}