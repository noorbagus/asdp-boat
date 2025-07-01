using UnityEngine;

public class WhaleObstacle : ObstacleBase
{
    [Header("Whale Properties")]
    [SerializeField] private float jumpHeight = 5f;
    [SerializeField] private float jumpDuration = 4f;
    [SerializeField] private float timeBetweenJumps = 6f;
    [SerializeField] private float surfaceYPosition = 0.5f;
    [SerializeField] private float underwaterYPosition = -1f;
    [SerializeField] private AnimationCurve jumpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("AI Behavior")]
    [SerializeField] private float aggroRadius = 40f;
    [SerializeField] private float interceptSpeed = 8f;
    [SerializeField] private float interceptDistance = 20f;
    [SerializeField] private bool enableIntercept = true;
    
    [Header("Position Constraints")]
       
    [Header("Debug")]
    // Removed [SerializeField] to avoid serialization conflict
    private float whaleDebugInterval = 2f;
    private float whaleLastDebugTime = 0f;
    
    // State tracking
    private Transform boatTransform;
    private Rigidbody boatRigidbody;
    private bool isJumping = false;
    private bool isIntercepting = false;
    private float jumpTimer = 0f;
    private float idleTimer = 0f;
    private Vector3 interceptTarget;
    private Vector3 jumpStartPosition;
    
    protected override void Start()
    {
        base.Start();
        
        // Find boat
        BoatController boat = FindObjectOfType<BoatController>();
        if (boat != null)
        {
            boatTransform = boat.transform;
            boatRigidbody = boat.GetComponent<Rigidbody>();
        }
        
        // Start underwater
        Vector3 pos = transform.position;
        pos.y = underwaterYPosition;
        transform.position = pos;
        jumpStartPosition = pos;
        
        InitializeJumpCurve();
        idleTimer = Random.Range(1f, timeBetweenJumps);
        
        WhaleDebugLog($"Whale initialized at {transform.position}");
    }
    
    protected override void Update()
    {
        UpdateWhaleAI();
        EnforceConstraints();
        DebugTracking();
    }
    
    protected override void Move()
    {
        // Override base movement - whales have custom AI movement
    }
    
    private void UpdateWhaleAI()
    {
        if (isJumping)
        {
            UpdateJump();
        }
        else
        {
            UpdateIdleState();
        }
    }
    
    private void UpdateIdleState()
    {
        if (enableIntercept && boatTransform != null)
        {
            float distanceToBoat = Vector3.Distance(transform.position, boatTransform.position);
            
            if (distanceToBoat <= aggroRadius)
            {
                CalculateInterceptTarget();
                MoveTowardsIntercept();
            }
        }
        
        // Check for jump trigger
        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f)
        {
            StartJump();
        }
    }
    
    private void CalculateInterceptTarget()
    {
        if (boatTransform == null) return;
        
        Vector3 boatPos = boatTransform.position;
        Vector3 boatVelocity = boatRigidbody ? boatRigidbody.velocity : boatTransform.forward * 5f;
        
        // Predict boat position
        Vector3 predictedBoatPos = boatPos + boatVelocity * (interceptDistance / interceptSpeed);
        
        // Position whale ahead of predicted path
        interceptTarget = predictedBoatPos + boatTransform.forward * interceptDistance;
        interceptTarget.y = underwaterYPosition;
        
        isIntercepting = true;
    }
    
    private void MoveTowardsIntercept()
    {
        if (!isIntercepting) return;
        
        Vector3 direction = (interceptTarget - transform.position).normalized;
        direction.y = 0; // Keep horizontal movement only
        
        Vector3 newPos = transform.position + direction * interceptSpeed * Time.deltaTime;
        newPos.y = underwaterYPosition;
        transform.position = newPos;
        
        // Stop intercepting when close to target
        float distanceToTarget = Vector3.Distance(transform.position, interceptTarget);
        if (distanceToTarget < 5f)
        {
            isIntercepting = false;
        }
    }
    
    private void StartJump()
    {
        isJumping = true;
        isIntercepting = false;
        jumpTimer = 0f;
        jumpStartPosition = transform.position;
        jumpStartPosition.y = underwaterYPosition;
        
        WhaleDebugLog($"Whale starting jump from {jumpStartPosition}");
    }
    
    private void UpdateJump()
    {
        jumpTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(jumpTimer / jumpDuration);
        
        if (progress >= 1f)
        {
            EndJump();
        }
        else
        {
            float heightProgress = jumpCurve.Evaluate(progress);
            float currentY = Mathf.Lerp(underwaterYPosition, surfaceYPosition + jumpHeight, heightProgress);
            
            Vector3 jumpPosition = new Vector3(
                jumpStartPosition.x,
                currentY,
                jumpStartPosition.z
            );
            
            transform.position = jumpPosition;
        }
    }
    
    private void EndJump()
    {
        isJumping = false;
        
        Vector3 endPos = new Vector3(jumpStartPosition.x, underwaterYPosition, jumpStartPosition.z);
        transform.position = endPos;
        
        idleTimer = timeBetweenJumps;
        WhaleDebugLog($"Whale jump ended, returned to {endPos}");
    }
    
    private void InitializeJumpCurve()
    {
        if (jumpCurve.length == 0)
        {
            Keyframe[] keys = new Keyframe[4];
            keys[0] = new Keyframe(0f, 0f, 0f, 2f);
            keys[1] = new Keyframe(0.3f, 1f, 0f, 0f);
            keys[2] = new Keyframe(0.7f, 1f, 0f, 0f);
            keys[3] = new Keyframe(1f, 0f, -2f, 0f);
            jumpCurve = new AnimationCurve(keys);
        }
    }
    
    private void EnforceConstraints()
    {
        Vector3 pos = transform.position;
        float distanceFromOrigin = Vector3.Distance(new Vector3(pos.x, 0, pos.z), Vector3.zero);
        
        if (distanceFromOrigin > maxDistanceFromOrigin)
        {
            // Teleport back to safe area
            Vector3 safePos = Vector3.zero + Random.insideUnitSphere * (maxDistanceFromOrigin * 0.5f);
            safePos.y = underwaterYPosition;
            transform.position = safePos;
            
            WhaleDebugLog($"Whale teleported to safe area: {safePos}");
        }
        
        // Ensure Y constraint when not jumping
        if (!isJumping && pos.y != underwaterYPosition)
        {
            pos.y = underwaterYPosition;
            transform.position = pos;
        }
    }
    
    private void DebugTracking()
    {
        if (enableDebugTracking && Time.time - whaleLastDebugTime >= whaleDebugInterval)
        {
            string state = isJumping ? "JUMPING" : (isIntercepting ? "INTERCEPTING" : "IDLE");
            WhaleDebugLog($"[{gameObject.name}] Pos: {transform.position:F2}, State: {state}, Target: {interceptTarget:F2}");
            whaleLastDebugTime = Time.time;
        }
    }
    
    protected override void HandlePlayerCollision(Collision collision)
    {
        // Only damage when jumping (visible)
        if (isJumping || transform.position.y > underwaterYPosition + 0.5f)
        {
            base.HandlePlayerCollision(collision);
            WhaleDebugLog("Whale collision with player during surface breach");
        }
    }
    
    // Renamed to avoid hiding base method
    private void WhaleDebugLog(string message)
    {
        if (enableDebugTracking)
        {
            Debug.Log($"[WhaleObstacle] {message}");
        }
    }
    
    // Public getters
    public bool IsJumping() => isJumping;
    public bool IsIntercepting() => isIntercepting;
    public Vector3 GetInterceptTarget() => interceptTarget;
    
    // Gizmos for visual debugging
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        
        // Draw aggro radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRadius);
        
        // Draw intercept target
        if (isIntercepting)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, interceptTarget);
            Gizmos.DrawWireSphere(interceptTarget, 2f);
        }
        
        // Draw jump arc
        if (isJumping)
        {
            Gizmos.color = Color.green;
            Vector3 peakPos = new Vector3(jumpStartPosition.x, surfaceYPosition + jumpHeight, jumpStartPosition.z);
            Gizmos.DrawLine(jumpStartPosition, peakPos);
            Gizmos.DrawWireSphere(peakPos, 1f);
        }
    }
}