using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// ScriptableObject defining a route as a node graph with weighted branching,
/// randomized wait times, and per-node micro-wander settings.
/// </summary>
[CreateAssetMenu(fileName = "Route_", menuName = "Chainbound/NPC/Routes/Route")]
public class NPCRouteAsset : ScriptableObject
{
    [Header("Route Nodes")]
    [Tooltip("List of route nodes (replaces legacy waypointIds)")]
    [SerializeField] private List<RouteNode> nodes = new List<RouteNode>();
    
    [Header("Route-Level Defaults")]
    [Tooltip("Default wait time range at each node (min, max) in seconds")]
    [SerializeField] private Vector2 defaultWaitTimeRange = new Vector2(2f, 4f);
    
    [Tooltip("If true, route loops back to first node after reaching last")]
    [SerializeField] private bool loopRoute = true;
    
    [Tooltip("If true, next node is chosen randomly (unless node specifies weighted)")]
    [SerializeField] private bool randomizeNextNode = false;
    
    [Tooltip("If randomizing, prevent returning to the immediately previous node")]
    [SerializeField] private bool preventImmediateBacktrack = true;
    
    [Tooltip("Default micro-wander settings for nodes without overrides")]
    [SerializeField] private MicroWanderSettings defaultMicroWander = new MicroWanderSettings();
    
    [Tooltip("Movement speed multiplier for this route (1.0 = normal speed)")]
    [SerializeField] private float speedMultiplier = 1f;
    
    // ============================================
    // LEGACY FIELDS (for backward compatibility)
    // ============================================
    [Header("Legacy (Auto-Migrated)")]
    [FormerlySerializedAs("waypointIds")]
    [SerializeField] private List<string> legacyWaypointIds = new List<string>();
    
    [FormerlySerializedAs("waitTimeAtWaypoint")]
    [SerializeField] private float legacyWaitTime = 2f;
    
    // Properties
    public int NodeCount => nodes != null ? nodes.Count : 0;
    public bool LoopRoute => loopRoute;
    public bool RandomizeNextNode => randomizeNextNode;
    public bool PreventImmediateBacktrack => preventImmediateBacktrack;
    public Vector2 DefaultWaitTimeRange => defaultWaitTimeRange;
    public MicroWanderSettings DefaultMicroWander => defaultMicroWander;
    public float SpeedMultiplier => speedMultiplier;
    
    // Legacy compatibility property
    public int WaypointCount => NodeCount;
    
    private void OnEnable()
    {
        MigrateLegacyData();
    }
    
    private void OnValidate()
    {
        MigrateLegacyData();
        AssignMissingNodeIds();
    }
    
    /// <summary>
    /// Migrate legacy waypointIds list to new node format.
    /// </summary>
    private void MigrateLegacyData()
    {
        if (legacyWaypointIds != null && legacyWaypointIds.Count > 0 && (nodes == null || nodes.Count == 0))
        {
            nodes = new List<RouteNode>();
            
            for (int i = 0; i < legacyWaypointIds.Count; i++)
            {
                string waypointId = legacyWaypointIds[i];
                if (string.IsNullOrEmpty(waypointId)) continue;
                
                nodes.Add(new RouteNode
                {
                    nodeId = $"node_{i}",
                    waypointId = waypointId,
                    waitTimeRange = Vector2.zero, // Use route defaults
                    useWeightedNext = false,
                    weightedNext = new List<WeightedNext>(),
                    microWanderOverride = null
                });
            }
            
            // Set default wait time from legacy value
            defaultWaitTimeRange = new Vector2(legacyWaitTime, legacyWaitTime);
            
            Debug.Log($"[NPCRouteAsset] Migrated {legacyWaypointIds.Count} legacy waypoints to nodes in '{name}'");
            
            // Clear legacy data after migration
            legacyWaypointIds.Clear();
        }
    }
    
    /// <summary>
    /// Auto-assign missing node IDs.
    /// </summary>
    private void AssignMissingNodeIds()
    {
        if (nodes == null) return;
        
        for (int i = 0; i < nodes.Count; i++)
        {
            if (string.IsNullOrEmpty(nodes[i].nodeId))
            {
                nodes[i].nodeId = $"node_{i}";
            }
        }
    }
    
    // ============================================
    // NODE ACCESS HELPERS
    // ============================================
    
    /// <summary>
    /// Get node by sequential index (wraps if looping).
    /// </summary>
    public RouteNode GetNodeByIndex(int index)
    {
        if (nodes == null || nodes.Count == 0)
            return null;
        
        if (loopRoute)
        {
            index = index % nodes.Count;
            if (index < 0) index += nodes.Count;
        }
        else
        {
            if (index < 0 || index >= nodes.Count)
                return null;
        }
        
        return nodes[index];
    }
    
