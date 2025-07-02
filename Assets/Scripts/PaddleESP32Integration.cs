using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ESP32GyroController))]
public class PaddleESP32Integration : MonoBehaviour
{
    [Header("Paddle Controller")]
    public PaddleIKController paddleController;
    
    [Header("Integration Settings")]
    [Range(0f, 5f)]
    public float angleMultiplier = 1.0f;
    [Range(0f, 0.95f)]
    public float angleSmoothing = 0.8f;
    
    [Header("UI Elements")]
    public Text statusText;
    public GameObject connectionIndicator;
    
    // References
    private ESP32GyroController gyroController;
    
    void Start()
    {
        // Get the ESP32GyroController component
        gyroController = GetComponent<ESP32GyroController>();
        
        if (gyroController == null)
        {
            Debug.LogError("ESP32GyroController component not found!");
            return;
        }
        
        // Set up references
        gyroController.paddleController = paddleController;
        gyroController.connectionIndicator = connectionIndicator;
        
        // Configure controller settings from PaddleIKController if available
        if (paddleController != null)
        {
            gyroController.dataSmoothing = paddleController.rawAngleSmoothing;
            gyroController.gyroMultiplier = paddleController.rawAngleMultiplier;
        }
        else
        {
            Debug.LogWarning("PaddleIKController reference not set!");
        }
        
        // Update settings on start
        UpdateSettings();
    }
    
    void Update()
    {
        UpdateStatus();
    }
    
    // Update the paddle controller settings
    public void UpdateSettings()
    {
        if (paddleController == null || gyroController == null) return;
        
        // Apply settings to paddle controller
        paddleController.useRawAngle = true;
        paddleController.rawAngleMultiplier = angleMultiplier;
        paddleController.rawAngleSmoothing = angleSmoothing;
        paddleController.overrideWithRawAngle = true;
        
        // Apply settings to gyro controller
        gyroController.dataSmoothing = angleSmoothing;
        gyroController.gyroMultiplier = angleMultiplier;
    }
    
    // Update the status text
    private void UpdateStatus()
    {
        if (statusText == null) return;
        
        if (gyroController != null)
        {
            if (gyroController.IsConnected())
            {
                statusText.text = "ESP32 Connected";
                statusText.color = Color.green;
                
                // Show current gyro value
                float gyroValue = gyroController.GetGyroValue();
                float smoothedValue = gyroController.GetSmoothedGyroValue();
                statusText.text += $"\nRaw: {gyroValue:F1}° | Smoothed: {smoothedValue:F1}°";
            }
            else
            {
                statusText.text = "ESP32 Disconnected";
                statusText.color = Color.red;
            }
        }
    }
    
    // Connect to ESP32 device (can be called from UI)
    public void Connect()
    {
        if (gyroController != null)
        {
            gyroController.ConnectToDevice();
        }
    }
    
    // Disconnect from ESP32 device (can be called from UI)
    public void Disconnect()
    {
        if (gyroController != null)
        {
            gyroController.DisconnectBluetooth();
        }
    }
}