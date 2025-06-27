using UnityEngine;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{
    [Header("Level Settings")]
    [SerializeField] private float levelLength = 300f; // Length of level in meters
    [SerializeField] private float levelWidth = 10f; // Width of navigable channel
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    
    [Header("Obstacle Settings")]
    [SerializeField] private GameObject[] obstaclePrefabs;
    [SerializeField] private int minObstacles = 3;
    [SerializeField] private int maxObstacles = 6;
    [SerializeField] private float minObstacleSpacing = 30f;
    
    [Header("Treasure Settings")]
    [SerializeField] private GameObject treasurePrefab;
    [SerializeField] private int minTreasures = 5;
    [SerializeField] private int maxTreasures = 8;
    [SerializeField] private int[] treasureValues = { 50, 100 };
    
    [Header("Boundary Settings")]
    [SerializeField] private GameObject boundaryPrefab;
    [SerializeField] private float boundaryHeight = 2f;
    
    private List<GameObject> levelObjects = new List<GameObject>();
    
    private void Awake()
    {
        // If we need to generate the level on startup
        if (startPoint != null && endPoint != null)
        {
            GenerateLevel();
        }
    }
    
    public void GenerateLevel()
    {
        // Clear any existing level objects
        ClearLevel();
        
        // Calculate level direction
        Vector3 levelDirection = (endPoint.position - startPoint.position).normalized;
        levelDirection.y = 0; // Keep level horizontal
        
        // Create boundaries
        CreateBoundaries();
        
        // Spawn obstacles
        SpawnObstacles();
        
        // Spawn treasures
        SpawnTreasures();
    }
    
    private void CreateBoundaries()
    {
        if (boundaryPrefab == null || startPoint == null || endPoint == null) return;
        
        // Calculate level direction and perpendicular
        Vector3 levelDirection = (endPoint.position - startPoint.position).normalized;
        levelDirection.y = 0;
        Vector3 levelPerpendicular = new Vector3(levelDirection.z, 0, -levelDirection.x);
        
        // Create left boundary
        Vector3 leftStart = startPoint.position + levelPerpendicular * (levelWidth * 0.5f);
        Vector3 leftEnd = endPoint.position + levelPerpendicular * (levelWidth * 0.5f);
        CreateBoundaryWall(leftStart, leftEnd);
        
        // Create right boundary
        Vector3 rightStart = startPoint.position - levelPerpendicular * (levelWidth * 0.5f);
        Vector3 rightEnd = endPoint.position - levelPerpendicular * (levelWidth * 0.5f);
        CreateBoundaryWall(rightStart, rightEnd);
    }
    
    private void CreateBoundaryWall(Vector3 start, Vector3 end)
    {
        if (boundaryPrefab == null) return;
        
        // Calculate direction and length
        Vector3 direction = (end - start).normalized;
        float length = Vector3.Distance(start, end);
        
        // Calculate center position
        Vector3 center = (start + end) * 0.5f;
        
        // Create wall object
        GameObject wall = Instantiate(boundaryPrefab, center, Quaternion.identity);
        wall.transform.parent = transform;
        
        // Scale the wall to match length
        wall.transform.localScale = new Vector3(1f, boundaryHeight, length);
        
        // Rotate to face correct direction
        float angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
        wall.transform.rotation = Quaternion.Euler(0, angle, 0);
        
        // Add to level objects
        levelObjects.Add(wall);
    }
    
    private void SpawnObstacles()
    {
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0) return;
        
        // Calculate level direction and length
        Vector3 levelDirection = (endPoint.position - startPoint.position).normalized;
        float length = Vector3.Distance(startPoint.position, endPoint.position);
        Vector3 levelPerpendicular = new Vector3(levelDirection.z, 0, -levelDirection.x);
        
        // Determine number of obstacles
        int numObstacles = Random.Range(minObstacles, maxObstacles + 1);
        
        // Track used positions to prevent overlap
        List<Vector3> usedPositions = new List<Vector3>();
        
        // Spawn obstacles
        for (int i = 0; i < numObstacles; i++)
        {
            // Select random obstacle prefab
            GameObject obstaclePrefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
            
            // Calculate position along path
            float distanceAlongPath = Random.Range(length * 0.2f, length * 0.8f); // Avoid start/end
            float offsetFromCenter = Random.Range(-levelWidth * 0.3f, levelWidth * 0.3f); // Not too close to boundaries
            
            Vector3 position = startPoint.position + (levelDirection * distanceAlongPath) + (levelPerpendicular * offsetFromCenter);
            
            // Check for minimum spacing between obstacles
            bool validPosition = true;
            foreach (Vector3 usedPos in usedPositions)
            {
                if (Vector3.Distance(position, usedPos) < minObstacleSpacing)
                {
                    validPosition = false;
                    break;
                }
            }
            
            // Skip if invalid position
            if (!validPosition)
            {
                // Try again (reduce i to retry)
                i--;
                continue;
            }
            
            // Spawn obstacle
            GameObject obstacle = Instantiate(obstaclePrefab, position, Quaternion.identity);
            obstacle.transform.parent = transform;
            
            // Random rotation
            float yRotation = Random.Range(0f, 360f);
            obstacle.transform.rotation = Quaternion.Euler(0, yRotation, 0);
            
            // Add to lists
            levelObjects.Add(obstacle);
            usedPositions.Add(position);
        }
    }
    
    private void SpawnTreasures()
    {
        if (treasurePrefab == null) return;
        
        // Calculate level direction and length
        Vector3 levelDirection = (endPoint.position - startPoint.position).normalized;
        float length = Vector3.Distance(startPoint.position, endPoint.position);
        Vector3 levelPerpendicular = new Vector3(levelDirection.z, 0, -levelDirection.x);
        
        // Determine number of treasures
        int numTreasures = Random.Range(minTreasures, maxTreasures + 1);
        
        // Track used positions to prevent overlap
        List<Vector3> usedPositions = new List<Vector3>();
        
        // Spawn treasures
        for (int i = 0; i < numTreasures; i++)
        {
            // Calculate position along path
            float distanceAlongPath = Random.Range(length * 0.1f, length * 0.9f);
            float offsetFromCenter = Random.Range(-levelWidth * 0.4f, levelWidth * 0.4f);
            
            Vector3 position = startPoint.position + (levelDirection * distanceAlongPath) + (levelPerpendicular * offsetFromCenter);
            
            // Check for minimum spacing between items
            bool validPosition = true;
            foreach (Vector3 usedPos in usedPositions)
            {
                if (Vector3.Distance(position, usedPos) < minObstacleSpacing * 0.5f)
                {
                    validPosition = false;
                    break;
                }
            }
            
            // Skip if invalid position
            if (!validPosition)
            {
                // Try again (reduce i to retry)
                i--;
                continue;
            }
            
            // Spawn treasure
            GameObject treasure = Instantiate(treasurePrefab, position, Quaternion.identity);
            treasure.transform.parent = transform;
            
            // Set treasure value based on position (harder to reach = more points)
            TreasureBox treasureBox = treasure.GetComponent<TreasureBox>();
            if (treasureBox != null)
            {
                // Treasures on the sides are worth more
                float normalizedOffset = Mathf.Abs(offsetFromCenter) / (levelWidth * 0.4f);
                int valueIndex = normalizedOffset > 0.6f ? 1 : 0; // Higher value if far from center
                treasureBox.pointValue = treasureValues[valueIndex];
            }
            
            // Add to lists
            levelObjects.Add(treasure);
            usedPositions.Add(position);
        }
    }
    
    public void ClearLevel()
    {
        // Destroy all level objects
        foreach (GameObject obj in levelObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        
        levelObjects.Clear();
    }
    
    // Helper method to visualize level in editor
    private void OnDrawGizmos()
    {
        if (startPoint != null && endPoint != null)
        {
            // Calculate level direction and perpendicular
            Vector3 levelDirection = (endPoint.position - startPoint.position).normalized;
            levelDirection.y = 0;
            Vector3 levelPerpendicular = new Vector3(levelDirection.z, 0, -levelDirection.x);
            
            // Draw level path
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(startPoint.position, endPoint.position);
            
            // Draw boundaries
            Gizmos.color = Color.red;
            Gizmos.DrawLine(
                startPoint.position + levelPerpendicular * (levelWidth * 0.5f),
                endPoint.position + levelPerpendicular * (levelWidth * 0.5f)
            );
            Gizmos.DrawLine(
                startPoint.position - levelPerpendicular * (levelWidth * 0.5f),
                endPoint.position - levelPerpendicular * (levelWidth * 0.5f)
            );
        }
    }
}