    /// <summary>
    /// Get node by its nodeId.
    /// </summary>
    public RouteNode GetNodeByNodeId(string nodeId)
    {
        if (nodes == null || string.IsNullOrEmpty(nodeId))
            return null;
        
        foreach (var node in nodes)
        {
            if (node.nodeId == nodeId)
                return node;
        }
        
        return null;
    }
    
    /// <summary>
    /// Get index of a node by its nodeId.
    /// </summary>
    public int GetNodeIndex(string nodeId)
    {
        if (nodes == null || string.IsNullOrEmpty(nodeId))
            return -1;
        
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].nodeId == nodeId)
                return i;
        }
        
        return -1;
    }
    
    /// <summary>
    /// Choose the next node based on route rules.
    /// </summary>
    /// <param name="current">Current node</param>
    /// <param name="currentIndex">Current node index (for sequential fallback)</param>
    /// <param name="previousNodeId">Previous node ID (for backtrack prevention)</param>
    /// <returns>Next node and its index, or (null, -1) if route ended</returns>
    public (RouteNode node, int index) ChooseNextNode(RouteNode current, int currentIndex, string previousNodeId)
    {
        if (nodes == null || nodes.Count == 0)
            return (null, -1);
        
        // 1. Check if current node has weighted next specified
        if (current != null && current.useWeightedNext && current.weightedNext != null && current.weightedNext.Count > 0)
        {
            var chosen = ChooseWeightedNext(current.weightedNext, previousNodeId);
            if (chosen != null)
            {
                int idx = GetNodeIndex(chosen.nodeId);
                return (chosen, idx);
            }
        }
        
        // 2. Route-level random selection
        if (randomizeNextNode && nodes.Count > 1)
        {
            var chosen = ChooseRandomNext(currentIndex, previousNodeId);
            if (chosen.node != null)
                return chosen;
        }
        
        // 3. Sequential fallback
        int nextIndex = currentIndex + 1;
        
        if (nextIndex >= nodes.Count)
        {
            if (loopRoute)
            {
                nextIndex = 0;
            }
            else
            {
                return (null, -1); // Route ended
            }
        }
        
        return (nodes[nextIndex], nextIndex);
    }
    
    /// <summary>
    /// Choose from weighted next options.
    /// </summary>
    private RouteNode ChooseWeightedNext(List<WeightedNext> options, string previousNodeId)
    {
        if (options == null || options.Count == 0)
            return null;
        
        // Build valid options (exclude previous if backtrack prevention enabled)
        List<WeightedNext> validOptions = new List<WeightedNext>();
        float totalWeight = 0f;
        
        foreach (var opt in options)
        {
            if (opt.weight <= 0f) continue;
            if (preventImmediateBacktrack && opt.nodeId == previousNodeId) continue;
            
            validOptions.Add(opt);
            totalWeight += opt.weight;
        }
        
        if (validOptions.Count == 0 || totalWeight <= 0f)
        {
            // Fallback: allow backtrack if no other options
            validOptions = options.FindAll(o => o.weight > 0f);
            totalWeight = 0f;
            foreach (var opt in validOptions)
                totalWeight += opt.weight;
        }
        
        if (validOptions.Count == 0 || totalWeight <= 0f)
            return null;
        
        // Weighted random selection
        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;
        
        foreach (var opt in validOptions)
        {
            cumulative += opt.weight;
            if (roll <= cumulative)
            {
                return GetNodeByNodeId(opt.nodeId);
            }
        }
        
        // Fallback to last valid option
        return GetNodeByNodeId(validOptions[validOptions.Count - 1].nodeId);
    }
    
    /// <summary>
    /// Choose random next node (excluding current, optionally excluding previous).
    /// </summary>
    private (RouteNode node, int index) ChooseRandomNext(int currentIndex, string previousNodeId)
    {
        if (nodes == null || nodes.Count <= 1)
            return (null, -1);
        
        // Build valid indices
        List<int> validIndices = new List<int>();
        
        for (int i = 0; i < nodes.Count; i++)
        {
            if (i == currentIndex) continue;
            if (preventImmediateBacktrack && nodes[i].nodeId == previousNodeId) continue;
            validIndices.Add(i);
        }
        
        if (validIndices.Count == 0)
        {
            // Fallback: allow any except current
            for (int i = 0; i < nodes.Count; i++)
            {
                if (i != currentIndex)
                    validIndices.Add(i);
            }
        }
        
        if (validIndices.Count == 0)
            return (null, -1);
        
        int chosenIndex = validIndices[UnityEngine.Random.Range(0, validIndices.Count)];
        return (nodes[chosenIndex], chosenIndex);
    }
    
    // ============================================
    // LEGACY API COMPATIBILITY
    // ============================================
    
    /// <summary>
    /// Get waypoint ID at index (legacy compatibility - wraps if looping).
    /// </summary>
    public string GetWaypointId(int index)
    {
        var node = GetNodeByIndex(index);
        return node?.waypointId ?? "";
    }
    
    /// <summary>
    /// Get all waypoint IDs (legacy compatibility).
    /// </summary>
    public IReadOnlyList<string> GetWaypointIds()
    {
        if (nodes == null) return new List<string>();
        
        var ids = new List<string>();
        foreach (var node in nodes)
        {
            ids.Add(node.waypointId);
        }
        return ids;
    }
    
    /// <summary>
    /// Get effective wait time range for a node.
    /// </summary>
    public Vector2 GetEffectiveWaitTimeRange(RouteNode node)
    {
        if (node != null && node.waitTimeRange.y > 0f)
        {
            return node.waitTimeRange;
        }
        return defaultWaitTimeRange;
    }
    
    /// <summary>
    /// Get effective micro-wander settings for a node.
    /// </summary>
    public MicroWanderSettings GetEffectiveMicroWander(RouteNode node)
    {
        if (node?.microWanderOverride != null && node.microWanderOverride.enabled)
        {
            return node.microWanderOverride;
        }
        return defaultMicroWander;
    }
    
    /// <summary>
    /// Validate route (check all waypoints exist).
    /// </summary>
    public bool Validate()
    {
        if (nodes == null || nodes.Count == 0)
        {
            Debug.LogWarning($"[NPCRouteAsset] Route '{name}' has no nodes!");
            return false;
        }
        
        bool valid = true;
        
        foreach (var node in nodes)
        {
            if (string.IsNullOrEmpty(node.waypointId))
            {
                Debug.LogWarning($"[NPCRouteAsset] Route '{name}' node '{node.nodeId}' has empty waypoint ID!");
                valid = false;
                continue;
            }
            
            if (!NPCWaypointRegistry.Instance.IsRegistered(node.waypointId))
            {
                Debug.LogWarning($"[NPCRouteAsset] Route '{name}' references unregistered waypoint: {node.waypointId}");
            }
            
            // Validate weighted next references
            if (node.useWeightedNext && node.weightedNext != null)
            {
                foreach (var weighted in node.weightedNext)
                {
                    if (GetNodeByNodeId(weighted.nodeId) == null)
                    {
                        Debug.LogWarning($"[NPCRouteAsset] Route '{name}' node '{node.nodeId}' references unknown node: {weighted.nodeId}");
                    }
                }
            }
        }
        
        return valid;
    }
}

