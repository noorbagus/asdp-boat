using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Transform to look at (camera/boat)")]
    public Transform targetTransform;
    
    [Tooltip("Automatically find MainCamera or boat if not assigned")]
    public bool autoFindTarget = true;
    
    [Header("Rotation Settings")]
    [Tooltip("Only rotate on Y-axis (billboard effect)")]
    public bool billboardMode = true;
    
    [Tooltip("Smooth rotation transition")]
    public bool smoothRotation = true;
    
    [Tooltip("Rotation speed when smoothing enabled")]
    [Range(1f, 20f)]
    public float rotationSpeed = 5.0f;
    
    [Header("Front Direction")]
    [Tooltip("Direction that represents the front of the model")]
    public Vector3 frontDirection = Vector3.forward;
    
    [Tooltip("Additional rotation offset in degrees")]
    public Vector3 frontOffset = Vector3.zero;
    
    [Header("Performance")]
    [Tooltip("When to update the rotation")]
    public UpdateMode updateMode = UpdateMode.EveryFrame;
    
    [Tooltip("Stop rotating if target is farther than this distance")]
    [Range(10f, 500f)]
    public float maxDistance = 100f;
    
    [Header("Debug")]
    [Tooltip("Show direction rays in scene view")]
    public bool showDebugRays = false;
    
    public enum UpdateMode
    {
        EveryFrame,
        FixedUpdate,
        Manual
    }
    
    // Private variables
    private Camera mainCamera;
    private BoatController boat;
    private bool hasValidTarget = false;
    private Vector3 lastTargetPosition;
    private float lastDistance;
    
    private void Start()
    {
        InitializeTarget();
    }
    
    private void InitializeTarget()
    {
        // Use assigned target if available
        if (targetTransform != null)
        {
            hasValidTarget = true;
            return;
        }
        
        // Auto-find target if enabled
        if (autoFindTarget)
        {
            // Try to find main camera
            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                targetTransform = mainCamera.transform;
                hasValidTarget = true;
                return;
            }
            
            // Try to find boat
            boat = FindObjectOfType<BoatController>();
            if (boat != null)
            {
                targetTransform = boat.transform;
                hasValidTarget = true;
                return;
            }
        }
        
        Debug.LogWarning($"[LookAtCamera] No target found for {gameObject.name}");
    }
    
    private void Update()
    {
        if (updateMode == UpdateMode.EveryFrame)
        {
            UpdateLookAt();
        }
    }
    
    private void FixedUpdate()
    {
        if (updateMode == UpdateMode.FixedUpdate)
        {
            UpdateLookAt();
        }
    }
    
    private void UpdateLookAt()
    {
        if (!hasValidTarget || targetTransform == null)
        {
            return;
        }
        
        // Check distance for performance
        Vector3 targetPos = targetTransform.position;
        float distance = Vector3.Distance(transform.position, targetPos);
        
        if (distance > maxDistance)
        {
            return;
        }
        
        // Calculate look direction
        Vector3 lookDirection = targetPos - transform.position;
        
        if (billboardMode)
        {
            lookDirection.y = 0f; // Keep upright
        }
        
        // Skip if direction is too small
        if (lookDirection.sqrMagnitude < 0.001f)
        {
            return;
        }
        
        // Calculate target rotation
        Quaternion lookRotation = Quaternion.LookRotation(lookDirection);
        
        // Apply front direction offset
        if (frontDirection != Vector3.forward)
        {
            Quaternion frontDirectionOffset = Quaternion.FromToRotation(Vector3.forward, frontDirection);
            lookRotation = lookRotation * frontDirectionOffset;
        }
        
        // Apply additional front offset
        if (frontOffset != Vector3.zero)
        {
            lookRotation = lookRotation * Quaternion.Euler(frontOffset);
        }
        
        // Apply rotation
        if (smoothRotation)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            transform.rotation = lookRotation;
        }
        
        // Store for debugging
        lastTargetPosition = targetPos;
        lastDistance = distance;
    }
    
    // Manual update method for custom control
    public void ManualUpdate()
    {
        if (updateMode == UpdateMode.Manual)
        {
            UpdateLookAt();
        }
    }
    
    // Public methods for runtime control
    public void SetTarget(Transform newTarget)
    {
        targetTransform = newTarget;
        hasValidTarget = (newTarget != null);
    }
    
    public void SetTarget(Camera camera)
    {
        SetTarget(camera != null ? camera.transform : null);
    }
    
    public void SetTarget(BoatController boat)
    {
        SetTarget(boat != null ? boat.transform : null);
    }
    
    public void SetFrontDirection(Vector3 direction)
    {
        frontDirection = direction.normalized;
    }
    
    public void SetFrontOffset(Vector3 offset)
    {
        frontOffset = offset;
    }
    
    public void EnableLookAt(bool enable)
    {
        enabled = enable;
    }
    
    // Getters for debugging
    public bool HasValidTarget() => hasValidTarget && targetTransform != null;
    public float GetDistanceToTarget() => lastDistance;
    public Vector3 GetTargetPosition() => lastTargetPosition;
    
    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (!showDebugRays || !hasValidTarget || targetTransform == null)
            return;
        
        // Draw line to target
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, targetTransform.position);
        
        // Draw front direction
        Gizmos.color = Color.red;
        Vector3 frontDir = transform.TransformDirection(frontDirection);
        Gizmos.DrawRay(transform.position, frontDir * 3f);
        
        // Draw look direction
        Gizmos.color = Color.green;
        Vector3 lookDir = (targetTransform.position - transform.position).normalized;
        if (billboardMode) lookDir.y = 0;
        Gizmos.DrawRay(transform.position, lookDir * 2f);
        
        // Draw max distance circle
        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, maxDistance);
    }
    
    // Context menu methods for testing
    [ContextMenu("Find Target Automatically")]
    private void FindTargetManually()
    {
        autoFindTarget = true;
        InitializeTarget();
    }
    
    [ContextMenu("Look At Target Now")]
    private void LookAtTargetNow()
    {
        UpdateLookAt();
    }
    
    [ContextMenu("Reset Rotation")]
    private void ResetRotation()
    {
        transform.rotation = Quaternion.identity;
    }
}