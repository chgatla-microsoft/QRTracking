using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.WSA;
using UnityEngine.Scripting.APIUpdating;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.XR.WSA
{
    [MovedFrom("UnityEngine.VR.WSA")]
    [AddComponentMenu("XR/Spatial Mapping Renderer", 12)]
    public class SpatialMappingRenderer : SpatialMappingBase
    {
        public enum RenderState { None = 0, Occlusion = 1, Visualization = 2 }

        [SerializeField]
        private RenderState m_CurrentRenderState = RenderState.Occlusion;

        public RenderState renderState
        {
            get
            {
                return m_CurrentRenderState;
            }

            set
            {
                m_CurrentRenderState = value;
                ApplyPropertiesToCachedSurfaces();
            }
        }

        // visualMaterial and occlusionMaterial are template materials and
        // should never be directly applied to a surface.  This is to
        // ensure that if a new material is assigned by the user, newly
        // baked meshes will continue to use the old material until the
        // render setting has been explicitly changed by the user.

        [SerializeField]
        private Material m_VisualMaterial;

        public Material visualMaterial
        {
            get
            {
                return m_VisualMaterial;
            }

            set
            {
                m_VisualMaterial = value;
            }
        }

        [SerializeField]
        private Material m_OcclusionMaterial;

        public Material occlusionMaterial
        {
            get
            {
                return m_OcclusionMaterial;
            }

            set
            {
                m_OcclusionMaterial = value;
            }
        }

        protected override void OnSurfaceDataReady(SpatialMappingBase requester, SurfaceData bakedData, bool outputWritten, float elapsedBakeTimeSeconds)
        {
            SpatialMappingBase.Surface surface;
            if (!surfaceObjects.TryGetValue(bakedData.id.handle, out surface))
            {
                // If we don't have the surface, ignore it because we may never
                // receive a removal for it.  And then it will be a zombie.
                return;
            }

            //Debug.Log(string.Format("Baked \"{0}\" : \"{1}\"", surfaceData.m_GameObject.name, bakedData.id.handle));

            // Let the component know that the current surface does not
            // need to be baked again until the system says the surface
            // has been updated.
            surface.awaitingBake = false;

            if (!outputWritten)
            {
                return;
            }

            if (surface.gameObject == null)
            {
                Debug.LogError(string.Format("A SpatialMappingRenderer component can not apply baked data to a surface with id \"{0}\" because its GameObject is null.", bakedData.id.handle));
                return;
            }

            if (requester != this)
            {
                CloneBakedComponents(bakedData, surface);
            }

            if (surface.meshRenderer == null)
            {
                surface.meshRenderer = surface.gameObject.GetComponent<MeshRenderer>();
                if (surface.meshRenderer == null)
                {
                    surface.meshRenderer = surface.gameObject.AddComponent<MeshRenderer>();
                }
                surface.meshRenderer.receiveShadows = false;
                surface.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            ApplyRenderSettings(surface.meshRenderer);
        }

        protected override void OnBeginSurfaceEviction(bool shouldBeActiveWhileRemoved, Surface surface)
        {
            if (surface.gameObject == null)
            {
                return;
            }

            if (surface.meshRenderer == null)
            {
                return;
            }

            surface.meshRenderer.enabled = shouldBeActiveWhileRemoved;
        }

        protected void ApplyPropertiesToCachedSurfaces()
        {
            if (surfaceObjects == null || surfaceObjects.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<int, Surface> kvp in surfaceObjects)
            {
                GameObject go = kvp.Value.gameObject;
                if (go == null)
                {
                    continue;
                }

                if (kvp.Value.meshRenderer == null)
                {
                    continue;
                }

                ApplyRenderSettings(kvp.Value.meshRenderer);
            }

            foreach (KeyValuePair<int, SpatialMappingBase.Surface> kvp in pendingSurfacesForEviction)
            {
                if (kvp.Value.meshRenderer == null)
                {
                    continue;
                }

                ApplyRenderSettings(kvp.Value.meshRenderer);

                if (ShouldRemainActiveWhileBeingRemoved(kvp.Value))
                {
                    kvp.Value.meshRenderer.enabled = renderState != RenderState.None;
                }
            }
        }

        private void ApplyRenderSettings(MeshRenderer meshRenderer)
        {
            if (meshRenderer == null)
            {
                return;
            }

            switch (renderState)
            {
                case RenderState.Occlusion:

                    meshRenderer.sharedMaterial = occlusionMaterial;
                    meshRenderer.enabled = true;
                    break;

                case RenderState.Visualization:

                    meshRenderer.sharedMaterial = visualMaterial;
                    meshRenderer.enabled = true;
                    break;

                case RenderState.None:
                    meshRenderer.enabled = false;
                    break;
            }
        }

        protected override void DestroySurface(Surface surface)
        {
            if (surface.meshRenderer != null)
            {
                surface.meshRenderer.sharedMaterial = null;
                surface.meshRenderer.enabled = false;
                Destroy(surface.meshRenderer);
                surface.meshRenderer = null;
            }

            base.DestroySurface(surface);
        }

        protected override void OnDestroy()
        {
            occlusionMaterial = null;
            visualMaterial = null;

            base.OnDestroy();
        }

        protected override void OnResetProperties()
        {
            base.OnResetProperties();
            ApplyPropertiesToCachedSurfaces();
        }

        protected override void Reset()
        {
            base.Reset();
#if UNITY_EDITOR
            SetupDefaultMaterials();
#endif
        }

#if UNITY_EDITOR
        private static Material s_DefaultOcclusionMaterial = null;
        private static Material s_DefaultVisualRenderMaterial = null;

        void SetupDefaultMaterials()
        {
            if (s_DefaultOcclusionMaterial == null)
            {
                s_DefaultOcclusionMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("VR/Materials/SpatialMappingOcclusion.mat");
            }

            if (s_DefaultVisualRenderMaterial == null)
            {
                s_DefaultVisualRenderMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("VR/Materials/SpatialMappingWireframe.mat");
            }

            // If the user starts with the default occlusion material and then dynamically
            // changes the render setting to the default custom material, we need to
            // have loaded both references at tool time so Unity will build the shaders
            // into the player.
            if (occlusionMaterial == null)
            {
                occlusionMaterial = s_DefaultOcclusionMaterial;
            }

            if (visualMaterial == null)
            {
                visualMaterial = s_DefaultVisualRenderMaterial;
            }
        }

#endif // if UNITY_EDITOR
    }
}
