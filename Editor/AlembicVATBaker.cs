using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

namespace StoryLabResearch.AlembicVAT
{
    public class AlembicVATBaker : ScriptableObject
    {
        #region Exposed properties
        public MeshFilter TargetMeshFilter;
        public AlembicStreamPlayer TargetAlembicStreamPlayer;

        // update the file paths whenever the output path or name are changed
        public string OutputPath { get { return _outputPath; } set { if (value != _outputPath) { _outputPath = value; UpdateFilePaths(); } } }
        private string _outputPath = "";
        public string OutputName { get { return _outputName; } set { if (value != _outputName) { _outputName = value; UpdateFilePaths(); } } }
        private string _outputName = "";

        public float FrameRate { get; set; } = 15f;
        // refs required by GUISlider, hence fields not properties
        public float StartTime;
        public float EndTime;
        #endregion

        #region Generated file paths
        // exposed for GUI use
        public string MeshPath { get; private set; }
        public bool MeshExists { get; private set; }
        public string MotionTexturePath { get; private set; }
        public bool MotionTextureExists { get; private set; }
        public string NormalTexturePath { get; private set; }
        public bool NormalTextureExists { get; private set; }
        public string MaterialPath { get; private set; }
        public bool MaterialExists { get; private set; }
        public string PrefabPath { get; private set; }
        public bool PrefabExists { get; private set; }
        #endregion

        #region Shader consts
        private const string _shaderName = "StoryLabResearch/VAT/TemplateVAT";
        private const string _shaderMotionTextureFieldReference = "_VAT_MotionTex";
        private const string _shaderNormalTextureFieldReference = "_VAT_NormalTex";
        #endregion

        #region Public methods
        /// <summary>
        /// Generate a prefab asset containing an animated mesh, baked from the target MeshFilter
        /// </summary>
        public void Bake()
        {
            // check that all inputs are valid
            if (!Validate()) throw new Exception($"One or more invalid inputs on {name}");

            // generate and save a mesh with the second UV channel prepared
            var mesh = CreatePreparedMesh();
            SaveAsset(mesh, MeshPath);

            // generate and save the animation textures
            var texs = CreateVertexAnimationTextures(mesh);
            SaveAsset(texs[0], MotionTexturePath);
            SaveAsset(texs[1], NormalTexturePath);

            // generate and save the placeholder material
            var mat = CreatePlaceholderMaterial(texs);
            SaveAsset(mat, MaterialPath);

            // refresh the asset database so the prefab can reference the assets correctly
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // generate and save a prefab with the mesh and material set up
            var prefab = CreatePrefab(mesh, mat);
            SaveAsset(prefab, PrefabPath);

            // clean up
            DestroyImmediate(prefab);
        }

        private void UpdateFilePaths()
        {
            // the "_...Exists" booleans are only used as a cache for the GUI, to prevent IO operations every GUI update
            // the paths are checked again immediately before saving each asset
            if (!string.IsNullOrWhiteSpace(_outputName))
            {
                MeshPath = Path.Combine("Assets", _outputPath, "Model", $"{_outputName}_mesh.asset");
                MeshExists = File.Exists(MeshPath);

                MotionTexturePath = Path.Combine("Assets", _outputPath, "Material", $"{_outputName}_motion.asset");
                MotionTextureExists = File.Exists(MotionTexturePath);

                NormalTexturePath = Path.Combine("Assets", _outputPath, "Material", $"{_outputName}_normal.asset");
                NormalTextureExists = File.Exists(NormalTexturePath);

                MaterialPath = Path.Combine("Assets", _outputPath, "Material", $"{_outputName}_mat.mat");
                MaterialExists = File.Exists(MaterialPath);

                PrefabPath = Path.Combine("Assets", _outputPath, $"{_outputName}.prefab");
                PrefabExists = File.Exists(PrefabPath);
            }
            else
            {
                MeshExists = false;
                MotionTextureExists = false;
                NormalTextureExists = false;
                MaterialExists = false;
                PrefabExists = false;
            }
        }

        /// <summary>
        /// Check the Target Mesh Filter's parents for an Alembic Stream Player
        /// </summary>
        /// <returns>An Alembic Stream Player if found, otherwise null</returns>
        public void UpdateAlembicStreamPlayer()
        {
            TargetAlembicStreamPlayer = null;
            if (TargetMeshFilter != null)
            {
                Transform parent = TargetMeshFilter.transform.parent;
                while (parent != null)
                {
                    TargetAlembicStreamPlayer = parent.GetComponent<AlembicStreamPlayer>();
                    if (TargetAlembicStreamPlayer != null)
                    {
                        StartTime = 0;
                        EndTime = TargetAlembicStreamPlayer.Duration;
                        break;
                    }
                    parent = parent.parent;
                }
            }
        }
        #endregion

        #region Private methods
        /// <returns>True if all parameters pass basic sanity checks, false otherwise</returns>
        private bool Validate()
        {
            return TargetMeshFilter != null
                && TargetAlembicStreamPlayer != null
                && EndTime > StartTime
                && FrameRate > Mathf.Epsilon
                && !string.IsNullOrEmpty(_outputName);
        }

        /// <summary>
        /// Create a copy of the mesh at the Alembic Stream Player's time = 0, with the second UV channel containing the coordinates for the animation texture
        /// </summary>
        /// <returns>A prepared mesh instance</returns>
        private Mesh CreatePreparedMesh()
        {
            // assume that time 0 is the model's rest position
            TargetAlembicStreamPlayer.UpdateImmediately(0f);

            // instantiate a copy of the target shared mesh - using _meshFilter.mesh creates warnings about memory leaks in editor mode
            Mesh mesh = Instantiate(TargetMeshFilter.sharedMesh);
            int vertexCount = mesh.vertexCount;

            // fill the second UV channel of the mesh copy with the x coordinate of the resulting texture for that vertex to sample
            // offset the coordinate to the centre of the pixel, to ensure consistency with interpolated sampling modes
            Vector2[] uv = new Vector2[vertexCount];
            for (int i = 0; i < vertexCount; i++) uv[i] = new Vector2((i + .5f) / vertexCount, 0f);
            mesh.SetUVs(1, uv);

            return mesh;
        }

