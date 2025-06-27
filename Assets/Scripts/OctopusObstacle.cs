using UnityEngine;

public class OctopusObstacle : ObstacleBase
{
    [Header("Octopus Properties")]
    [SerializeField] private Transform[] tentacles;
    [SerializeField] private float tentacleSpeed = 2f;
    [SerializeField] private float maxTentacleExtension = 3f;
    [SerializeField] private float timeBetweenMovements = 2f;
    
    private Vector3[] tentacleStartPositions;
    private Vector3[] tentacleTargetPositions;
    private float movementTimer = 0f;
    private bool extending = false;
    
    protected override void Start()
    {
        base.Start();
        
        // Initialize tentacle positions
        if (tentacles.Length > 0)
        {
            tentacleStartPositions = new Vector3[tentacles.Length];
            tentacleTargetPositions = new Vector3[tentacles.Length];
            
            for (int i = 0; i < tentacles.Length; i++)
            {
                tentacleStartPositions[i] = tentacles[i].localPosition;
                tentacleTargetPositions[i] = tentacleStartPositions[i];
            }
            
            // Start with random movement timer
            movementTimer = Random.Range(0f, timeBetweenMovements);
        }
        else
        {
            // If no tentacles assigned, try to find child objects with "Tentacle" in name
            Transform[] childObjects = GetComponentsInChildren<Transform>();
            System.Collections.Generic.List<Transform> foundTentacles = new System.Collections.Generic.List<Transform>();
            
            foreach (Transform child in childObjects)
            {
                if (child != transform && child.name.Contains("Tentacle"))
                {
                    foundTentacles.Add(child);
                }
            }
            
            if (foundTentacles.Count > 0)
            {
                tentacles = foundTentacles.ToArray();
                tentacleStartPositions = new Vector3[tentacles.Length];
                tentacleTargetPositions = new Vector3[tentacles.Length];
                
                for (int i = 0; i < tentacles.Length; i++)
                {
                    tentacleStartPositions[i] = tentacles[i].localPosition;
                    tentacleTargetPositions[i] = tentacleStartPositions[i];
                }
                
                // Start with random movement timer
                movementTimer = Random.Range(0f, timeBetweenMovements);
            }
            else
            {
                Debug.LogWarning("No tentacles assigned to OctopusObstacle: " + name);
            }
        }
    }
    
    protected override void Update()
    {
        base.Update();
        
        // Skip if no tentacles
        if (tentacles == null || tentacles.Length == 0) return;
        
        // Count down to next movement
        movementTimer -= Time.deltaTime;
        if (movementTimer <= 0f)
        {
            // Toggle between extending and retracting
            extending = !extending;
            
            // Set new target positions
            for (int i = 0; i < tentacles.Length; i++)
            {
                if (extending)
                {
                    // Extend in random direction
                    Vector3 randomDir = new Vector3(
                        Random.Range(-1f, 1f),
                        Random.Range(-0.2f, 0.2f), // Limited vertical movement
                        Random.Range(-1f, 1f)
                    ).normalized;
                    
                    tentacleTargetPositions[i] = tentacleStartPositions[i] + randomDir * maxTentacleExtension;
                }
                else
                {
                    // Retract to starting position
                    tentacleTargetPositions[i] = tentacleStartPositions[i];
                }
            }
            
            // Reset timer
            movementTimer = timeBetweenMovements;
        }
        
        // Move tentacles toward target positions
        for (int i = 0; i < tentacles.Length; i++)
        {
            tentacles[i].localPosition = Vector3.Lerp(
                tentacles[i].localPosition,
                tentacleTargetPositions[i],
                tentacleSpeed * Time.deltaTime
            );
        }
    }
    
    // Add colliders to tentacles
    public void SetupTentacleColliders(float radius = 0.3f)
    {
        if (tentacles == null || tentacles.Length == 0) return;
        
        for (int i = 0; i < tentacles.Length; i++)
        {
            // Add collider if not already present
            if (tentacles[i].GetComponent<Collider>() == null)
            {
                CapsuleCollider collider = tentacles[i].gameObject.AddComponent<CapsuleCollider>();
                collider.radius = radius;
                collider.height = tentacles[i].localScale.y * 2;
                collider.direction = 1; // Y-axis
                collider.isTrigger = true;
            }
        }
    }
    
    // Handle collision with tentacles
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Get player boat and rigidbody
            BoatController boat = other.GetComponent<BoatController>();
            Rigidbody playerRb = other.GetComponent<Rigidbody>();
            
            // Apply damage via game manager
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.TakeDamage(damageAmount / 2); // Less damage than direct collision
            }
            
            // Apply smaller knockback
            if (playerRb != null)
            {
                Vector3 knockbackDirection = other.transform.position - transform.position;
                knockbackDirection.y = 0; // Keep knockback horizontal
                playerRb.AddForce(knockbackDirection.normalized * knockbackForce * 0.5f, ForceMode.Impulse);
            }
            
            // Play effects at contact point
            PlayCollisionEffects(other.ClosestPoint(transform.position));
        }
    }
}