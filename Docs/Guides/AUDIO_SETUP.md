# Audio System Setup Guide

## Overview

This audio system provides data-driven SFX/UI/Ambience playback and 2-layer music with sample-accurate sync (Cassette-Beasts style fusion behavior).

## Required Setup

### 1. Create AudioMixer Asset

**IMPORTANT:** The AudioMixer asset must be created manually in Unity Editor. The code expects a mixer with the following structure:

1. Create a new AudioMixer asset:
   - Right-click in Project → Create → Audio → Audio Mixer
   - Name it `MainAudioMixer.mixer`
   - Place it in `Assets/Audio/` folder

2. Set up Mixer Groups:
   ```
   Master
   ├── Music
   │   ├── Music_Base
   │   └── Music_Layer
   ├── SFX
   ├── UI
   └── Ambience
   ```

3. Expose Parameters:
   - Right-click each parameter → "Expose"
   - Name them exactly:
     - `MasterVolDb` (on Master group)
     - `MusicVolDb` (on Music group)
     - `MusicLayerVolDb` (on Music_Layer group)
     - `SFXVolDb` (on SFX group)
     - `UIVolDb` (on UI group)
     - `AmbVolDb` (on Ambience group)

### 2. Assign Mixer to Services

1. Add `AudioService` and `MusicConductor` components to your GameServices GameObject (as children of GameServicesContainer)
2. Assign the `MainAudioMixer.mixer` asset to both services' "Audio Mixer" field in the inspector

### 3. Create Audio Assets

1. **Sound Events** (`SoundEventSO`):
   - Right-click → Create → SignalJam → Audio → Sound Event
   - Configure clips, bus, volume/pitch ranges, 3D settings, polyphony
   - Place in `Assets/Data/Audio/SFX/` or similar

2. **Music Cues** (`MusicCueSO`):
   - Right-click → Create → SignalJam → Audio → Music Cue
   - Assign base and layer stems (must be same length for sync)
   - Configure volumes and crossfade settings
   - Place in `Assets/Data/Audio/Music/`

3. **Audio Library** (Optional):
   - Right-click → Create → SignalJam → Audio → Audio Library
   - Drag Sound Events and Music Cues into lists for ID-based lookup

### 4. Configure Music Stems

**CRITICAL:** For sample-accurate sync, music stems must:
- Have identical length (within 10ms tolerance)
- Use `Streaming` load type (set in AudioClip import settings)
- Use consistent sample rate

## Usage Examples

### Playing SFX
```csharp
var soundEvent = // ... get SoundEventSO ...
var handle = AudioService.Instance.Play(soundEvent);
// Optional: handle.Stop(0.5f); // fade out
```

### Playing Music
```csharp
var musicCue = // ... get MusicCueSO ...
MusicConductor.Instance.Play(musicCue, transitionSeconds: 1f);
```

### Controlling Music Layer
```csharp
// Acquire token to activate layer
var token = MusicConductor.Instance.AcquireLayerToken("Combat", fadeSeconds: 0.5f);

// ... combat active ...

// Release token to fade out layer
token.Release(); // Uses cue's crossfadeSeconds
// Or: MusicConductor.Instance.ReleaseLayerToken(token, fadeSeconds: 0.3f);
```

### Setting Bus Volumes
```csharp
IAudioService audio = AudioService.Instance;
audio.SetBusVolume(AudioBus.SFX, -6f); // -6 dB
audio.SetBusVolume(AudioBus.Music, 0f); // 0 dB (full volume)
```

### Runtime Pitch/Volume Control
```csharp
var handle = AudioService.Instance.Play(soundEvent);

// Adjust pitch at runtime (e.g., for running footsteps)
handle.SetPitch(1.3f); // 30% faster

// Adjust volume at runtime
handle.SetVolume(0.5f); // 50% volume

// Check if still playing
if (AudioService.Instance.IsPlaying(handle)) { ... }
```

## Footstep System

### 1. Create FootstepConfigSO
- Right-click → Create → SignalJam → Audio → Footstep Config
- Add surface type entries (Default, Grass, Stone, Wood, etc.)
- Assign looping SoundEventSO to each surface type
- Configure walk/run pitch multipliers
- Map Unity layers to surface types in the Layer Mapping array

### 2. Create Footstep SoundEventSO Assets
For each surface type:
- Create a Sound Event with `loop = false` (interval-based playback, not looping)
- Set `is3D = true` for spatial audio
- Add your footstep audio clips
- Set appropriate volume (pitch variation optional, disabled by default)

### 3. Configure FootstepConfigSO
1. Open your `FootstepConfigSO` asset
2. For each surface type entry:
   - Set `walkInterval` (e.g., 0.5s = slower tempo)
   - Set `runInterval` (e.g., 0.3s = faster tempo)
   - Lower interval = faster tempo (more footsteps per second)
   - Keep `pitch = 1.0` and `usePitchVariation = false` to avoid distortion
   - Or enable pitch variation for subtle tone changes (keep range 0.9-1.1)

