using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Mediapipe.Tasks.Vision.PoseLandmarker;

public class CameraPreviewUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private RawImage cameraPreview;
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private RectTransform previewContainer;
    [SerializeField] private GameObject poseOverlay;
    
    [Header("Preview Settings")]
    [SerializeField] private Vector2 previewSize = new Vector2(200, 150);
    [SerializeField] private Vector2 previewPosition = new Vector2(-10, -10);
    [SerializeField] private float previewAlpha = 0.8f;
    [SerializeField] private bool showPreview = true;
    [SerializeField] private bool showPoseOverlay = true;
    
    [Header("Visual Feedback")]
    [SerializeField] private Image leftHandIndicator;
    [SerializeField] private Image rightHandIndicator;
    [SerializeField] private UnityEngine.Color normalColor = UnityEngine.Color.white;
    [SerializeField] private UnityEngine.Color activeColor = UnityEngine.Color.yellow;
    [SerializeField] private UnityEngine.Color triggerColor = UnityEngine.Color.red;
    [SerializeField] private float indicatorPulseSpeed = 2f;
    
    [Header("Status Display")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI confidenceText;
    [SerializeField] private Image connectionIndicator;
    [SerializeField] private UnityEngine.Color connectedColor = UnityEngine.Color.green;
    [SerializeField] private UnityEngine.Color disconnectedColor = UnityEngine.Color.red;
    
    [Header("Pose Visualization")]
    [SerializeField] private LineRenderer[] poseLines;
    [SerializeField] private Transform[] landmarkMarkers;
    [SerializeField] private Material poseMaterial;
    [SerializeField] private float markerSize = 3f;
    [SerializeField] private bool showHandConnections = true;
    [SerializeField] private bool showShoulderLine = true;
    
    [Header("Animation")]
    [SerializeField] private bool enableAnimations = true;
    [SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0.8f, 1, 1f);
    
    // References
    private CameraBodyTracker bodyTracker;
    private WebCamTexture cameraTexture;
    private CanvasGroup canvasGroup;
    
    // State tracking
    private bool isInitialized = false;
    private bool leftHandTriggered = false;
    private bool rightHandTriggered = false;
    private float leftTriggerTime = 0f;
    private float rightTriggerTime = 0f;
    private Vector2 originalPreviewSize;
    
    // Animation state
    private float currentAlpha = 0f;
    private float targetAlpha = 0f;
    private bool isVisible = false;
    
    private void Awake()
    {
        InitializeComponents();
    }
    
    private void Start()
    {
        SetupPreview();
        FindBodyTracker();
    }
    
    private void InitializeComponents()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        if (previewContainer != null)
        {
            originalPreviewSize = previewContainer.sizeDelta;
        }
        
        InitializePoseVisualization();
        isInitialized = true;
    }
    
    private void SetupPreview()
    {
        if (previewContainer != null)
        {
            previewContainer.sizeDelta = previewSize;
            previewContainer.anchoredPosition = previewPosition;
            previewContainer.anchorMin = new Vector2(1, 1);
            previewContainer.anchorMax = new Vector2(1, 1);
        }
        
        currentAlpha = showPreview ? previewAlpha : 0f;
        targetAlpha = currentAlpha;
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = currentAlpha;
        }
        
        UpdateIndicators(false, false);
    }
    
    private void FindBodyTracker()
    {
        if (bodyTracker == null)
        {
            bodyTracker = FindObjectOfType<CameraBodyTracker>();
            
            if (bodyTracker != null)
            {
                bodyTracker.OnPoseDetected += OnPoseDetected;
                bodyTracker.OnCameraStatusChanged += OnCameraStatusChanged;
                bodyTracker.OnErrorOccurred += OnError;
            }
        }
    }
    
    private void Update()
    {
        if (!isInitialized) return;
        
        UpdateAlpha();
        UpdateIndicators();
        UpdateStatus();
        UpdateTriggerTimers();
    }
    
    private void UpdateAlpha()
    {
        if (enableAnimations && Mathf.Abs(currentAlpha - targetAlpha) > 0.01f)
        {
            currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
            
            if (canvasGroup != null)
            {
                canvasGroup.alpha = currentAlpha;
            }
        }
    }
    
    private void UpdateIndicators()
    {
        if (bodyTracker == null) return;
        
        bool leftLower = bodyTracker.IsLeftHandLower();
        bool rightLower = bodyTracker.IsRightHandLower();
        
        UpdateIndicators(leftLower, rightLower);
    }
    
    private void UpdateIndicators(bool leftActive, bool rightActive)
    {
        float time = Time.time;
        
        // Update left indicator
        if (leftHandIndicator != null)
        {
            UnityEngine.Color leftColor = normalColor;
            
            if (leftHandTriggered && time - leftTriggerTime < 0.5f)
            {
                leftColor = UnityEngine.Color.Lerp(triggerColor, activeColor, (time - leftTriggerTime) * 2f);
            }
            else if (leftActive)
            {
                float pulse = Mathf.Sin(time * indicatorPulseSpeed) * 0.3f + 0.7f;
                leftColor = UnityEngine.Color.Lerp(normalColor, activeColor, pulse);
            }
            
            leftHandIndicator.color = leftColor;
        }
        
        // Update right indicator
        if (rightHandIndicator != null)
        {
            UnityEngine.Color rightColor = normalColor;
            
            if (rightHandTriggered && time - rightTriggerTime < 0.5f)
            {
                rightColor = UnityEngine.Color.Lerp(triggerColor, activeColor, (time - rightTriggerTime) * 2f);
            }
            else if (rightActive)
            {
                float pulse = Mathf.Sin(time * indicatorPulseSpeed) * 0.3f + 0.7f;
                rightColor = UnityEngine.Color.Lerp(normalColor, activeColor, pulse);
            }
            
            rightHandIndicator.color = rightColor;
        }
    }
    
    private void UpdateStatus()
    {
        if (bodyTracker == null) return;
        
        if (connectionIndicator != null)
        {
            connectionIndicator.color = bodyTracker.IsCameraActive() ? connectedColor : disconnectedColor;
        }
        
        if (statusText != null)
        {
            string status = "Camera: " + (bodyTracker.IsCameraActive() ? "Active" : "Inactive");
            if (bodyTracker.HasPoseDetection())
            {
                status += " | Pose: Detected";
            }
            else if (bodyTracker.IsCameraActive())
            {
                status += " | Pose: Searching...";
            }
            
            statusText.text = status;
        }
        
        if (confidenceText != null && bodyTracker.HasPoseDetection())
        {
            var poseData = bodyTracker.GetCurrentPoseData();
            confidenceText.text = $"Confidence: {poseData.confidence:F2}";
        }
        else if (confidenceText != null)
        {
            confidenceText.text = "";
        }
    }
    
    private void UpdateTriggerTimers()
    {
        float time = Time.time;
        
        if (leftHandTriggered && time - leftTriggerTime > 0.5f)
        {
            leftHandTriggered = false;
        }
        
        if (rightHandTriggered && time - rightTriggerTime > 0.5f)
        {
            rightHandTriggered = false;
        }
    }
    
    private void InitializePoseVisualization()
    {
        if (!showPoseOverlay || poseOverlay == null) return;
        
        if (landmarkMarkers == null || landmarkMarkers.Length == 0)
        {
            CreateLandmarkMarkers();
        }
        
        if (poseLines == null || poseLines.Length == 0)
        {
            CreatePoseLines();
        }
    }
    
    private void CreateLandmarkMarkers()
    {
        landmarkMarkers = new Transform[6]; // 2 hands + 2 shoulders + 2 elbows
        
        for (int i = 0; i < landmarkMarkers.Length; i++)
        {
            GameObject marker = new GameObject($"LandmarkMarker_{i}");
            marker.transform.SetParent(poseOverlay.transform);
            
            var image = marker.AddComponent<Image>();
            image.sprite = CreateCircleSprite();
            image.color = i < 2 ? (i == 0 ? UnityEngine.Color.red : UnityEngine.Color.blue) : UnityEngine.Color.yellow;
            
            var rectTransform = marker.GetComponent<RectTransform>();
            rectTransform.sizeDelta = Vector2.one * markerSize;
            
            landmarkMarkers[i] = marker.transform;
        }
    }
    
    private void CreatePoseLines()
    {
        poseLines = new LineRenderer[3];
        
        for (int i = 0; i < poseLines.Length; i++)
        {
            GameObject lineObj = new GameObject($"PoseLine_{i}");
            lineObj.transform.SetParent(poseOverlay.transform);
            
            var lineRenderer = lineObj.AddComponent<LineRenderer>();
            lineRenderer.material = poseMaterial ?? CreateDefaultLineMaterial();
            lineRenderer.startWidth = 2f;
            lineRenderer.endWidth = 2f;
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = false;
            
            poseLines[i] = lineRenderer;
        }
    }
    
    private Sprite CreateCircleSprite()
    {
        int size = 16;
        Texture2D texture = new Texture2D(size, size);
        UnityEngine.Color[] pixels = new UnityEngine.Color[size * size];
        
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * size + x] = distance <= radius ? UnityEngine.Color.white : UnityEngine.Color.clear;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f);
    }
    
    private Material CreateDefaultLineMaterial()
    {
        Material mat = new Material(Shader.Find("UI/Default"));
        mat.color = UnityEngine.Color.white;
        return mat;
    }
    
    // Event handlers
    private void OnPoseDetected(PoseDetectionData poseData)
    {
        if (!showPoseOverlay || !isVisible) return;
        
        UpdatePoseVisualization(poseData);
    }
    
    private void OnCameraStatusChanged(bool isActive)
    {
        SetVisible(isActive && showPreview);
    }
    
    private void OnError(string error)
    {
        if (statusText != null)
        {
            statusText.text = $"Error: {error}";
            statusText.color = UnityEngine.Color.red;
        }
    }
    
    private void UpdatePoseVisualization(PoseDetectionData poseData)
    {
        if (!poseData.isValid || poseOverlay == null) return;
        
        if (landmarkMarkers != null && landmarkMarkers.Length >= 6)
        {
            Vector2 previewSize = previewContainer.sizeDelta;
            
            Vector2 leftHandPos = new Vector2(
                poseData.leftHand.x * previewSize.x - previewSize.x * 0.5f,
                poseData.leftHand.y * previewSize.y - previewSize.y * 0.5f
            );
            Vector2 rightHandPos = new Vector2(
                poseData.rightHand.x * previewSize.x - previewSize.x * 0.5f,
                poseData.rightHand.y * previewSize.y - previewSize.y * 0.5f
            );
            Vector2 leftShoulderPos = new Vector2(
                poseData.leftShoulder.x * previewSize.x - previewSize.x * 0.5f,
                poseData.leftShoulder.y * previewSize.y - previewSize.y * 0.5f
            );
            Vector2 rightShoulderPos = new Vector2(
                poseData.rightShoulder.x * previewSize.x - previewSize.x * 0.5f,
                poseData.rightShoulder.y * previewSize.y - previewSize.y * 0.5f
            );
            
            landmarkMarkers[0].localPosition = leftHandPos;
            landmarkMarkers[1].localPosition = rightHandPos;
            landmarkMarkers[2].localPosition = leftShoulderPos;
            landmarkMarkers[3].localPosition = rightShoulderPos;
            
            landmarkMarkers[0].gameObject.SetActive(poseData.leftHandVisible);
            landmarkMarkers[1].gameObject.SetActive(poseData.rightHandVisible);
            landmarkMarkers[2].gameObject.SetActive(poseData.leftShoulderConfidence > 0.5f);
            landmarkMarkers[3].gameObject.SetActive(poseData.rightShoulderConfidence > 0.5f);
        }
        
        if (poseLines != null && showHandConnections)
        {
            if (poseLines.Length > 0 && poseData.leftHandVisible && poseData.leftShoulderConfidence > 0.5f)
            {
                poseLines[0].SetPosition(0, landmarkMarkers[0].localPosition);
                poseLines[0].SetPosition(1, landmarkMarkers[2].localPosition);
                poseLines[0].enabled = true;
            }
            else if (poseLines.Length > 0)
            {
                poseLines[0].enabled = false;
            }
            
            if (poseLines.Length > 1 && poseData.rightHandVisible && poseData.rightShoulderConfidence > 0.5f)
            {
                poseLines[1].SetPosition(0, landmarkMarkers[1].localPosition);
                poseLines[1].SetPosition(1, landmarkMarkers[3].localPosition);
                poseLines[1].enabled = true;
            }
            else if (poseLines.Length > 1)
            {
                poseLines[1].enabled = false;
            }
            
            if (poseLines.Length > 2 && showShoulderLine && 
                poseData.leftShoulderConfidence > 0.5f && poseData.rightShoulderConfidence > 0.5f)
            {
                poseLines[2].SetPosition(0, landmarkMarkers[2].localPosition);
                poseLines[2].SetPosition(1, landmarkMarkers[3].localPosition);
                poseLines[2].enabled = true;
            }
            else if (poseLines.Length > 2)
            {
                poseLines[2].enabled = false;
            }
        }
    }
    
    // Public control methods
    public void SetCameraTexture(WebCamTexture texture)
    {
        cameraTexture = texture;
        
        if (cameraPreview != null)
        {
            cameraPreview.texture = texture;
        }
        
        SetVisible(texture != null && showPreview);
    }
    
    public void SetVisible(bool visible)
    {
        isVisible = visible;
        targetAlpha = visible ? previewAlpha : 0f;
        
        if (!enableAnimations)
        {
            currentAlpha = targetAlpha;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = currentAlpha;
            }
        }
    }
    
    public void SetPreviewSize(Vector2 size)
    {
        previewSize = size;
        if (previewContainer != null)
        {
            previewContainer.sizeDelta = size;
        }
    }
    
    public void SetPreviewPosition(Vector2 position)
    {
        previewPosition = position;
        if (previewContainer != null)
        {
            previewContainer.anchoredPosition = position;
        }
    }
    
    public void SetPreviewAlpha(float alpha)
    {
        previewAlpha = Mathf.Clamp01(alpha);
        if (isVisible)
        {
            targetAlpha = previewAlpha;
        }
    }
    
    public void TogglePreview()
    {
        showPreview = !showPreview;
        SetVisible(showPreview && cameraTexture != null);
    }
    
    public void TogglePoseOverlay()
    {
        showPoseOverlay = !showPoseOverlay;
        if (poseOverlay != null)
        {
            poseOverlay.SetActive(showPoseOverlay);
        }
    }
    
    public void OnPaddleTrigger(bool isLeft)
    {
        float time = Time.time;
        
        if (isLeft)
        {
            leftHandTriggered = true;
            leftTriggerTime = time;
        }
        else
        {
            rightHandTriggered = true;
            rightTriggerTime = time;
        }
    }
    
    public void AnimateIn()
    {
        if (enableAnimations && previewContainer != null)
        {
            StartCoroutine(ScaleAnimation(Vector2.zero, previewSize, 0.3f));
        }
        SetVisible(true);
    }
    
    public void AnimateOut()
    {
        if (enableAnimations && previewContainer != null)
        {
            StartCoroutine(ScaleAnimation(previewSize, Vector2.zero, 0.3f));
        }
        SetVisible(false);
    }
    
    private System.Collections.IEnumerator ScaleAnimation(Vector2 fromSize, Vector2 toSize, float duration)
    {
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = scaleCurve.Evaluate(elapsed / duration);
            
            Vector2 currentSize = Vector2.Lerp(fromSize, toSize, t);
            previewContainer.sizeDelta = currentSize;
            
            yield return null;
        }
        
        previewContainer.sizeDelta = toSize;
    }
    
    private void OnDestroy()
    {
        if (bodyTracker != null)
        {
            bodyTracker.OnPoseDetected -= OnPoseDetected;
            bodyTracker.OnCameraStatusChanged -= OnCameraStatusChanged;
            bodyTracker.OnErrorOccurred -= OnError;
        }
    }
}