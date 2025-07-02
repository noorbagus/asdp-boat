using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI livesText;
    [SerializeField] private TextMeshProUGUI connectionStatusText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Slider healthBar;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject levelCompletePanel;
    [SerializeField] private TextMeshProUGUI finalScoreText;
    
    [Header("Camera Preview Integration")]
    [SerializeField] private CameraPreviewUI cameraPreviewUI;
    [SerializeField] private Button togglePreviewButton;
    [SerializeField] private Button cameraSettingsButton;
    [SerializeField] private InputSettingsManager inputSettings;
    [SerializeField] private GameObject cameraStatusPanel;
    [SerializeField] private TextMeshProUGUI cameraStatusText;
    [SerializeField] private Image cameraConnectionIndicator;
    
    [Header("Input Mode Display")]
    [SerializeField] private TextMeshProUGUI inputModeText;
    [SerializeField] private GameObject inputModePanel;
    [SerializeField] private Color keyboardColor = Color.white;
    [SerializeField] private Color bluetoothColor = Color.blue;
    [SerializeField] private Color cameraColor = Color.green;
    
    [Header("Game Settings")]
    [SerializeField] private float levelTimeLimit = 180f; // 3 minutes
    
    // Component references
    private CameraBodyTracker cameraTracker;
    private BoatController boatController;
    
    private float currentTime;
    private bool isGameActive = false;
    private bool isPaused = false;
    
    private void Start()
    {
        InitializeComponents();
        InitializeUI();
        SetupCameraIntegration();
    }
    
    private void InitializeComponents()
    {
        // Find camera components
        if (cameraPreviewUI == null)
            cameraPreviewUI = FindObjectOfType<CameraPreviewUI>();
        
        if (cameraTracker == null)
            cameraTracker = FindObjectOfType<CameraBodyTracker>();
        
        if (boatController == null)
            boatController = FindObjectOfType<BoatController>();
        
        if (inputSettings == null)
            inputSettings = FindObjectOfType<InputSettingsManager>();
    }
    
    private void InitializeUI()
    {
        // Initialize standard UI elements
        UpdateScore(0);
        UpdateLives(5);
        UpdateConnectionStatus("Disconnected");
        
        if (healthBar != null)
            healthBar.value = 1.0f;
        
        // Hide panels
        if (pausePanel != null) pausePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        
        // Setup buttons
        SetupButtons();
        
        // Start timer
        currentTime = levelTimeLimit;
        isGameActive = true;
        Time.timeScale = 1.0f;
    }
    
    private void SetupButtons()
    {
        if (togglePreviewButton != null)
            togglePreviewButton.onClick.AddListener(ToggleCameraPreview);
        
        if (cameraSettingsButton != null)
            cameraSettingsButton.onClick.AddListener(ShowCameraSettings);
    }
    
    private void SetupCameraIntegration()
    {
        // Subscribe to camera events
        if (cameraTracker != null)
        {
            cameraTracker.OnCameraStatusChanged += OnCameraStatusChanged;
            cameraTracker.OnErrorOccurred += OnCameraError;
        }
        
        // Update camera preview visibility based on input mode
        UpdateCameraPreviewVisibility();
        UpdateInputModeDisplay();
    }
    
    private void Update()
    {
        if (isGameActive && !isPaused)
        {
            UpdateTimer();
            
            if (Input.GetKeyDown(KeyCode.Escape))
                TogglePause();
        }
        
        UpdateCameraStatus();
        UpdateInputModeDisplay();
    }
    
    private void UpdateTimer()
    {
        currentTime -= Time.deltaTime;
        
        if (currentTime <= 0)
        {
            currentTime = 0;
            GameOver();
        }
        
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(currentTime / 60);
            int seconds = Mathf.FloorToInt(currentTime % 60);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
    
    private void UpdateCameraStatus()
    {
        if (cameraStatusText == null || cameraTracker == null) return;
        
        string status = "";
        Color indicatorColor = Color.red;
        
        if (cameraTracker.IsInitialized())
        {
            if (cameraTracker.IsCameraActive())
            {
                if (cameraTracker.HasPoseDetection())
                {
                    status = "Camera: Active - Pose Detected";
                    indicatorColor = Color.green;
                }
                else
                {
                    status = "Camera: Active - Searching";
                    indicatorColor = Color.yellow;
                }
            }
            else
            {
                status = "Camera: Inactive";
                indicatorColor = Color.gray;
            }
        }
        else
        {
            status = "Camera: Not Initialized";
            indicatorColor = Color.red;
        }
        
        cameraStatusText.text = status;
        
        if (cameraConnectionIndicator != null)
            cameraConnectionIndicator.color = indicatorColor;
    }
    
    private void UpdateInputModeDisplay()
    {
        if (inputModeText == null || boatController == null) return;
        
        var inputMode = boatController.GetInputMode();
        string modeText = "";
        Color modeColor = Color.white;
        
        switch (inputMode)
        {
            case BoatController.InputMode.Keyboard:
                modeText = "Keyboard";
                modeColor = keyboardColor;
                break;
            case BoatController.InputMode.BluetoothSensor:
                modeText = "Bluetooth";
                modeColor = bluetoothColor;
                break;
            case BoatController.InputMode.KeyboardWithDirectControl:
                modeText = "Direct Control";
                modeColor = keyboardColor;
                break;
            case BoatController.InputMode.CameraBodyTracking:
                modeText = "Camera";
                modeColor = cameraColor;
                break;
        }
        
        inputModeText.text = $"Input: {modeText}";
        inputModeText.color = modeColor;
        
        // Show/hide camera-specific UI
        UpdateCameraPreviewVisibility();
    }
    
    private void UpdateCameraPreviewVisibility()
    {
        bool isCameraMode = boatController != null && 
                           boatController.GetInputMode() == BoatController.InputMode.CameraBodyTracking;
        
        // Show camera preview only in camera mode
        if (cameraPreviewUI != null)
        {
            cameraPreviewUI.SetVisible(isCameraMode && cameraTracker != null && cameraTracker.IsCameraActive());
        }
        
        // Show camera status panel in camera mode
        if (cameraStatusPanel != null)
        {
            cameraStatusPanel.SetActive(isCameraMode);
        }
        
        // Show camera control buttons in camera mode
        if (togglePreviewButton != null)
        {
            togglePreviewButton.gameObject.SetActive(isCameraMode);
        }
        
        if (cameraSettingsButton != null)
        {
            cameraSettingsButton.gameObject.SetActive(isCameraMode);
        }
    }
    
    // Camera event handlers
    private void OnCameraStatusChanged(bool isActive)
    {
        UpdateCameraPreviewVisibility();
        
        if (connectionStatusText != null)
        {
            string status = isActive ? "Camera Active" : "Camera Inactive";
            UpdateConnectionStatus(status);
        }
    }
    
    private void OnCameraError(string error)
    {
        if (connectionStatusText != null)
        {
            UpdateConnectionStatus($"Camera Error: {error}");
        }
    }
    
    // Button handlers
    private void ToggleCameraPreview()
    {
        if (cameraPreviewUI != null)
        {
            cameraPreviewUI.TogglePreview();
        }
    }
    
    private void ShowCameraSettings()
    {
        if (inputSettings != null)
        {
            inputSettings.ShowSettings();
        }
    }
    
    // Standard UI methods (unchanged)
    public void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = score.ToString();
    }
    
    public void UpdateLives(int lives)
    {
        if (livesText != null)
        {
            livesText.text = $"{lives}/5";
            
            if (lives <= 1)
                livesText.color = Color.red;
            else if (lives <= 2)
                livesText.color = Color.yellow;
            else
                livesText.color = Color.white;
        }
    }
    
    public void UpdateHealth(float healthPercent)
    {
        if (healthBar != null)
        {
            healthBar.value = Mathf.Clamp01(healthPercent);
            
            if (healthBar.value < 0.3f)
                healthBar.fillRect.GetComponent<Image>().color = Color.red;
            else if (healthBar.value < 0.6f)
                healthBar.fillRect.GetComponent<Image>().color = Color.yellow;
            else
                healthBar.fillRect.GetComponent<Image>().color = Color.green;
        }
    }
    
    public void UpdateConnectionStatus(string status)
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = status;
            
            if (status.Contains("Connected") || status.Contains("Active"))
                connectionStatusText.color = Color.green;
            else if (status.Contains("Disconnected") || status.Contains("failed") || status.Contains("Error"))
                connectionStatusText.color = Color.red;
            else
                connectionStatusText.color = Color.yellow;
        }
    }
    
    public void TogglePause()
    {
        isPaused = !isPaused;
        
        if (pausePanel != null)
            pausePanel.SetActive(isPaused);
        
        Time.timeScale = isPaused ? 0f : 1f;
    }
    
    public void Resume()
    {
        if (isPaused)
            TogglePause();
    }
    
    public void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    public void LoadMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
    
    public void GameOver()
    {
        isGameActive = false;
        
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
        
        if (finalScoreText != null && scoreText != null)
            finalScoreText.text = "Final Score: " + scoreText.text.Replace("Score: ", "");
        
        Time.timeScale = 0f;
    }
    
    public void LevelComplete(int finalScore)
    {
        isGameActive = false;
        
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(true);
        
        if (finalScoreText != null)
            finalScoreText.text = "Final Score: " + finalScore;
        
        Time.timeScale = 0f;
    }
    
    public void NextLevel()
    {
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(nextSceneIndex);
        else
            LoadMainMenu();
    }
    
    // Public methods for camera integration
    public void SetCameraPreviewVisible(bool visible)
    {
        if (cameraPreviewUI != null)
            cameraPreviewUI.SetVisible(visible);
    }
    
    public void OnPaddleTrigger(bool isLeft)
    {
        if (cameraPreviewUI != null)
            cameraPreviewUI.OnPaddleTrigger(isLeft);
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (cameraTracker != null)
        {
            cameraTracker.OnCameraStatusChanged -= OnCameraStatusChanged;
            cameraTracker.OnErrorOccurred -= OnCameraError;
        }
    }
}