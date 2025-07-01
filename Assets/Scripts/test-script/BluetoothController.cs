using UnityEngine;
using System.Text;
using System.Collections;

public class BluetoothController : MonoBehaviour
{
    [Header("Bluetooth Settings")]
    [SerializeField] private string deviceName = "ferizy-paddle";
    [SerializeField] private bool autoStart = true;

    [Header("Paddle Configuration")]
    [SerializeField] private float leftThreshold = -30f;
    [SerializeField] private float rightThreshold = 30f;
    [SerializeField] private float neutralThreshold = 15f;
    [SerializeField] private float debounceTime = 0.3f;
    
    [Header("References")]
    [SerializeField] private BoatController boatController;
    [SerializeField] private PaddleIKController paddleController;
    [SerializeField] private UIManager uiManager;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    // Public status for debugging
    public bool isConnected = false;
    public float currentAngle = 0f;
    public string lastReceivedData = "";
    
    // Paddle state tracking
    private bool isLeftPaddle = false;
    private bool isRightPaddle = false;
    private bool canTriggerLeft = true;
    private bool canTriggerRight = true;
    private float lastLeftTime = 0f;
    private float lastRightTime = 0f;
    
    // Esp32BleLib instance (following sample pattern)
    private Esp32BleLib m_Esp32BleLib;
    
    private void Start()
    {
        DebugLog("BluetoothController starting...");
        
        // Initialize Esp32BleLib following sample code pattern
        m_Esp32BleLib = gameObject.AddComponent<Esp32BleLib>();
        
        if (autoStart)
        {
            m_Esp32BleLib.Esp32BleLibStart();
            DebugLog("Esp32BleLib started");
            UpdateConnectionStatus("Bluetooth initialized, searching for device...");
        }
        
        // Start connection check
        StartCoroutine(ConnectionCheckRoutine());
    }
    
    private void Update()
    {
        if (m_Esp32BleLib == null) return;
        
        // Read data using sample code pattern
        byte[] readdata = new byte[] { };
        
        // UpdateRead returns true if data is available
        if (!m_Esp32BleLib.UpdateRead(ref readdata))
        {
            return; // No data available
        }
        
        // Process received data
        ProcessBluetoothData(readdata);
    }
    
