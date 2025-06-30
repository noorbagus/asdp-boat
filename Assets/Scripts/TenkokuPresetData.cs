using UnityEngine;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class TenkokuPresetData
{
    [Header("Preset Info")]
    public string presetName = "New Preset";
    public string description = "";
    
    [Header("Time Settings")]
    public int currentYear = 2024;
    public int currentMonth = 6;
    public int currentDay = 15;
    public int currentHour = 12;
    public int currentMinute = 0;
    public float setLatitude = 0f;
    public float setLongitude = 0f;
    
    [Header("Weather Settings")]
    public float weather_cloudAltoStratusAmt = 0.1f;
    public float weather_cloudCirrusAmt = 0.2f;
    public float weather_cloudCumulusAmt = 0.5f;
    public float weather_OvercastAmt = 0.0f;
    public float weather_cloudScale = 1.0f;
    public float weather_cloudSpeed = 0.2f;
    public float weather_RainAmt = 0.0f;
    public float weather_SnowAmt = 0.0f;
    public float weather_FogAmt = 0.0f;
    public float weather_FogHeight = 500.0f;
    public float weather_WindAmt = 0.3f;
    public float weather_WindDir = 180.0f;
    public float weather_temperature = 72.0f;
    public float weather_rainbow = 0.0f;
    public float weather_lightning = 0.0f;
    public float weather_lightningDir = 60.0f;
    public float weather_lightningRange = 180.0f;
    
    [Header("Sky Settings")]
    public float overallTint = 1.0f;
    public float skyTint = 1.0f;
    public float ambientTint = 1.0f;
}

public class TenkokuPresetManager : MonoBehaviour
{
    [Header("Tenkoku Reference")]
    [SerializeField] private Tenkoku.Core.TenkokuModule tenkokuModule;
    
    [Header("Preset Management")]
    [SerializeField] private List<TenkokuPresetData> presets = new List<TenkokuPresetData>();
    [SerializeField] private int currentPresetIndex = -1;
    [SerializeField] private string presetsFolderPath = "TenkokuPresets";
    
    [Header("Live Mode Controls")]
    [SerializeField] private KeyCode saveCurrentKey = KeyCode.F5;
    [SerializeField] private KeyCode loadPresetKey = KeyCode.F9;
    [SerializeField] private KeyCode nextPresetKey = KeyCode.F6;
    [SerializeField] private KeyCode prevPresetKey = KeyCode.F4;
    
    [Header("UI Display")]
    [SerializeField] private bool showPresetUI = true;
    [SerializeField] private string newPresetName = "My Preset";
    
    private string presetsDirectory;
    private bool isInitialized = false;
    
    private void Start()
    {
        InitializePresetManager();
        LoadAllPresets();
    }
    
    private void Update()
    {
        HandleKeyboardInput();
    }
    
    private void InitializePresetManager()
    {
        // Find Tenkoku module if not assigned
        if (tenkokuModule == null)
        {
            GameObject tenkokuObject = GameObject.Find("Tenkoku DynamicSky");
            if (tenkokuObject != null)
            {
                tenkokuModule = tenkokuObject.GetComponent<Tenkoku.Core.TenkokuModule>();
            }
        }
        
        // Setup presets directory
        presetsDirectory = Path.Combine(Application.persistentDataPath, presetsFolderPath);
        if (!Directory.Exists(presetsDirectory))
        {
            Directory.CreateDirectory(presetsDirectory);
        }
        
        isInitialized = (tenkokuModule != null);
        
        if (!isInitialized)
        {
            Debug.LogWarning("[TenkokuPresetManager] Tenkoku Module not found!");
        }
        else
        {
            Debug.Log("[TenkokuPresetManager] Initialized successfully");
            Debug.Log($"[TenkokuPresetManager] Presets directory: {presetsDirectory}");
        }
    }
    
    private void HandleKeyboardInput()
    {
        if (!isInitialized) return;
        
        // Save current settings as new preset
        if (Input.GetKeyDown(saveCurrentKey))
        {
            SaveCurrentAsNewPreset();
        }
        
        // Load selected preset
        if (Input.GetKeyDown(loadPresetKey) && currentPresetIndex >= 0 && currentPresetIndex < presets.Count)
        {
            LoadPreset(currentPresetIndex);
        }
        
        // Navigate presets
        if (Input.GetKeyDown(nextPresetKey))
        {
            NextPreset();
        }
        
        if (Input.GetKeyDown(prevPresetKey))
        {
            PreviousPreset();
        }
    }
    
