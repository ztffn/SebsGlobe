using UnityEngine;
using UnityEditor;

namespace SebsGlobe.Clouds
{
    [CustomEditor(typeof(CloudSystem))]
    public class CloudSystemEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            CloudSystem cloudSystem = (CloudSystem)target;
            
            EditorGUILayout.Space();
            
            // Prominent respawn button
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Cloud Control", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Respawn Cloud Particles", GUILayout.Height(30)))
            {
                if (Application.isPlaying)
                {
                    Undo.RecordObject(cloudSystem, "Respawn Clouds");
                    cloudSystem.SpawnCloudParticles();
                    EditorUtility.SetDirty(cloudSystem);
                    serializedObject.ApplyModifiedProperties();
                }
                else
                {
                    EditorUtility.DisplayDialog("Cloud System", "Respawn only works in Play Mode!", "OK");
                }
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Draw default inspector
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            
            EditorGUILayout.Space();
            
            // Add helpful info box
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Setup Checklist:", EditorStyles.boldLabel);
            
            // Check if references are assigned
            SerializedProperty computeShaderProp = serializedObject.FindProperty("cloudComputeShader");
            SerializedProperty materialProp = serializedObject.FindProperty("cloudMaterial");
            SerializedProperty playerProp = serializedObject.FindProperty("player");
            
            bool computeShaderAssigned = computeShaderProp.objectReferenceValue != null;
            bool materialAssigned = materialProp.objectReferenceValue != null;
            bool playerAssigned = playerProp.objectReferenceValue != null;
            
            DrawChecklistItem("Compute Shader Assigned", computeShaderAssigned);
            DrawChecklistItem("Cloud Material Assigned", materialAssigned);
            DrawChecklistItem("Player Transform Assigned", playerAssigned);
            
            if (computeShaderAssigned && materialAssigned && playerAssigned)
            {
                EditorGUILayout.HelpBox("✓ Cloud system is ready to use!", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("⚠ Missing required references. Check the Setup Instructions in the README.", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Performance info
            if (Application.isPlaying)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Runtime Info:", EditorStyles.boldLabel);
                
                SerializedProperty particleCountProp = serializedObject.FindProperty("particleCount");
                int particleCount = particleCountProp.intValue;
                
                EditorGUILayout.LabelField($"Total Particles: {particleCount:N0}");
                EditorGUILayout.LabelField($"Estimated Memory: ~{(particleCount * 40) / 1024f:F1} KB");
                
                EditorGUILayout.EndVertical();
            }
        }
        
        private void DrawChecklistItem(string label, bool isComplete)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(isComplete ? "✓" : "✗", GUILayout.Width(20));
            EditorGUILayout.LabelField(label);
            EditorGUILayout.EndHorizontal();
        }
    }
} 