        /// <summary>
        /// Generate the motion and normal textures
        /// </summary>
        /// <param name="mesh">The modified mesh with second UV channel set to VAT coordinates</param>
        /// <returns>A Texture2D array of two elements in the order motion, normal</returns>
        private Texture2D[] CreateVertexAnimationTextures(Mesh mesh)
        {
            // cache for legibility
            Vector3[] originVertices = mesh.vertices;
            Vector3[] originNormals = mesh.normals;
            int vertexCount = originVertices.Length;

            // work out the number of frames with this framerate and time
            int frames = Mathf.FloorToInt(FrameRate * (EndTime - StartTime));

            // we will fill in color arrays in the loop and then assign them to the textures all at once, as this is much faster than calling setpixel over and over
            Color[] motionColors = new Color[vertexCount * frames];
            Color[] normalColors = new Color[vertexCount * frames];

            // store cyclerate rather than framerate to minimise work done in shader
            float cycleRate = 1 / (EndTime - StartTime);

            // limit the threads to the number of logical processors
            ParallelOptions options = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };

            // loop over every frame at our chosen framerate
            for (int frame = 0; frame < frames; frame++)
            {
                // move the stream player to the next time
                TargetAlembicStreamPlayer.UpdateImmediately(Mathf.Lerp(StartTime, EndTime, (float)frame / frames));

                // cache for legibility
                Vector3[] vertices = TargetMeshFilter.sharedMesh.vertices;
                Vector3[] normals = TargetMeshFilter.sharedMesh.normals;

                // check the topology is consistent
                if (vertices.Length != vertexCount) throw new Exception("Number of vertices in mesh has changed, cannot generate vertex animation texture");

                // loop over each vertex and find their offsets
                Parallel.For(0, vertexCount, options, v =>
                {
                    // note that a Color is just a Vector4
                    // we can't directly assign a Vector3 to a Color, but we can directily assign a Vector3 to a Vector4 and we can directly assign a Vector4 to a Color
                    // at least that saves us one step...

                    // motion
                    Vector4 offset = vertices[v] - originVertices[v];
                    // store pixelsPerSecond in every pixel of motion alpha so we don't need to do any sample position calculations in the shader
                    offset.w = cycleRate;
                    motionColors[v + (frame * vertexCount)] = offset;

                    // normal
                    Vector4 difference = normals[v] - originNormals[v];
                    normalColors[v + (frame * vertexCount)] = difference;
                });
            }

            // a Texture2D as RGBAHalfs is an unclamped 2D array of half precision floats

            // setpixels invocation is slower than setpixels32, but we need an unclamped float range rather than ints in the 0-255 range
            #pragma warning disable UNT0017 

            // save motion texture
            Texture2D motionTex = new(vertexCount, frames, TextureFormat.RGBAHalf, false);
            motionTex.SetPixels(0, 0, vertexCount, frames, motionColors);

            // save normals texture
            Texture2D normalTex = new(vertexCount, frames, TextureFormat.RGBAHalf, false);
            normalTex.SetPixels(0, 0, vertexCount, frames, normalColors);

            // restore setpixels warning
            #pragma warning restore UNT0017

            // return as an array of known order
            return new Texture2D[] { motionTex, normalTex };
        }

        /// <param name="textures">Animation textures in order motion, normal</param>
        /// <returns>A blank material with the animation applied</returns>
        private Material CreatePlaceholderMaterial(Texture2D[] textures)
        {
            // pretty self explanatory
            Shader shad = Shader.Find(_shaderName);
            if (shad == null) throw new NullReferenceException($"Cannot find shader with name {_shaderName}");
            Material mat = new(shad);
            mat.SetTexture(_shaderMotionTextureFieldReference, textures[0]);
            mat.SetTexture(_shaderNormalTextureFieldReference, textures[1]);

            return mat;
        }

        /// <summary>
        /// Create a prefab with the mesh and placeholder material applied
        /// </summary>
        private GameObject CreatePrefab(Mesh mesh, Material mat)
        {
            // add a temporary empty gameobject to the open scene
            GameObject prefab = new();

            // populate it with the mesh and placeholder material
            var mf = prefab.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = prefab.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            return prefab;
        }

        /// <summary>
        /// Wrapper for AssetDatabase.CreateAsset and PrefabUtility.SaveAsPrefabAsset which handles overwriting and missing directories
        /// </summary>
        /// 
        /// <param name="asset">A <see cref="UnityEngine.Object"/> to save as an asset, or a <see cref="GameObject"/> save as the root of a prefab</param>
        /// <param name="path">Path to the save location, starting at Assets, including file name and extension</param>
        private void SaveAsset(UnityEngine.Object asset, string path)
        {
            // there is no such thing as "overwrite" - delete the asset if it exists
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

            // if the path existed, we clearly don't need to create it
            // otherwise try to create it as a precautionary measure (assetdatabase.createasset fails if the folder does not exist)
            else Directory.CreateDirectory(Path.GetDirectoryName(path));

            // save according to object type
            if(asset is GameObject)
            {
                PrefabUtility.SaveAsPrefabAsset(asset as GameObject, path);
            }
            else
            {
                // do not hide the asset - otherwise the prefab will not be able to locate it
                // assets created in the inspector appear to be hidden by default
                asset.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(asset, path);
            }
        }
        #endregion
    }
}