using UnityEngine;
using UnityEngine.UI;

public class ESP32TestScene : MonoBehaviour
{
    [Header("Bluetooth Connection")]
    public ESP32GyroController gyroController;
    public string deviceName = "ferizy-paddle";
    public bool autoConnect = true;
    
    [Header("Visual Components")]
    public Transform paddleVisual;
    public Text statusText;
    public Text dataText;
    public GameObject connectionIndicator;
    
    [Header("Debug")]
    public bool showRawAngle = true;
    
    // Visual rotation values
    private float targetRotation = 0f;
    private float currentRotation = 0f;
    private float rotationSpeed = 5f;
    
    void Start()
    {
        if (gyroController == null)
        {
            Debug.LogError("ESP32GyroController reference not set!");
            return;
        }
        
        // Configure controller
        gyroController.deviceName = deviceName;
        gyroController.autoConnect = autoConnect;
        gyroController.debugText = dataText;
        gyroController.connectionIndicator = connectionIndicator;
    }
    
    void Update()
    {
        UpdateVisuals();
        UpdateStatusText();
    }
    
    private void UpdateVisuals()
    {
        if (paddleVisual == null) return;
        
        // Get gyro angle from controller
        if (gyroController != null && gyroController.IsConnected())
        {
            targetRotation = gyroController.GetSmoothedGyroValue();
        }
        
        // Apply rotation smoothly
        currentRotation = Mathf.LerpAngle(currentRotation, targetRotation, rotationSpeed * Time.deltaTime);
        
        // Apply rotation to paddle visual
        paddleVisual.rotation = Quaternion.Euler(0, 0, currentRotation);
    }
    
    private void UpdateStatusText()
    {
        if (statusText == null) return;
        
        if (gyroController != null)
        {
            if (gyroController.IsConnected())
            {
                statusText.text = "Connected to: " + deviceName;
                statusText.color = Color.green;
                
                if (showRawAngle)
                {
                    statusText.text += $"\nAngle: {gyroController.GetGyroValue():F1}°";
                    statusText.text += $"\nSmoothed: {gyroController.GetSmoothedGyroValue():F1}°";
                    statusText.text += $"\nLast Packet: {gyroController.GetLastPacket()}";
                }
            }
            else
            {
                statusText.text = "Disconnected";
                statusText.color = Color.red;
            }
        }
    }
    
    public void ConnectButton()
    {
        if (gyroController != null)
        {
            gyroController.ConnectToDevice();
        }
    }
    
    public void DisconnectButton()
    {
        if (gyroController != null)
        {
            gyroController.DisconnectBluetooth();
        }
    }
}