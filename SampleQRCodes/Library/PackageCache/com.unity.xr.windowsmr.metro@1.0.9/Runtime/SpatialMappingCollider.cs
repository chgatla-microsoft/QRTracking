using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.WSA;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.XR.WSA
{
    [MovedFrom("UnityEngine.VR.WSA")]
    [AddComponentMenu("XR/Spatial Mapping Collider", 12)]
    public class SpatialMappingCollider : SpatialMappingBase
    {
        // Set the mesh layer to the default layer, which is 1, by default.
        [SerializeField]
        private int m_Layer = 0;

        public int layer
        {
            get
            {
                return m_Layer;
            }

            set
            {
                m_Layer = value;
                ApplyPropertiesToCachedSurfaces();
            }
        }

        [SerializeField]
        private PhysicMaterial m_Material;

        public PhysicMaterial material
        {
            get
            {
                return m_Material;
            }

            set
            {
                m_Material = value;
                ApplyPropertiesToCachedSurfaces();
            }
        }

        [SerializeField]
        bool m_EnableCollisions = true;

        public bool enableCollisions
        {
            get
            {
                return m_EnableCollisions;
            }

            set
            {
                m_EnableCollisions = value;
                ApplyPropertiesToCachedSurfaces();
            }
        }

        protected override void Awake()
        {
            bakePhysics = true;
        }

        protected override void OnSurfaceDataReady(SpatialMappingBase requester, SurfaceData bakedData, bool outputWritten, float elapsedBakeTimeSeconds)
        {
            SpatialMappingBase.Surface surfaceData;
            if (!surfaceObjects.TryGetValue(bakedData.id.handle, out surfaceData))
            {
                // If we don't have the surface, ignore it because we may never
                // receive a removal for it.  And then it will be a zombie.
                return;
            }

            // Let the component know that the current surface does not
            // need to be baked again until the system says the surface
            // has been updated.
            surfaceData.awaitingBake = false;

            if (!outputWritten)
            {
                return;
            }

            if (surfaceData.gameObject == null)
            {
                Debug.LogError(string.Format("A SpatialMappingCollider component can not apply baked data to the surface with id \"{0}\" because its GameObject is null.", bakedData.id.handle));
                return;
            }

            if (bakedData.outputCollider == null)
            {
                return;
            }

            if (requester != this)
            {
                CloneBakedComponents(bakedData, surfaceData);
            }

            bakedData.outputCollider.gameObject.layer = layer;

            if (material != null)
            {
                bakedData.outputCollider.material = material;
            }
        }

        protected override void OnBeginSurfaceEviction(bool shouldBeActiveWhileRemoved, SpatialMappingBase.Surface surfaceData)
        {
            if (surfaceData.gameObject == null)
            {
                return;
            }

            if (surfaceData.meshCollider == null)
            {
                return;
            }

            surfaceData.meshCollider.enabled = shouldBeActiveWhileRemoved;
        }

        protected override void UpdateSurfaceData(Surface surface)
        {
            base.UpdateSurfaceData(surface);

            SurfaceData tempSurfaceData = surface.surfaceData;
            tempSurfaceData.bakeCollider = bakePhysics;
            tempSurfaceData.outputCollider = surface.meshCollider;
            surface.surfaceData = tempSurfaceData;
        }

        protected override void AddRequiredComponentsForBaking(Surface surface)
        {
            base.AddRequiredComponentsForBaking(surface);

            if (surface.meshCollider == null)
            {
                surface.meshCollider = surface.gameObject.AddComponent<MeshCollider>() as MeshCollider;
            }

            SurfaceData tempSurfaceData = surface.surfaceData;
            tempSurfaceData.outputCollider = surface.meshCollider;
            surface.surfaceData = tempSurfaceData;
        }

        protected void ApplyPropertiesToCachedSurfaces()
        {
            if (material == null)
            {
                return;
            }

            ForEachSurfaceInCache(delegate(SpatialMappingBase.Surface surface)
                {
                    if (surface.meshCollider == null)
                    {
                        return;
                    }

                    if (surface.gameObject != null)
                    {
                        if (surface.gameObject.layer != layer)
                        {
                            surface.gameObject.layer = layer;
                        }
                    }

                    if (surface.meshCollider.material != material)
                    {
                        surface.meshCollider.material = material;
                    }

                    if (surface.meshCollider.enabled != enableCollisions)
                    {
                        surface.meshCollider.enabled = enableCollisions;
                    }
                });
        }

        protected override void OnResetProperties()
        {
            base.OnResetProperties();
            ApplyPropertiesToCachedSurfaces();
        }
    }
}
