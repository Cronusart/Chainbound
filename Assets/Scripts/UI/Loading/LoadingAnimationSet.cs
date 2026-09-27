using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data container for a loading animation set.
/// Defines sprite frames, animation speed, and optional rotation.
/// </summary>
[System.Serializable]
public class LoadingAnimationSet
{
    [Tooltip("Unique identifier for this animation set")]
    public string id = "";
    
    [Tooltip("Sprite frames for animation (ordered)")]
    public List<Sprite> frames = new List<Sprite>();
    
    [Tooltip("Animation frames per second")]
    [Range(1f, 60f)]
    public float fps = 12f;
    
    [Tooltip("Size of the animation image (optional override)")]
    public Vector2 sizeDelta = new Vector2(128, 128);
    
    [Tooltip("Enable continuous rotation")]
    public bool enableRotation = false;
    
    [Tooltip("Rotation speed in degrees per second")]
    [Range(0f, 720f)]
    public float rotationSpeedDegreesPerSecond = 90f;
}
