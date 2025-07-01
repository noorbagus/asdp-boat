using UnityEngine;

public class BlendshapeLooper : MonoBehaviour
{
    [Header("Blendshape Settings")]
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] private int blendshapeIndex = 0;
    [SerializeField] private float speed = 1f;
    [SerializeField] private float minValue = 0f;
    [SerializeField] private float maxValue = 100f;
    
    [Header("Animation Type")]
    [SerializeField] private AnimationType animationType = AnimationType.PingPong;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    public enum AnimationType
    {
        PingPong,    // 0->100->0->100
        Loop,        // 0->100->0->100 (reset to 0)
        Sine         // Smooth sine wave
    }
    
    private float currentTime = 0f;
    private Mesh skinnedMesh;
    private int blendShapeCount;
    
    private void Awake()
    {
        // Get SkinnedMeshRenderer component
        if (skinnedMeshRenderer == null)
            skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
            
        if (skinnedMeshRenderer != null)
            skinnedMesh = skinnedMeshRenderer.sharedMesh;
    }
    
    private void Start()
    {
        // DON'T disable Animator - let bones work normally
        
        // Validation checks
        if (skinnedMeshRenderer == null)
        {
            if (showDebugInfo) Debug.LogError($"[BlendshapeLooper] No SkinnedMeshRenderer found on {gameObject.name}!");
            enabled = false;
            return;
        }
        
        if (skinnedMesh == null)
        {
            if (showDebugInfo) Debug.LogError($"[BlendshapeLooper] No mesh found on SkinnedMeshRenderer!");
            enabled = false;
            return;
        }
        
        blendShapeCount = skinnedMesh.blendShapeCount;
        
        if (blendShapeCount == 0)
        {
            if (showDebugInfo) Debug.LogError($"[BlendshapeLooper] No blendshapes found in mesh!");
            enabled = false;
            return;
        }
        
        if (blendshapeIndex >= blendShapeCount || blendshapeIndex < 0)
        {
            if (showDebugInfo) Debug.LogError($"[BlendshapeLooper] Blendshape index {blendshapeIndex} is invalid! Available: 0-{blendShapeCount - 1}");
            enabled = false;
            return;
        }
        
        // Debug info
        if (showDebugInfo)
        {
            Debug.Log($"[BlendshapeLooper] Started on {gameObject.name}");
            Debug.Log($"[BlendshapeLooper] Total blendshapes: {blendShapeCount}");
            Debug.Log($"[BlendshapeLooper] Using blendshape {blendshapeIndex}: {skinnedMesh.GetBlendShapeName(blendshapeIndex)}");
            
            // List all available blendshapes
            for (int i = 0; i < blendShapeCount; i++)
            {
                Debug.Log($"[BlendshapeLooper] Available blendshape {i}: {skinnedMesh.GetBlendShapeName(i)}");
            }
        }
    }
    
    private void Update()
    {
        if (skinnedMeshRenderer == null || !enabled) return;
        
        currentTime += Time.deltaTime * speed;
        
        float blendValue = CalculateBlendValue();
        skinnedMeshRenderer.SetBlendShapeWeight(blendshapeIndex, blendValue);
        
        // Debug current value every second
        if (showDebugInfo && Time.time % 1f < Time.deltaTime)
        {
            Debug.Log($"[BlendshapeLooper] Current blend value: {blendValue:F2}");
        }
    }
    
    private void LateUpdate()
    {
        // Override blendshape after Animator updates
        if (skinnedMeshRenderer != null && enabled)
        {
            float blendValue = CalculateBlendValue();
            skinnedMeshRenderer.SetBlendShapeWeight(blendshapeIndex, blendValue);
        }
    }
    
    private float CalculateBlendValue()
    {
        switch (animationType)
        {
            case AnimationType.PingPong:
                return Mathf.Lerp(minValue, maxValue, Mathf.PingPong(currentTime, 1f));
                
            case AnimationType.Loop:
                return Mathf.Lerp(minValue, maxValue, currentTime % 1f);
                
            case AnimationType.Sine:
                return Mathf.Lerp(minValue, maxValue, (Mathf.Sin(currentTime) + 1f) * 0.5f);
                
            default:
                return minValue;
        }
    }
    
    // Manual testing methods
    [ContextMenu("Test Blendshape Max")]
    public void TestBlendshapeMax()
    {
        if (skinnedMeshRenderer != null)
        {
            skinnedMeshRenderer.SetBlendShapeWeight(blendshapeIndex, maxValue);
            if (showDebugInfo) Debug.Log($"[BlendshapeLooper] Manual test: Set to {maxValue}");
        }
    }
    
    [ContextMenu("Test Blendshape Min")]
    public void TestBlendshapeMin()
    {
        if (skinnedMeshRenderer != null)
        {
            skinnedMeshRenderer.SetBlendShapeWeight(blendshapeIndex, minValue);
            if (showDebugInfo) Debug.Log($"[BlendshapeLooper] Manual test: Set to {minValue}");
        }
    }
    
    [ContextMenu("List All Blendshapes")]
    public void ListAllBlendshapes()
    {
        if (skinnedMesh != null)
        {
            Debug.Log($"[BlendshapeLooper] Listing all {skinnedMesh.blendShapeCount} blendshapes:");
            for (int i = 0; i < skinnedMesh.blendShapeCount; i++)
            {
                Debug.Log($"  {i}: {skinnedMesh.GetBlendShapeName(i)}");
            }
        }
    }
    
    // Public methods for runtime control
    public void SetBlendshapeIndex(int index)
    {
        if (index >= 0 && index < blendShapeCount)
        {
            blendshapeIndex = index;
            if (showDebugInfo) Debug.Log($"[BlendshapeLooper] Changed to blendshape {index}: {skinnedMesh.GetBlendShapeName(index)}");
        }
    }
    
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }
    
    public void SetRange(float min, float max)
    {
        minValue = min;
        maxValue = max;
    }
    
    public void ResetAnimation()
    {
        currentTime = 0f;
    }
}