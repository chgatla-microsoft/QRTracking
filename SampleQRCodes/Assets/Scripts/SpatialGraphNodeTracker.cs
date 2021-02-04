using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.Utilities;

#if WINDOWS_UWP
using Windows.Perception.Spatial;
#endif

namespace QRTracking
{
    public class SpatialGraphNodeTracker : MonoBehaviour
    {
        private System.Guid _id;
        private SpatialGraphNode node;

        public System.Guid Id
        {
            get => _id;

            set
            {
                if (_id != value)
                {
                    _id = value;
                    InitializeSpatialGraphNode(force: true);
                }
            }
        }

        // Use this for initialization
        void Start()
        {
            InitializeSpatialGraphNode();
        }

        // Update is called once per frame
        void Update()
        {
            InitializeSpatialGraphNode();
            if (node != null && node.TryLocate(FrameTime.OnUpdate, out Pose pose))
            {
                // If there is a parent to the camera that means we are using teleport and we should not apply the teleport
                // to these objects so apply the inverse
                if (CameraCache.Main.transform.parent != null)
                {
                    pose = pose.GetTransformedBy(CameraCache.Main.transform.parent);
                }

                gameObject.transform.SetPositionAndRotation(pose.position, pose.rotation);
                //Debug.Log("Id= " + id + " QRPose = " +  pose.position.ToString("F7") + " QRRot = "  +  pose.rotation.ToString("F7"));
            }
        }

        private void InitializeSpatialGraphNode(bool force = false)
        {
            if (node == null || force)
            {
                node = (Id != System.Guid.Empty) ? SpatialGraphNode.FromStaticNodeId(Id) : null;
                Debug.Log("Initialize SpatialGraphNode Id= " + Id);
            }
        }

        private enum FrameTime { OnUpdate, OnBeforeRender }

        private class SpatialGraphNode
        {
            public System.Guid Id { get; private set; }
#if WINDOWS_UWP
            private SpatialCoordinateSystem CoordinateSystem = null;
#endif

            public static SpatialGraphNode FromStaticNodeId(System.Guid id)
            {
#if WINDOWS_UWP
                var coordinateSystem = Windows.Perception.Spatial.Preview.SpatialGraphInteropPreview.CreateCoordinateSystemForNode(id);
                return coordinateSystem == null ? null :
                    new SpatialGraphNode()
                    {
                        Id = id,
                        CoordinateSystem = coordinateSystem
                    };
#endif
                return null;
            }


            public bool TryLocate(FrameTime frameTime, out Pose pose)
            {
                pose = Pose.identity;

#if WINDOWS_UWP
                Quaternion rotation = Quaternion.identity;
                Vector3 translation = new Vector3(0.0f, 0.0f, 0.0f);
                    
                System.IntPtr rootCoordnateSystemPtr = UnityEngine.XR.WindowsMR.WindowsMREnvironment.OriginSpatialCoordinateSystem;
                SpatialCoordinateSystem rootSpatialCoordinateSystem = (SpatialCoordinateSystem)System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(rootCoordnateSystemPtr);

                // Get the relative transform from the unity origin
                System.Numerics.Matrix4x4? relativePose = CoordinateSystem.TryGetTransformTo(rootSpatialCoordinateSystem);

                if (relativePose != null)
                {
                    System.Numerics.Vector3 scale;
                    System.Numerics.Quaternion rotation1;
                    System.Numerics.Vector3 translation1;
       
                    System.Numerics.Matrix4x4 newMatrix = relativePose.Value;

                    // Platform coordinates are all right handed and unity uses left handed matrices. so we convert the matrix
                    // from rhs-rhs to lhs-lhs 
                    // Convert from right to left coordinate system
                    newMatrix.M13 = -newMatrix.M13;
                    newMatrix.M23 = -newMatrix.M23;
                    newMatrix.M43 = -newMatrix.M43;

                    newMatrix.M31 = -newMatrix.M31;
                    newMatrix.M32 = -newMatrix.M32;
                    newMatrix.M34 = -newMatrix.M34;

                    System.Numerics.Matrix4x4.Decompose(newMatrix, out scale, out rotation1, out translation1);
                    translation = new Vector3(translation1.X, translation1.Y, translation1.Z);
                    rotation = new Quaternion(rotation1.X, rotation1.Y, rotation1.Z, rotation1.W);
                    pose = new Pose(translation, rotation);
                    return true;
                }
                else
                {
                    // Debug.Log("Id= " + id + " Unable to locate qrcode" );
                }
#endif
                return false;
            }

        }
    }
}