using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

public class AlembicMeshVATGenerator : ScriptableObject
{
    public MeshFilter _meshToBake;
    public AlembicStreamPlayer _alembicStreamPlayer;
    public string _outputPath = "";
    public string _outputName = "AlembicExport";
    public float _frameRate = 30f;
    public float _startTime;
    public float _endTime;

    private const string _shaderName = "StoryLabARU/VAT/PlaceholderVAT";
    private const string _shaderTextureFieldReference = "_VAT_MotionTex";
    private const string _shaderMotionRangeFieldReference = "_VAT_MotionRange";
    private const string _shaderFrameRateFieldReference = "_VAT_FrameRate";

    public float _motionRange = .5f;

    public string _fullOutputMeshName;
    public bool _meshExists;
    public string _fullOutputTextureName;
    public bool _textureExists;
    public string _fullOutputMaterialName;
    public bool _materialExists;
    public string _fullOutputPrefabName;
    public bool _prefabExists;

    public void OnGenerateButton()
    {
        _alembicStreamPlayer.UpdateImmediately(0f);

        var mesh = CreatePreparedMesh();

        var tex = CreateVertexAnimationTexture();

        var mat = CreatePlaceholderMateral(tex);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        CreatePrefab(mesh, mat);
    }

    private Material CreatePlaceholderMateral(Texture2D tex)
    {
        Shader shad = Shader.Find(_shaderName);
        if (shad == null) throw new Exception("Cannot find shader with name " + _shaderName);
        Material mat = new(shad);
        mat.SetTexture(_shaderTextureFieldReference, tex);
        mat.SetFloat(_shaderMotionRangeFieldReference, _motionRange);
        mat.SetFloat(_shaderFrameRateFieldReference, _frameRate);

        mat.hideFlags = HideFlags.None;
        if (_materialExists) AssetDatabase.DeleteAsset(_fullOutputMaterialName);
        else Directory.CreateDirectory(Path.GetDirectoryName(_fullOutputMaterialName));
        AssetDatabase.CreateAsset(mat, _fullOutputMaterialName);

        return mat;
    }

    private void CreatePrefab(Mesh mesh, Material mat)
    {
        GameObject go = new();
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        PrefabUtility.SaveAsPrefabAsset(go, Path.Combine("Assets", _outputPath, $"{_outputName}.prefab"));
        DestroyImmediate(go);
    }

    private Texture2D CreateVertexAnimationTexture()
    {
        Vector3[] origins = _meshToBake.sharedMesh.vertices;
        int vertexCount = origins.Length;
        int frames = CalculateFrames();

        Color[] colors = new Color[vertexCount * frames];
        for (int frame = 0; frame < frames; frame++)
        {
            _alembicStreamPlayer.UpdateImmediately(Mathf.Lerp(_startTime, _endTime, (float)frame / frames));

            Mesh m = _meshToBake.sharedMesh;
            Vector3[] vertices = m.vertices;

            if (origins.Length != vertices.Length) throw new Exception("Number of vertices in mesh has changed, cannot generate vertex animation texture");

            Parallel.For(0, vertexCount, new ParallelOptions { MaxDegreeOfParallelism = 32 }, v =>
            {
                Vector3 vect = ((vertices[v] - origins[v]) / _motionRange) + (Vector3.one * .5f);
                colors[v + (frame * vertexCount)] = new Color(vect.x, vect.y, vect.z);
            });
        }

        Texture2D tex = new(vertexCount, frames);
        tex.SetPixels(0, 0, vertexCount, frames, colors);
        tex.hideFlags = HideFlags.None;
        if (_textureExists) AssetDatabase.DeleteAsset(_fullOutputTextureName);
        else Directory.CreateDirectory(Path.GetDirectoryName(_fullOutputTextureName));
        AssetDatabase.CreateAsset(tex, _fullOutputTextureName);

        return tex;
    }

