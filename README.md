# Alembic to Vertex Animation Texture
## Description

!!  BRP (NOT TESTED) AND URP ONLY FOR NOW !!

A utility to bake an Alembic animated mesh to a vertex animation texture for more efficient rendering and compatibility with mobile devices. Includes a template shader to play back the animation, and a subshader to allow you to add motion to your own shaders.

## Advantages
 - Playback is lightweight compared to the standard Alembic package.
 - Animations can be played on mobile platforms which do not allow Alembic playback.
 - Free frame interpolation!
 - Free seamless loops, even if the original animation does not loop seamlessly!
 - Free loop modes!
 - Animation blending!
 - The animation can be played back not only with different speeds, but also different intensities.

## Limitations
- As the texture format used is RBGAHalf, it is subject to the compatibility limitations noted at https://docs.unity3d.com/Manual/class-TextureImporterOverride.html, namely:
> When on OpenGL ES 2.0 / WebGL 1: requires OES_texture_half_float extension support.
- The utility currently only supports meshes with a single material.
- The utility currently does not attempt any material remapping; only a placeholder shader is provided.
- Only URP and BRP (not tested) are supported for now.

## Installation
Install via Package Manager (https://github.com/StoryLab-Research-Institute/AlembicVertexAnimationTexture.git)

## Usage
The utility is available under StoryLabResearch > Alembic VAT Baker.

Add an Alembic model to the open scene, then drop the mesh you wish to generate an animation texture for into the Mesh To Bake. Set the parameters as desired, and click Bake.

The available properties are:
 - Target Mesh Filter: The child mesh of the Alembic model to bake
 - Frame Rate: The framerate at which the animation will be baked. Note that you can often set this relatively low to save file size, because of the free frame interpolation provided by the shader
 - Time range: The region of the Alembic animation to bake. This may be useful if you have several different animations in sequence in your Alembic file.
 - Bake vertex normals: By default, both the motion and the surface normal of the vertices will be baked into animation textures. If your model will be unlit (or if the range of motion is very small) you can save file size by disabling this option
 - Output folder: Folder (within the Assets folder) to save the resulting files to. Sub-folders will be created in this location for the material and model files.
 - Output name: 
 
 ## Output

The utility will always output the following:
 - A motion vertex animation texture, at *Material/{outputName}_motion.asset*
 
If you choose to bake the vertex normals, it will also output:
 - A normals vertex animation texture, at *Material/{outputName}_normal.asset*
 
Unless you select the "Textures only" checkbox, it will also output:
 - A modified mesh file, at *Model/{outputName}_mesh.asset*
 - A placeholder animation material which loops the animation continuously, at *Material/{outputName}_mat.mat*
 - A prefab with the mesh with material applied, at *{outputName}.prefab*
 
## Texture properties
As the animation is sampled from a texture, you can change the way the animation is played back by changing the properties on the generated texture assets. You probably want to use the same settings for both the motion and the normal animation texture.

Key notes:
- Filter mode can be used to enable frame interpolation. If you want to play back the animation exactly as it is recorded (e.g. for stylised low-framerate animations), you may wish to set this to "point" - otherwise, set it to "Bilinear" for interpolated playback.
- Wrap mode can be used to control looping of the animation
 
## Shader properties
For simplicity, a single subshader is responsible for the entire animation system. This shader can be controlled using keywords to enable or diasble features as required. This does, however, mean that the material properties window is somewhat cluttered even for a simple shader.

Key notes:
 - Secondary motion properties (suffixed with "2") are only used if the "VAT_UseBlend" property is active. When "VAT_UseBlend" is active, you can blend between the primary and secondary animation using the "VAT_BlendFactor" property. Of course, when this property is active, the complexity of the shader is doubled.
 - Normal textures are only active if "VAT_UseNormals" is active. This affects both the base and secondary normal textures. If your animation does not involve significant orientation change, or if you are using unlit visuals, you may wish to disable normal textures to halve the animation textures required and to reduce shader complexity.
 - If "VAT_UseAutoTime" is active, the animation will continuously loop at the speed determined by the cycle time stored in the alpha channel of the motion texture, multipled by the "VAT_AutoSpeedMultipler" property. The start point of the automatic animation can be altered using the "VAT_CycleOffset" property.
 - If "VAT_UseAutoTime" is not active, the animation time will be manually controlled through script or the inspector using the "VAT_Time" proeprty.
 
## Creating your own shaders
A subshader is provided to allow you to add motion to your own shaders. If you name the appropriate properties on your custom shader the same as they are named in the subshader, you should be able to swap the placeholder shader on the generated material for your own, and the animation will carry across properly. To make this easier (so you don't need to copy and paste every property), an empty template shader is provided in Packages/StoryLabResearch Alembic VAT/Runtime/Shaders/TemplateVAT for you to duplicate and modify with the features you require.

## Mechanism
The utility produces textures to represent the animation as offsets from the rest position of the mesh (taken to be at time = 0). The columns of the texture files represent the vertices. The rows represent frames of animation. Offsets are stored as X, Y, Z = R, G, B. The animation cycle rate is stored in every pixel of the alpha channel of the motion texture, and is sampled in the shader at (0, 0) (for every vertex). Similarly, vertex normals are baked as differences from the rest vector. 

The texture format is RGBAHalf. This format is not subject to sRGB colour correction, and provides decent precision. It is also not clamped to 0-1, so offsets can range from -65504 to 65504 metres, reducing in precision with distance from the origin. It's an uncompressed 2D array of half-precision floats, you know how they work.

An altered mesh file is also produced, in which the second UV channel (UV1) is used to store the vertex's column location in the animation texture. This is hard-coded in the shader and canot be switched by keywords - if you require this channel for something else, you will need to create a duplicate shader.

A shader reads the data in the texture file in the vertex stage and applies the appropriate offsets to the position and normals of the vertices. The shader uses keywords to generate an appropriate shader variant. To reduce complexity, the shader uses the cycle rate from the motion texture for the corresponsing normal texture.

Because the animation is sampled from a texture, we can get some very nice features for free - for instance, by sampling the texture with linear interpolation, we get free frame interpolation, meaning that the animation will always play smoothly regardless of the recorded framerate (though obviously with some loss of fidelity at very low baked framerates). For example, an animation texture baked at 15FPS will play back quit happily at 400FPS.

The animation texture file sizes are relatively large. This can be reduced by reducing the framerate of the baked textures (15 frames per second is typically sufficient) and allowing the interpolation to fill in the gaps, or by reducing the geometry of the model before importing. The file size of the textures produced can be calculated as:
> mesh vertices * frames * 8 (bytes)

For example, a mesh with 5000 vertices and a 10 second animation baked at 15 frames per second will produce textures of 6MB.

## Intended development
 - Shaders for HDRP
 - Workflow to allow the baking of multi-mesh Alembic models
 - Setting to toggle adoption of parent scale and heirarchy (?)
 - Support for multi-material meshes
 - Introduction of a placeholder shader which replicates the URP Lit standard shader
