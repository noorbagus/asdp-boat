using UnityEngine;

public class WhaleObstacle : ObstacleBase
{
    [Header("Whale Properties")]
    [SerializeField] private float jumpHeight = 5f;
    [SerializeField] private float jumpDuration = 2f;
    [SerializeField] private float timeBetweenJumps = 5f;
    [SerializeField] private AnimationCurve jumpCurve;
    
    private Vector3 startPosition;
    private Vector3 peakPosition;
    private bool isJumping = false;
    private float jumpTimer = 0f;
    private float idleTimer = 0f;
    
    protected override void Start()
    {
        base.Start();
        startPosition = transform.position;
        peakPosition = startPosition + Vector3.up * jumpHeight;
        
        // Start with random idle time to stagger whale jumps
        idleTimer = Random.Range(0f, timeBetweenJumps);
        
        // Initialize jump curve if not set
        if (jumpCurve.length == 0)
        {
            // Create default jump curve
            Keyframe[] keys = new Keyframe[3];
            keys[0] = new Keyframe(0f, 0f);
            keys[1] = new Keyframe(0.5f, 1f);
            keys[2] = new Keyframe(1f, 0f);
            
            jumpCurve = new AnimationCurve(keys);
        }
    }
    
    protected override void Update()
    {
        base.Update();
        
        if (!isJumping)
        {
            // Count down to next jump
            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f)
            {
                StartJump();
            }
        }
        else
        {
            // Update jump animation
            UpdateJump();
        }
    }
    
    private void StartJump()
    {
        isJumping = true;
        jumpTimer = 0f;
    }
    
    private void UpdateJump()
    {
        jumpTimer += Time.deltaTime;
        float progress = jumpTimer / jumpDuration;
        
        if (progress >= 1f)
        {
            // Jump complete
            isJumping = false;
            transform.position = startPosition;
            idleTimer = timeBetweenJumps;
        }
        else
        {
            // Animate along jump curve
            float heightProgress = jumpCurve.Evaluate(progress);
            Vector3 newPosition = Vector3.Lerp(startPosition, peakPosition, heightProgress);
            
            // Add forward movement during jump
            if (canMove)
            {
                newPosition += transform.forward * moveSpeed * Time.deltaTime;
            }
            
            transform.position = newPosition;
        }
    }
    
    // Override the collision method to only damage player during jumps
    protected override void HandlePlayerCollision(Collision collision)
    {
        // Only damage the player if the whale is above water (jumping)
        if (isJumping && transform.position.y > startPosition.y + 0.5f)
        {
            base.HandlePlayerCollision(collision);
        }
    }
}