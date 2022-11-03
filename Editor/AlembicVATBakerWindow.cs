using UnityEditor;
using UnityEngine;

namespace StoryLabResearch.AlembicVAT
{
    public class AlembicVATBakerWindow : EditorWindow
    {
        private AlembicVATBaker m_baker;
        private SerializedObject m_serializedBaker;

        private bool m_automaticOutputName = true;

        [MenuItem("StoryLabResearch/Alembic VAT Baker")]
        public static void ShowWindow() => GetWindow(typeof(AlembicVATBakerWindow));

        public void OnGUI()
        {
            // === GUI BAKE BUTTON REGION ===
            // we check this every time we draw the GUI because actions such as entering or exiting playmode can destroy these instances
            if (m_baker == null) m_baker = CreateInstance<AlembicVATBaker>();
            // we need a serialization of the generator so the GUI can access serialized properties
            if (m_serializedBaker == null || m_serializedBaker.targetObject == null || m_serializedBaker.targetObject != m_baker) m_serializedBaker = new SerializedObject(m_baker);

            // only enable the bake button if we have a valid target
            GUI.enabled = m_baker.TargetMeshFilter != null && m_baker.TargetAlembicStreamPlayer != null;
            if (GUILayout.Button("Bake", GUILayout.Height(32))) m_baker.Bake();
            GUI.enabled = true;

            EditorGUILayout.Space();

            // === GUI MESH SELECTION REGION ===
            // store current target mesh filter reference for comparison
            MeshFilter lastTargetMeshFilter = m_baker.TargetMeshFilter;
            // update the target mesh filter
            EditorGUILayout.PropertyField(m_serializedBaker.FindProperty(nameof(m_baker.TargetMeshFilter)));

            // set the properties on the object the serializedobject is inspecting
            m_serializedBaker.ApplyModifiedProperties();

            // if the selected meshfilter has been changed...
            if (lastTargetMeshFilter != m_baker.TargetMeshFilter)
            {
                if (m_automaticOutputName && m_baker.TargetMeshFilter != null) m_baker.OutputName = m_baker.TargetMeshFilter.name;
                m_baker.UpdateAlembicStreamPlayer();
            }

            // if the selected mesh filter is not a child of an Alembic Stream Player (or does not exist)...
            if (m_baker.TargetAlembicStreamPlayer == null)
            {
                EditorGUILayout.HelpBox("Add the Alembic model to a scene, then select the child MeshFilter component you want to bake the animation from and assign it to this field.", MessageType.Info);
                return;
            }

            // === GUI BAKING SETTINGS REGION ===
            // if we have a valid target meshfilter with an Alembic Stream Player parent...
            // (note we have already returned if we do not have a player)
            if (m_baker.TargetMeshFilter != null)
            {
                // we have a valid mesh which is a part of an Alembic model!

                EditorGUILayout.Space();

                // === GUI FRAMERATE REGION ===
                m_baker.FrameRate = EditorGUILayout.Slider("Frame Rate", m_baker.FrameRate, .1f, 60f);

                EditorGUILayout.Space();

                // === GUI TIME RANGE REGION ===
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Time Range (s)");
                EditorGUIUtility.labelWidth = 35f;
                m_baker.StartTime = EditorGUILayout.FloatField("from", m_baker.StartTime, GUILayout.MinWidth(80f));
                EditorGUIUtility.labelWidth = 20f;
                m_baker.EndTime = EditorGUILayout.FloatField("to", m_baker.EndTime, GUILayout.MinWidth(80f));
                EditorGUIUtility.labelWidth = 0f;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.MinMaxSlider(ref m_baker.StartTime, ref m_baker.EndTime, 0f, m_baker.TargetAlembicStreamPlayer.Duration);

                EditorGUILayout.Space();

                // === GUI NORMALS REGION ===
                m_baker.includeNormals = EditorGUILayout.Toggle("Bake vertex normals", m_baker.includeNormals);

                EditorGUILayout.Space();

                // === GUI OUTPUT REGION ===
                m_baker.OutputPath = EditorGUILayout.TextField("Output folder", m_baker.OutputPath);
                GUI.enabled = !m_automaticOutputName;
                m_baker.OutputName = EditorGUILayout.TextField("Output name", m_baker.OutputName);
                GUI.enabled = true;
                m_automaticOutputName = EditorGUILayout.Toggle("Automatic output name", m_automaticOutputName);

                EditorGUILayout.Space();

                // === GUI TEXTURES ONLY REGION ===
                m_baker.TexturesOnly = EditorGUILayout.Toggle("Textures only", m_baker.TexturesOnly);

                // info boxes when assets already exist
                // only display prefab-related info boxes when not in textures only mode
                if (!m_baker.TexturesOnly && m_baker.MeshExists) EditorGUILayout.HelpBox($"A mesh already exists at {m_baker.MeshPath}", MessageType.Info);
                if (m_baker.MotionTextureExists) EditorGUILayout.HelpBox($"A motion texture already exists at { m_baker.MotionTexturePath}", MessageType.Info);
                if (m_baker.NormalTextureExists) EditorGUILayout.HelpBox($"A normal texture already exists at {m_baker.NormalTexturePath}", MessageType.Info);
                if (!m_baker.TexturesOnly && m_baker.MaterialExists) EditorGUILayout.HelpBox($"A material already exists at {m_baker.MaterialPath}", MessageType.Info);
                if (!m_baker.TexturesOnly && m_baker.PrefabExists) EditorGUILayout.HelpBox($"A prefab already exists at {m_baker.PrefabPath}", MessageType.Info);
            }
        }
    }
}