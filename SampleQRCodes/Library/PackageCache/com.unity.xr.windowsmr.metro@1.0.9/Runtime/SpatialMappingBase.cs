using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.WSA;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.XR.WSA
{
    [MovedFrom("UnityEngine.VR.WSA")]

    public abstract class SpatialMappingBase : MonoBehaviour
    {
        static readonly float s_MovementUpdateThresholdSqr = 0.0001f;
        static readonly float s_EvictionUpdateTickThresholdSqr = 100.0f; // 10 * 10

        static int s_ObserverIdCounter = 0;

        public delegate void SurfaceDataReadyCallback(SpatialMappingBase requester, SurfaceData bakedData, bool outputWritten, float elapsedBakeTimeSeconds);

        public enum VolumeType
        {
            Sphere = 0,
            AxisAlignedBox = 1
        }

        public enum LODType
        {
            High = 0,
            Medium = 1,
            Low = 2
        }

        public class Surface
        {
            public SurfaceId surfaceId { get; set; }
            public System.DateTime updateTime { get; set; }
            public GameObject gameObject { get; set; }
            public SurfaceData surfaceData { get; set; }
            public int remainingUpdatesToLive { get; set; }
            public bool awaitingBake { get; set; }

            public MeshFilter meshFilter { get; set; }
            public MeshRenderer meshRenderer { get; set; }
            public MeshCollider meshCollider { get; set; }
            public WorldAnchor worldAnchor { get; set; }
        }

        [SerializeField]
        GameObject m_SurfaceParent;
        public GameObject surfaceParent
        {
            get { return m_SurfaceParent; }
            set { m_SurfaceParent = value; }
        }

        [SerializeField]
        bool m_FreezeUpdates = false;
        public bool freezeUpdates
        {
            get { return m_FreezeUpdates; }
            set { m_FreezeUpdates = value; }
        }

        [SerializeField]
        VolumeType m_VolumeType = VolumeType.AxisAlignedBox;
        public VolumeType volumeType
        {
            get { return m_VolumeType; }
            set { m_VolumeType = value; }
        }

        [SerializeField]
        float m_SphereRadius = 2.0f;
        public float sphereRadius
        {
            get { return m_SphereRadius; }
            set { m_SphereRadius = value; }
        }

        [SerializeField]
        Vector3 m_HalfBoxExtents = Vector3.one * 4.0f;
        public Vector3 halfBoxExtents
        {
            get { return m_HalfBoxExtents; }
            set { m_HalfBoxExtents = value; }
        }

        [SerializeField]
        LODType m_LodType = LODType.Medium;
        public LODType lodType
        {
            get { return m_LodType; }
            set { m_LodType = value; }
        }

        [SerializeField]
        int m_NumUpdatesBeforeRemoval = 10;
        public int numUpdatesBeforeRemoval
        {
            get { return m_NumUpdatesBeforeRemoval; }
            set { m_NumUpdatesBeforeRemoval = value; }
        }

        [SerializeField]
        float m_SecondsBetweenUpdates = 2.5f;
        public float secondsBetweenUpdates
        {
            get { return m_SecondsBetweenUpdates; }
            set { m_SecondsBetweenUpdates = value; }
        }

        protected bool m_BakePhysics = false;
        public bool bakePhysics
        {
            get
            {
                return m_BakePhysics;
            }

            protected set
            {
                m_BakePhysics = value;
            }
        }
        protected int observerId { get; set; }
        protected SurfaceObserver surfaceObserver { get; set; }
        protected Dictionary<int, Surface> surfaceObjects { get; set; }
        protected Bounds bounds { get; set; }
        protected Vector3 lastUpdatedObserverPosition { get; set; }
        protected Camera selectedCamera { get; set; }
        protected float nextSurfaceChangeUpdateTime { get; set; }

        private Dictionary<int, Surface> m_PendingSurfacesForEviction = new Dictionary<int, Surface>();
        protected Dictionary<int, Surface> pendingSurfacesForEviction
        {
            get
            {
                return m_PendingSurfacesForEviction;
            }

            set
            {
                m_PendingSurfacesForEviction = value;
            }
        }

        private List<int> m_SurfacesToRemoveFromDict = new List<int>();
        protected List<int> surfacesToRemoveFromDict
        {
            get
            {
                return m_SurfacesToRemoveFromDict;
            }

            set
            {
                m_SurfacesToRemoveFromDict = value;
            }
        }

        protected bool m_SurfaceParentWasDynamicallyCreated = false;
        protected bool surfaceParentWasDynamicallyCreated
        {
            get
            {
                return m_SurfaceParentWasDynamicallyCreated;
            }

            set
            {
                m_SurfaceParentWasDynamicallyCreated = value;
            }
        }

        private static readonly int[] s_LodToPcm = { 2000, 750, 200 };
        protected static int[] lodToPcm
        {
            get
            {
                return s_LodToPcm;
            }
        }

        private SurfaceData bestSurfaceDataNull = new SurfaceData();

        public static LODType GetLODFromTPCM(double trianglesPerCubicMeter)
        {
            if (trianglesPerCubicMeter >= 1999.0)
            {
                return LODType.High;
            }
            else if (trianglesPerCubicMeter >= 749.0 && trianglesPerCubicMeter <= 751.0)
            {
                return LODType.Medium;
            }

            return LODType.Low;
        }

        // In the future, we may need to put logic in the awake
        // method.  In that case, we will want derived classes to
        // override the functionality.
        protected virtual void Awake()
        {
        }

        protected virtual void Start()
        {
            observerId = s_ObserverIdCounter++;
            surfaceObjects = new Dictionary<int, Surface>();
            selectedCamera = Camera.main;

            // Guarantee we update immediately when the component starts up.
            nextSurfaceChangeUpdateTime = float.MinValue;

            surfaceObserver = new SurfaceObserver();
            SpatialMappingContext.Instance.RegisterComponent(this, OnSurfaceDataReady, TryGetHighestPriorityRequest, surfaceObserver);
            bounds = new Bounds(this.transform.position, halfBoxExtents);

            UpdatePosition();
        }

        protected virtual void OnEnable()
        {
            if (surfaceObjects != null && surfaceObjects.Count > 0)
            {
                foreach (KeyValuePair<int, Surface> kvp in surfaceObjects)
                {
                    if (kvp.Value.gameObject != null)
                    {
                        kvp.Value.gameObject.SetActive(true);
                    }
                }
            }

            if (pendingSurfacesForEviction != null && pendingSurfacesForEviction.Count > 0)
            {
                foreach (KeyValuePair<int, Surface> kvp in pendingSurfacesForEviction)
                {
                    if (kvp.Value.gameObject == null)
                    {
                        Debug.LogWarning(string.Format("Can not activate the surface id \"{0}\" because its GameObject is null.", kvp.Key));
                        continue;
                    }

                    kvp.Value.gameObject.SetActive(true);
                }
            }
        }

        protected virtual void OnDisable()
        {
            if (surfaceObjects != null && surfaceObjects.Count > 0)
            {
                foreach (KeyValuePair<int, Surface> kvp in surfaceObjects)
                {
                    if (kvp.Value.gameObject != null)
                    {
                        kvp.Value.gameObject.SetActive(false);
                    }
                }
            }

            if (pendingSurfacesForEviction != null && pendingSurfacesForEviction.Count > 0)
            {
                foreach (KeyValuePair<int, Surface> kvp in pendingSurfacesForEviction)
                {
                    if (kvp.Value.gameObject == null)
                    {
                        continue;
                    }

                    kvp.Value.gameObject.SetActive(false);
                }
            }
        }

        protected virtual void OnDestroy()
        {
            SpatialMappingContext.Instance.DeregisterComponent(this);

            if (surfaceObjects != null && surfaceObjects.Count > 0)
            {
                foreach (KeyValuePair<int, Surface> kvp in surfaceObjects)
                {
                    DestroySurface(kvp.Value);
                }

                surfaceObjects.Clear();
            }

            if (pendingSurfacesForEviction != null && pendingSurfacesForEviction.Count > 0)
            {
                foreach (KeyValuePair<int, Surface> kvp in pendingSurfacesForEviction)
                {
                    if (kvp.Value.gameObject == null)
                    {
                        continue;
                    }

                    DestroySurface(kvp.Value);
                }

                pendingSurfacesForEviction.Clear();
            }

            if (surfaceParentWasDynamicallyCreated)
            {
                Destroy(surfaceParent);
                surfaceParent = null;
            }

            surfaceObserver.Dispose();
            surfaceObserver = null;
        }

        protected virtual void Update()
        {
            if (Vector3.SqrMagnitude(lastUpdatedObserverPosition - this.transform.position) > s_MovementUpdateThresholdSqr)
            {
                UpdatePosition();
            }

            if (!freezeUpdates)
            {
                if (Time.time >= nextSurfaceChangeUpdateTime)
                {
                    surfaceObserver.Update(OnSurfaceChanged);
                    ProcessEvictedObjects();
                    nextSurfaceChangeUpdateTime = Time.time + secondsBetweenUpdates;
                    SpatialMappingContext.Instance.ComponentHasDataRequests();
                }
            }
        }

        protected void UpdatePosition()
        {
            if (volumeType == VolumeType.Sphere)
            {
                surfaceObserver.SetVolumeAsSphere(this.transform.position, sphereRadius);
            }
            else if (volumeType == VolumeType.AxisAlignedBox)
            {
                surfaceObserver.SetVolumeAsAxisAlignedBox(this.transform.position, halfBoxExtents);
                Bounds tempBounds = bounds;
                tempBounds.center = this.transform.position;
                tempBounds.extents = halfBoxExtents;
                bounds = tempBounds;
            }

            lastUpdatedObserverPosition = this.transform.position;
        }

        // delegate for receiving surface change data from the scripting API SurfaceObserver
        private void OnSurfaceChanged(SurfaceId surfaceId, SurfaceChange changeType, Bounds bounds, System.DateTime updateTime)
        {
            switch (changeType)
            {
                case SurfaceChange.Added:
                case SurfaceChange.Updated:
                    OnAddOrUpdateSurface(surfaceId, updateTime, bakePhysics);
                    break;

                case SurfaceChange.Removed:
                    OnRemoveSurface(surfaceId);
                    break;

                default:
                    break;
            }
        }

        // create new surface records as needed and add the specified surface to the dictionary
        private void OnAddOrUpdateSurface(SurfaceId surfaceId, System.DateTime updateTime, bool bakePhysics)
        {
            Surface surface = null;

            // If the surface is pending for removal, we should remove it
            // from the removal list and place it back in the active surface list.
            if (pendingSurfacesForEviction.ContainsKey(surfaceId.handle))
            {
                surfaceObjects[surfaceId.handle] = pendingSurfacesForEviction[surfaceId.handle];
                pendingSurfacesForEviction.Remove(surfaceId.handle);
            }
            else if (!surfaceObjects.ContainsKey(surfaceId.handle))
            {
                surface = CreateSurface(surfaceId);
                surface.surfaceData = new SurfaceData();

                surfaceObjects.Add(surfaceId.handle, surface);
            }

            if (surface == null)
            {
                surface = surfaceObjects[surfaceId.handle];
            }

            SurfaceData tempSurfaceData = surface.surfaceData;
            tempSurfaceData.id = surfaceId;
            tempSurfaceData.bakeCollider = bakePhysics;
            tempSurfaceData.trianglesPerCubicMeter = lodToPcm[(int)lodType];
            surface.surfaceData = tempSurfaceData;

            surface.awaitingBake = true;
            surface.updateTime = updateTime;

            AddRequiredComponentsForBaking(surface);
        }

        private Surface CreateSurface(SurfaceId surfaceId)
        {
            Surface surface = new Surface();
            surface.surfaceId = surfaceId;
            surface.awaitingBake = false;

            return surface;
        }

        protected void CloneBakedComponents(SurfaceData bakedData, Surface target)
        {
            if (target == null)
            {
                return;
            }

            if (bakedData.outputMesh != null && target.meshFilter != null)
            {
                Destroy(target.meshFilter.mesh);
                target.meshFilter.mesh = bakedData.outputMesh.sharedMesh;
            }
        }

        protected virtual void AddRequiredComponentsForBaking(Surface surface)
        {
            if (surfaceParent == null)
            {
                surfaceParent = new GameObject(string.Format("Surface Parent{0}", observerId));
                surfaceParentWasDynamicallyCreated = true;
            }

            if (surface.gameObject == null)
            {
                // be resilient in the face of users manually destroying spatial mapping sub-objects.
                surface.gameObject = new GameObject(string.Format("spatial-mapping-surface{0}_{1}", observerId, surface.surfaceId.handle));
                surface.gameObject.transform.parent = surfaceParent.transform;
            }

            if (surface.meshFilter == null)
            {
                surface.meshFilter = surface.gameObject.GetComponent<MeshFilter>();
                if (surface.meshFilter == null)
                {
                    surface.meshFilter = surface.gameObject.AddComponent<MeshFilter>();
                }
            }

            SurfaceData tempSurfaceData = surface.surfaceData;
            tempSurfaceData.outputMesh = surface.meshFilter;
            if (surface.worldAnchor == null)
            {
                surface.worldAnchor = surface.gameObject.GetComponent<WorldAnchor>();
                if (surface.worldAnchor == null)
                {
                    surface.worldAnchor = surface.gameObject.AddComponent<WorldAnchor>();
                }
            }
            tempSurfaceData.outputAnchor = surface.worldAnchor;
            surface.surfaceData = tempSurfaceData;
        }

        // remove the specified surface object from the in-use dictionary and add it to the removal
        // dictionary.  The removal dictionary will cache this object until it is either re-added to the in-use set or until it times out and is destroyed.
        protected void OnRemoveSurface(SurfaceId surfaceId)
        {
            if (surfaceObjects == null || surfaceObjects.Count == 0)
            {
                return;
            }

            Surface sd;
            if (!surfaceObjects.TryGetValue(surfaceId.handle, out sd))
            {
                Debug.LogWarning(string.Format("Can not remove the surface id \"{0}\" because it is not an active surface.", surfaceId.handle));
                return;
            }

            surfaceObjects.Remove(surfaceId.handle);

            // If the user wants the surface to be destroyed immediately,
            // they can set the NumUpdatesBeforeRemoval to 0.  Otherwise we should
            // wait the correct number of updates before destroying it.
            if (numUpdatesBeforeRemoval < 1)
            {
                DestroySurface(sd);
                return;
            }

            OnBeginSurfaceEviction(ShouldRemainActiveWhileBeingRemoved(sd), sd);

            sd.remainingUpdatesToLive = numUpdatesBeforeRemoval + 1;
            pendingSurfacesForEviction.Add(surfaceId.handle, sd);
        }

        protected abstract void OnBeginSurfaceEviction(bool shouldBeActiveWhileRemoved, Surface surface);

        protected bool ShouldRemainActiveWhileBeingRemoved(Surface surface)
        {
            if (surface.gameObject == null)
            {
                return false;
            }

            // If this is parented to the main camera, we have to disable it regardless of it being within bounds.
            // If we don't, the mesh could appear to be locked to the user's head which can make them feel sick.
            GameObject mainCameraGameObject = selectedCamera.gameObject;

            bool parentedToCamera = surface.gameObject == mainCameraGameObject;
            Transform currentTransform = surface.gameObject.transform.parent;
            while (!parentedToCamera && currentTransform != null)
            {
                if (currentTransform.gameObject == mainCameraGameObject)
                {
                    parentedToCamera = true;
                    break;
                }

                currentTransform = currentTransform.parent;
            }

            if (parentedToCamera == true)
            {
                return false;
            }

            // If the device thinks the surface has been removed and it is
            // within our surface bounding volume, we should disable it
            // incase it gets updated soon after.
            if (BoundsContains(surface.gameObject.transform.position))
            {
                return false;
            }

            return true;
        }

        protected bool BoundsContains(Vector3 position)
        {
            if (volumeType == VolumeType.Sphere)
            {
                if (Vector3.SqrMagnitude(position - this.transform.position) <= sphereRadius * sphereRadius)
                {
                    return true;
                }
            }
            else if (volumeType == VolumeType.AxisAlignedBox)
            {
                return bounds.Contains(position);
            }

            return false;
        }

        // data ready event called by the context to inform the component that data is available from the request queue
        // This method should be implemented by derived components.
        protected abstract void OnSurfaceDataReady(SpatialMappingBase requester, SurfaceData bakedData, bool outputWritten, float elapsedBakeTimeSeconds);

        // destroy this object cleanup all associated resources
        protected virtual void DestroySurface(Surface surface)
        {
            surface.remainingUpdatesToLive = -1;

            if (surface.meshFilter != null)
            {
                if (surface.meshFilter.mesh != null)
                {
                    Destroy(surface.meshFilter.mesh);
                }
            }

            GameObject.Destroy(surface.gameObject);
            surface.gameObject = null;
        }

        // decrement update timeout for all evicted objects destroying those who have timed out
        protected void ProcessEvictedObjects()
        {
            if (pendingSurfacesForEviction == null || pendingSurfacesForEviction.Count == 0)
            {
                return;
            }

            surfacesToRemoveFromDict.Clear();
            foreach (KeyValuePair<int, Surface> kvp in pendingSurfacesForEviction)
            {
                if (kvp.Value.gameObject == null)
                {
                    surfacesToRemoveFromDict.Add(kvp.Key);
                    continue;
                }

                Surface evictionObject = kvp.Value;
                Vector3 surfacePosition = evictionObject.gameObject.transform.position;

                // Only tick the count if either the object is not in the observed bounds or the user is within 10m of it
                if (!BoundsContains(surfacePosition) ||
                    Vector3.SqrMagnitude(surfacePosition - this.transform.position) <= s_EvictionUpdateTickThresholdSqr)
                {
                    if (evictionObject.remainingUpdatesToLive-- <= 0)
                    {
                        DestroySurface(evictionObject);
                        surfacesToRemoveFromDict.Add(kvp.Key);
                    }
                }
            }

            for (int i = 0; i < surfacesToRemoveFromDict.Count; ++i)
            {
                pendingSurfacesForEviction.Remove(surfacesToRemoveFromDict[i]);
            }
        }

        protected virtual bool TryGetHighestPriorityRequest(out SurfaceData bestSurfaceData)
        {
            bestSurfaceData = bestSurfaceDataNull;

            if (surfaceObjects == null || surfaceObjects.Count == 0)
            {
                return false;
            }

            Surface bestSurface = null;
            foreach (KeyValuePair<int, Surface> kvp in surfaceObjects)
            {
                if (!kvp.Value.awaitingBake)
                {
                    // ignore surfaces that have already been baked
                    continue;
                }

                if (bestSurface == null)
                {
                    bestSurface = kvp.Value;
                    continue;
                }

                if (kvp.Value.updateTime < bestSurface.updateTime)
                {
                    bestSurface = kvp.Value;
                }
            }

            if (bestSurface == null)
            {
                // nothing to do
                return false;
            }

            AddRequiredComponentsForBaking(bestSurface);
            UpdateSurfaceData(bestSurface);
            bestSurfaceData = bestSurface.surfaceData;

            return true;
        }

        protected virtual void UpdateSurfaceData(Surface surface)
        {
            SurfaceData tempSurfaceData = surface.surfaceData;

            tempSurfaceData.id = surface.surfaceId;
            tempSurfaceData.trianglesPerCubicMeter = lodToPcm[(int)lodType];
            tempSurfaceData.bakeCollider = false;
            tempSurfaceData.outputMesh = surface.meshFilter;

            surface.surfaceData = tempSurfaceData;
        }

        protected void ForEachSurfaceInCache(System.Action<Surface> callback)
        {
            if (callback == null)
            {
                return;
            }

            if (surfaceObjects == null || surfaceObjects.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<int, Surface> kvp in surfaceObjects)
            {
                callback(kvp.Value);
            }

            foreach (KeyValuePair<int, Surface> kvp in pendingSurfacesForEviction)
            {
                if (ShouldRemainActiveWhileBeingRemoved(kvp.Value))
                {
                    callback(kvp.Value);
                }
            }
        }

        // Called when properties should be updated due to
        // serialization changes.
        protected virtual void OnResetProperties()
        {
        }

        protected virtual void OnDidApplyAnimationProperties()
        {
            OnResetProperties();
        }

        protected virtual void Reset()
        {
            OnResetProperties();
        }

#if UNITY_EDITOR
        // This is Unity's own OnValidate method which is invoked when
        // changing values in the Inspector/undo/redo.
        // In the future, we may need to put logic in the awake
        // method.  In that case, we will want derived classes to
        // override the functionality.
        protected virtual void OnValidate()
        {
            OnResetProperties();
        }

#endif // if UNITY_EDITOR
    }
}
