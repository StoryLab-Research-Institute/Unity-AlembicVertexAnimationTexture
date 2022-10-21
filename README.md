# StorylabARU_AlembicVAT
## Description
A utility to bake an Alembic animated mesh to a vertex animation texture mesh for more efficient rendering and compatibility with mobile devices. Includes shaders to play back the animation.

As a bonus, because the animation is sampled from the texture, the shader provides free frame interpolation in the animation, allowing it to play back at native rate even if it was recorded at 15FPS, and creating seamless loops.

## Format
The animation is stored in a texture file as offsets from the original position of the vertices, stored as X,Y,Z = R,G,B, scaled to the range of the animation.

## Installation
Install via Package Manager (https://github.com/CN41ARU/StorylabARU_AlembicVAT.git)

## Usage
The utility is available under Window > Alembic Mesh VAT Generator.

Add an Alembic model to the open scene, then drop the mesh you wish to generate an animation texture for into the Mesh To Bake. Set the range of motion (or determine it automatically) - this should be set as low as possible, since the precision of the animation will be *Motion Range * 2 / 256*.