    private Mesh CreatePreparedMesh()
    {
        Mesh mesh = _meshToBake.sharedMesh;
        int vertexCount = mesh.vertexCount;
        Vector2[] uv = new Vector2[vertexCount];
        for (int i = 0; i < vertexCount; i++) uv[i] = new Vector2((i + .5f) / vertexCount, 1f);
        mesh.SetUVs(1, uv);
        mesh.hideFlags = HideFlags.None;
        if (_meshExists) AssetDatabase.DeleteAsset(_fullOutputMeshName);
        else Directory.CreateDirectory(Path.GetDirectoryName(_fullOutputMeshName));
        AssetDatabase.CreateAsset(mesh, _fullOutputMeshName);

        return mesh;
    }

    public void OnPreCalculateMotionRangeButton()
    {
        int frames = CalculateFrames();

        Vector3[] origins = _meshToBake.sharedMesh.vertices;
        float maxOffset = 0f;
        for (int frame = 0; frame < frames; frame++)
        {
            _alembicStreamPlayer.UpdateImmediately(Mathf.Lerp(_startTime, _endTime, (float)frame / frames));

            Vector3[] frameVertices = _meshToBake.sharedMesh.vertices;

            if (frameVertices.Length != origins.Length) throw new Exception("Number of vertices in mesh has changed, cannot generate vertex animation texture");

            for (int i = 0; i < frameVertices.Length; i++)
            {
                Vector3 offset = frameVertices[i] - origins[i];
                if (offset.x * offset.x > maxOffset * maxOffset) maxOffset = Mathf.Abs(offset.x);
                if (offset.y * offset.y > maxOffset * maxOffset) maxOffset = Mathf.Abs(offset.y);
                if (offset.z * offset.z > maxOffset * maxOffset) maxOffset = Mathf.Abs(offset.z);
            }
        }

        _motionRange = maxOffset;
    }

    private int CalculateFrames()
    {
        return Mathf.FloorToInt(_frameRate * (_endTime - _startTime));
    }

    internal void UpdateOutputLocations()
    {
        _fullOutputMeshName = Path.Combine("Assets", _outputPath, $"{_outputName}_mesh.asset");
        _meshExists = File.Exists(_fullOutputMeshName);

        _fullOutputTextureName = Path.Combine("Assets", _outputPath, $"{_outputName}_motion_range{_motionRange}.asset");
        _textureExists = File.Exists(_fullOutputTextureName);

        _fullOutputMaterialName = Path.Combine("Assets", _outputPath, $"{_outputName}_mat.mat");
        _materialExists = File.Exists(_fullOutputMaterialName);

        _fullOutputPrefabName = Path.Combine("Assets", _outputPath, $"{_outputName},prefab");
        _prefabExists = File.Exists(_fullOutputPrefabName);
    }
}

public class AlembicMeshVATGeneratorEditorWindow : EditorWindow
{
    public AlembicMeshVATGenerator m_generator;
    public SerializedObject m_serializedGenerator;

    private bool m_automaticOutputName = true;

    private bool m_workflowIsMesh;

[MenuItem("Window/Alembic Mesh VAT Generator")]
    public static void ShowWindow() => GetWindow(typeof(AlembicMeshVATGeneratorEditorWindow));

    private void OnEnable()
    {
        m_generator = CreateInstance<AlembicMeshVATGenerator>();

        // create a serialization of the generator for the GUI to be able to access property fields
        m_serializedGenerator = new SerializedObject(m_generator);
    }