/// <summary>
/// A single node in a route graph.
/// </summary>
[Serializable]
public class RouteNode
{
    [Tooltip("Stable ID for this node within the route (auto-assigned if empty)")]
    public string nodeId;
    
    [Tooltip("Waypoint ID referencing NPCWaypointRegistry")]
    public string waypointId;
    
    [Tooltip("Wait time range (min, max) in seconds. If max <= 0, uses route defaults.")]
    public Vector2 waitTimeRange;
    
    [Tooltip("If true, use weightedNext list for branching instead of route-level rules")]
    public bool useWeightedNext;
    
    [Tooltip("Weighted options for next node (only used if useWeightedNext is true)")]
    public List<WeightedNext> weightedNext = new List<WeightedNext>();
    
    [Tooltip("Per-node micro-wander override (null/disabled = use route defaults)")]
    public MicroWanderSettings microWanderOverride;
}

/// <summary>
/// Weighted reference to another route node.
/// </summary>
[Serializable]
public class WeightedNext
{
    [Tooltip("Node ID to potentially move to")]
    public string nodeId;
    
    [Tooltip("Selection weight (higher = more likely)")]
    public float weight = 1f;
}

/// <summary>
/// Settings for micro-wander behavior during dwell.
/// </summary>
[Serializable]
public class MicroWanderSettings
{
    [Tooltip("Enable micro-wander during dwell")]
    public bool enabled = false;
    
    [Tooltip("Wander radius around waypoint anchor (world units)")]
    public float radius = 1.5f;
    
    [Tooltip("Time between wander hops (min, max) in seconds")]
    public Vector2 intervalRange = new Vector2(2f, 5f);
    
    [Tooltip("Idle time after reaching each hop (min, max) in seconds")]
    public Vector2 dwellRange = new Vector2(1f, 3f);
    
    [Tooltip("Maximum wander steps per dwell window (safety cap)")]
    public int maxStepsPerWait = 5;
}