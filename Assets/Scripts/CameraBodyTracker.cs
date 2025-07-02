using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Mediapipe;
using Mediapipe.Unity;

public class CameraBodyTracker : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private int cameraIndex = 0;
    [SerializeField] private int targetWidth = 640;
    [SerializeField] private int targetHeight = 480;
    [SerializeField] private int targetFPS = 30;
    [SerializeField] private bool autoStartCamera = true;
    
    [Header("Detection Settings")]
    [SerializeField] private float paddleThreshold = 0.05f; // Y position difference threshold
    [SerializeField] private float debounceTime = 0.3f;
    [SerializeField] private float confidenceThreshold = 0.7f;
    [SerializeField] private bool useFallbackToShoulders = true;
    [SerializeField] private float shoulderThreshold = 0.08f;
    
    [Header("Visual Feedback")]
    [SerializeField] private bool showDebugOverlay = true;
    [SerializeField] private bool drawPoseLandmarks = true;
    [SerializeField] private Color leftHandColor = Color.red;
    [SerializeField] private Color rightHandColor = Color.blue;
    [SerializeField] private Color shoulderColor = Color.yellow;
    
    [Header("References")]
    [SerializeField] private BoatController boatController;
    [SerializeField] private CameraPreviewUI previewUI;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showPerformanceStats = true;
    
    // MediaPipe components
    private PoseTracking poseTracking;
    private WebCamTexture webCamTexture;
    private Texture2D inputTexture;
    private bool isInitialized = false;
    private bool isProcessing = false;
    
    // Pose detection data
    private PoseDetectionData currentPoseData;
    private bool hasPoseDetection = false;
    
    // Paddle detection state
    private bool canPaddleLeft = true;
    private bool canPaddleRight = true;
    private float lastLeftPaddleTime = 0f;
    private float lastRightPaddleTime = 0f;
    private bool isLeftHandLower = false;
    private bool isRightHandLower = false;
    
    // Performance tracking
    private float lastFrameTime = 0f;
    private float avgProcessingTime = 0f;
    private int frameCount = 0;
    private Queue<float> frameTimes = new Queue<float>();
    
    // Camera management
    private string[] availableCameras;
    private bool isCameraActive = false;
    
    // Events
    public System.Action<PoseDetectionData> OnPoseDetected;
    public System.Action<bool> OnCameraStatusChanged;
    public System.Action<string> OnErrorOccurred;
    
    private void Start()
    {
        InitializeSystem();
    }
    
    private void InitializeSystem()
    {
        DebugLog("Initializing Camera Body Tracker...");
        
        // Find boat controller if not assigned
        if (boatController == null)
        {
            boatController = FindObjectOfType<BoatController>();
            if (boatController == null)
            {
                DebugLog("ERROR: BoatController not found!");
                return;
            }
        }
        
        // Find preview UI if not assigned
        if (previewUI == null)
        {
            previewUI = FindObjectOfType<CameraPreviewUI>();
        }
        
        // Initialize pose detection data
        currentPoseData = new PoseDetectionData();
        
        // Get available cameras
        RefreshAvailableCameras();
        
        if (autoStartCamera)
        {
            StartCoroutine(InitializeCameraAndMediaPipe());
        }
    }
    
    private void RefreshAvailableCameras()
    {
        availableCameras = new string[WebCamTexture.devices.Length];
        for (int i = 0; i < WebCamTexture.devices.Length; i++)
        {
            availableCameras[i] = WebCamTexture.devices[i].name;
            DebugLog($"Camera {i}: {availableCameras[i]}");
        }
    }
    
    private IEnumerator InitializeCameraAndMediaPipe()
    {
        // Initialize MediaPipe
        yield return StartCoroutine(InitializeMediaPipe());
        
        if (!isInitialized)
        {
            DebugLog("ERROR: MediaPipe initialization failed!");
            OnErrorOccurred?.Invoke("MediaPipe initialization failed");
            yield break;
        }
        
        // Start camera
        yield return StartCoroutine(StartCamera());
        
        if (isCameraActive)
        {
            DebugLog("✓ Camera Body Tracker initialized successfully");
            
            // Set boat controller input mode
            if (boatController != null)
            {
                // Add CameraBodyTracking to InputMode enum first
                // boatController.SetInputMode(BoatController.InputMode.CameraBodyTracking);
            }
            
            // Update preview UI
            if (previewUI != null)
            {
                previewUI.SetCameraTexture(webCamTexture);
            }
        }
    }
    
    private IEnumerator InitializeMediaPipe()
    {
        try
        {
            DebugLog("Initializing MediaPipe Pose solution...");
            
            // Initialize MediaPipe pose tracking
            poseTracking = new PoseTracking();
            
            // Configure pose tracking settings
            var config = new PoseTrackingConfig
            {
                model_complexity = 1, // 0=lite, 1=full, 2=heavy
                smooth_landmarks = true,
                enable_segmentation = false,
                smooth_segmentation = false,
                min_detection_confidence = confidenceThreshold,
                min_tracking_confidence = 0.5f,
                static_image_mode = false
            };
            
            poseTracking.Initialize(config);
            
            // Wait for initialization
            yield return new WaitForSeconds(0.5f);
            
            isInitialized = true;
            DebugLog("✓ MediaPipe initialized");
        }
        catch (System.Exception e)
        {
            DebugLog($"MediaPipe initialization error: {e.Message}");
            OnErrorOccurred?.Invoke($"MediaPipe error: {e.Message}");
            isInitialized = false;
        }
    }
    
    private IEnumerator StartCamera()
    {
        try
        {
            if (WebCamTexture.devices.Length == 0)
            {
                DebugLog("ERROR: No cameras found!");
                OnErrorOccurred?.Invoke("No cameras available");
                yield break;
            }
            
            // Clamp camera index
            cameraIndex = Mathf.Clamp(cameraIndex, 0, WebCamTexture.devices.Length - 1);
            
            DebugLog($"Starting camera: {availableCameras[cameraIndex]}");
            
            // Create webcam texture
            webCamTexture = new WebCamTexture(
                availableCameras[cameraIndex], 
                targetWidth, 
                targetHeight, 
                targetFPS
            );
            
            // Start camera
            webCamTexture.Play();
            
            // Wait for camera to start
            int attempts = 0;
            while (!webCamTexture.isPlaying && attempts < 50)
            {
                yield return new WaitForSeconds(0.1f);
                attempts++;
            }
            
            if (!webCamTexture.isPlaying)
            {
                DebugLog("ERROR: Camera failed to start!");
                OnErrorOccurred?.Invoke("Camera failed to start");
                yield break;
            }
            
            // Create input texture for MediaPipe
            inputTexture = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
            
            isCameraActive = true;
            OnCameraStatusChanged?.Invoke(true);
            
            DebugLog($"✓ Camera started - Resolution: {webCamTexture.width}x{webCamTexture.height} @ {webCamTexture.requestedFPS}fps");
        }
        catch (System.Exception e)
        {
            DebugLog($"Camera initialization error: {e.Message}");
            OnErrorOccurred?.Invoke($"Camera error: {e.Message}");
            isCameraActive = false;
        }
    }
    
    private void Update()
    {
        if (!isInitialized || !isCameraActive || webCamTexture == null || !webCamTexture.isPlaying)
            return;
        
        // Process frame if not already processing
        if (!isProcessing)
        {
            StartCoroutine(ProcessFrame());
        }
        
        // Update performance stats
        UpdatePerformanceStats();
    }
    
    private IEnumerator ProcessFrame()
    {
        isProcessing = true;
        float startTime = Time.realtimeSinceStartup;
        
        try
        {
            // Convert webcam texture to MediaPipe input
            yield return StartCoroutine(ConvertTextureForMediaPipe());
            
            // Process pose detection
            yield return StartCoroutine(ProcessPoseDetection());
            
            // Analyze hand positions for paddle detection
            if (hasPoseDetection)
            {
                AnalyzeHandPositions();
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Frame processing error: {e.Message}");
        }
        finally
        {
            // Calculate processing time
            float processingTime = Time.realtimeSinceStartup - startTime;
            UpdateFrameTimeStats(processingTime);
            
            isProcessing = false;
        }
    }
    
    private IEnumerator ConvertTextureForMediaPipe()
    {
        // Get pixels from webcam texture
        Color32[] pixels = webCamTexture.GetPixels32();
        
        // Convert to RGB24 format for MediaPipe
        inputTexture.SetPixels32(pixels);
        inputTexture.Apply();
        
        yield return null;
    }
    
    private IEnumerator ProcessPoseDetection()
    {
        try
        {
            // Send frame to MediaPipe
            var result = poseTracking.ProcessImage(inputTexture);
            
            // Wait for result
            yield return new WaitUntil(() => result != null);
            
            if (result.pose_landmarks != null && result.pose_landmarks.landmark.Count > 0)
            {
                // Update pose data
                UpdatePoseData(result.pose_landmarks);
                hasPoseDetection = true;
                
                // Trigger event
                OnPoseDetected?.Invoke(currentPoseData);
            }
            else
            {
                hasPoseDetection = false;
                currentPoseData.isValid = false;
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Pose detection error: {e.Message}");
            hasPoseDetection = false;
        }
    }
    
    private void UpdatePoseData(NormalizedLandmarkList landmarks)
    {
        currentPoseData.isValid = true;
        currentPoseData.timestamp = Time.time;
        currentPoseData.confidence = CalculateOverallConfidence(landmarks);
        
        // Extract key landmarks (MediaPipe pose landmark indices)
        // 15 = left wrist, 16 = right wrist
        // 11 = left shoulder, 12 = right shoulder
        
        if (landmarks.landmark.Count > 16)
        {
            // Left hand (wrist)
            var leftWrist = landmarks.landmark[15];
            currentPoseData.leftHand = new Vector2(leftWrist.x, 1f - leftWrist.y); // Flip Y
            currentPoseData.leftHandConfidence = leftWrist.visibility;
            
            // Right hand (wrist)
            var rightWrist = landmarks.landmark[16];
            currentPoseData.rightHand = new Vector2(rightWrist.x, 1f - rightWrist.y); // Flip Y
            currentPoseData.rightHandConfidence = rightWrist.visibility;
            
            // Shoulders
            var leftShoulder = landmarks.landmark[11];
            var rightShoulder = landmarks.landmark[12];
            currentPoseData.leftShoulder = new Vector2(leftShoulder.x, 1f - leftShoulder.y);
            currentPoseData.rightShoulder = new Vector2(rightShoulder.x, 1f - rightShoulder.y);
            currentPoseData.leftShoulderConfidence = leftShoulder.visibility;
            currentPoseData.rightShoulderConfidence = rightShoulder.visibility;
            
            // Update hand visibility flags
            currentPoseData.leftHandVisible = leftWrist.visibility > confidenceThreshold;
            currentPoseData.rightHandVisible = rightWrist.visibility > confidenceThreshold;
        }
    }
    
    private float CalculateOverallConfidence(NormalizedLandmarkList landmarks)
    {
        float totalConfidence = 0f;
        int validLandmarks = 0;
        
        foreach (var landmark in landmarks.landmark)
        {
            if (landmark.visibility > 0)
            {
                totalConfidence += landmark.visibility;
                validLandmarks++;
            }
        }
        
        return validLandmarks > 0 ? totalConfidence / validLandmarks : 0f;
    }
    
    private void AnalyzeHandPositions()
    {
        if (!currentPoseData.isValid) return;
        
        bool useShoulderFallback = false;
        
        // Check if hands are visible and confident
        bool leftHandValid = currentPoseData.leftHandVisible && currentPoseData.leftHandConfidence > confidenceThreshold;
        bool rightHandValid = currentPoseData.rightHandVisible && currentPoseData.rightHandConfidence > confidenceThreshold;
        
        Vector2 leftPos, rightPos;
        
        if (leftHandValid && rightHandValid)
        {
            // Use hand positions
            leftPos = currentPoseData.leftHand;
            rightPos = currentPoseData.rightHand;
        }
        else if (useFallbackToShoulders && 
                 currentPoseData.leftShoulderConfidence > confidenceThreshold && 
                 currentPoseData.rightShoulderConfidence > confidenceThreshold)
        {
            // Fall back to shoulder positions
            leftPos = currentPoseData.leftShoulder;
            rightPos = currentPoseData.rightShoulder;
            useShoulderFallback = true;
            DebugLog("Using shoulder fallback for paddle detection");
        }
        else
        {
            // Not enough data for detection
            return;
        }
        
        // Calculate Y position difference
        float yDifference = leftPos.y - rightPos.y;
        float threshold = useShoulderFallback ? shoulderThreshold : paddleThreshold;
        
        // Detect paddle motions
        bool leftShouldPaddle = yDifference < -threshold; // Left hand/shoulder lower
        bool rightShouldPaddle = yDifference > threshold;  // Right hand/shoulder lower
        
        // Trigger paddle actions with debounce
        if (leftShouldPaddle && canPaddleLeft && Time.time - lastLeftPaddleTime > debounceTime)
        {
            TriggerLeftPaddle();
        }
        
        if (rightShouldPaddle && canPaddleRight && Time.time - lastRightPaddleTime > debounceTime)
        {
            TriggerRightPaddle();
        }
        
        // Update state for visual feedback
        isLeftHandLower = leftShouldPaddle;
        isRightHandLower = rightShouldPaddle;
        
        // Debug logging
        if (enableDebugLogs && Time.time - lastFrameTime > 0.5f) // Log every 0.5s
        {
            DebugLog($"Pose Analysis - Y Diff: {yDifference:F3}, L:{leftPos.y:F3}, R:{rightPos.y:F3}, " +
                    $"Threshold: {threshold:F3}, Fallback: {useShoulderFallback}");
        }
    }
    
    private void TriggerLeftPaddle()
    {
        if (boatController != null)
        {
            boatController.PaddleLeft();
            lastLeftPaddleTime = Time.time;
            DebugLog("LEFT PADDLE triggered by camera!");
        }
        
        StartCoroutine(PaddleCooldown(true));
    }
    
    private void TriggerRightPaddle()
    {
        if (boatController != null)
        {
            boatController.PaddleRight();
            lastRightPaddleTime = Time.time;
            DebugLog("RIGHT PADDLE triggered by camera!");
        }
        
        StartCoroutine(PaddleCooldown(false));
    }
    
    private IEnumerator PaddleCooldown(bool isLeft)
    {
        if (isLeft)
        {
            canPaddleLeft = false;
            yield return new WaitForSeconds(debounceTime);
            canPaddleLeft = true;
        }
        else
        {
            canPaddleRight = false;
            yield return new WaitForSeconds(debounceTime);
            canPaddleRight = true;
        }
    }
    
    private void UpdatePerformanceStats()
    {
        frameCount++;
        
        if (Time.time - lastFrameTime > 1f) // Update every second
        {
            float fps = frameCount / (Time.time - lastFrameTime);
            
            if (showPerformanceStats)
            {
                DebugLog($"Performance - FPS: {fps:F1}, Avg Processing: {avgProcessingTime * 1000:F1}ms");
            }
            
            frameCount = 0;
            lastFrameTime = Time.time;
        }
    }
    
    private void UpdateFrameTimeStats(float processingTime)
    {
        frameTimes.Enqueue(processingTime);
        
        // Keep only last 30 frame times
        while (frameTimes.Count > 30)
        {
            frameTimes.Dequeue();
        }
        
        // Calculate average
        float total = 0f;
        foreach (float time in frameTimes)
        {
            total += time;
        }
        avgProcessingTime = total / frameTimes.Count;
    }
    
    // Public control methods
    public void StartTracking()
    {
        if (!isInitialized)
        {
            StartCoroutine(InitializeCameraAndMediaPipe());
        }
        else if (!isCameraActive)
        {
            StartCoroutine(StartCamera());
        }
    }
    
    public void StopTracking()
    {
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Stop();
            isCameraActive = false;
            OnCameraStatusChanged?.Invoke(false);
            DebugLog("Camera tracking stopped");
        }
    }
    
    public void SwitchCamera(int newCameraIndex)
    {
        if (newCameraIndex >= 0 && newCameraIndex < availableCameras.Length)
        {
            cameraIndex = newCameraIndex;
            
            if (isCameraActive)
            {
                StartCoroutine(RestartCamera());
            }
        }
    }
    
    private IEnumerator RestartCamera()
    {
        StopTracking();
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(StartCamera());
        
        if (previewUI != null && isCameraActive)
        {
            previewUI.SetCameraTexture(webCamTexture);
        }
    }
    
    public void SetDetectionThreshold(float threshold)
    {
        paddleThreshold = Mathf.Clamp(threshold, 0.01f, 0.2f);
        DebugLog($"Detection threshold set to: {paddleThreshold:F3}");
    }
    
    public void SetDebounceTime(float time)
    {
        debounceTime = Mathf.Clamp(time, 0.1f, 2f);
        DebugLog($"Debounce time set to: {debounceTime:F1}s");
    }
    
    // Getters
    public bool IsInitialized() => isInitialized;
    public bool IsCameraActive() => isCameraActive;
    public bool HasPoseDetection() => hasPoseDetection;
    public PoseDetectionData GetCurrentPoseData() => currentPoseData;
    public WebCamTexture GetCameraTexture() => webCamTexture;
    public string[] GetAvailableCameras() => availableCameras;
    public int GetCurrentCameraIndex() => cameraIndex;
    public float GetAverageProcessingTime() => avgProcessingTime;
    public bool IsLeftHandLower() => isLeftHandLower;
    public bool IsRightHandLower() => isRightHandLower;
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[CameraBodyTracker] {message}");
        }
    }
    
    private void OnDestroy()
    {
        StopTracking();
        
        if (poseTracking != null)
        {
            poseTracking.Dispose();
        }
        
        if (inputTexture != null)
        {
            Destroy(inputTexture);
        }
    }
    
    private void OnApplicationQuit()
    {
        StopTracking();
    }
    
    // Debug GUI
    private void OnGUI()
    {
        if (!showDebugOverlay) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 350, 300));
        GUILayout.Box("Camera Body Tracker Debug");
        
        GUILayout.Label($"Initialized: {isInitialized}");
        GUILayout.Label($"Camera Active: {isCameraActive}");
        GUILayout.Label($"Has Pose: {hasPoseDetection}");
        GUILayout.Label($"Processing: {isProcessing}");
        
        if (isCameraActive && webCamTexture != null)
        {
            GUILayout.Label($"Camera: {availableCameras[cameraIndex]}");
            GUILayout.Label($"Resolution: {webCamTexture.width}x{webCamTexture.height}");
        }
        
        if (hasPoseDetection)
        {
            GUILayout.Label($"Confidence: {currentPoseData.confidence:F2}");
            GUILayout.Label($"Left Hand: {(currentPoseData.leftHandVisible ? "Visible" : "Hidden")}");
            GUILayout.Label($"Right Hand: {(currentPoseData.rightHandVisible ? "Visible" : "Hidden")}");
            
            if (isLeftHandLower || isRightHandLower)
            {
                GUI.color = Color.yellow;
                GUILayout.Label($"Paddle: {(isLeftHandLower ? "LEFT" : "RIGHT")}");
                GUI.color = Color.white;
            }
        }
        
        GUILayout.Label($"Avg Processing: {avgProcessingTime * 1000:F1}ms");
        
        GUILayout.Space(10);
        
        if (GUILayout.Button(isCameraActive ? "Stop Tracking" : "Start Tracking"))
        {
            if (isCameraActive)
                StopTracking();
            else
                StartTracking();
        }
        
        if (availableCameras != null && availableCameras.Length > 1)
        {
            if (GUILayout.Button("Switch Camera"))
            {
                int nextCamera = (cameraIndex + 1) % availableCameras.Length;
                SwitchCamera(nextCamera);
            }
        }
        
        GUILayout.EndArea();
    }
}