    public void OnGUI()
    {
        EditorGUILayout.LabelField("Workflow");
        EditorGUILayout.BeginHorizontal();
        if (m_workflowIsMesh) GUI.color = new Color(.5f, 1f, .5f);
        if (GUILayout.Button("Single Mesh")) m_workflowIsMesh = true;
        GUI.color = m_workflowIsMesh ? Color.white: new Color(.5f, 1f, .5f);
        if (GUILayout.Button("Whole Model")) m_workflowIsMesh = false;
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // ignoring nasty conditional for now since we don't have an implementation for the full model workflow yet
        GUI.enabled = m_workflowIsMesh ?
            m_generator._meshToBake != null && m_generator._alembicStreamPlayer != null :
            false;
        if(GUILayout.Button("Generate", GUILayout.Height(32))) m_generator.OnGenerateButton();
        GUI.enabled = true;

        EditorGUILayout.Space();

        if (m_workflowIsMesh)
        {
            // === mesh selection region ===
            MeshFilter lastMeshFilter = m_generator._meshToBake;
            EditorGUILayout.PropertyField(m_serializedGenerator.FindProperty(nameof(m_generator._meshToBake)));

            // this sets the properties on the object the serializedobject is inspecting
            m_serializedGenerator.ApplyModifiedProperties();

            // if the selected meshfilter has changed...
            if (lastMeshFilter != m_generator._meshToBake)
            {
                // ...check the new meshfilter's parents for an Alembic Stream Player
                m_generator._alembicStreamPlayer = null;
                Transform parent = m_generator._meshToBake.transform.parent;
                while (parent != null)
                {
                    m_generator._alembicStreamPlayer = parent.GetComponent<AlembicStreamPlayer>();
                    if (m_generator._alembicStreamPlayer != null)
                    {
                        m_generator._startTime = 0;
                        m_generator._endTime = m_generator._alembicStreamPlayer.Duration;
                        break;
                    }
                    parent = parent.parent;
                }
            }

            if (m_generator._meshToBake == null || m_generator._alembicStreamPlayer == null)
            {
                EditorGUILayout.HelpBox("Add the Alembic model to a scene, then select the child MeshFilter component you want to bake the animation from and assign it to this field.", MessageType.Info);
            }

            if (m_generator._meshToBake != null)
            {
                if (m_generator._alembicStreamPlayer == null)
                {
                    EditorGUILayout.HelpBox("Selected MeshFilter does not have a parent AlembicStreamPlayer", MessageType.Error);
                }
                else
                {
                    // we have a valid mesh which is a part of an Alembic model!

                    EditorGUILayout.Space();

                    // === framerate region ===
                    m_generator._frameRate = EditorGUILayout.Slider("Frame Rate", m_generator._frameRate, .1f, 60f);

                    EditorGUILayout.Space();

                    // === time range region ===
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Time Range (s)");
                    EditorGUIUtility.labelWidth = 35f;
                    EditorGUILayout.FloatField("from", m_generator._startTime, GUILayout.MinWidth(80f));
                    EditorGUIUtility.labelWidth = 20f;
                    EditorGUILayout.FloatField("to", m_generator._endTime, GUILayout.MinWidth(80f));
                    EditorGUIUtility.labelWidth = 0f;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.MinMaxSlider(ref m_generator._startTime, ref m_generator._endTime, 0f, m_generator._alembicStreamPlayer.Duration);

                    EditorGUILayout.Space();

                    // === motion range region ===
                    EditorGUILayout.FloatField("Motion range (m)", m_generator._motionRange);
                    if (GUILayout.Button("Precalculate motion range")) m_generator.OnPreCalculateMotionRangeButton();

                    EditorGUILayout.Space();

                    // === output region ===
                    m_generator._outputPath = EditorGUILayout.TextField("Output folder", m_generator._outputPath);
                    GUI.enabled = !m_automaticOutputName;
                    m_generator._outputName = EditorGUILayout.TextField("Output name", m_generator._outputName);
                    GUI.enabled = true;
                    m_automaticOutputName = EditorGUILayout.Toggle("Automatic output name", m_automaticOutputName);
                    if (m_automaticOutputName) m_generator._outputName = m_generator._meshToBake.name;
                    m_generator.UpdateOutputLocations();
                    if (m_generator._meshExists) EditorGUILayout.HelpBox("A mesh already exists at " + m_generator._fullOutputMeshName, MessageType.Warning);
                    if (m_generator._textureExists) EditorGUILayout.HelpBox("A texture already exists at " + m_generator._fullOutputTextureName, MessageType.Warning);
                    if (m_generator._materialExists) EditorGUILayout.HelpBox("A material already exists at " + m_generator._fullOutputMaterialName, MessageType.Warning);
                    if (m_generator._prefabExists) EditorGUILayout.HelpBox("A prefab already exists at " + m_generator._fullOutputPrefabName, MessageType.Warning);
                }
            }
        }
        else
        {
            // whole model workflow here
        }
    }
}