    private void ProcessBluetoothData(byte[] readdata)
    {
        if (readdata.Length == 0) return;
        
        try
        {
            // Convert to string (following sample pattern)
            string text = System.Text.Encoding.UTF8.GetString(readdata);
            lastReceivedData = text;
            
            DebugLog($"Received: {text}");
            
            // Handle angle data format: "A:X.X"
            if (text.StartsWith("A:"))
            {
                ProcessAngleData(text);
            }
            // Handle comma-separated format (like sample: "x,y,z,")
            else if (text.Contains(","))
            {
                ProcessCommaData(text);
            }
            // Handle legacy paddle commands
            else if (text.Equals("L:1"))
            {
                TriggerLeftPaddle();
            }
            else if (text.Equals("R:1"))
            {
                TriggerRightPaddle();
            }
            
            // Update connection status
            if (!isConnected)
            {
                isConnected = true;
                UpdateConnectionStatus("Connected to paddle controller");
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Error processing data: {e.Message}");
        }
    }
    
    private void ProcessAngleData(string data)
    {
        // Extract angle from "A:X.X" format
        string angleText = data.Substring(2);
        if (float.TryParse(angleText, out float angle))
        {
            currentAngle = angle;
            
            // Update paddle visualization
            UpdatePaddleVisualization(angle);
            
            // Detect paddle actions
            DetectPaddleFromAngle(angle);
        }
    }
    
    private void ProcessCommaData(string text)
    {
        // Handle comma-separated data (following sample pattern)
        string[] arr = text.Split(',');
        
        if (arr.Length >= 3)
        {
            if (float.TryParse(arr[0], out float x) && 
                float.TryParse(arr[1], out float y) && 
                float.TryParse(arr[2], out float z))
            {
                // Calculate roll angle from accelerometer data
                float roll = Mathf.Atan2(y, z) * Mathf.Rad2Deg;
                currentAngle = roll;
                
                DebugLog($"Accel data: x={x:F2}, y={y:F2}, z={z:F2}, roll={roll:F2}");
                
                // Update paddle visualization and detect actions
                UpdatePaddleVisualization(roll);
                DetectPaddleFromAngle(roll);
            }
        }
    }
    
    private void UpdatePaddleVisualization(float angle)
    {
        if (paddleController != null)
        {
            // Update paddle animation based on angle
            // Note: Add this method to PaddleIKController if needed
            // paddleController.SetRawAngle(angle);
        }
    }
    
    private void DetectPaddleFromAngle(float angle)
    {
        // Left paddle detection
        if (angle < leftThreshold && !isLeftPaddle && canTriggerLeft)
        {
            isLeftPaddle = true;
            isRightPaddle = false;
            TriggerLeftPaddle();
            StartCoroutine(ResetLeftTrigger());
        }
        // Right paddle detection  
        else if (angle > rightThreshold && !isRightPaddle && canTriggerRight)
        {
            isRightPaddle = true;
            isLeftPaddle = false;
            TriggerRightPaddle();
            StartCoroutine(ResetRightTrigger());
        }
        // Neutral position
        else if (Mathf.Abs(angle) < neutralThreshold)
        {
            if (isLeftPaddle || isRightPaddle)
            {
                ResetPaddleState();
            }
        }
    }
    
    private void TriggerLeftPaddle()
    {
        if (Time.time - lastLeftTime < debounceTime) return;
        
        DebugLog("Left paddle triggered!");
        lastLeftTime = Time.time;
        
        // Update boat physics
        if (boatController != null)
        {
            boatController.PaddleLeft();
        }
        
        // Update paddle animation
        if (paddleController != null)
        {
            paddleController.ForcePattern((int)PaddleIKController.PaddlePattern.ConsecutiveLeft);
        }
    }
    
    private void TriggerRightPaddle()
    {
        if (Time.time - lastRightTime < debounceTime) return;
        
        DebugLog("Right paddle triggered!");
        lastRightTime = Time.time;
        
        // Update boat physics
        if (boatController != null)
        {
            boatController.PaddleRight();
        }
        
        // Update paddle animation
        if (paddleController != null)
        {
            paddleController.ForcePattern((int)PaddleIKController.PaddlePattern.ConsecutiveRight);
        }
    }
    
    private void ResetPaddleState()
    {
        isLeftPaddle = false;
        isRightPaddle = false;
        
        if (paddleController != null)
        {
            paddleController.ForcePattern((int)PaddleIKController.PaddlePattern.None);
        }
    }
    
    private IEnumerator ResetLeftTrigger()
    {
        canTriggerLeft = false;
        yield return new WaitForSeconds(debounceTime);
        canTriggerLeft = true;
    }
    
    private IEnumerator ResetRightTrigger()
    {
        canTriggerRight = false;
        yield return new WaitForSeconds(debounceTime);
        canTriggerRight = true;
    }
    
    private IEnumerator ConnectionCheckRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(2f);
            
            // Simple connection check - if no data for 3 seconds, assume disconnected
            if (string.IsNullOrEmpty(lastReceivedData) && isConnected)
            {
                isConnected = false;
                UpdateConnectionStatus("Connection lost");
            }
        }
    }
    
    private void UpdateConnectionStatus(string status)
    {
        if (uiManager != null)
        {
            uiManager.UpdateConnectionStatus(status);
        }
    }
    
    private void OnApplicationQuit()
    {
        DebugLog("Application quitting, cleaning up Bluetooth...");
        
        if (m_Esp32BleLib != null)
        {
            m_Esp32BleLib.Quit();
        }
    }
    
    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[BluetoothController] {message}");
        }
    }
    
    // Public methods for testing
    [ContextMenu("Test Left Paddle")]
    public void TestLeftPaddle()
    {
        TriggerLeftPaddle();
    }
    
    [ContextMenu("Test Right Paddle")]  
    public void TestRightPaddle()
    {
        TriggerRightPaddle();
    }
    
    [ContextMenu("Restart Bluetooth")]
    public void RestartBluetooth()
    {
        if (m_Esp32BleLib != null)
        {
            m_Esp32BleLib.Quit();
            m_Esp32BleLib.Esp32BleLibStart();
            UpdateConnectionStatus("Bluetooth restarted");
        }
    }
    
    // Send data to ESP32 (following sample pattern)
    public void SendCommand(byte value1, byte value2)
    {
        if (m_Esp32BleLib != null)
        {
            byte[] writedata = new byte[2] { value1, value2 };
            m_Esp32BleLib.Command(writedata);
            DebugLog($"Sent command: {value1}, {value2}");
        }
    }
    
    // Getters for debugging
    public bool IsConnected() => isConnected;
    public float GetCurrentAngle() => currentAngle;
    public string GetLastData() => lastReceivedData;
}