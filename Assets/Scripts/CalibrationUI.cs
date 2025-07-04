using UnityEngine;

public class CalibrationUI : MonoBehaviour
{
    [Header("UI Settings")]
    public bool showCalibrationUI = true;
    public Color backgroundColor = new Color(0, 0, 0, 0.8f);
    public Color progressBarColor = Color.green;
    public Color textColor = Color.white;
    
    private GyroIntegratedController gyroController;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private Texture2D backgroundTexture;
    private Texture2D progressTexture;
    
    void Start()
    {
        gyroController = FindObjectOfType<GyroIntegratedController>();
        SetupStyles();
    }
    
    void SetupStyles()
    {
        // Title style
        titleStyle = new GUIStyle();
        titleStyle.fontSize = 32;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = textColor;
        
        // Subtitle style
        subtitleStyle = new GUIStyle();
        subtitleStyle.fontSize = 18;
        subtitleStyle.alignment = TextAnchor.MiddleCenter;
        subtitleStyle.normal.textColor = textColor;
        
        // Textures
        backgroundTexture = CreateTexture(backgroundColor);
        progressTexture = CreateTexture(progressBarColor);
    }
    
    Texture2D CreateTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
    
    void OnGUI()
    {
        if (!showCalibrationUI || gyroController == null) return;
        
        if (gyroController.IsCalibrating())
        {
            DrawCalibrationOverlay();
        }
        else if (!gyroController.IsCalibrated())
        {
            DrawNotCalibratedOverlay();
        }
    }
    
    void DrawCalibrationOverlay()
    {
        // Full screen overlay
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), backgroundTexture);
        
        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        
        // Title
        GUI.Label(new Rect(centerX - 200, centerY - 100, 400, 50), 
                  "CALIBRATING GYRO", titleStyle);
        
        // Instruction
        GUI.Label(new Rect(centerX - 200, centerY - 40, 400, 30), 
                  "Hold paddle steady and level", subtitleStyle);
        
        // Progress bar background
        float barWidth = 400;
        float barHeight = 20;
        float barX = centerX - barWidth * 0.5f;
        float barY = centerY + 20;
        
        GUI.color = Color.gray;
        GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), backgroundTexture);
        
        // Progress bar fill
        float progress = GetCalibrationProgress();
        GUI.color = progressBarColor;
        GUI.DrawTexture(new Rect(barX, barY, barWidth * progress, barHeight), progressTexture);
        
        // Progress text
        GUI.color = textColor;
        GUI.Label(new Rect(centerX - 100, centerY + 60, 200, 30), 
                  $"{progress * 100:F0}%", subtitleStyle);
        
        // Sample count
        GUI.Label(new Rect(centerX - 150, centerY + 90, 300, 25), 
                  $"Samples: {GetCalibrationSamples()}", subtitleStyle);
        
        GUI.color = Color.white;
    }
    
    void DrawNotCalibratedOverlay()
    {
        // Semi-transparent overlay
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), backgroundTexture);
        
        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        
        // Warning
        GUI.color = Color.red;
        GUI.Label(new Rect(centerX - 200, centerY - 60, 400, 50), 
                  "GYRO NOT CALIBRATED", titleStyle);
        
        GUI.color = textColor;
        GUI.Label(new Rect(centerX - 200, centerY - 10, 400, 30), 
                  "Press CALIBRATE to start", subtitleStyle);
        
        // Calibrate button
        if (GUI.Button(new Rect(centerX - 75, centerY + 30, 150, 40), "CALIBRATE"))
        {
            gyroController.StartCalibration();
        }
        
        GUI.color = Color.white;
    }
    
    float GetCalibrationProgress()
    {
        if (gyroController == null) return 0f;
        
        // Calculate based on remaining time
        float elapsed = gyroController.calibrationDuration - gyroController.GetCalibrationTimer();
        return Mathf.Clamp01(elapsed / gyroController.calibrationDuration);
    }
    
    int GetCalibrationSamples()
    {
        if (gyroController == null) return 0;
        return gyroController.GetCalibrationSampleCount();
    }
    
    void OnDestroy()
    {
        if (backgroundTexture != null) Destroy(backgroundTexture);
        if (progressTexture != null) Destroy(progressTexture);
    }
}