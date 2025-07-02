using UnityEngine;
using System.Collections.Generic;

public enum AnimationType
{
    PingPong,    // 0->100->0->100
    Loop,        // 0->100->0->100 (reset to 0)
    Sine,        // Smooth sine wave
    Random       // Random values
}

[System.Serializable]
public class BlendshapeData
{
    public string name;
    public int index;
    public float speed = 1f;
    public float minValue = 0f;
    public float maxValue = 100f;
    public AnimationType animationType = AnimationType.PingPong;
    public bool enabled = true;
    
    [HideInInspector] public float currentTime = 0f;
}

public class BlendshapeLooper : MonoBehaviour
{
    [Header("Blendshape Settings")]
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] private List<BlendshapeData> blendshapes = new List<BlendshapeData>();
    
    [Header("Global Settings")]
    [SerializeField] private float globalSpeedMultiplier = 1f;
    [SerializeField] private bool playAll = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    private Mesh skinnedMesh;
    private int blendShapeCount;
    
    private void Awake()
    {
        if (skinnedMeshRenderer == null)
            skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
            
        if (skinnedMeshRenderer != null)
            skinnedMesh = skinnedMeshRenderer.sharedMesh;
    }
    
    private void Start()
    {
        if (!ValidateComponents()) return;
        
        blendShapeCount = skinnedMesh.blendShapeCount;
        
        if (showDebugInfo)
        {
            Debug.Log($"[BlendshapeLooper] Started with {blendShapeCount} blendshapes");
        }
    }
    
    private bool ValidateComponents()
    {
        if (skinnedMeshRenderer == null)
        {
            if (showDebugInfo) Debug.LogError("[BlendshapeLooper] No SkinnedMeshRenderer found!");
            enabled = false;
            return false;
        }
        
        if (skinnedMesh == null)
        {
            if (showDebugInfo) Debug.LogError("[BlendshapeLooper] No mesh found!");
            enabled = false;
            return false;
        }
        
        return true;
    }
    
    private void Update()
    {
        if (!enabled || !playAll) return;
        
        foreach (var blend in blendshapes)
        {
            if (!blend.enabled) continue;
            
            blend.currentTime += Time.deltaTime * blend.speed * globalSpeedMultiplier;
            
            float blendValue = CalculateBlendValue(blend);
            skinnedMeshRenderer.SetBlendShapeWeight(blend.index, blendValue);
        }
    }
    
    private void LateUpdate()
    {
        if (!enabled || !playAll) return;
        
        // Override after Animator
        foreach (var blend in blendshapes)
        {
            if (!blend.enabled) continue;
            
            float blendValue = CalculateBlendValue(blend);
            skinnedMeshRenderer.SetBlendShapeWeight(blend.index, blendValue);
        }
    }
    
    private float CalculateBlendValue(BlendshapeData blend)
    {
        switch (blend.animationType)
        {
            case AnimationType.PingPong:
                return Mathf.Lerp(blend.minValue, blend.maxValue, Mathf.PingPong(blend.currentTime, 1f));
                
            case AnimationType.Loop:
                return Mathf.Lerp(blend.minValue, blend.maxValue, blend.currentTime % 1f);
                
            case AnimationType.Sine:
                return Mathf.Lerp(blend.minValue, blend.maxValue, (Mathf.Sin(blend.currentTime) + 1f) * 0.5f);
                
            case AnimationType.Random:
                if (Time.fixedTime % 0.1f < Time.fixedDeltaTime) // Update every 0.1s
                    return Random.Range(blend.minValue, blend.maxValue);
                return skinnedMeshRenderer.GetBlendShapeWeight(blend.index);
                
            default:
                return blend.minValue;
        }
    }
    
    // Auto-detect and populate blendshapes
    [ContextMenu("Auto Setup All Blendshapes")]
    public void AutoSetupBlendshapes()
    {
        if (skinnedMesh == null) return;
        
        blendshapes.Clear();
        
        for (int i = 0; i < skinnedMesh.blendShapeCount; i++)
        {
            blendshapes.Add(new BlendshapeData
            {
                name = skinnedMesh.GetBlendShapeName(i),
                index = i,
                speed = Random.Range(0.5f, 2f),
                animationType = (AnimationType)Random.Range(0, 3)
            });
        }
        
        if (showDebugInfo)
            Debug.Log($"[BlendshapeLooper] Auto-setup {blendshapes.Count} blendshapes");
    }
    
    [ContextMenu("List All Blendshapes")]
    public void ListAllBlendshapes()
    {
        if (skinnedMesh == null) return;
        
        Debug.Log($"[BlendshapeLooper] Available blendshapes:");
        for (int i = 0; i < skinnedMesh.blendShapeCount; i++)
        {
            Debug.Log($"  {i}: {skinnedMesh.GetBlendShapeName(i)}");
        }
    }
    
    // Control methods
    public void PlayBlendshape(int index)
    {
        if (index < blendshapes.Count)
            blendshapes[index].enabled = true;
    }
    
    public void StopBlendshape(int index)
    {
        if (index < blendshapes.Count)
            blendshapes[index].enabled = false;
    }
    
    public void PlayAll()
    {
        playAll = true;
    }
    
    public void StopAll()
    {
        playAll = false;
    }
    
    public void ResetAll()
    {
        foreach (var blend in blendshapes)
        {
            blend.currentTime = 0f;
        }
    }
    
    public void SetGlobalSpeed(float speed)
    {
        globalSpeedMultiplier = speed;
    }
}