    // ===== PRESET CREATION =====
    
    public void SaveCurrentAsNewPreset()
    {
        if (!isInitialized) 
        {
            Debug.LogError("[TenkokuPresetManager] Cannot save - Tenkoku not initialized!");
            return;
        }
        
        TenkokuPresetData newPreset = CreatePresetFromCurrentSettings();
        newPreset.presetName = newPresetName + " " + System.DateTime.Now.ToString("HH-mm-ss");
        
        presets.Add(newPreset);
        SavePresetToFile(newPreset);
        
        currentPresetIndex = presets.Count - 1;
        
        Debug.Log($"[TenkokuPresetManager] ✓ SUCCESS: Saved Tenkoku preset '{newPreset.presetName}'");
        Debug.Log($"[TenkokuPresetManager] Weather: Clouds={newPreset.weather_cloudCumulusAmt:F2}, Rain={newPreset.weather_RainAmt:F2}, Wind={newPreset.weather_WindAmt:F2}");
        Debug.Log($"[TenkokuPresetManager] Time: {newPreset.currentHour:00}:{newPreset.currentMinute:00}, Location: {newPreset.setLatitude:F1}°, {newPreset.setLongitude:F1}°");
    }
    
    public void UpdateCurrentPreset()
    {
        if (!isInitialized || currentPresetIndex < 0 || currentPresetIndex >= presets.Count) 
        {
            Debug.LogError("[TenkokuPresetManager] Cannot update - No valid preset selected!");
            return;
        }
        
        TenkokuPresetData updatedPreset = CreatePresetFromCurrentSettings();
        updatedPreset.presetName = presets[currentPresetIndex].presetName; // Keep original name
        
        presets[currentPresetIndex] = updatedPreset;
        SavePresetToFile(updatedPreset);
        
        Debug.Log($"[TenkokuPresetManager] ✓ SUCCESS: Updated Tenkoku preset '{updatedPreset.presetName}'");
        Debug.Log($"[TenkokuPresetManager] New values saved to preset file");
    }
    
    private TenkokuPresetData CreatePresetFromCurrentSettings()
    {
        TenkokuPresetData preset = new TenkokuPresetData();
        
        // Copy time settings
        preset.currentYear = tenkokuModule.currentYear;
        preset.currentMonth = tenkokuModule.currentMonth;
        preset.currentDay = tenkokuModule.currentDay;
        preset.currentHour = tenkokuModule.currentHour;
        preset.currentMinute = tenkokuModule.currentMinute;
        preset.setLatitude = tenkokuModule.setLatitude;
        preset.setLongitude = tenkokuModule.setLongitude;
        
        // Copy weather settings
        preset.weather_cloudAltoStratusAmt = tenkokuModule.weather_cloudAltoStratusAmt;
        preset.weather_cloudCirrusAmt = tenkokuModule.weather_cloudCirrusAmt;
        preset.weather_cloudCumulusAmt = tenkokuModule.weather_cloudCumulusAmt;
        preset.weather_OvercastAmt = tenkokuModule.weather_OvercastAmt;
        preset.weather_cloudScale = tenkokuModule.weather_cloudScale;
        preset.weather_cloudSpeed = tenkokuModule.weather_cloudSpeed;
        preset.weather_RainAmt = tenkokuModule.weather_RainAmt;
        preset.weather_SnowAmt = tenkokuModule.weather_SnowAmt;
        preset.weather_FogAmt = tenkokuModule.weather_FogAmt;
        preset.weather_FogHeight = tenkokuModule.weather_FogHeight;
        preset.weather_WindAmt = tenkokuModule.weather_WindAmt;
        preset.weather_WindDir = tenkokuModule.weather_WindDir;
        preset.weather_temperature = tenkokuModule.weather_temperature;
        preset.weather_rainbow = tenkokuModule.weather_rainbow;
        preset.weather_lightning = tenkokuModule.weather_lightning;
        preset.weather_lightningDir = tenkokuModule.weather_lightningDir;
        preset.weather_lightningRange = tenkokuModule.weather_lightningRange;
        
        return preset;
    }
    
