using UnityEngine;
using System.Collections;

public class CalibrationUI : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private bool enableCalibrationUI = true;
    [SerializeField] private float readyDisplayTime = 2f;
    
    [Header("Styling")]
    [SerializeField] private Color backgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
    [SerializeField] private Color accentColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField] private Color progressColor = new Color(0.3f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color textColor = Color.white;
    
    private GravityCalibrator calibrator;
    private bool isVisible = false;
    private float fadeAlpha = 0f;
    
    // UI Styles
    private GUIStyle titleStyle;
    private GUIStyle instructionStyle;
    private GUIStyle progressStyle;
    private Texture2D backgroundTexture;
    private Texture2D progressTexture;
    private Texture2D progressBgTexture;
    
    private void Start()
    {
        calibrator = FindObjectOfType<GravityCalibrator>();
        SetupStyles();
        SetupEventListeners();
    }
    
    private void SetupStyles()
    {
        // Title style - large, bold
        titleStyle = new GUIStyle();
        titleStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.06f);
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = textColor;
        
        // Instruction style - medium, clean
        instructionStyle = new GUIStyle();
        instructionStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.035f);
        instructionStyle.alignment = TextAnchor.MiddleCenter;
        instructionStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 0.9f);
        instructionStyle.wordWrap = true;
        
        // Progress text style
        progressStyle = new GUIStyle();
        progressStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.04f);
        progressStyle.fontStyle = FontStyle.Bold;
        progressStyle.alignment = TextAnchor.MiddleCenter;
        progressStyle.normal.textColor = accentColor;
        
        // Create textures
        backgroundTexture = CreateTexture(backgroundColor);
        progressTexture = CreateTexture(progressColor);
        progressBgTexture = CreateTexture(new Color(0.2f, 0.2f, 0.3f, 0.8f));
    }
    
    private Texture2D CreateTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
    
    private void SetupEventListeners()
    {
        if (calibrator != null)
        {
            calibrator.OnCalibrationStateChanged += OnCalibrationStateChanged;
            calibrator.OnCalibrationComplete += OnCalibrationComplete;
        }
    }
    
    private void OnCalibrationStateChanged(bool isCalibrating)
    {
        if (isCalibrating)
        {
            ShowCalibrationUI();
        }
    }
    
    private void OnCalibrationComplete(Vector3 offset)
    {
        StartCoroutine(ShowCompletionAndHide());
    }
    
    private IEnumerator ShowCompletionAndHide()
    {
        yield return new WaitForSeconds(readyDisplayTime);
        HideCalibrationUI();
    }
    
    public void ShowCalibrationUI()
    {
        isVisible = true;
        fadeAlpha = 1f;
    }
    
    public void HideCalibrationUI()
    {
        isVisible = false;
        fadeAlpha = 0f;
    }
    
    private void OnGUI()
    {
        if (!enableCalibrationUI || !isVisible || calibrator == null) return;
        
        // Full screen background
        GUI.color = new Color(1f, 1f, 1f, fadeAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), backgroundTexture);
        
        // Calculate responsive dimensions
        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        float cardWidth = Mathf.Min(Screen.width * 0.7f, 800f);
        float cardHeight = Screen.height * 0.4f;
        
        // Main card background with rounded effect (fake with multiple rects)
        Rect cardRect = new Rect(centerX - cardWidth * 0.5f, centerY - cardHeight * 0.5f, cardWidth, cardHeight);
        GUI.color = new Color(1f, 1f, 1f, fadeAlpha * 0.9f);
        
        // Draw card shadow
        GUI.DrawTexture(new Rect(cardRect.x + 8, cardRect.y + 8, cardRect.width, cardRect.height), 
                       CreateTexture(new Color(0, 0, 0, 0.3f)));
        
        // Draw main card
        GUI.DrawTexture(cardRect, CreateTexture(new Color(0.1f, 0.1f, 0.15f, 0.95f)));
        
        // Title
        GUI.color = new Color(1f, 1f, 1f, fadeAlpha);
        float titleY = cardRect.y + cardHeight * 0.15f;
        GUI.Label(new Rect(cardRect.x, titleY, cardWidth, titleStyle.fontSize + 10), 
                  calibrator.IsCalibrated() ? "Calibration Complete!" : "Calibrating Paddle", titleStyle);
        
        // Icon/Status indicator
        float iconSize = Screen.height * 0.08f;
        Rect iconRect = new Rect(centerX - iconSize * 0.5f, titleY + titleStyle.fontSize + 20, iconSize, iconSize);
        
        if (calibrator.IsCalibrated())
        {
            // Checkmark circle
            GUI.color = new Color(progressColor.r, progressColor.g, progressColor.b, fadeAlpha);
            GUI.DrawTexture(iconRect, CreateTexture(progressColor));
            GUI.color = new Color(1f, 1f, 1f, fadeAlpha);
            GUI.Label(iconRect, "✓", new GUIStyle { fontSize = Mathf.RoundToInt(iconSize * 0.6f), 
                     alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });
        }
        else
        {
            // Spinning loading circle (fake animation with time-based rotation)
            Matrix4x4 matrixBackup = GUI.matrix;
            GUIUtility.RotateAroundPivot(Time.time * 90f, iconRect.center);
            GUI.color = new Color(accentColor.r, accentColor.g, accentColor.b, fadeAlpha);
            GUI.DrawTexture(iconRect, CreateTexture(accentColor));
            GUI.matrix = matrixBackup;
        }
        
        // Instruction text
        GUI.color = new Color(1f, 1f, 1f, fadeAlpha * 0.8f);
        float instrY = iconRect.y + iconSize + 30;
        string instruction = calibrator.IsCalibrated() ? "Ready to play!" : "Hold paddle flat and still";
        GUI.Label(new Rect(cardRect.x + 20, instrY, cardWidth - 40, instructionStyle.fontSize + 10), 
                  instruction, instructionStyle);
        
        // Progress bar (only during calibration)
        if (!calibrator.IsCalibrated())
        {
            float progress = calibrator.GetCalibrationProgress();
            float barWidth = cardWidth * 0.8f;
            float barHeight = 12f;
            float barX = centerX - barWidth * 0.5f;
            float barY = instrY + instructionStyle.fontSize + 40;
            
            // Progress bar background
            GUI.color = new Color(1f, 1f, 1f, fadeAlpha * 0.3f);
            GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), progressBgTexture);
            
            // Progress bar fill with glow effect
            GUI.color = new Color(progressColor.r, progressColor.g, progressColor.b, fadeAlpha);
            float fillWidth = barWidth * progress;
            GUI.DrawTexture(new Rect(barX, barY, fillWidth, barHeight), progressTexture);
            
            // Progress glow
            if (progress > 0)
            {
                GUI.color = new Color(progressColor.r, progressColor.g, progressColor.b, fadeAlpha * 0.3f);
                GUI.DrawTexture(new Rect(barX, barY - 2, fillWidth, barHeight + 4), progressTexture);
            }
            
            // Progress text
            GUI.color = new Color(1f, 1f, 1f, fadeAlpha);
            string progressText = $"{calibrator.GetCurrentSampleCount()} / {calibrator.GetRequiredSamples()}";
            GUI.Label(new Rect(cardRect.x, barY + barHeight + 15, cardWidth, progressStyle.fontSize + 5), 
                     progressText, progressStyle);
            
            // Status text
            GUI.color = new Color(1f, 1f, 1f, fadeAlpha * 0.7f);
            string status = "Keep the paddle steady to collect samples";
            GUIStyle statusStyle = new GUIStyle(instructionStyle);
            statusStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.025f);
            GUI.Label(new Rect(cardRect.x + 20, barY + barHeight + 45, cardWidth - 40, statusStyle.fontSize + 5), 
                     status, statusStyle);
        }
        
        GUI.color = Color.white;
    }
    
    public bool IsVisible() => isVisible;
    
    private void OnDestroy()
    {
        if (calibrator != null)
        {
            calibrator.OnCalibrationStateChanged -= OnCalibrationStateChanged;
            calibrator.OnCalibrationComplete -= OnCalibrationComplete;
        }
        
        // Cleanup textures
        if (backgroundTexture != null) DestroyImmediate(backgroundTexture);
        if (progressTexture != null) DestroyImmediate(progressTexture);
        if (progressBgTexture != null) DestroyImmediate(progressBgTexture);
    }
}