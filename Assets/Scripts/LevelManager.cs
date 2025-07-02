using UnityEngine;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{
    [Header("Level Bounds")]
    [SerializeField] private Transform boatStartPosition;
    [SerializeField] private Transform boatObject;
    [SerializeField] private Transform finishPoint;
    [SerializeField] private float levelLength = 120f;
    
    [Header("S-Path Generation")]
    [SerializeField] private float pathAmplitude = 30f;
    [SerializeField] private float pathFrequency = 0.02f;
    [SerializeField] private int pathResolution = 20;
    [SerializeField] private float pathWidth = 50f;
    [SerializeField] private AnimationCurve pathVariationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Obstacles")]
    [SerializeField] private GameObject whalePrefab;
    [SerializeField] private GameObject octopusPrefab;
    [SerializeField] private int numWhales = 2;
    [SerializeField] private int numOctopuses = 3;
    [SerializeField] private float obstacleDistanceFromPath = 15f;
    
    [Header("Object Scales")]
    [Range(0.1f, 5.0f)]
    [SerializeField] private float whaleScale = 1.0f;
    [Range(0.1f, 5.0f)]
    [SerializeField] private float octopusScale = 1.0f;
    [Range(0.1f, 5.0f)]
    [SerializeField] private float treasureScale = 1.0f;
    
    [Header("Object Rotations")]
    [SerializeField] private Vector3 whaleRotation = Vector3.zero;
    [SerializeField] private Vector3 octopusRotation = Vector3.zero;
    [SerializeField] private Vector3 treasureRotation = Vector3.zero;
    [SerializeField] private bool randomizeRotations = true;
    [SerializeField] private float rotationVariance = 30f;
    
    [Header("Treasures")]
    [SerializeField] private GameObject treasurePrefab;
    [SerializeField] private int numTreasures = 8;
    [SerializeField] private int[] treasureValues = { 50, 100 };
    
    [Header("Spawn Settings")]
    [SerializeField] private float minSpacing = 40f;
    [SerializeField] private int maxSpawnAttempts = 15;
    [SerializeField] private bool enableMovement = true;
    [SerializeField] private float moveSpeed = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showGizmos = true;
    
    // Runtime data
    private List<GameObject> spawnedObstacles = new List<GameObject>();
    private List<GameObject> spawnedTreasures = new List<GameObject>();
    private List<Vector3> usedPositions = new List<Vector3>();
    private List<Vector3> generatedPath = new List<Vector3>();
    private int totalPossibleScore = 0;
    
    private void Start()
    {
        // Auto-find boat if not assigned
        if (boatObject == null && boatStartPosition != null)
        {
            BoatController boat = boatStartPosition.GetComponent<BoatController>();
            if (boat != null)
            {
                boatObject = boatStartPosition;
            }
        }
        
        GenerateLevel();
    }
    
    public void GenerateLevel()
    {
        ClearLevel();
        
        // Generate S-path
        generatedPath = GenerateSPath();
        
        if (generatedPath.Count == 0)
        {
            DebugLog("ERROR: Failed to generate path!");
            return;
        }
        
        DebugLog($"Generated S-path with {generatedPath.Count} points");
        
        // Spawn objects along path
        SpawnObstaclesAlongPath();
        SpawnTreasuresAlongPath();
        
        DebugLog($"Level generated - Obstacles: {spawnedObstacles.Count}, Treasures: {spawnedTreasures.Count}, Total Score: {totalPossibleScore}");
    }
    
    private List<Vector3> GenerateSPath()
    {
        List<Vector3> path = new List<Vector3>();
        
        if (boatStartPosition == null || finishPoint == null)
        {
            DebugLog("ERROR: Start or finish position not assigned!");
            return path;
        }
        
        Vector3 startPos = boatStartPosition.position;
        Vector3 endPos = finishPoint.position;
        float distance = Vector3.Distance(startPos, endPos);
        
        // Use levelLength if finish point is too close
        if (distance < levelLength * 0.5f)
        {
            endPos = startPos + boatStartPosition.forward * levelLength;
        }
        
        // Add randomization to path parameters
        float actualAmplitude = pathAmplitude * Random.Range(0.7f, 1.3f);
        float actualFrequency = pathFrequency * Random.Range(0.7f, 1.3f);
        float phaseShift = Random.Range(0f, Mathf.PI * 2f);
        
        for (int i = 0; i < pathResolution; i++)
        {
            float progress = (float)i / (pathResolution - 1);
            
            // Linear interpolation between start and end
            Vector3 basePoint = Vector3.Lerp(startPos, endPos, progress);
            
            // Add S-curve variation
            float sValue = Mathf.Sin(progress * Mathf.PI * 2f * actualFrequency + phaseShift);
            float amplitudeAtProgress = pathVariationCurve.Evaluate(progress) * actualAmplitude;
            
            // Apply perpendicular offset
            Vector3 direction = (endPos - startPos).normalized;
            Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
            
            Vector3 pathPoint = basePoint + perpendicular * sValue * amplitudeAtProgress;
            path.Add(pathPoint);
        }
        
        return path;
    }
    
    private void SpawnObstaclesAlongPath()
    {
        if (generatedPath.Count == 0) return;
        
        // Spawn whales
        SpawnObstacleType(whalePrefab, numWhales, whaleScale, whaleRotation, "whale");
        
        // Spawn octopuses
        SpawnObstacleType(octopusPrefab, numOctopuses, octopusScale, octopusRotation, "octopus");
    }
    
    private void SpawnObstacleType(GameObject prefab, int count, float scale, Vector3 baseRotation, string typeName)
    {
        if (prefab == null || count == 0) return;
        
        float pathLength = generatedPath.Count - 1;
        float segmentLength = pathLength / (count + 1);
        
        for (int i = 1; i <= count; i++)
        {
            // Find position along path
            float pathProgress = i * segmentLength;
            int pathIndex = Mathf.FloorToInt(pathProgress);
            float localProgress = pathProgress - pathIndex;
            
            // Get path point
            Vector3 pathPoint;
            if (pathIndex >= generatedPath.Count - 1)
            {
                pathPoint = generatedPath[generatedPath.Count - 1];
            }
            else
            {
                pathPoint = Vector3.Lerp(generatedPath[pathIndex], generatedPath[pathIndex + 1], localProgress);
            }
            
            // Find spawn position near path
            Vector3 spawnPos = FindValidObstaclePosition(pathPoint, pathIndex);
            
            if (spawnPos != Vector3.zero)
            {
                // Calculate rotation with variance
                Quaternion rotation = CalculateObjectRotation(baseRotation);
                
                GameObject obstacle = Instantiate(prefab, spawnPos, rotation);
                obstacle.transform.parent = transform;
                
                // Apply scale
                obstacle.transform.localScale = Vector3.one * scale;
                
                if (enableMovement)
                {
                    SetupObstacleMovement(obstacle);
                }
                
               
                spawnedObstacles.Add(obstacle);
                usedPositions.Add(spawnPos);
                
                DebugLog($"Spawned {typeName} at {spawnPos} with scale {scale} and rotation {rotation.eulerAngles}");
            }
        }
    }
    
    private Vector3 FindValidObstaclePosition(Vector3 pathPoint, int pathIndex)
    {
        // Calculate path direction
        Vector3 pathDirection = Vector3.forward;
        if (pathIndex > 0 && pathIndex < generatedPath.Count - 1)
        {
            pathDirection = (generatedPath[pathIndex + 1] - generatedPath[pathIndex - 1]).normalized;
        }
        
        Vector3 perpendicular = Vector3.Cross(pathDirection, Vector3.up).normalized;
        
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // Random side and distance
            float side = Random.Range(0f, 1f) > 0.5f ? 1f : -1f;
            float distance = obstacleDistanceFromPath + Random.Range(-10f, 15f);
            
            Vector3 testPos = pathPoint + perpendicular * side * distance;
            testPos.y = pathPoint.y; // Keep same height as path
            
            if (IsValidSpawnPosition(testPos))
            {
                return testPos;
            }
            
            // Try opposite side if first side fails
            if (attempt == maxSpawnAttempts / 2)
            {
                side *= -1f;
            }
        }
        
        return Vector3.zero;
    }
    
    private void SpawnTreasuresAlongPath()
    {
        if (generatedPath.Count == 0 || treasurePrefab == null) return;
        
        float pathLength = generatedPath.Count - 1;
        float segmentLength = pathLength / (numTreasures + 1);
        
        for (int i = 1; i <= numTreasures; i++)
        {
            // Offset treasures slightly from obstacles
            float pathProgress = i * segmentLength * 0.9f;
            int pathIndex = Mathf.FloorToInt(pathProgress);
            float localProgress = pathProgress - pathIndex;
            
            // Get path point
            Vector3 pathPoint;
            if (pathIndex >= generatedPath.Count - 1)
            {
                pathPoint = generatedPath[generatedPath.Count - 1];
            }
            else
            {
                pathPoint = Vector3.Lerp(generatedPath[pathIndex], generatedPath[pathIndex + 1], localProgress);
            }
            
            // Find spawn position closer to path center
            Vector3 spawnPos = FindValidTreasurePosition(pathPoint, pathIndex);
            
            if (spawnPos != Vector3.zero)
            {
                // Calculate rotation with variance
                Quaternion rotation = CalculateObjectRotation(treasureRotation);
                
                GameObject treasure = Instantiate(treasurePrefab, spawnPos, rotation);
                treasure.transform.parent = transform;
                
                // Apply scale
                treasure.transform.localScale = Vector3.one * treasureScale;
                
                // Set treasure value
                TreasureBox treasureBox = treasure.GetComponent<TreasureBox>();
                if (treasureBox != null)
                {
                    int pointValue = treasureValues[Random.Range(0, treasureValues.Length)];
                    treasureBox.pointValue = pointValue;
                    totalPossibleScore += pointValue;
                }
                
                spawnedTreasures.Add(treasure);
                usedPositions.Add(spawnPos);
                
                DebugLog($"Spawned treasure at {spawnPos} with scale {treasureScale} and rotation {rotation.eulerAngles}");
            }
        }
    }
    
    private Vector3 FindValidTreasurePosition(Vector3 pathPoint, int pathIndex)
    {
        // Calculate path direction
        Vector3 pathDirection = Vector3.forward;
        if (pathIndex > 0 && pathIndex < generatedPath.Count - 1)
        {
            pathDirection = (generatedPath[pathIndex + 1] - generatedPath[pathIndex - 1]).normalized;
        }
        
        Vector3 perpendicular = Vector3.Cross(pathDirection, Vector3.up).normalized;
        
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // Random position within path width
            float offsetDistance = Random.Range(-pathWidth * 0.7f, pathWidth * 0.7f);
            Vector3 testPos = pathPoint + perpendicular * offsetDistance;
            testPos.y = pathPoint.y + 0.2f; // Slightly above water
            
            if (IsValidSpawnPosition(testPos))
            {
                return testPos;
            }
        }
        
        return Vector3.zero;
    }
    
    private bool IsValidSpawnPosition(Vector3 position)
    {
        // Check against finish point
        if (finishPoint != null && Vector3.Distance(position, finishPoint.position) < 15f)
        {
            return false;
        }
        
        // Check against boat start
        if (boatStartPosition != null && Vector3.Distance(position, boatStartPosition.position) < minSpacing * 0.5f)
        {
            return false;
        }
        
        // Check against all used positions
        foreach (Vector3 usedPos in usedPositions)
        {
            if (Vector3.Distance(position, usedPos) < minSpacing)
            {
                return false;
            }
        }
        
        return true;
    }
    
    private Quaternion CalculateObjectRotation(Vector3 baseRotation)
    {
        Vector3 finalRotation = baseRotation;
        
        if (randomizeRotations)
        {
            // Add random variance to each axis
            finalRotation.x += Random.Range(-rotationVariance, rotationVariance);
            finalRotation.y += Random.Range(-rotationVariance, rotationVariance);
            finalRotation.z += Random.Range(-rotationVariance, rotationVariance);
        }
        
        return Quaternion.Euler(finalRotation);
    }
    
    private void SetupObstacleMovement(GameObject obstacle)
    {
        ObstacleBase obstacleBase = obstacle.GetComponent<ObstacleBase>();
        if (obstacleBase != null)
        {
            obstacleBase.SetMovementEnabled(enableMovement);
            obstacleBase.SetMoveSpeed(moveSpeed * Random.Range(0.5f, 1.5f));
            
            Vector3 randomDir = new Vector3(
                Random.Range(-0.5f, 0.5f),
                0f, 
                Random.Range(-0.5f, 0.5f)
            ).normalized;
            
            obstacleBase.SetMoveDirection(randomDir);
        }
    }
      
    // Runtime adjustment methods
    public void UpdateWhaleScale(float newScale)
    {
        whaleScale = Mathf.Clamp(newScale, 0.1f, 5.0f);
        UpdateObjectScales(whalePrefab, whaleScale);
    }
    
    public void UpdateOctopusScale(float newScale)
    {
        octopusScale = Mathf.Clamp(newScale, 0.1f, 5.0f);
        UpdateObjectScales(octopusPrefab, octopusScale);
    }
    
    public void UpdateTreasureScale(float newScale)
    {
        treasureScale = Mathf.Clamp(newScale, 0.1f, 5.0f);
        UpdateObjectScales(treasurePrefab, treasureScale);
    }
    
    private void UpdateObjectScales(GameObject prefab, float scale)
    {
        if (prefab == null) return;
        
        List<GameObject> objectsToUpdate = new List<GameObject>();
        
        if (prefab == whalePrefab)
        {
            objectsToUpdate.AddRange(spawnedObstacles.FindAll(obj => 
                obj.GetComponent<WhaleObstacle>() != null));
        }
        else if (prefab == octopusPrefab)
        {
            objectsToUpdate.AddRange(spawnedObstacles.FindAll(obj => 
                obj.GetComponent<OctopusObstacle>() != null));
        }
        else if (prefab == treasurePrefab)
        {
            objectsToUpdate.AddRange(spawnedTreasures);
        }
        
        foreach (GameObject obj in objectsToUpdate)
        {
            if (obj != null)
            {
                obj.transform.localScale = Vector3.one * scale;
            }
        }
    }
    
    public void ClearLevel()
    {
        foreach (GameObject obstacle in spawnedObstacles)
        {
            if (obstacle != null) 
            {
                if (Application.isPlaying)
                    Destroy(obstacle);
                else
                    DestroyImmediate(obstacle);
            }
        }
        
        foreach (GameObject treasure in spawnedTreasures)
        {
            if (treasure != null) 
            {
                if (Application.isPlaying)
                    Destroy(treasure);
                else
                    DestroyImmediate(treasure);
            }
        }
        
        spawnedObstacles.Clear();
        spawnedTreasures.Clear();
        usedPositions.Clear();
        generatedPath.Clear();
        totalPossibleScore = 0;
        
        DebugLog("Level cleared");
    }
    
    [ContextMenu("Generate Level (Manual)")]
    public void ManualGenerateLevel()
    {
        GenerateLevel();
    }
    
    [ContextMenu("Clear Level")]
    public void ManualClearLevel()
    {
        ClearLevel();
    }
    
    // Public getters
    public List<Vector3> GetGeneratedPath() => generatedPath;
    public List<GameObject> GetSpawnedObstacles() => spawnedObstacles;
    public List<GameObject> GetSpawnedTreasures() => spawnedTreasures;
    public int GetTotalPossibleScore() => totalPossibleScore;
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[LevelManager] {message}");
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        // Draw generated S-path
        if (generatedPath.Count > 1)
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i < generatedPath.Count - 1; i++)
            {
                Gizmos.DrawLine(generatedPath[i], generatedPath[i + 1]);
            }
            
            // Draw path width boundaries
            Gizmos.color = Color.cyan;
            for (int i = 0; i < generatedPath.Count - 1; i++)
            {
                Vector3 direction = (generatedPath[i + 1] - generatedPath[i]).normalized;
                Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
                
                Vector3 leftBound = generatedPath[i] + perpendicular * pathWidth;
                Vector3 rightBound = generatedPath[i] - perpendicular * pathWidth;
                
                Gizmos.DrawLine(leftBound, rightBound);
            }
        }
        
        // Draw spawn positions
        foreach (Vector3 pos in usedPositions)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(pos, 2f);
        }
        
        // Draw start and finish
        if (boatStartPosition != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(boatStartPosition.position, 3f);
        }
        
        if (finishPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(finishPoint.position, 3f);
        }
    }
}