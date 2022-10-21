using UnityEditor;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

namespace StoryLabARU.AlembicVAT
{
    public class AlembicVATBakerWindow : EditorWindow
    {
        private AlembicVATBaker m_baker;
        private SerializedObject m_serializedBaker;

        private bool m_automaticOutputName = true;

        [MenuItem("Window/Alembic VAT Baker")]
        public static void ShowWindow() => GetWindow(typeof(AlembicVATBakerWindow));

        public void OnGUI()
        {
            // we check this every time round because actions such as entering or exiting playmode can destroy these instances
            if (m_baker == null) m_baker = CreateInstance<AlembicVATBaker>();
            // create a serialization of the generator for the GUI to be able to access property fields
            if (m_serializedBaker == null || m_serializedBaker.targetObject == null) m_serializedBaker = new SerializedObject(m_baker);

            // ignoring nasty conditional for now since we don't have an implementation for the full model workflow yet
            GUI.enabled = m_baker.TargetMeshFilter != null && m_baker.TargetAlembicStreamPlayer != null;
            if (GUILayout.Button("Bake", GUILayout.Height(32))) m_baker.Bake();
            GUI.enabled = true;

            EditorGUILayout.Space();

            // === mesh selection region ===
            MeshFilter lastTargetMeshFilter = m_baker.TargetMeshFilter;
            EditorGUILayout.PropertyField(m_serializedBaker.FindProperty(nameof(m_baker.TargetMeshFilter)));

            // set the properties on the object the serializedobject is inspecting
            m_serializedBaker.ApplyModifiedProperties();

            // if the selected meshfilter has changed...
            if (lastTargetMeshFilter != m_baker.TargetMeshFilter)
            {
                UpdateAlembicStreamPlayer();
                if (m_automaticOutputName) m_baker.OutputName = m_baker.TargetMeshFilter.name;
            }

            // if the selected mesh filter is not a child of an Alemic Stream Player (or does not exist)...
            if (m_baker.TargetAlembicStreamPlayer == null)
            {
                EditorGUILayout.HelpBox("Add the Alembic model to a scene, then select the child MeshFilter component you want to bake the animation from and assign it to this field.", MessageType.Info);
                return;
            }

            // if we have a valid target meshfilter with an Alembic Stream Player parent...
            if (m_baker.TargetMeshFilter != null)
            {
                // we have a valid mesh which is a part of an Alembic model!

                EditorGUILayout.Space();

                // === framerate region ===
                m_baker.FrameRate = EditorGUILayout.Slider("Frame Rate", m_baker.FrameRate, .1f, 60f);

                EditorGUILayout.Space();

                // === time range region ===
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Time Range (s)");
                EditorGUIUtility.labelWidth = 35f;
                EditorGUILayout.FloatField("from", m_baker.StartTime, GUILayout.MinWidth(80f));
                EditorGUIUtility.labelWidth = 20f;
                EditorGUILayout.FloatField("to", m_baker.EndTime, GUILayout.MinWidth(80f));
                EditorGUIUtility.labelWidth = 0f;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.MinMaxSlider(ref m_baker.StartTime, ref m_baker.EndTime, 0f, m_baker.TargetAlembicStreamPlayer.Duration);

                EditorGUILayout.Space();

                // === output region ===
                m_baker.OutputPath = EditorGUILayout.TextField("Output folder", m_baker.OutputPath);
                GUI.enabled = !m_automaticOutputName;
                m_baker.OutputName = EditorGUILayout.TextField("Output name", m_baker.OutputName);
                GUI.enabled = true;
                m_automaticOutputName = EditorGUILayout.Toggle("Automatic output name", m_automaticOutputName);

                // info boxes when assets already exist
                if (m_baker.MeshExists) EditorGUILayout.HelpBox($"A mesh already exists at {m_baker.MeshPath}", MessageType.Info);
                if (m_baker.MotionTextureExists) EditorGUILayout.HelpBox($"A motion texture already exists at { m_baker.MotionTexturePath}", MessageType.Info);
                if (m_baker.NormalTextureExists) EditorGUILayout.HelpBox($"A normal texture already exists at {m_baker.NormalTexturePath}", MessageType.Info);
                if (m_baker.MaterialExists) EditorGUILayout.HelpBox($"A material already exists at {m_baker.MaterialPath}", MessageType.Info);
                if (m_baker.PrefabExists) EditorGUILayout.HelpBox($"A prefab already exists at {m_baker.PrefabPath}", MessageType.Info);
            }
        }

        private void UpdateAlembicStreamPlayer()
        {
            // check the meshfilter's parents for an Alembic Stream Player
            // set the AlembicStreamPlayer to null initially so if the TargetMeshFilter is null or if we never find an AlembicStreamPlayer we can return null
            m_baker.TargetAlembicStreamPlayer = null;
            if (m_baker.TargetMeshFilter != null)
            {
                Transform parent = m_baker.TargetMeshFilter.transform.parent;
                while (parent != null)
                {
                    m_baker.TargetAlembicStreamPlayer = parent.GetComponent<AlembicStreamPlayer>();
                    if (m_baker.TargetAlembicStreamPlayer != null)
                    {
                        m_baker.StartTime = 0;
                        m_baker.EndTime = m_baker.TargetAlembicStreamPlayer.Duration;
                        break;
                    }
                    parent = parent.parent;
                }
            }
        }
    }
}