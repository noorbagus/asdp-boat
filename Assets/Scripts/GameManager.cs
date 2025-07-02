using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private float startingHealth = 100f;
    [SerializeField] private int maxLives = 5;
    [SerializeField] private float levelTimeLimit = 180f; // 3 minutes
    [SerializeField] private int scoreToWin = 300; // Optional win condition

    [Header("References")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private BoatController playerBoat;
    [SerializeField] private InputSettingsManager inputSettings;
    [SerializeField] private CameraBodyTracker cameraTracker;
    [SerializeField] private CameraPreviewUI cameraPreviewUI;

    [Header("Camera Integration")]
    [SerializeField] private bool autoLoadCameraSettings = true;
    [SerializeField] private bool autoStartCameraInCameraMode = true;
    [SerializeField] private float cameraInitDelay = 1f;

    // Game state
    private float currentHealth;
    private int currentScore = 0;
    private int currentLives;
    private bool isGameActive = false;
    private bool isLevelComplete = false;
    private bool isGameOver = false;

    // Camera settings
    private InputSettingsManager.CameraInputSettings cameraSettings;

    // Singleton pattern
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        // Singleton implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }

    private void Start()
    {
        InitializeGame();
        InitializeCameraSystem();
        StartGame();
    }

    private void InitializeGame()
    {
        // Initialize game state
        currentHealth = startingHealth;
        currentScore = 0;
        currentLives = maxLives;
        isGameActive = true;
        isLevelComplete = false;
        isGameOver = false;
        
        // Find references if not set
        FindComponents();
        
        // Update UI
        UpdateUI();
    }

    private void FindComponents()
    {
        if (uiManager == null)
            uiManager = FindObjectOfType<UIManager>();
        
        if (playerBoat == null)
            playerBoat = FindObjectOfType<BoatController>();
            
        if (inputSettings == null)
            inputSettings = FindObjectOfType<InputSettingsManager>();
            
        if (cameraTracker == null)
            cameraTracker = FindObjectOfType<CameraBodyTracker>();
            
        if (cameraPreviewUI == null)
            cameraPreviewUI = FindObjectOfType<CameraPreviewUI>();
    }

    private void InitializeCameraSystem()
    {
        if (!autoLoadCameraSettings) return;

        // Load camera settings from PlayerPrefs
        LoadCameraSettings();

        // Apply settings to camera system
        if (inputSettings != null)
        {
            inputSettings.ApplySettings(cameraSettings);
        }

        // Set input mode based on saved preferences
        var savedInputMode = (BoatController.InputMode)PlayerPrefs.GetInt("InputMode", 0);
        if (playerBoat != null)
        {
            playerBoat.SetInputMode(savedInputMode);
        }

        // Auto-start camera if in camera mode
        if (savedInputMode == BoatController.InputMode.CameraBodyTracking && autoStartCameraInCameraMode)
        {
            StartCoroutine(InitializeCameraWithDelay());
        }
    }

    private void LoadCameraSettings()
    {
        cameraSettings = new InputSettingsManager.CameraInputSettings
        {
            selectedCamera = PlayerPrefs.GetInt("CameraIndex", 0),
            sensitivity = PlayerPrefs.GetFloat("CameraSensitivity", 0.05f),
            debounceTime = PlayerPrefs.GetFloat("CameraDebounce", 0.3f),
            enableShoulderFallback = PlayerPrefs.GetInt("ShoulderFallback", 1) == 1,
            showPreview = PlayerPrefs.GetInt("ShowPreview", 1) == 1,
            showPoseOverlay = PlayerPrefs.GetInt("ShowPoseOverlay", 1) == 1,
            previewSize = PlayerPrefs.GetFloat("PreviewSize", 1f),
            previewAlpha = PlayerPrefs.GetFloat("PreviewAlpha", 0.8f),
            previewPosition = PlayerPrefs.GetInt("PreviewPosition", 0)
        };
    }

    private IEnumerator InitializeCameraWithDelay()
    {
        yield return new WaitForSeconds(cameraInitDelay);

        if (cameraTracker != null)
        {
            cameraTracker.StartTracking();
        }
    }

    private void UpdateUI()
    {
        if (uiManager != null)
        {
            uiManager.UpdateHealth(currentHealth / startingHealth);
            uiManager.UpdateScore(currentScore);
            uiManager.UpdateLives(currentLives);
        }
    }

    private void StartGame()
    {
        isGameActive = true;
        Time.timeScale = 1.0f;
        
        // Enable player controls
        if (playerBoat != null)
        {
            playerBoat.enabled = true;
        }
    }

    public void PauseGame()
    {
        isGameActive = false;
        Time.timeScale = 0f;
        
        if (uiManager != null)
        {
            uiManager.TogglePause();
        }
    }

    public void ResumeGame()
    {
        isGameActive = true;
        Time.timeScale = 1.0f;
        
        if (uiManager != null)
        {
            uiManager.TogglePause();
        }
    }

    public void TakeDamage(int damageAmount)
    {
        if (!isGameActive || isGameOver || isLevelComplete) return;
        
        currentHealth -= damageAmount;
        
        // Update UI
        if (uiManager != null)
        {
            uiManager.UpdateHealth(currentHealth / startingHealth);
        }
        
        // Check for game over
        if (currentHealth <= 0)
        {
            GameOver();
        }
    }

    public void AddScore(int points)
    {
        if (!isGameActive || isGameOver || isLevelComplete) return;
        
        currentScore += points;
        
        // Update UI
        if (uiManager != null)
        {
            uiManager.UpdateScore(currentScore);
        }
        
        // Optional: Check for score-based win condition
        if (currentScore >= scoreToWin)
        {
            LevelComplete();
        }
    }

    // Reduce score (for whale collision)
    public void ReduceScore(int points)
    {
        if (!isGameActive || isGameOver || isLevelComplete) return;
        
        currentScore -= points;
        
        // Ensure score doesn't go below 0
        if (currentScore < 0)
        {
            currentScore = 0;
        }
        
        // Update UI
        if (uiManager != null)
        {
            uiManager.UpdateScore(currentScore);
        }
    }

    // Reduce lives (for octopus collision)
    public void ReduceLives(int livesToReduce = 1)
    {
        if (!isGameActive || isGameOver || isLevelComplete) return;
        
        currentLives -= livesToReduce;
        
        // Ensure lives don't go below 0
        if (currentLives < 0)
        {
            currentLives = 0;
        }
        
        // Update UI
        if (uiManager != null)
        {
            uiManager.UpdateLives(currentLives);
        }
        
        // Check for game over
        if (currentLives <= 0)
        {
            GameOver();
        }
    }

    // Handle treasure collection
    public void CollectTreasure(GameObject treasure)
    {
        AddScore(100); // Fixed 100 points per treasure
        
        // Destroy treasure
        if (treasure != null)
        {
            Destroy(treasure);
        }
        
        Debug.Log("Treasure collected! +100 points");
    }

    // Handle whale collision
    public void HitWhale(GameObject whale)
    {
        ReduceScore(50); // Lose 50 points
        
        // Destroy whale
        if (whale != null)
        {
            Destroy(whale);
        }
        
        Debug.Log("Hit whale! -50 points");
    }

    // Handle octopus collision
    public void HitOctopus(GameObject octopus)
    {
        ReduceLives(1); // Lose 1 life
        
        // Destroy octopus
        if (octopus != null)
        {
            Destroy(octopus);
        }
        
        Debug.Log("Hit octopus! -1 life");
    }

    public void GameOver()
    {
        isGameOver = true;
        isGameActive = false;
        
        // Disable player controls
        if (playerBoat != null)
        {
            playerBoat.enabled = false;
        }
        
        // Stop camera tracking
        if (cameraTracker != null && cameraTracker.IsCameraActive())
        {
            cameraTracker.StopTracking();
        }
        
        // Show game over UI
        if (uiManager != null)
        {
            uiManager.GameOver();
        }
    }

    public void LevelComplete()
    {
        isLevelComplete = true;
        isGameActive = false;
        
        // Disable player controls
        if (playerBoat != null)
        {
            playerBoat.enabled = false;
        }
        
        // Stop camera tracking
        if (cameraTracker != null && cameraTracker.IsCameraActive())
        {
            cameraTracker.StopTracking();
        }
        
        // Show level complete UI
        if (uiManager != null)
        {
            uiManager.LevelComplete(currentScore);
        }
    }

    public void RestartLevel()
    {
        // Save current input mode
        if (playerBoat != null)
        {
            PlayerPrefs.SetInt("InputMode", (int)playerBoat.GetInputMode());
        }
        
        // Reload current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMainMenu()
    {
        // Save current input mode
        if (playerBoat != null)
        {
            PlayerPrefs.SetInt("InputMode", (int)playerBoat.GetInputMode());
        }
        
        // Stop camera tracking
        if (cameraTracker != null && cameraTracker.IsCameraActive())
        {
            cameraTracker.StopTracking();
        }
        
        // Load main menu scene
        SceneManager.LoadScene("MainMenu");
    }

    public void NextLevel()
    {
        // Save current input mode
        if (playerBoat != null)
        {
            PlayerPrefs.SetInt("InputMode", (int)playerBoat.GetInputMode());
        }
        
        // Load next scene
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            // No more levels, go back to main menu
            LoadMainMenu();
        }
    }

    // Camera system methods
    public void SwitchInputMode(BoatController.InputMode newMode)
    {
        if (playerBoat != null)
        {
            var oldMode = playerBoat.GetInputMode();
            playerBoat.SetInputMode(newMode);
            
            // Handle camera mode changes
            if (newMode == BoatController.InputMode.CameraBodyTracking)
            {
                if (cameraTracker != null && !cameraTracker.IsCameraActive())
                {
                    cameraTracker.StartTracking();
                }
            }
            else if (oldMode == BoatController.InputMode.CameraBodyTracking)
            {
                if (cameraTracker != null && cameraTracker.IsCameraActive())
                {
                    cameraTracker.StopTracking();
                }
            }
            
            // Save preference
            PlayerPrefs.SetInt("InputMode", (int)newMode);
        }
    }

    public void OnCameraPaddleTrigger(bool isLeft)
    {
        // Notify UI for visual feedback
        if (uiManager != null)
        {
            uiManager.OnPaddleTrigger(isLeft);
        }
    }
    
    // Getters for current game state
    public float GetHealthPercent() { return currentHealth / startingHealth; }
    public int GetScore() { return currentScore; }
    public int GetLives() { return currentLives; }
    public int GetMaxLives() { return maxLives; }
    public bool IsGameActive() { return isGameActive; }
    public bool IsGameOver() { return isGameOver; }
    public bool IsLevelComplete() { return isLevelComplete; }
    public InputSettingsManager.CameraInputSettings GetCameraSettings() { return cameraSettings; }
}