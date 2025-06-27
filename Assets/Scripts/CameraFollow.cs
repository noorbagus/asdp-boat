using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -5f);
    
    [Header("Follow Settings")]
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float rotationSmooth = 2f;
    [SerializeField] private bool followRotation = false;
    [SerializeField] private bool lookAtTarget = true;
    
    [Header("Advanced Settings")]
    [SerializeField] private float minDistance = 3f;
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float heightDamping = 2f;
    
    private Vector3 currentVelocity;
    
    private void Start()
    {
        // If no target is specified, try to find the player
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogWarning("Camera Follow: No target assigned and no Player tag found");
            }
        }
        
        // Set initial position
        if (target != null)
        {
            transform.position = target.position + offset;
            
            if (lookAtTarget)
            {
                transform.LookAt(target);
            }
        }
    }
    
    private void LateUpdate()
    {
        if (target == null) return;
        
        // Calculate the desired position
        Vector3 desiredPosition = CalculateDesiredPosition();
        
        // Smoothly move the camera
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, 1f / smoothSpeed);
        
        // Handle rotation
        if (followRotation)
        {
            // Smoothly rotate to match target's rotation
            Quaternion desiredRotation = Quaternion.Euler(0, target.eulerAngles.y, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmooth * Time.deltaTime);
        }
        else if (lookAtTarget)
        {
            // Simply look at the target
            transform.LookAt(target);
        }
    }
    
    private Vector3 CalculateDesiredPosition()
    {
        // Calculate position based on target position and offset
        Vector3 desiredPosition = target.position + offset;
        
        // Apply any additional positioning logic here
        
        return desiredPosition;
    }
    
    // Methods to adjust camera settings at runtime
    
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }
    
    public void SetSmoothSpeed(float newSpeed)
    {
        smoothSpeed = newSpeed;
    }
    
    public void EnableRotationFollow(bool enable)
    {
        followRotation = enable;
    }
    
    // Method to shake the camera (useful for impacts)
    public void ShakeCamera(float intensity = 0.5f, float duration = 0.5f)
    {
        StartCoroutine(DoCameraShake(intensity, duration));
    }
    
    private System.Collections.IEnumerator DoCameraShake(float intensity, float duration)
    {
        Vector3 originalPosition = transform.localPosition;
        float elapsed = 0.0f;
        
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * intensity;
            float y = Random.Range(-1f, 1f) * intensity;
            
            transform.localPosition = new Vector3(
                originalPosition.x + x,
                originalPosition.y + y,
                originalPosition.z
            );
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        transform.localPosition = originalPosition;
    }
}