# Bootstrap Scene Setup Instructions

The Bootstrap scene must be created manually in Unity Editor. Follow these steps:

## Step-by-Step Setup

### 1. Create Bootstrap Scene

1. In Unity Editor: **File > New Scene**
2. Choose **Basic (Built-in)** template
3. Save as: `Assets/Scenes/Bootstrap.unity`
4. **Important**: Delete the default Main Camera and Directional Light (we don't need them)

### 2. Create GameServices Root

1. Create empty GameObject: **GameServices**
2. Add component: `GameServicesContainer`
3. This will mark it as DontDestroyOnLoad

### 3. Add SceneLoader Service

1. Create child GameObject under GameServices: **SceneLoader**
2. Add component: `SceneLoader`
3. Configure in Inspector:
   - **Loading Screen UI**: Will assign after creating UI (leave empty for now)
   - **Scene Music Config**: Assign your SceneMusicConfigSO asset (optional)
   - **Min Display Time Full Loading**: 1.0
   - **Min Display Time Quick Fade**: 0.3
   - **Progress Activation Threshold**: 0.9
   - **Auto Load First Scene**: false (or true if you want auto-load)
   - **First Scene Name**: "MainMenu" or "Loomville" (if auto-load enabled)

### 4. Create LoadingScreenUI Canvas

1. Create Canvas GameObject: **LoadingScreenUI**
   - **Render Mode**: Screen Space - Overlay
   - **Sort Order**: 10000
   - **Canvas Scaler**: Scale With Screen Size
   - **Reference Resolution**: 1920 x 1080

2. Create child: **FadePanel**
   - Add `Image` component
   - Set anchor to **stretch-stretch** (full screen)
   - Set color to **Black**
   - Add `CanvasGroup` component
   - Set CanvasGroup **Alpha** to 0
   - Set **Blocks Raycasts** to false

3. Create child: **LoadingContainer**
   - Anchor: **middle-center**
   - Width: 400, Height: 300
   - Add `Image` component (optional background)
   - Set color to semi-transparent black (optional)

4. Under LoadingContainer, create: **LoadingImage**
   - Add `Image` component
   - Size: 128 x 128
   - Add `UIImageFrameAnimator` component

5. Under LoadingContainer, create: **ProgressBar** (optional, for FullLoading style)
   - Add `Slider` component
   - Set anchor: **bottom-center**
   - Position Y: -50
   - Width: 300, Height: 20
   - Initially disable GameObject

6. Under LoadingContainer, create: **TipsText**
   - Add `TextMeshProUGUI` component
   - Set anchor: **bottom-center**
   - Position Y: -100
   - Width: 350, Height: 100
   - Set alignment: **center**
   - Set font size: 18
   - Initially disable GameObject

7. Add `LoadingScreenUI` component to **LoadingScreenUI** canvas root
8. Configure LoadingScreenUI component:
   - **Fade Panel**: Drag FadePanel GameObject
   - **Loading Container**: Drag LoadingContainer GameObject
   - **Loading Image**: Drag LoadingImage GameObject
   - **Progress Bar**: Drag ProgressBar Slider (if created)
   - **Tips Text**: Drag TipsText GameObject
   - **Frame Animator**: Auto-assigned (or drag LoadingImage)
   - **Animation Sets**: Add sets later (see main setup guide)
   - **Tips**: Add tips later (see main setup guide)
   - **Fade In Duration**: 0.3
   - **Fade Out Duration**: 0.3

### 5. Link Components

1. Select **SceneLoader** GameObject
2. Drag **LoadingScreenUI** canvas to SceneLoader's **Loading Screen UI** field

### 6. Set as First Scene

1. **File > Build Settings**
2. Click **Add Open Scenes** (if Bootstrap is open)
3. Drag Bootstrap scene to **index 0** (first position)
4. Verify Bootstrap is at the top of the list

### 7. Test

1. Press Play
2. If auto-load is enabled, you should see loading screen briefly
3. If auto-load is disabled, Bootstrap will stay loaded (ready for manual scene loads)

## Notes

- Bootstrap scene should be **minimal** - no gameplay content
- All services persist via DontDestroyOnLoad
- LoadingScreenUI canvas will be visible during transitions
- You can add other services to GameServices root as needed (SpawnService, MusicConductor, etc.)

## Next Steps

After Bootstrap is set up:
1. Configure animation sets (see `Docs/Guides/LOADING_SCREEN_SETUP.md`)
2. Add tips (see `Docs/Guides/LOADING_SCREEN_SETUP.md`)
3. Test scene transitions
4. Add ISceneWarmup to large scenes if needed
