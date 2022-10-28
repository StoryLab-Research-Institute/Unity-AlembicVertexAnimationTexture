# Alembic to Vertex Animation Texture
## Description

!! URP ONLY FOR NOW !!

A utility to bake an Alembic animated mesh to a vertex animation texture for more efficient rendering and compatibility with mobile devices. Includes shaders to play back the animation.

## Advantages
 - Playback is extremely lightweight compared to the standard Alembic package, since all the work at runtime is done on the GPU, rather than recomputing the mesh, recalculating the normals, and re-uploading the mesh to the GPU every frame.
 - Animations can be played back on mobile platforms which do not allow Alembic playback.
 - Because the animation is sampled from a texture, the shader provides free frame interpolation in the animation, allowing it to play back at native rate regardless of the baked rate, and creating seamless loops.
 - The animation can be played back not only with different speeds, but also different intensities.

## Mechanism
The utility produces textures to represent the animation as offsets from the rest position of the mesh (taken to be at time = 0). The columns of the texture files represent the vertices. The rows represent frames of animation. Offsets are stored as X, Y, Z = R, G, B. The animation framerate is stored in every pixel of the alpha channel of the motion texture, and is sampled at (0, 0).

The texture format is RGBAHalf. This format is not subject to sRGB colour correction, and provides decent precision. It is also not clamped to 0-1, so offsets can range from -65504 to 65504 metres, reducing in precision with distance from the origin. It's an uncompressed 2D array of half-precision floats, you know how they work.

An altered mesh file is also produced, in which the second UV channel is used to store the vertex's column in the animation texture.

A shader reads the data in the texture file in the vertex stage and applies the appropriate offsets to the position and normals of the vertices.

## Installation
Install via Package Manager (https://github.com/StoryLab-Research-Institute/AlembicVertexAnimationTexture.git)

## Usage
The utility is available under Window > Alembic VAT Baker.

Add an Alembic model to the open scene, then drop the mesh you wish to generate an animation texture for into the Mesh To Bake. Set the parameters as desired, and click Bake. It will output the following:

 - A modified mesh file (as noted above)
 - A placeholder animation material which loops the animation continuously in realtime
 - A motion texture
 - A normals texture
 - A prefab with the mesh with material applied

## Limitations
The animation texture file sizes are relatively large. This can be reduced by reducing the framerate of the baked textures (15 frames per second is typically sufficient) and allowing the interpolation to fill in the gaps, or by reducing the geometry of the model before importing. The file size of the textures produced can be calculated as:
> mesh vertices * frames * 8 (bytes)

For example, a mesh with 5000 vertices and a 10 second animation baked at 15 frames per second will produce textures of 6MB.

As the texture format used is RBGAHalf, it is subject to the compatibility limitations noted at https://docs.unity3d.com/Manual/class-TextureImporterOverride.html, namely:
> When on OpenGL ES 2.0 / WebGL 1: requires OES_texture_half_float extension support.

The utility only supports meshes with a single material.

The utility does not attempt any material remapping; only a placeholder shader is provided. A subshader is provided to allow you to add motion to your own shaders. If you name the appropriate properties on your custom shader the same as they are named in the subshader, you should be able to swap the placeholder shader on the material for your own one and the animation will carry across properly.

Only URP is supported for now.