### 4. Add FootstepController to Player
1. Add `FootstepController` component to player GameObject
2. Assign the `FootstepConfigSO` asset
3. Configure ground detection settings to match your player
4. The controller auto-detects movement and surface changes
5. Footsteps play at intervals based on walk/run speed (tempo control, not pitch)

## Scene Music System

### 1. Create SceneMusicConfigSO
- Right-click → Create → SignalJam → Audio → Scene Music Config
- Add entries mapping scene names to MusicCueSO assets
- Configure transition durations per scene
- Optionally set scenes to start with layer active

### 2. Assign to SceneTransitionManager
1. Select your SceneTransitionManager (under GameServices)
2. Assign the SceneMusicConfigSO to the "Scene Music Config" field
3. Music will automatically transition when loading scenes

## Spatial Ambience Zones

Spatial ambience zones play ambient sounds that fade based on distance from the player. Perfect for:
- Ocean sounds near docks/shorelines
- Forest ambience in wooded areas
- Cave echoes underground
- Machine hums near factories

### Setup Steps

1. **Create Ambience Sound Event**:
   - Right-click → Create → SignalJam → Audio → Sound Event
   - Name it (e.g., `Ocean_Ambience`)
   - **IMPORTANT**: Set `Is 3D = false` so volume follows player distance. Enable `Is 3D` later only if you want that event to pan/roll off against the camera listener.
   - Set `Output Bus = Ambience`
   - Set `Loop = true` for looping beds, or `Loop = false` for sporadic one-shots
   - Configure volume/pitch ranges
   - Add your ambience audio clip(s)

2. **Place AmbienceZone in Scene**:
   - Create empty GameObject at desired location (e.g., near docks)
   - Add `AmbienceZone` component (SphereCollider auto-added)
   - Assign the SoundEventSO to "Ambience Sound"
   - Configure distance fade:
     - `Max Volume Distance`: Distance where sound is loudest (e.g., 5 units)
     - `Min Volume Distance`: Distance where sound fades to zero (e.g., 50 units)
   - Adjust `Distance Fade Curve` for custom fade shape
   - Set `Max Volume Multiplier` (0-1) for overall volume control

3. **Optional Configuration**:
   - `Play On Start`: Auto-start when scene loads (or wait for player to enter zone)
   - `Fade In Duration`: Smooth fade when entering zone
   - `Fade Out Duration`: Smooth fade when leaving zone
   - `Auto Find Player`: Automatically tracks Player tag (or assign custom target)

### Example: Ocean Ambience at Docks

1. Create `Ocean_Ambience` SoundEventSO:
   - Is 3D: ❌ (player-distance volume; opt into 3D only for camera spatialization)
   - Output Bus: Ambience
   - Loop: ✅
   - Volume: 0.6-0.8

2. Place AmbienceZone GameObject:
   - Position: Near dock area
   - Max Volume Distance: 10 (loudest when very close)
   - Min Volume Distance: 100 (audible from far away)
   - Max Volume Multiplier: 0.7

3. Result: Ocean sounds fade in as player approaches docks, fade out as they leave.

`AmbienceZoneSporadicLayer` one-shots use the same rule: 2D + the zone's player-distance volume unless the SoundEventSO has `Is 3D` enabled.

### Debugging

- Enable `Draw Gizmos` to visualize fade zones in Scene view:
  - **Green sphere**: Max volume distance
  - **Red sphere**: Min volume distance
  - **Yellow sphere**: Zone center
  - **Cyan line**: Connection to target (player)
- Enable `Enable Debug Logs` for playback events

## Performance Notes

- AudioSources are pooled (default: 16 2D, 8 3D)
- Polyphony limits are strictly enforced per-event
- No allocations in Play() hot path
- Music stems use streaming for memory efficiency

## Debugging

Enable debug logs in AudioService and MusicConductor inspector:
- `enableDebugLogs`: Logs all playback events
- `showDebugUI` (MusicConductor): Shows on-screen debug info

Check GameServicesContainer validation to ensure services are detected.

## File Structure

```
Assets/Scripts/Audio/
├── Data/
│   ├── AudioBus.cs           # Bus enum (SFX, UI, Ambience, etc.)
│   ├── SoundEventSO.cs       # SFX/UI/Ambience sound events
│   ├── MusicCueSO.cs         # 2-layer music cues
│   ├── AudioLibrarySO.cs     # ID-based lookup (optional)
│   ├── FootstepConfigSO.cs   # Surface-to-sound mapping
│   ├── SceneMusicConfigSO.cs # Scene-to-music mapping
│   └── SurfaceType.cs        # Surface type enum
├── Interfaces/
│   ├── IAudioService.cs      # SFX service interface
│   └── IMusicConductor.cs    # Music service interface
├── Services/
│   ├── AudioService.cs       # Pooled audio playback
│   └── MusicConductor.cs     # Sample-accurate music
├── Handles/
│   └── MusicLayerToken.cs    # Token for layer control
└── Components/
    ├── FootstepController.cs # Player footstep component
    └── AmbienceZone.cs       # Spatial ambience zones
```
