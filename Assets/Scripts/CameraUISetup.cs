using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CameraUISetup : MonoBehaviour
{
    [Header("Main UI References")]
    public Canvas mainCanvas;
    public GameObject uiParent;
    
    [Header("Auto-Create UI")]
    [SerializeField] private bool createUIOnStart = true;
    [SerializeField] private bool setupEventListeners = true;
    
    // UI Components (will be created)
    private GameObject cameraPreviewPanel;
    private RawImage cameraPreviewImage;
    private GameObject inputModePanel;
    private TMP_Dropdown inputModeDropdown;
    private GameObject cameraSettingsPanel;
    private TMP_Dropdown cameraSelectionDropdown;
    private Slider sensitivitySlider;
    private Slider debounceSlider;
    private Toggle shoulderFallbackToggle;
    private Button testCameraButton;
    private TextMeshProUGUI statusText;
    private Image connectionIndicator;
    
    // References
    private CameraBodyTracker cameraTracker;
    private BoatController boatController;
    
    private void Start()
    {
        if (createUIOnStart)
        {
            SetupCompleteUI();
        }
    }
    
    [ContextMenu("Setup Complete UI")]
    public void SetupCompleteUI()
    {
        // Find or create main canvas
        SetupCanvas();
        
        // Create UI panels
        CreateCameraPreviewUI();
        CreateInputModeUI();
        CreateCameraSettingsUI();
        
        // Find references
        FindReferences();
        
        // Setup event listeners
        if (setupEventListeners)
        {
            SetupEventListeners();
        }
        
        Debug.Log("✓ Complete Camera UI Setup finished!");
    }
    
    private void SetupCanvas()
    {
        if (mainCanvas == null)
        {
            mainCanvas = FindObjectOfType<Canvas>();
            
            if (mainCanvas == null)
            {
                // Create main canvas
                GameObject canvasGO = new GameObject("Main Canvas");
                mainCanvas = canvasGO.AddComponent<Canvas>();
                mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                mainCanvas.sortingOrder = 100;
                
                // Add CanvasScaler and GraphicRaycaster
                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                
                canvasGO.AddComponent<GraphicRaycaster>();
                
                Debug.Log("Created Main Canvas");
            }
        }
        
        if (uiParent == null)
        {
            uiParent = new GameObject("Camera UI Parent");
            uiParent.transform.SetParent(mainCanvas.transform, false);
            
            var rect = uiParent.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
    
    private void CreateCameraPreviewUI()
    {
        // Camera Preview Panel (Top-right corner)
        cameraPreviewPanel = CreateUIPanel("Camera Preview Panel", uiParent.transform);
        var previewRect = cameraPreviewPanel.GetComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(1f, 1f);
        previewRect.anchorMax = new Vector2(1f, 1f);
        previewRect.anchoredPosition = new Vector2(-10f, -10f);
        previewRect.sizeDelta = new Vector2(300f, 200f);
        previewRect.pivot = new Vector2(1f, 1f);
        
        // Background
        var bg = cameraPreviewPanel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.8f);
        
        // Camera Preview Image
        var previewImageGO = new GameObject("Camera Preview");
        previewImageGO.transform.SetParent(cameraPreviewPanel.transform, false);
        
        cameraPreviewImage = previewImageGO.AddComponent<RawImage>();
        var imageRect = previewImageGO.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = new Vector2(10f, 40f);
        imageRect.offsetMax = new Vector2(-10f, -10f);
        
        // Status Text
        var statusGO = CreateUIText("Status Text", cameraPreviewPanel.transform);
        statusText = statusGO.GetComponent<TextMeshProUGUI>();
        statusText.text = "Camera: Inactive";
        statusText.fontSize = 14f;
        statusText.alignment = TextAlignmentOptions.Left;
        
        var statusRect = statusGO.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 20f);
        statusRect.sizeDelta = new Vector2(-40f, 20f);
        
        // Connection Indicator
        var indicatorGO = new GameObject("Connection Indicator");
        indicatorGO.transform.SetParent(cameraPreviewPanel.transform, false);
        
        connectionIndicator = indicatorGO.AddComponent<Image>();
        connectionIndicator.color = Color.red;
        
        var indicatorRect = indicatorGO.GetComponent<RectTransform>();
        indicatorRect.anchorMin = new Vector2(1f, 0f);
        indicatorRect.anchorMax = new Vector2(1f, 0f);
        indicatorRect.anchoredPosition = new Vector2(-20f, 20f);
        indicatorRect.sizeDelta = new Vector2(10f, 10f);
        
        Debug.Log("Created Camera Preview UI");
    }
    
    private void CreateInputModeUI()
    {
        // Input Mode Panel (Top-left corner)
        inputModePanel = CreateUIPanel("Input Mode Panel", uiParent.transform);
        var modeRect = inputModePanel.GetComponent<RectTransform>();
        modeRect.anchorMin = new Vector2(0f, 1f);
        modeRect.anchorMax = new Vector2(0f, 1f);
        modeRect.anchoredPosition = new Vector2(10f, -10f);
        modeRect.sizeDelta = new Vector2(250f, 80f);
        modeRect.pivot = new Vector2(0f, 1f);
        
        // Background
        var bg = inputModePanel.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        
        // Label
        var labelGO = CreateUIText("Input Mode Label", inputModePanel.transform);
        var label = labelGO.GetComponent<TextMeshProUGUI>();
        label.text = "Input Mode:";
        label.fontSize = 16f;
        label.fontStyle = FontStyles.Bold;
        
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.6f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(10f, 0f);
        labelRect.offsetMax = new Vector2(-10f, -5f);
        
        // Dropdown
        var dropdownGO = CreateUIDropdown("Input Mode Dropdown", inputModePanel.transform);
        inputModeDropdown = dropdownGO.GetComponent<TMP_Dropdown>();
        
        // Add options
        inputModeDropdown.options.Clear();
        inputModeDropdown.options.Add(new TMP_Dropdown.OptionData("Keyboard"));
        inputModeDropdown.options.Add(new TMP_Dropdown.OptionData("Bluetooth"));
        inputModeDropdown.options.Add(new TMP_Dropdown.OptionData("Direct Control"));
        inputModeDropdown.options.Add(new TMP_Dropdown.OptionData("Camera Tracking"));
        inputModeDropdown.value = 0;
        
        var dropdownRect = dropdownGO.GetComponent<RectTransform>();
        dropdownRect.anchorMin = new Vector2(0f, 0f);
        dropdownRect.anchorMax = new Vector2(1f, 0.6f);
        dropdownRect.offsetMin = new Vector2(10f, 10f);
        dropdownRect.offsetMax = new Vector2(-10f, -5f);
        
        Debug.Log("Created Input Mode UI");
    }
    
    private void CreateCameraSettingsUI()
    {
        // Camera Settings Panel (Left side, below input mode)
        cameraSettingsPanel = CreateUIPanel("Camera Settings Panel", uiParent.transform);
        var settingsRect = cameraSettingsPanel.GetComponent<RectTransform>();
        settingsRect.anchorMin = new Vector2(0f, 0.5f);
        settingsRect.anchorMax = new Vector2(0f, 0.5f);
        settingsRect.anchoredPosition = new Vector2(10f, 0f);
        settingsRect.sizeDelta = new Vector2(300f, 400f);
        settingsRect.pivot = new Vector2(0f, 0.5f);
        
        // Background
        var bg = cameraSettingsPanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        
        // Title
        var titleGO = CreateUIText("Settings Title", cameraSettingsPanel.transform);
        var title = titleGO.GetComponent<TextMeshProUGUI>();
        title.text = "Camera Settings";
        title.fontSize = 18f;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.9f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(10f, -5f);
        titleRect.offsetMax = new Vector2(-10f, -5f);
        
        float yPos = 0.8f;
        float itemHeight = 0.15f;
        
        // Camera Selection
        CreateSettingGroup("Camera:", out var cameraLabelRect, out var cameraControlRect, yPos, itemHeight);
        
        var cameraDropdownGO = CreateUIDropdown("Camera Dropdown", cameraSettingsPanel.transform);
        cameraSelectionDropdown = cameraDropdownGO.GetComponent<TMP_Dropdown>();
        PositionRectTransform(cameraDropdownGO.GetComponent<RectTransform>(), cameraControlRect);
        
        yPos -= itemHeight;
        
        // Sensitivity Slider
        CreateSettingGroup("Sensitivity:", out var sensLabelRect, out var sensControlRect, yPos, itemHeight);
        
        var sensSliderGO = CreateUISlider("Sensitivity Slider", cameraSettingsPanel.transform);
        sensitivitySlider = sensSliderGO.GetComponent<Slider>();
        sensitivitySlider.minValue = 0.01f;
        sensitivitySlider.maxValue = 0.2f;
        sensitivitySlider.value = 0.05f;
        PositionRectTransform(sensSliderGO.GetComponent<RectTransform>(), sensControlRect);
        
        yPos -= itemHeight;
        
        // Debounce Slider
        CreateSettingGroup("Debounce:", out var debounceLabelRect, out var debounceControlRect, yPos, itemHeight);
        
        var debounceSliderGO = CreateUISlider("Debounce Slider", cameraSettingsPanel.transform);
        debounceSlider = debounceSliderGO.GetComponent<Slider>();
        debounceSlider.minValue = 0.1f;
        debounceSlider.maxValue = 2f;
        debounceSlider.value = 0.3f;
        PositionRectTransform(debounceSliderGO.GetComponent<RectTransform>(), debounceControlRect);
        
        yPos -= itemHeight;
        
        // Shoulder Fallback Toggle
        CreateSettingGroup("Use Shoulders:", out var shoulderLabelRect, out var shoulderControlRect, yPos, itemHeight);
        
        var shoulderToggleGO = CreateUIToggle("Shoulder Toggle", cameraSettingsPanel.transform);
        shoulderFallbackToggle = shoulderToggleGO.GetComponent<Toggle>();
        shoulderFallbackToggle.isOn = true;
        PositionRectTransform(shoulderToggleGO.GetComponent<RectTransform>(), shoulderControlRect);
        
        yPos -= itemHeight * 1.5f;
        
        // Test Camera Button
        var testButtonGO = CreateUIButton("Test Camera", cameraSettingsPanel.transform);
        testCameraButton = testButtonGO.GetComponent<Button>();
        
        var buttonText = testButtonGO.GetComponentInChildren<TextMeshProUGUI>();
        buttonText.text = "Test Camera";
        
        var buttonRect = testButtonGO.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.1f, yPos - itemHeight);
        buttonRect.anchorMax = new Vector2(0.9f, yPos);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        
        // Initially hide camera settings
        cameraSettingsPanel.SetActive(false);
        
        Debug.Log("Created Camera Settings UI");
    }
    
    private void CreateSettingGroup(string labelText, out RectTransform labelRect, out RectTransform controlRect, float yPos, float height)
    {
        // Label
        var labelGO = CreateUIText($"{labelText} Label", cameraSettingsPanel.transform);
        var label = labelGO.GetComponent<TextMeshProUGUI>();
        label.text = labelText;
        label.fontSize = 14f;
        label.alignment = TextAlignmentOptions.MiddleLeft;
        
        labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.05f, yPos - height);
        labelRect.anchorMax = new Vector2(0.4f, yPos);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        
        // Control area
        controlRect = new RectTransform();
        controlRect.anchorMin = new Vector2(0.45f, yPos - height + 0.02f);
        controlRect.anchorMax = new Vector2(0.95f, yPos - 0.02f);
    }
    
    private void PositionRectTransform(RectTransform rect, RectTransform template)
    {
        rect.anchorMin = template.anchorMin;
        rect.anchorMax = template.anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
    
    private void FindReferences()
    {
        cameraTracker = FindObjectOfType<CameraBodyTracker>();
        boatController = FindObjectOfType<BoatController>();
        
        if (cameraTracker == null)
        {
            Debug.LogWarning("CameraBodyTracker not found!");
        }
        
        if (boatController == null)
        {
            Debug.LogWarning("BoatController not found!");
        }
    }
    
    private void SetupEventListeners()
    {
        // Input Mode Dropdown
        if (inputModeDropdown != null)
        {
            inputModeDropdown.onValueChanged.AddListener(OnInputModeChanged);
        }
        
        // Camera Selection Dropdown
        if (cameraSelectionDropdown != null)
        {
            cameraSelectionDropdown.onValueChanged.AddListener(OnCameraChanged);
            PopulateCameraDropdown();
        }
        
        // Sensitivity Slider
        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }
        
        // Debounce Slider
        if (debounceSlider != null)
        {
            debounceSlider.onValueChanged.AddListener(OnDebounceChanged);
        }
        
        // Shoulder Fallback Toggle
        if (shoulderFallbackToggle != null)
        {
            shoulderFallbackToggle.onValueChanged.AddListener(OnShoulderFallbackChanged);
        }
        
        // Test Camera Button
        if (testCameraButton != null)
        {
            testCameraButton.onClick.AddListener(OnTestCameraClicked);
        }
        
        // Camera Tracker Events
        if (cameraTracker != null)
        {
            cameraTracker.OnCameraStatusChanged += UpdateCameraStatus;
            cameraTracker.OnErrorOccurred += OnCameraError;
        }
        
        Debug.Log("Event listeners setup complete");
    }
    
    // Event Handlers
    private void OnInputModeChanged(int value)
    {
        if (boatController != null)
        {
            boatController.SetInputMode((BoatController.InputMode)value);
        }
        
        // Show/hide camera settings based on selection
        bool showCameraSettings = (value == 3); // Camera Tracking mode
        cameraSettingsPanel.SetActive(showCameraSettings);
        
        Debug.Log($"Input mode changed to: {(BoatController.InputMode)value}");
    }
    
    private void OnCameraChanged(int value)
    {
        if (cameraTracker != null)
        {
            cameraTracker.SwitchCamera(value);
        }
    }
    
    private void OnSensitivityChanged(float value)
    {
        if (cameraTracker != null)
        {
            cameraTracker.SetDetectionThreshold(value);
        }
    }
    
    private void OnDebounceChanged(float value)
    {
        if (cameraTracker != null)
        {
            cameraTracker.SetDebounceTime(value);
        }
    }
    
    private void OnShoulderFallbackChanged(bool value)
    {
        // Implementation depends on your camera tracker setup
        Debug.Log($"Shoulder fallback: {value}");
    }
    
    private void OnTestCameraClicked()
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
    
    private void UpdateCameraStatus(bool isActive)
    {
        if (statusText != null)
        {
            statusText.text = isActive ? "Camera: Active" : "Camera: Inactive";
        }
        
        if (connectionIndicator != null)
        {
            connectionIndicator.color = isActive ? Color.green : Color.red;
        }
        
        if (cameraTracker != null && isActive)
        {
            var cameraTexture = cameraTracker.GetCameraTexture();
            if (cameraPreviewImage != null)
            {
                cameraPreviewImage.texture = cameraTexture;
            }
        }
    }
    
    private void OnCameraError(string error)
    {
        if (statusText != null)
        {
            statusText.text = $"Error: {error}";
        }
    }
    
    private void PopulateCameraDropdown()
    {
        if (cameraTracker != null && cameraSelectionDropdown != null)
        {
            var cameras = cameraTracker.GetAvailableCameras();
            cameraSelectionDropdown.options.Clear();
            
            for (int i = 0; i < cameras.Length; i++)
            {
                cameraSelectionDropdown.options.Add(new TMP_Dropdown.OptionData($"Camera {i}: {cameras[i]}"));
            }
            
            cameraSelectionDropdown.value = cameraTracker.GetCurrentCameraIndex();
        }
    }
    
    // Helper methods for creating UI elements
    private GameObject CreateUIPanel(string name, Transform parent)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        panel.AddComponent<RectTransform>();
        return panel;
    }
    
    private GameObject CreateUIText(string name, Transform parent)
    {
        var textGO = new GameObject(name);
        textGO.transform.SetParent(parent, false);
        
        var rect = textGO.AddComponent<RectTransform>();
        var text = textGO.AddComponent<TextMeshProUGUI>();
        text.color = Color.white;
        text.fontSize = 14f;
        
        return textGO;
    }
    
    private GameObject CreateUIButton(string name, Transform parent)
    {
        var buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(parent, false);
        
        var rect = buttonGO.AddComponent<RectTransform>();
        var image = buttonGO.AddComponent<Image>();
        var button = buttonGO.AddComponent<Button>();
        
        // Button styling
        image.color = new Color(0.3f, 0.3f, 0.8f, 1f);
        
        // Button text
        var textGO = CreateUIText("Text", buttonGO.transform);
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        var text = textGO.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        
        return buttonGO;
    }
    
    private GameObject CreateUISlider(string name, Transform parent)
    {
        var sliderGO = new GameObject(name);
        sliderGO.transform.SetParent(parent, false);
        
        var rect = sliderGO.AddComponent<RectTransform>();
        var slider = sliderGO.AddComponent<Slider>();
        
        // Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        // Fill Area
        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;
        
        // Fill
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRect = fillGO.AddComponent<RectTransform>();
        var fillImage = fillGO.AddComponent<Image>();
        fillImage.color = new Color(0.4f, 0.6f, 1f, 1f);
        
        // Handle Slide Area
        var handleAreaGO = new GameObject("Handle Slide Area");
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        var handleAreaRect = handleAreaGO.AddComponent<RectTransform>();
        
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = Vector2.zero;
        handleAreaRect.offsetMax = Vector2.zero;
        
        // Handle
        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        var handleRect = handleGO.AddComponent<RectTransform>();
        var handleImage = handleGO.AddComponent<Image>();
        handleImage.color = Color.white;
        
        handleRect.sizeDelta = new Vector2(20f, 0f);
        
        // Configure slider
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        
        return sliderGO;
    }
    
    private GameObject CreateUIToggle(string name, Transform parent)
    {
        var toggleGO = new GameObject(name);
        toggleGO.transform.SetParent(parent, false);
        
        var rect = toggleGO.AddComponent<RectTransform>();
        var toggle = toggleGO.AddComponent<Toggle>();
        
        // Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(toggleGO.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        
        bgRect.anchorMin = new Vector2(0f, 0.5f);
        bgRect.anchorMax = new Vector2(0f, 0.5f);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.sizeDelta = new Vector2(20f, 20f);
        
        // Checkmark
        var checkGO = new GameObject("Checkmark");
        checkGO.transform.SetParent(bgGO.transform, false);
        var checkRect = checkGO.AddComponent<RectTransform>();
        var checkImage = checkGO.AddComponent<Image>();
        checkImage.color = Color.green;
        
        checkRect.anchorMin = Vector2.zero;
        checkRect.anchorMax = Vector2.one;
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;
        
        // Configure toggle
        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;
        
        return toggleGO;
    }
    
    private GameObject CreateUIDropdown(string name, Transform parent)
    {
        var dropdownGO = new GameObject(name);
        dropdownGO.transform.SetParent(parent, false);
        
        var rect = dropdownGO.AddComponent<RectTransform>();
        var image = dropdownGO.AddComponent<Image>();
        var dropdown = dropdownGO.AddComponent<TMP_Dropdown>();
        
        image.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        
        // Label
        var labelGO = CreateUIText("Label", dropdownGO.transform);
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 0f);
        labelRect.offsetMax = new Vector2(-25f, 0f);
        
        var labelText = labelGO.GetComponent<TextMeshProUGUI>();
        labelText.alignment = TextAlignmentOptions.MiddleLeft;
        
        // Arrow
        var arrowGO = CreateUIText("Arrow", dropdownGO.transform);
        var arrowRect = arrowGO.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1f, 0.5f);
        arrowRect.anchorMax = new Vector2(1f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(-15f, 0f);
        arrowRect.sizeDelta = new Vector2(20f, 20f);
        
        var arrowText = arrowGO.GetComponent<TextMeshProUGUI>();
        arrowText.text = "▼";
        arrowText.alignment = TextAlignmentOptions.Center;
        
        // Template (create but disable)
        var templateGO = CreateUIPanel("Template", dropdownGO.transform);
        var templateRect = templateGO.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.anchoredPosition = new Vector2(0f, 2f);
        templateRect.sizeDelta = new Vector2(0f, 150f);
        
        var templateImage = templateGO.AddComponent<Image>();
        templateImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        
        templateGO.AddComponent<ScrollRect>();
        templateGO.SetActive(false);
        
        // Configure dropdown
        dropdown.captionText = labelText;
        dropdown.template = templateRect;
        
        return dropdownGO;
    }
    
    private void OnDestroy()
    {
        // Cleanup event listeners
        if (cameraTracker != null)
        {
            cameraTracker.OnCameraStatusChanged -= UpdateCameraStatus;
            cameraTracker.OnErrorOccurred -= OnCameraError;
        }
    }
}