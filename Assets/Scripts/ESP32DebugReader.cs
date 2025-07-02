using UnityEngine;
using System.IO.Ports;

public class ESP32DebugReader : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int baudRate = 115200;
    [SerializeField] private string comPort = "COM9";
    [SerializeField] private bool autoDetect = true;
    
    [Header("Debug Settings")]
    [SerializeField] private float debugInterval = 0.5f;
    [SerializeField] private bool showParsedData = true;
    
    private SerialPort serialPort;
    private float lastDebugTime = 0f;
    private float currentGyro = 0f;
    private float currentAccel = 0f;
    
    void Start()
    {
        if (autoDetect)
            FindESP32();
        else
            ConnectToPort(comPort);
    }
    
    void Update()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            try
            {
                if (serialPort.BytesToRead > 0)
                {
                    string data = serialPort.ReadExisting();
                    
                    if (!string.IsNullOrEmpty(data) && Time.time - lastDebugTime > debugInterval)
                    {
                        ProcessData(data);
                        lastDebugTime = Time.time;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Read error: {e.Message}");
            }
        }
    }
    
    void ProcessData(string data)
    {
        string[] lines = data.Split('\n');
        
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            
            if (trimmed.StartsWith("G:"))
            {
                Debug.Log($"ESP32 Raw: {trimmed}");
                
                if (showParsedData)
                {
                    ParseAndDisplay(trimmed);
                }
            }
        }
    }
    
    void ParseAndDisplay(string dataLine)
    {
        try
        {
            // Parse G:-6.9,A:2267
            string[] parts = dataLine.Split(',');
            
            if (parts.Length >= 2)
            {
                // Gyro
                if (parts[0].StartsWith("G:"))
                {
                    string gyroStr = parts[0].Substring(2);
                    if (float.TryParse(gyroStr, out float gyro))
                        currentGyro = gyro;
                }
                
                // Accel
                if (parts[1].StartsWith("A:"))
                {
                    string accelStr = parts[1].Substring(2);
                    if (float.TryParse(accelStr, out float accel))
                        currentAccel = accel;
                }
                
                Debug.Log($"Parsed - Gyro: {currentGyro:F1}°, Accel: {currentAccel:F0}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Parse error: {e.Message}");
        }
    }
    
    void FindESP32()
    {
        string[] ports = SerialPort.GetPortNames();
        
        foreach (string port in ports)
        {
            if (port == "COM1" || port == "COM2") continue;
            
            if (TestPort(port))
            {
                Debug.Log($"ESP32 found on {port}");
                ConnectToPort(port);
                return;
            }
        }
        
        Debug.LogWarning("ESP32 not found");
    }
    
    bool TestPort(string port)
    {
        try
        {
            SerialPort test = new SerialPort(port, baudRate);
            test.ReadTimeout = 1000;
            test.Open();
            
            System.Threading.Thread.Sleep(500);
            
            if (test.BytesToRead > 0)
            {
                string data = test.ReadExisting();
                test.Close();
                return data.Contains("G:") && data.Contains("A:");
            }
            test.Close();
        }
        catch { }
        return false;
    }
    
    void ConnectToPort(string port)
    {
        try
        {
            serialPort = new SerialPort(port, baudRate);
            serialPort.Open();
            Debug.Log($"Connected to {port}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Connection failed: {e.Message}");
        }
    }
    
    void OnApplicationQuit()
    {
        if (serialPort != null && serialPort.IsOpen)
            serialPort.Close();
    }
    
    // GUI for real-time monitoring
    void OnGUI()
    {
        if (serialPort == null || !serialPort.IsOpen) return;
        
        GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 100));
        GUILayout.Box("ESP32 Debug Monitor");
        GUILayout.Label($"Gyro X: {currentGyro:F1}°");
        GUILayout.Label($"Accel Y: {currentAccel:F0}");
        GUILayout.Label($"Connected: {serialPort.IsOpen}");
        GUILayout.EndArea();
    }
}