    // ===== PRESET LOADING =====
    
    public void LoadPreset(int index)
    {
        if (!isInitialized || index < 0 || index >= presets.Count) 
        {
            Debug.LogError("[TenkokuPresetManager] Cannot load - Invalid preset index!");
            return;
        }
        
        TenkokuPresetData preset = presets[index];
        ApplyPresetToTenkoku(preset);
        currentPresetIndex = index;
        
        Debug.Log($"[TenkokuPresetManager] ✓ SUCCESS: Loaded Tenkoku preset '{preset.presetName}'");
        Debug.Log($"[TenkokuPresetManager] Applied: Clouds={preset.weather_cloudCumulusAmt:F2}, Rain={preset.weather_RainAmt:F2}, Time={preset.currentHour:00}:{preset.currentMinute:00}");
    }
    
    public void LoadPreset(string presetName)
    {
        for (int i = 0; i < presets.Count; i++)
        {
            if (presets[i].presetName == presetName)
            {
                LoadPreset(i);
                return;
            }
        }
        
        Debug.LogWarning($"[TenkokuPresetManager] Preset not found: {presetName}");
    }
    
    private void ApplyPresetToTenkoku(TenkokuPresetData preset)
    {
        // Apply time settings
        tenkokuModule.currentYear = preset.currentYear;
        tenkokuModule.currentMonth = preset.currentMonth;
        tenkokuModule.currentDay = preset.currentDay;
        tenkokuModule.currentHour = preset.currentHour;
        tenkokuModule.currentMinute = preset.currentMinute;
        tenkokuModule.setLatitude = preset.setLatitude;
        tenkokuModule.setLongitude = preset.setLongitude;
        
        // Apply weather settings
        tenkokuModule.weather_cloudAltoStratusAmt = preset.weather_cloudAltoStratusAmt;
        tenkokuModule.weather_cloudCirrusAmt = preset.weather_cloudCirrusAmt;
        tenkokuModule.weather_cloudCumulusAmt = preset.weather_cloudCumulusAmt;
        tenkokuModule.weather_OvercastAmt = preset.weather_OvercastAmt;
        tenkokuModule.weather_cloudScale = preset.weather_cloudScale;
        tenkokuModule.weather_cloudSpeed = preset.weather_cloudSpeed;
        tenkokuModule.weather_RainAmt = preset.weather_RainAmt;
        tenkokuModule.weather_SnowAmt = preset.weather_SnowAmt;
        tenkokuModule.weather_FogAmt = preset.weather_FogAmt;
        tenkokuModule.weather_FogHeight = preset.weather_FogHeight;
        tenkokuModule.weather_WindAmt = preset.weather_WindAmt;
        tenkokuModule.weather_WindDir = preset.weather_WindDir;
        tenkokuModule.weather_temperature = preset.weather_temperature;
        tenkokuModule.weather_rainbow = preset.weather_rainbow;
        tenkokuModule.weather_lightning = preset.weather_lightning;
        tenkokuModule.weather_lightningDir = preset.weather_lightningDir;
        tenkokuModule.weather_lightningRange = preset.weather_lightningRange;
    }
    
    // ===== PRESET NAVIGATION =====
    
    public void NextPreset()
    {
        if (presets.Count == 0) return;
        
        currentPresetIndex = (currentPresetIndex + 1) % presets.Count;
        LoadPreset(currentPresetIndex);
    }
    
    public void PreviousPreset()
    {
        if (presets.Count == 0) return;
        
        currentPresetIndex--;
        if (currentPresetIndex < 0) currentPresetIndex = presets.Count - 1;
        LoadPreset(currentPresetIndex);
    }
    
    // ===== FILE MANAGEMENT =====
    
    private void SavePresetToFile(TenkokuPresetData preset)
    {
        string fileName = SanitizeFileName(preset.presetName) + ".json";
        string filePath = Path.Combine(presetsDirectory, fileName);
        
        try
        {
            string json = JsonUtility.ToJson(preset, true);
            File.WriteAllText(filePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TenkokuPresetManager] Failed to save preset: {e.Message}");
        }
    }
    
