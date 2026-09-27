using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scope for variable storage (local vs global).
/// </summary>
public enum VariableScope
{
    /// <summary>Local to current sequence (cleared on sequence end).</summary>
    Local,
    
    /// <summary>Global across all sequences (persists until explicitly cleared).</summary>
    Global
}

/// <summary>
/// Typed variable store with scope and persistence support.
/// </summary>
public class SequenceVariableStore
{
    // Local variables (cleared per sequence)
    private readonly Dictionary<string, object> localVariables = new Dictionary<string, object>();
    
    // Global variables (persist across sequences)
    private readonly Dictionary<string, object> globalVariables = new Dictionary<string, object>();
    
    // Variables marked for persistence (saved to SequenceState)
    private readonly HashSet<string> persistentKeys = new HashSet<string>();

    /// <summary>Optional chain for child contexts: read globals from parent store(s).</summary>
    private SequenceVariableStore parentGlobalScope;
    
    /// <summary>
    /// Set a variable with type safety.
    /// </summary>
    public void Set<T>(string key, T value, VariableScope scope = VariableScope.Local)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[SequenceVariableStore] Cannot set variable with empty key!");
            return;
        }
        
        var targetDict = scope == VariableScope.Local ? localVariables : globalVariables;
        targetDict[key] = value;
    }
    
    /// <summary>
    /// Get a variable with type safety.
    /// </summary>
    public T Get<T>(string key, T defaultValue = default)
    {
        if (TryGet<T>(key, out T value))
        {
            return value;
        }
        return defaultValue;
    }
    
    /// <summary>
    /// Try to get a variable with type safety.
    /// </summary>
    public bool TryGet<T>(string key, out T value)
    {
        value = default(T);
        
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }
        
        // Check local first, then global
        if (localVariables.TryGetValue(key, out object localObj))
        {
            if (localObj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            return false;
        }
        
        if (globalVariables.TryGetValue(key, out object globalObj))
        {
            if (globalObj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            return false;
        }

        // Inherited globals (child track contexts)
        for (SequenceVariableStore p = parentGlobalScope; p != null; p = p.parentGlobalScope)
        {
            if (p.TryGetGlobalOnly(key, out T inherited))
            {
                value = inherited;
                return true;
            }
        }
        
        return false;
    }

    /// <summary>Wire parent store so this store can read parent global variables after own local/global miss.</summary>
    public void SetParentGlobalScope(SequenceVariableStore parent)
    {
        parentGlobalScope = parent;
    }

    private bool TryGetGlobalOnly<T>(string key, out T value)
    {
        value = default;
        if (string.IsNullOrEmpty(key))
            return false;
        if (!globalVariables.TryGetValue(key, out object globalObj))
            return false;
        if (globalObj is T typedValue)
        {
            value = typedValue;
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Check if a variable exists (in either scope).
    /// </summary>
    public bool HasKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return localVariables.ContainsKey(key) || globalVariables.ContainsKey(key);
    }
    
    /// <summary>
    /// Remove a variable from the specified scope.
    /// </summary>
    public void Remove(string key, VariableScope scope = VariableScope.Local)
    {
        if (string.IsNullOrEmpty(key)) return;
        
        var targetDict = scope == VariableScope.Local ? localVariables : globalVariables;
        targetDict.Remove(key);
        persistentKeys.Remove(key);
    }
    
    /// <summary>
    /// Mark a variable as persistent (will be saved to SequenceState).
    /// </summary>
    public void MarkPersistent(string key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            persistentKeys.Add(key);
        }
    }
    
    /// <summary>
    /// Clear all local variables (called when sequence ends).
    /// </summary>
    public void ClearLocal()
    {
        localVariables.Clear();
    }
    
    /// <summary>
    /// Clear all variables (local and global).
    /// </summary>
    public void ClearAll()
    {
        localVariables.Clear();
        globalVariables.Clear();
        persistentKeys.Clear();
    }
    
    /// <summary>
    /// Get all persistent variables as a dictionary for serialization.
    /// Only includes variables marked as persistent.
    /// </summary>
    public Dictionary<string, string> GetPersistentVariables()
    {
        var result = new Dictionary<string, string>();
        
        foreach (string key in persistentKeys)
        {
            if (localVariables.TryGetValue(key, out object localValue))
            {
                result[key] = SerializeValue(localValue);
            }
            else if (globalVariables.TryGetValue(key, out object globalValue))
            {
                result[key] = SerializeValue(globalValue);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Restore persistent variables from serialized dictionary.
    /// </summary>
    public void RestorePersistentVariables(Dictionary<string, string> serialized)
    {
        if (serialized == null) return;
        
        foreach (var kvp in serialized)
        {
            persistentKeys.Add(kvp.Key);
            object value = DeserializeValue(kvp.Value);
            if (value != null)
            {
                // Restore to global scope by default
                globalVariables[kvp.Key] = value;
            }
        }
    }
    
    /// <summary>
    /// Serialize a value to string (for persistence).
    /// </summary>
    private string SerializeValue(object value)
    {
        if (value == null) return "";
        
        if (value is bool b) return b.ToString();
        if (value is int i) return i.ToString();
        if (value is float f) return f.ToString();
        if (value is string s) return s;
        if (value is Vector3 v3) return $"{v3.x},{v3.y},{v3.z}";
        
        return value.ToString();
    }
    
    /// <summary>
    /// Deserialize a string value (for persistence).
    /// Note: Type information is lost, defaults to string.
    /// For proper type restoration, use Set<T> with explicit type.
    /// </summary>
    private object DeserializeValue(string serialized)
    {
        if (string.IsNullOrEmpty(serialized)) return "";
        
        // Try parsing as different types
        if (bool.TryParse(serialized, out bool boolVal)) return boolVal;
        if (int.TryParse(serialized, out int intVal)) return intVal;
        if (float.TryParse(serialized, out float floatVal)) return floatVal;
        
        // Check for Vector3 format "x,y,z"
        if (serialized.Contains(","))
        {
            string[] parts = serialized.Split(',');
            if (parts.Length == 3 &&
                float.TryParse(parts[0], out float x) &&
                float.TryParse(parts[1], out float y) &&
                float.TryParse(parts[2], out float z))
            {
                return new Vector3(x, y, z);
            }
        }
        
        return serialized; // Default to string
    }
    
    /// <summary>
    /// Get all variables (for debugging).
    /// </summary>
    public Dictionary<string, object> GetAllVariables()
    {
        var all = new Dictionary<string, object>();
        foreach (var kvp in localVariables)
        {
            all[kvp.Key] = kvp.Value;
        }
        foreach (var kvp in globalVariables)
        {
            if (!all.ContainsKey(kvp.Key))
            {
                all[kvp.Key] = kvp.Value;
            }
        }
        return all;
    }
}

