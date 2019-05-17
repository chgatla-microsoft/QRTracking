using UnityEngine;
using UnityEditor;
using System.Collections;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.XR.WSA
{
    [MovedFrom("UnityEngine.VR.WSA")]
    public class SpatialMappingBaseInspector : Editor
    {
        private static readonly GUIContent s_GeneralSettingsLabelContent = new GUIContent("General Settings");
        private static readonly GUIContent s_SurfaceParentLabelContent = new GUIContent("Surface Parent", "All surface mesh GameObjects will be children of the surface parent.  If no surface parent has been assigned, a surface parent will be generated.");
        private static readonly GUIContent s_FreezeUpdatesLabelContent = new GUIContent("Freeze Updates");
        private static readonly GUIContent s_TimeBetweenUpdatesLabelContent = new GUIContent("Time Between Updates", "Time, in seconds, to wait between spatial mapping updates.");
        private static readonly GUIContent s_RemoveSurfacesLabelContent = new GUIContent("Remove Surfaces Immediately");
        private static readonly GUIContent s_RemovalUpdateCountLabelContent = new GUIContent("Removal Update Count", "Total number of updates before a surface is removed when it is no longer in the surface observer's bounding volume.");
        private static readonly GUIContent s_LODLabelContent = new GUIContent("Level Of Detail", "The quality of the resulting mesh. Lower is better for performance and physics while higher will look more accurate and is better for rendering.");
        private static readonly GUIContent s_BoundingVolumeLabelContent = new GUIContent("Bounding Volume Type", "The shape of the bounds for the observed region.");
        private static readonly GUIContent s_RadiusInMetersLabelContent = new GUIContent("Radius In Meters", "The radius of the observation sphere volume in meters.");
        private static readonly GUIContent s_BoxExtentsInMetersLabelContent = new GUIContent("Half Extents In Meters", "The extents of the observation volume in meters.");

        private SpatialMappingBase m_SMBase = null;

        private SerializedProperty m_SurfaceParentProp = null;
        private SerializedProperty m_FreezeUpdatesProp = null;
        private SerializedProperty m_SecondsBetweenUpdatesProp = null;
        private SerializedProperty m_NumUpdatesBeforeRemovalProp = null;
        private SerializedProperty m_LodProp = null;
        private SerializedProperty m_VolumeProp = null;
        private SerializedProperty m_SphereRadiusProp = null;
        private SerializedProperty m_HalfBoxExtentsProp = null;

        protected virtual void OnEnable()
        {
            m_SMBase = this.target as SpatialMappingBase;

            m_SurfaceParentProp = this.serializedObject.FindProperty("m_SurfaceParent");
            m_FreezeUpdatesProp = this.serializedObject.FindProperty("m_FreezeUpdates");
            m_SecondsBetweenUpdatesProp = this.serializedObject.FindProperty("m_SecondsBetweenUpdates");
            m_NumUpdatesBeforeRemovalProp = this.serializedObject.FindProperty("m_NumUpdatesBeforeRemoval");
            m_LodProp = this.serializedObject.FindProperty("m_LodType");
            m_VolumeProp = this.serializedObject.FindProperty("m_VolumeType");
            m_SphereRadiusProp = this.serializedObject.FindProperty("m_SphereRadius");
            m_HalfBoxExtentsProp = this.serializedObject.FindProperty("m_HalfBoxExtents");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField(s_GeneralSettingsLabelContent, EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(m_SurfaceParentProp, s_SurfaceParentLabelContent);
            EditorGUILayout.PropertyField(m_FreezeUpdatesProp, s_FreezeUpdatesLabelContent);
            EditorGUILayout.PropertyField(m_SecondsBetweenUpdatesProp, s_TimeBetweenUpdatesLabelContent);

            if (m_NumUpdatesBeforeRemovalProp.intValue <= 0)
            {
                Rect removeImmediatelyRect = EditorGUILayout.GetControlRect(true);
                EditorGUI.BeginProperty(removeImmediatelyRect, s_RemoveSurfacesLabelContent, m_NumUpdatesBeforeRemovalProp);

                bool removeImmediately = EditorGUI.Toggle(removeImmediatelyRect, s_RemoveSurfacesLabelContent, m_SMBase.numUpdatesBeforeRemoval <= 0);
                if (removeImmediately)
                {
                    m_NumUpdatesBeforeRemovalProp.intValue = 0;
                }
                else
                {
                    m_NumUpdatesBeforeRemovalProp.intValue = 1;
                }

                EditorGUI.EndProperty();
            }
            else
            {
                EditorGUILayout.PropertyField(m_NumUpdatesBeforeRemovalProp, s_RemovalUpdateCountLabelContent);
            }

            EditorGUILayout.PropertyField(m_LodProp, s_LODLabelContent);
            EditorGUILayout.PropertyField(m_VolumeProp, s_BoundingVolumeLabelContent);

            SpatialMappingBase.VolumeType volumeType = (SpatialMappingBase.VolumeType)m_VolumeProp.enumValueIndex;
            if (volumeType == SpatialMappingBase.VolumeType.Sphere)
            {
                Rect sphereRadiusRect = EditorGUILayout.GetControlRect(true);
                EditorGUI.BeginProperty(sphereRadiusRect, s_RadiusInMetersLabelContent, m_SphereRadiusProp);

                float originalSphereRadiusMeters = m_SphereRadiusProp.floatValue;
                m_SphereRadiusProp.floatValue = EditorGUI.FloatField(sphereRadiusRect, s_RadiusInMetersLabelContent, m_SphereRadiusProp.floatValue);

                if (m_SphereRadiusProp.floatValue < Mathf.Epsilon)
                {
                    m_SphereRadiusProp.floatValue = originalSphereRadiusMeters;
                }

                EditorGUI.EndProperty();
            }
            else
            {
                EditorGUILayout.PropertyField(m_HalfBoxExtentsProp, s_BoxExtentsInMetersLabelContent);
            }
        }
    }
}
