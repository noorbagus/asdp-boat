using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Mediapipe.Unity;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers;

public class CameraBodyTracker : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private int cameraIndex = 0;
    [SerializeField] private int targetWidth = 640;
    [SerializeField] private int targetHeight = 480;
    [SerializeField] private int targetFPS = 30;
    [SerializeField] private bool autoStartCamera = true;
    
    [Header("Detection Settings")]
    [SerializeField] private float paddleThreshold = 0.05f;
    [SerializeField] private float debounceTime = 0.3f;
    [SerializeField] private float confidenceThreshold = 0.7f;
    [SerializeField] private bool useFallbackToShoulders = true;
    [SerializeField] private float shoulderThreshold = 0.08f;
    
    [Header("Visual Feedback")]
    [SerializeField] private bool showDebugOverlay = true;
    [SerializeField] private UnityEngine.Color leftHandColor = UnityEngine.Color.red;
    [SerializeField] private UnityEngine.Color rightHandColor = UnityEngine.Color.blue;
    [SerializeField] private UnityEngine.Color shoulderColor = UnityEngine.Color.yellow;
    
    [Header("References")]
    [SerializeField] private BoatController boatController;
    [SerializeField] private CameraPreviewUI previewUI;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showPerformanceStats = true;
    
    // MediaPipe components
    private PoseLandmarker poseLandmarker;
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
        
        if (boatController == null)
        {
            boatController = FindObjectOfType<BoatController>();
            if (boatController == null)
            {
                DebugLog("ERROR: BoatController not found!");
                return;
            }
        }
        
        if (previewUI == null)
        {
            previewUI = FindObjectOfType<CameraPreviewUI>();
        }
        
        currentPoseData = new PoseDetectionData();
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
        yield return StartCoroutine(InitializeMediaPipe());
        
        if (!isInitialized)
        {
            DebugLog("ERROR: MediaPipe initialization failed!");
            OnErrorOccurred?.Invoke("MediaPipe initialization failed");
            yield break;
        }
        
        yield return StartCoroutine(StartCamera());
        
        if (isCameraActive)
        {
            DebugLog("✓ Camera Body Tracker initialized successfully");
            
            if (boatController != null)
            {
                boatController.SetInputMode(BoatController.InputMode.CameraBodyTracking);
            }
            
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
            
            var options = new PoseLandmarkerOptions(
                baseOptions: new Tasks.Core.BaseOptions(Tasks.Core.BaseOptions.Delegate.CPU, modelAssetPath: "pose_landmarker_full.bytes"),
                runningMode: Tasks.Vision.Core.RunningMode.LIVE_STREAM,
                numPoses: 1,
                minPoseDetectionConfidence: confidenceThreshold,
                minPosePresenceConfidence: 0.5f,
                minTrackingConfidence: 0.5f,
                outputSegmentationMasks: false,
                resultCallback: OnPoseLandmarkDetectionOutput
            );
            
            poseLandmarker = PoseLandmarker.CreateFromOptions(options);
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
            
            cameraIndex = Mathf.Clamp(cameraIndex, 0, WebCamTexture.devices.Length - 1);
            DebugLog($"Starting camera: {availableCameras[cameraIndex]}");
            
            webCamTexture = new WebCamTexture(
                availableCameras[cameraIndex], 
                targetWidth, 
                targetHeight, 
                targetFPS
            );
            
            webCamTexture.Play();
            
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
        
        if (!isProcessing)
        {
            StartCoroutine(ProcessFrame());
        }
        
        UpdatePerformanceStats();
    }
    
    private IEnumerator ProcessFrame()
    {
        isProcessing = true;
        float startTime = Time.realtimeSinceStartup;
        
        try
        {
            yield return StartCoroutine(ConvertTextureForMediaPipe());
            yield return StartCoroutine(ProcessPoseDetection());
            
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
            float processingTime = Time.realtimeSinceStartup - startTime;
            UpdateFrameTimeStats(processingTime);
            isProcessing = false;
        }
    }
    
    private IEnumerator ConvertTextureForMediaPipe()
    {
        Color32[] pixels = webCamTexture.GetPixels32();
        inputTexture.SetPixels32(pixels);
        inputTexture.Apply();
        yield return null;
    }
    
    private IEnumerator ProcessPoseDetection()
    {
        try
        {
            var mpImage = new Mediapipe.Image(ImageFormat.Srgb, inputTexture.width, inputTexture.height, 
                                            inputTexture.width * 3, inputTexture.GetRawTextureData());
            
            poseLandmarker.DetectAsync(mpImage, GetCurrentTimestampMillisec());
            yield return null;
        }
        catch (System.Exception e)
        {
            DebugLog($"Pose detection error: {e.Message}");
            hasPoseDetection = false;
        }
    }
    
    private void OnPoseLandmarkDetectionOutput(PoseLandmarkerResult result, Mediapipe.Image image, long timestamp)
    {
        if (result.poseLandmarks != null && result.poseLandmarks.Count > 0)
        {
            UpdatePoseData(result.poseLandmarks[0]);
            hasPoseDetection = true;
            OnPoseDetected?.Invoke(currentPoseData);
        }
        else
        {
            hasPoseDetection = false;
            currentPoseData.isValid = false;
        }
    }
    
    private void UpdatePoseData(IList<NormalizedLandmark> landmarks)
    {
        currentPoseData.isValid = true;
        currentPoseData.timestamp = Time.time;
        
        if (landmarks.Count > 16)
        {
            // Left hand (wrist - index 15)
            var leftWrist = landmarks[15];
            currentPoseData.leftHand = new Vector2(leftWrist.x, 1f - leftWrist.y);
            currentPoseData.leftHandConfidence = leftWrist.visibility ?? 0f;
            
            // Right hand (wrist - index 16)
            var rightWrist = landmarks[16];
            currentPoseData.rightHand = new Vector2(rightWrist.x, 1f - rightWrist.y);
            currentPoseData.rightHandConfidence = rightWrist.visibility ?? 0f;
            
            // Shoulders (left - index 11, right - index 12)
            var leftShoulder = landmarks[11];
            var rightShoulder = landmarks[12];
            currentPoseData.leftShoulder = new Vector2(leftShoulder.x, 1f - leftShoulder.y);
            currentPoseData.rightShoulder = new Vector2(rightShoulder.x, 1f - rightShoulder.y);
            currentPoseData.leftShoulderConfidence = leftShoulder.visibility ?? 0f;
            currentPoseData.rightShoulderConfidence = rightShoulder.visibility ?? 0f;
            
            // Update visibility flags
            currentPoseData.leftHandVisible = currentPoseData.leftHandConfidence > confidenceThreshold;
            currentPoseData.rightHandVisible = currentPoseData.rightHandConfidence > confidenceThreshold;
            
            // Calculate overall confidence
            currentPoseData.confidence = CalculateOverallConfidence(landmarks);
        }
    }
    
    private float CalculateOverallConfidence(IList<NormalizedLandmark> landmarks)
    {
        float totalConfidence = 0f;
        int validLandmarks = 0;
        
        foreach (var landmark in landmarks)
        {
            if (landmark.visibility.HasValue && landmark.visibility.Value > 0)
            {
                totalConfidence += landmark.visibility.Value;
                validLandmarks++;
            }
        }
        
        return validLandmarks > 0 ? totalConfidence / validLandmarks : 0f;
    }
    
    private void AnalyzeHandPositions()
    {
        if (!currentPoseData.isValid) return;
        
        bool useShoulderFallback = false;
        bool leftHandValid = currentPoseData.leftHandVisible && currentPoseData.leftHandConfidence > confidenceThreshold;
        bool rightHandValid = currentPoseData.rightHandVisible && currentPoseData.rightHandConfidence > confidenceThreshold;
        
        Vector2 leftPos, rightPos;
        
        if (leftHandValid && rightHandValid)
        {
            leftPos = currentPoseData.leftHand;
            rightPos = currentPoseData.rightHand;
        }
        else if (useFallbackToShoulders && 
                 currentPoseData.leftShoulderConfidence > confidenceThreshold && 
                 currentPoseData.rightShoulderConfidence > confidenceThreshold)
        {
            leftPos = currentPoseData.leftShoulder;
            rightPos = currentPoseData.rightShoulder;
            useShoulderFallback = true;
        }
        else
        {
            return;
        }
        
        float yDifference = leftPos.y - rightPos.y;
        float threshold = useShoulderFallback ? shoulderThreshold : paddleThreshold;
        
        bool leftShouldPaddle = yDifference < -threshold;
        bool rightShouldPaddle = yDifference > threshold;
        
        if (leftShouldPaddle && canPaddleLeft && Time.time - lastLeftPaddleTime > debounceTime)
        {
            TriggerLeftPaddle();
        }
        
        if (rightShouldPaddle && canPaddleRight && Time.time - lastRightPaddleTime > debounceTime)
        {
            TriggerRightPaddle();
        }
        
        isLeftHandLower = leftShouldPaddle;
        isRightHandLower = rightShouldPaddle;
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
        
        if (Time.time - lastFrameTime > 1f)
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
        
        while (frameTimes.Count > 30)
        {
            frameTimes.Dequeue();
        }
        
        float total = 0f;
        foreach (float time in frameTimes)
        {
            total += time;
        }
        avgProcessingTime = total / frameTimes.Count;
    }
    
    private long GetCurrentTimestampMillisec()
    {
        return (long)(Time.realtimeSinceStartup * 1000);
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
    }
    
    public void SetDebounceTime(float time)
    {
        debounceTime = Mathf.Clamp(time, 0.1f, 2f);
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
        
        if (poseLandmarker != null)
        {
            poseLandmarker.Dispose();
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
                GUI.color = UnityEngine.Color.yellow;
                GUILayout.Label($"Paddle: {(isLeftHandLower ? "LEFT" : "RIGHT")}");
                GUI.color = UnityEngine.Color.white;
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