    private void LoadAllPresets()
    {
        if (!Directory.Exists(presetsDirectory)) return;
        
        presets.Clear();
        string[] files = Directory.GetFiles(presetsDirectory, "*.json");
        
        foreach (string file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                TenkokuPresetData preset = JsonUtility.FromJson<TenkokuPresetData>(json);
                presets.Add(preset);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TenkokuPresetManager] Failed to load preset {file}: {e.Message}");
            }
        }
        
        Debug.Log($"[TenkokuPresetManager] Loaded {presets.Count} presets");
    }
    
    public void DeletePreset(int index)
    {
        if (index < 0 || index >= presets.Count) return;
        
        TenkokuPresetData preset = presets[index];
        string fileName = SanitizeFileName(preset.presetName) + ".json";
        string filePath = Path.Combine(presetsDirectory, fileName);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        
        presets.RemoveAt(index);
        
        // Adjust current index
        if (currentPresetIndex >= index)
        {
            currentPresetIndex--;
        }
        
        Debug.Log($"[TenkokuPresetManager] Deleted preset: {preset.presetName}");
    }
    
    private string SanitizeFileName(string fileName)
    {
        string sanitized = fileName;
        char[] invalidChars = Path.GetInvalidFileNameChars();
        
        foreach (char c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }
        
        return sanitized;
    }
    
    // ===== UI DISPLAY =====
    
    private void OnGUI()
    {
        if (!showPresetUI || !isInitialized) return;
        
        // Background panel
        GUI.Box(new Rect(10, 10, 300, 200), "Tenkoku Preset Manager");
        
        // Current preset info
        if (currentPresetIndex >= 0 && currentPresetIndex < presets.Count)
        {
            GUI.Label(new Rect(20, 35, 280, 20), $"Current: {presets[currentPresetIndex].presetName}");
        }
        else
        {
            GUI.Label(new Rect(20, 35, 280, 20), "No preset selected");
        }
        
        // New preset name input
        GUI.Label(new Rect(20, 60, 100, 20), "New Preset:");
        newPresetName = GUI.TextField(new Rect(120, 60, 180, 20), newPresetName);
        
        // Control buttons
        if (GUI.Button(new Rect(20, 85, 80, 25), $"Save ({saveCurrentKey})"))
        {
            SaveCurrentAsNewPreset();
        }
        
        if (GUI.Button(new Rect(110, 85, 80, 25), "Update"))
        {
            UpdateCurrentPreset();
        }
        
        if (GUI.Button(new Rect(200, 85, 80, 25), $"Load ({loadPresetKey})"))
        {
            if (currentPresetIndex >= 0) LoadPreset(currentPresetIndex);
        }
        
        // Navigation buttons
        if (GUI.Button(new Rect(20, 115, 60, 25), $"< ({prevPresetKey})"))
        {
            PreviousPreset();
        }
        
        if (GUI.Button(new Rect(90, 115, 60, 25), $"> ({nextPresetKey})"))
        {
            NextPreset();
        }
        
        if (GUI.Button(new Rect(160, 115, 60, 25), "Delete"))
        {
            if (currentPresetIndex >= 0) DeletePreset(currentPresetIndex);
        }
        
        // Preset list
        GUI.Label(new Rect(20, 145, 280, 20), $"Presets ({presets.Count}):");
        
        for (int i = 0; i < Mathf.Min(presets.Count, 3); i++)
        {
            bool isSelected = (i == currentPresetIndex);
            string prefix = isSelected ? "► " : "   ";
            GUI.Label(new Rect(20, 165 + (i * 15), 280, 15), $"{prefix}{presets[i].presetName}");
        }
        
        // Controls help
        GUI.Label(new Rect(20, 220, 280, 40), 
            $"Controls:\n{saveCurrentKey}=Save | {loadPresetKey}=Load | {prevPresetKey}/{nextPresetKey}=Navigate");
    }
    
    // ===== PUBLIC ACCESSORS =====
    
    public List<string> GetPresetNames()
    {
        List<string> names = new List<string>();
        foreach (var preset in presets)
        {
            names.Add(preset.presetName);
        }
        return names;
    }
    
    public int GetCurrentPresetIndex()
    {
        return currentPresetIndex;
    }
    
    public int GetPresetCount()
    {
        return presets.Count;
    }
}