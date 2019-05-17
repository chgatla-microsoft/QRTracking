using UnityEngine;
using UnityEditor;
using System.Collections;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.XR.WSA
{
    [MovedFrom("UnityEngine.VR.WSA")]
    [CanEditMultipleObjects]
    [CustomEditor(typeof(SpatialMappingRenderer))]
    public class SpatialMappingRendererInspector : SpatialMappingBaseInspector
    {
        static readonly string s_VariableOcclusionMaterial = "m_OcclusionMaterial";
        static readonly string s_VariableVisualMaterial = "m_VisualMaterial";

        private static readonly GUIContent s_RenderSettingsLabelContent = new GUIContent("Render Settings");
        private static readonly GUIContent s_RenderStateLabelContent = new GUIContent("Render State", "This field specifies the material that should be applied to all surfaces.");
        private static readonly GUIContent s_OcclusionMaterialLabelContent = new GUIContent("Occlusion Material", "The occlusion material is intended to occlude holograms that should be hidden from the user.");
        private static readonly GUIContent s_CustomMaterialLabelContent = new GUIContent("Visual Material", "The visual material is intended to be used for the purpose of visualizing the surfaces.");
        private static readonly string s_OcclusionMaterialInUseMsg = "The occlusion render state will use the occlusion material.";
        private static readonly string s_VisualizationMaterialInUseMsg = "The visualization render state will use the visualization material.";
        private static readonly string s_NoMaterialInUseMsg = "No material is in use.  Nothing will be rendered.";

        private SerializedProperty m_RenderStateProp = null;
        private SerializedProperty m_OcculsionMaterialProp = null;
        private SerializedProperty m_VisualMaterialProp = null;

        private SpatialMappingRenderer m_SMRenderer = null;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_SMRenderer = target as SpatialMappingRenderer;
            CacheSerializedProperties();
        }

        public override void OnInspectorGUI()
        {
            this.serializedObject.Update();

            ManageRenderSettings();
            EditorGUILayout.Separator();
            base.OnInspectorGUI();

            this.serializedObject.ApplyModifiedProperties();
        }

        void CacheSerializedProperties()
        {
            m_RenderStateProp = this.serializedObject.FindProperty("m_CurrentRenderState");
            m_OcculsionMaterialProp = this.serializedObject.FindProperty(s_VariableOcclusionMaterial);
            m_VisualMaterialProp = this.serializedObject.FindProperty(s_VariableVisualMaterial);
        }

        void ManageRenderSettings()
        {
            EditorGUILayout.LabelField(s_RenderSettingsLabelContent, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_RenderStateProp, s_RenderStateLabelContent);

            if (m_SMRenderer.renderState == SpatialMappingRenderer.RenderState.Occlusion)
            {
                EditorGUILayout.HelpBox(s_OcclusionMaterialInUseMsg, MessageType.Info);
            }
            else if (m_SMRenderer.renderState == SpatialMappingRenderer.RenderState.Visualization)
            {
                EditorGUILayout.HelpBox(s_VisualizationMaterialInUseMsg, MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(s_NoMaterialInUseMsg, MessageType.Info);
            }

            EditorGUILayout.PropertyField(m_OcculsionMaterialProp, s_OcclusionMaterialLabelContent);
            EditorGUILayout.PropertyField(m_VisualMaterialProp, s_CustomMaterialLabelContent);
        }
    }
}
