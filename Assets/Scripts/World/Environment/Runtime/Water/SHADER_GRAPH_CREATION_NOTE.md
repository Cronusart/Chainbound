# Shader Graph Creation Note

## Important

The `WaterPixelArt.shadergraph` file needs to be created **manually in Unity** using the Shader Graph editor. Due to the complexity of Shader Graph JSON structure, it's not practical to generate a complete, functional shader graph programmatically.

## Quick Start

1. **Open Unity**
2. **Create Shader Graph**:
   - Right-click in `Assets/Scripts/Environment/Water/`
   - Select **Create > Shader Graph > URP > Lit Shader Graph** (or Unlit)
   - Name it `WaterPixelArt.shadergraph`
3. **Follow Setup Guide**:
   - Open `WATER_SHADER_SETUP.md`
   - Follow Step 1 through Step 5 to build the shader graph
   - The guide includes detailed instructions for:
     - Configuring graph settings
     - Adding custom function nodes
     - Adding all properties
     - Wiring the shader logic

## What's Already Provided

✅ **HLSL Functions File**: `WaterPixelArtFunctions.hlsl` - All custom functions ready to use  
✅ **Complete Setup Guide**: `WATER_SHADER_SETUP.md` - Step-by-step instructions  
✅ **Authoring Guide**: `WATER_SHADER_AUTHORING.md` - How to create shoreline masks  
✅ **Troubleshooting Guide**: `WATER_SHADER_TROUBLESHOOTING.md` - Common issues and solutions

## Estimated Time

Creating the shader graph following the setup guide should take approximately **30-60 minutes** depending on your familiarity with Shader Graph.

## Alternative: Use Existing Water Shader as Template

If you have access to Unity's Shader Graph water examples (from Package Manager samples), you can:
1. Open an existing water shader graph
2. Modify it to match the WaterPixelArt requirements
3. Add the custom functions from `WaterPixelArtFunctions.hlsl`
4. Adjust properties and logic as needed

## Need Help?

Refer to:
- `WATER_SHADER_SETUP.md` for detailed setup instructions
- Unity Shader Graph documentation
- Unity's water shader examples in Package Manager

