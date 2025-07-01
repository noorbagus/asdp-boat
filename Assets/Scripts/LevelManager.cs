using UnityEngine;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{
    [Header("Level Bounds")]
    [SerializeField] private Transform boatStartPosition;
    [SerializeField] private Transform finishPoint;
    [SerializeField] private float channelWidth = 10f;
    [SerializeField] private float levelLength = 120f;
    
    [Header("Fixed Spawn Area")]
    [SerializeField] private Vector3 spawnAreaCenter = Vector3.zero;
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(100f, 1f, 100f);
    [SerializeField] private bool useFixedSpawnArea = true;
    
    [Header("Manual References")]
    [SerializeField] private Suimono.Core.SuimonoModule manualSuimonoModule;
    [SerializeField] private Suimono.Core.SuimonoObject manualWaterSurface;
    
    [Header("Water Integration")]
    [SerializeField] private bool autoDetectWaterBounds = false;
    [SerializeField] private float finishIslandRadius = 15f;
    [SerializeField] private Vector2 finishOffset = new Vector2(0.8f, 0f);
    
    [Header("Obstacles")]
    [SerializeField] private GameObject[] obstaclePrefabs;
    [SerializeField] private int minObstacles = 3;
    [SerializeField] private int maxObstacles = 6;
    [SerializeField] private float minObstacleSpacing = 15f;
    
    [Header("Whale Settings")]
    [SerializeField] private GameObject[] whalePrefabs;
    [SerializeField] private int minWhales = 1;
    [SerializeField] private int maxWhales = 3;
    [SerializeField] private float whaleSpawnMinDistance = 30f;
    [SerializeField] private float whaleSpawnMaxDistance = 80f;
    [SerializeField] private float whaleLateralSpread = 20f;
    [SerializeField] private float whaleDepth = -1.5f;
    
    [Header("Octopus Settings")]
    [SerializeField] private GameObject[] octopusPrefabs;
    [SerializeField] private int minOctopuses = 1;
    [SerializeField] private int maxOctopuses = 4;
    [SerializeField] private float octopusDepth = -0.5f;
    
    [Header("Treasures")]
    [SerializeField] private GameObject treasurePrefab;
    [SerializeField] private int minTreasures = 5;
    [SerializeField] private int maxTreasures = 8;
    [SerializeField] private int[] treasureValues = { 50, 100 };
    [SerializeField] private float treasureHeight = 0.2f;
    
    [Header("Movement Settings")]
    [SerializeField] private float patrolDistance = 8f;
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private bool enableMovement = true;
    
    [Header("Spawn Settings")]
    [SerializeField] private bool useRandomDistribution = true;
    [SerializeField] private float minSpacing = 8f;
    [SerializeField] private float spawnMargin = 10f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showGizmos = true;
    
    // Runtime data
    private List<GameObject> spawnedObstacles = new List<GameObject>();
    private List<GameObject> spawnedTreasures = new List<GameObject>();
    private List<Vector3> usedPositions = new List<Vector3>();
    private Bounds waterBounds;
    private Suimono.Core.SuimonoModule suimonoModule;
    private int totalPossibleScore = 0;
    
    private void Awake()
    {
        InitializeWaterBounds();
        
        if (autoDetectWaterBounds)
        {
            AutoPositionFinishPoint();
        }
    }
    
    private void Start()
    {
        GenerateLevel();
    }
    
    private void InitializeWaterBounds()
    {
        // Try to find Suimono module
        if (manualSuimonoModule != null)
        {
            suimonoModule = manualSuimonoModule;
            DebugLog("Using manual Suimono module reference");
        }
        else
        {
            suimonoModule = FindObjectOfType<Suimono.Core.SuimonoModule>();
            if (suimonoModule == null)
            {
                GameObject suimonoObj = GameObject.Find("SUIMONO_Module");
                if (suimonoObj != null)
                {
                    suimonoModule = suimonoObj.GetComponent<Suimono.Core.SuimonoModule>();
                }
            }
            DebugLog($"Auto-found Suimono module: {suimonoModule != null}");
        }
        
        // Auto-detect boat if not assigned
        if (boatStartPosition == null)
        {
            BoatController boat = FindObjectOfType<BoatController>();
            if (boat != null)
            {
                boatStartPosition = boat.transform;
                DebugLog($"Auto-detected boat at: {boatStartPosition.position}");
            }
            else
            {
                DebugLog("WARNING: No boat start position found!");
            }
        }
        
        // Always use fixed bounds for consistent spawning
        SetFixedWaterBounds();
    }
    
    private void SetFixedWaterBounds()
    {
        waterBounds = new Bounds(spawnAreaCenter, spawnAreaSize);
        DebugLog($"Fixed water bounds set: Center={spawnAreaCenter}, Size={spawnAreaSize}");
    }
    
    private void AutoPositionFinishPoint()
    {
        if (finishPoint == null) return;
        
        Vector3 newFinishPos = new Vector3(
            waterBounds.min.x + (waterBounds.size.x * finishOffset.x),
            0f,
            waterBounds.min.z + (waterBounds.size.z * (0.5f + finishOffset.y))
        );
        
        finishPoint.position = newFinishPos;
        DebugLog($"Auto-positioned finish point at: {newFinishPos}");
    }
    
    public void GenerateLevel()
    {
        ClearLevel();
        
        DebugLog($"Starting level generation with bounds: {waterBounds}");
        
        SpawnObstacles();
        SpawnWhales();
        SpawnOctopuses();
        SpawnTreasures();
        
        DebugLog($"Level generated - Obstacles: {spawnedObstacles.Count}, Treasures: {spawnedTreasures.Count}, Total Score: {totalPossibleScore}");
    }
    
    private void SpawnObstacles()
    {
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0) 
        {
            DebugLog("No obstacle prefabs assigned!");
            return;
        }
        
        int obstacleCount = Random.Range(minObstacles, maxObstacles + 1);
        DebugLog($"Attempting to spawn {obstacleCount} obstacles");
        
        int successfulSpawns = 0;
        
        for (int i = 0; i < obstacleCount; i++)
        {
            GameObject obstaclePrefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
            Vector3 spawnPos = GetPredictiveSpawnPosition(obstaclePrefab, 0.0f); // Regular obstacles at water surface
            
            if (spawnPos == Vector3.zero) 
            {
                DebugLog($"Failed to find spawn position for obstacle {i + 1}");
                continue;
            }
            
            try
            {
                GameObject obstacle = Instantiate(obstaclePrefab, spawnPos, GetRandomRotation());
                obstacle.transform.parent = transform;
                
                if (enableMovement)
                {
                    SetupObstacleMovement(obstacle);
                }
                
                spawnedObstacles.Add(obstacle);
                usedPositions.Add(spawnPos);
                successfulSpawns++;
                
                DebugLog($"Successfully spawned obstacle {obstacle.name} at {spawnPos}");
            }
            catch (System.Exception e)
            {
                DebugLog($"Error spawning obstacle: {e.Message}");
            }
        }
        
        DebugLog($"Total obstacles spawned: {successfulSpawns}/{obstacleCount}");
    }

    private void SpawnWhales()
    {
        if (whalePrefabs == null || whalePrefabs.Length == 0) 
        {
            DebugLog("No whale prefabs assigned!");
            return;
        }
        
        int whaleCount = Random.Range(minWhales, maxWhales + 1);
        DebugLog($"Attempting to spawn {whaleCount} whales");
        
        int successfulSpawns = 0;
        
        for (int i = 0; i < whaleCount; i++)
        {
            GameObject whalePrefab = whalePrefabs[Random.Range(0, whalePrefabs.Length)];
            Vector3 spawnPos = GetWhaleSpawnPosition();
            
            if (spawnPos == Vector3.zero) 
            {
                DebugLog($"Failed to find spawn position for whale {i + 1}");
                continue;
            }
            
            try
            {
                // Set the correct depth for whales
                spawnPos.y = whaleDepth;
                
                GameObject whale = Instantiate(whalePrefab, spawnPos, GetRandomRotation());
                whale.transform.parent = transform;
                
                // Whales have their own AI, but still initialize movement
                if (enableMovement)
                {
                    SetupObstacleMovement(whale);
                }
                
                spawnedObstacles.Add(whale);
                usedPositions.Add(spawnPos);
                successfulSpawns++;
                
                DebugLog($"Successfully spawned whale at {spawnPos}");
            }
            catch (System.Exception e)
            {
                DebugLog($"Error spawning whale: {e.Message}");
            }
        }
        
        DebugLog($"Total whales spawned: {successfulSpawns}/{whaleCount}");
    }

    private void SpawnOctopuses()
    {
        if (octopusPrefabs == null || octopusPrefabs.Length == 0) 
        {
            DebugLog("No octopus prefabs assigned!");
            return;
        }
        
        int octopusCount = Random.Range(minOctopuses, maxOctopuses + 1);
        DebugLog($"Attempting to spawn {octopusCount} octopuses");
        
        int successfulSpawns = 0;
        
        for (int i = 0; i < octopusCount; i++)
        {
            GameObject octopusPrefab = octopusPrefabs[Random.Range(0, octopusPrefabs.Length)];
            Vector3 spawnPos = GetPredictiveSpawnPosition(octopusPrefab, octopusDepth);
            
            if (spawnPos == Vector3.zero) 
            {
                DebugLog($"Failed to find spawn position for octopus {i + 1}");
                continue;
            }
            
            try
            {
                GameObject octopus = Instantiate(octopusPrefab, spawnPos, GetRandomRotation());
                octopus.transform.parent = transform;
                
                if (enableMovement)
                {
                    SetupObstacleMovement(octopus);
                }
                
                spawnedObstacles.Add(octopus);
                usedPositions.Add(spawnPos);
                successfulSpawns++;
                
                DebugLog($"Successfully spawned octopus at {spawnPos}");
            }
            catch (System.Exception e)
            {
                DebugLog($"Error spawning octopus: {e.Message}");
            }
        }
        
        DebugLog($"Total octopuses spawned: {successfulSpawns}/{octopusCount}");
    }
    
    private Vector3 GetPredictiveSpawnPosition(GameObject prefab, float depth)
    {
        if (boatStartPosition == null) return Vector3.zero;
        
        bool isWhale = prefab.GetComponent<WhaleObstacle>() != null;
        int maxAttempts = 100;
        float minSpacingToUse = isWhale ? minObstacleSpacing : minSpacing;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 spawnPos;
            
            if (isWhale)
            {
                spawnPos = GetWhaleSpawnPosition();
            }
            else
            {
                // Regular obstacles - random within bounds
                spawnPos = new Vector3(
                    Random.Range(waterBounds.min.x + spawnMargin, waterBounds.max.x - spawnMargin),
                    depth, // Use specified depth parameter
                    Random.Range(waterBounds.min.z + spawnMargin, waterBounds.max.z - spawnMargin)
                );
            }
            
            if (IsValidSpawnPosition(spawnPos, minSpacingToUse))
            {
                DebugLog($"Found valid spawn position at attempt {attempt + 1}: {spawnPos}");
                return spawnPos;
            }
        }
        
        DebugLog($"Warning: Could not find valid spawn position after {maxAttempts} attempts");
        return Vector3.zero;
    }

    private Vector3 GetWhaleSpawnPosition()
    {
        if (boatStartPosition == null) return Vector3.zero;
        
        // Spawn whales ahead of boat path
        Vector3 boatForward = boatStartPosition.forward;
        float distanceAhead = Random.Range(whaleSpawnMinDistance, whaleSpawnMaxDistance);
        float lateralOffset = Random.Range(-whaleLateralSpread, whaleLateralSpread);
        
        Vector3 spawnPos = boatStartPosition.position + 
                          boatForward * distanceAhead + 
                          boatStartPosition.right * lateralOffset;
        
        // Depth is applied later
        spawnPos.y = 0f;
        
        return spawnPos;
    }
    
    [System.Serializable]
    public class TreasureType
    {
        public string name = "Standard";
        public int pointValue = 100;
        public Color glowColor = Color.yellow;
        [Range(0.5f, 2.0f)]
        public float scale = 1.0f;
        [Range(0.0f, 1.0f)]
        public float spawnProbability = 1.0f;
    }

    [Header("Treasure Types")]
    [SerializeField] private TreasureType[] treasureTypes = new TreasureType[]
    {
        new TreasureType { name = "Common", pointValue = 50, glowColor = Color.yellow, scale = 0.8f, spawnProbability = 0.7f },
        new TreasureType { name = "Rare", pointValue = 100, glowColor = new Color(1f, 0.6f, 0f), scale = 1.0f, spawnProbability = 0.25f },
        new TreasureType { name = "Legendary", pointValue = 200, glowColor = new Color(1f, 0f, 0.6f), scale = 1.2f, spawnProbability = 0.05f }
    };
    
    private void SpawnTreasures()
    {
        if (treasurePrefab == null) 
        {
            DebugLog("ERROR: No treasure prefab assigned!");
            return;
        }
        
        if (treasureTypes == null || treasureTypes.Length == 0)
        {
            DebugLog("WARNING: No treasure types defined! Using default values.");
            treasureTypes = new TreasureType[] { new TreasureType() };
        }
        
        int treasureCount = Random.Range(minTreasures, maxTreasures + 1);
        DebugLog($"Attempting to spawn {treasureCount} treasures");
        
        int successfulSpawns = 0;
        
        for (int i = 0; i < treasureCount; i++)
        {
            Vector3 spawnPos = GetTreasureSpawnPosition();
            if (spawnPos == Vector3.zero) 
            {
                DebugLog($"Failed to find spawn position for treasure {i + 1}");
                continue;
            }
            
            try
            {
                // Select treasure type based on probability
                TreasureType selectedType = SelectTreasureType();
                
                GameObject treasure = Instantiate(treasurePrefab, spawnPos, Quaternion.identity);
                treasure.transform.parent = transform;
                
                // Apply scale based on treasure type
                treasure.transform.localScale = Vector3.one * selectedType.scale;
                
                TreasureBox treasureBox = treasure.GetComponent<TreasureBox>();
                if (treasureBox != null)
                {
                    // Set point value from treasure type
                    treasureBox.pointValue = selectedType.pointValue;
                    totalPossibleScore += selectedType.pointValue;
                    
                    // Apply visual customization if possible
                    ParticleSystem collectEffect = treasureBox.GetComponent<ParticleSystem>();
                    if (collectEffect != null)
                    {
                        var mainModule = collectEffect.main;
                        mainModule.startColor = selectedType.glowColor;
                    }
                    
                    // Try to find and color renderer materials if any
                    Renderer treasureRenderer = treasureBox.GetComponentInChildren<Renderer>();
                    if (treasureRenderer != null)
                    {
                        // Find emission material if available
                        foreach (Material mat in treasureRenderer.materials)
                        {
                            if (mat.HasProperty("_EmissionColor"))
                            {
                                mat.SetColor("_EmissionColor", selectedType.glowColor * 0.5f);
                            }
                        }
                    }
                    
                    DebugLog($"Treasure spawned: Type={selectedType.name}, Value={selectedType.pointValue}, Scale={selectedType.scale}");
                }
                
                spawnedTreasures.Add(treasure);
                usedPositions.Add(spawnPos);
                successfulSpawns++;
            }
            catch (System.Exception e)
            {
                DebugLog($"Error spawning treasure: {e.Message}");
            }
        }
        
        DebugLog($"Total treasures spawned: {successfulSpawns}/{treasureCount}");
    }
    
    private TreasureType SelectTreasureType()
    {
        // Calculate total probability
        float totalProbability = 0f;
        foreach (TreasureType type in treasureTypes)
        {
            totalProbability += type.spawnProbability;
        }
        
        // If total probability is 0, return first type
        if (totalProbability <= 0f)
        {
            return treasureTypes[0];
        }
        
        // Select random value based on probabilities
        float randomValue = Random.Range(0f, totalProbability);
        float currentProbability = 0f;
        
        // Find which treasure type was selected
        foreach (TreasureType type in treasureTypes)
        {
            currentProbability += type.spawnProbability;
            if (randomValue <= currentProbability)
            {
                return type;
            }
        }
        
        // Fallback to first type
        return treasureTypes[0];
    }
    
    private Vector3 GetTreasureSpawnPosition()
    {
        int maxAttempts = 100;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 spawnPos = new Vector3(
                Random.Range(waterBounds.min.x + spawnMargin, waterBounds.max.x - spawnMargin),
                treasureHeight, // Use the specified treasure height
                Random.Range(waterBounds.min.z + spawnMargin, waterBounds.max.z - spawnMargin)
            );
            
            if (IsValidSpawnPosition(spawnPos, minSpacing))
            {
                return spawnPos;
            }
        }
        
        return Vector3.zero;
    }
    
    private bool IsValidSpawnPosition(Vector3 position, float minDistance)
    {
        // Check against finish point
        if (finishPoint != null && Vector3.Distance(position, finishPoint.position) < finishIslandRadius)
        {
            return false;
        }
        
        // Check against boat start position (reduced distance check)
        if (boatStartPosition != null && Vector3.Distance(position, boatStartPosition.position) < minDistance * 0.5f)
        {
            return false;
        }
        
        // Check against all used positions
        foreach (Vector3 usedPos in usedPositions)
        {
            if (Vector3.Distance(position, usedPos) < minDistance)
            {
                return false;
            }
        }
        
        return true;
    }
    
    private Quaternion GetRandomRotation()
    {
        return Quaternion.Euler(0, Random.Range(0f, 360f), 0);
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
            
            // Set spawn area boundaries for obstacle movement constraints
            obstacleBase.SetBoundaryConstraints(
                spawnAreaCenter,
                spawnAreaSize,
                spawnAreaSize.magnitude * 0.5f
            );
            
            DebugLog($"Movement setup for {obstacle.name} - Speed: {obstacleBase.GetMoveSpeed()}, Dir: {randomDir}");
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
        totalPossibleScore = 0;
        
        DebugLog("Level cleared");
    }
    
    [ContextMenu("Generate Level (Manual)")]
    public void ManualGenerateLevel()
    {
        if (!Application.isPlaying)
        {
            InitializeWaterBounds();
            if (autoDetectWaterBounds)
            {
                AutoPositionFinishPoint();
            }
        }
        GenerateLevel();
    }
    
    [ContextMenu("Clear Level")]
    public void ManualClearLevel()
    {
        ClearLevel();
    }
    
    // Public getters
    public List<GameObject> GetSpawnedObstacles() => spawnedObstacles;
    public List<GameObject> GetSpawnedTreasures() => spawnedTreasures;
    public int GetTotalPossibleScore() => totalPossibleScore;
    public Bounds GetWaterBounds() => waterBounds;
    
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
        
        // Draw fixed spawn area
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
        
        // Draw whale spawn zone
        if (boatStartPosition != null)
        {
            Gizmos.color = Color.red;
            Vector3 boatForward = boatStartPosition.forward;
            Vector3 whaleZoneStart = boatStartPosition.position + boatForward * whaleSpawnMinDistance;
            Vector3 whaleZoneEnd = boatStartPosition.position + boatForward * whaleSpawnMaxDistance;
            
            // Draw whale spawn corridor
            Vector3 left = boatStartPosition.right * -whaleLateralSpread;
            Vector3 right = boatStartPosition.right * whaleLateralSpread;
            
            Gizmos.DrawLine(whaleZoneStart + left, whaleZoneStart + right);
            Gizmos.DrawLine(whaleZoneEnd + left, whaleZoneEnd + right);
            Gizmos.DrawLine(whaleZoneStart + left, whaleZoneEnd + left);
            Gizmos.DrawLine(whaleZoneStart + right, whaleZoneEnd + right);
            
            // Draw underwater indicator
            Vector3 whaleDepthPos = whaleZoneStart + boatForward * whaleSpawnMaxDistance * 0.5f;
            whaleDepthPos.y = whaleDepth;
            Gizmos.DrawWireSphere(whaleDepthPos, 2f);
            Gizmos.DrawLine(whaleDepthPos, new Vector3(whaleDepthPos.x, 0, whaleDepthPos.z));
        }
        
        // Draw octopus depth level
        if (spawnedObstacles.Count > 0)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // Orange
            Vector3 octopusDepthPos = spawnAreaCenter;
            octopusDepthPos.y = octopusDepth;
            Gizmos.DrawWireSphere(octopusDepthPos, 2f);
            Gizmos.DrawLine(octopusDepthPos, new Vector3(octopusDepthPos.x, 0, octopusDepthPos.z));
        }
        
        // Draw treasure height level
        if (spawnedTreasures.Count > 0)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f); // Yellow
            Vector3 treasureHeightPos = spawnAreaCenter;
            treasureHeightPos.y = treasureHeight;
            treasureHeightPos.x += 5f; // Offset for clarity
            Gizmos.DrawWireSphere(treasureHeightPos, 1f);
            Gizmos.DrawLine(treasureHeightPos, new Vector3(treasureHeightPos.x, 0, treasureHeightPos.z));
        }
        
        // Draw spawn positions
        Gizmos.color = Color.yellow;
        foreach (Vector3 pos in usedPositions)
        {
            Gizmos.DrawWireSphere(pos, 1f);
        }
        
        // Draw finish island area
        if (finishPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(finishPoint.position, finishIslandRadius);
        }
        
        // Draw start area
        if (boatStartPosition != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(boatStartPosition.position, 2f);
        }
    }
}