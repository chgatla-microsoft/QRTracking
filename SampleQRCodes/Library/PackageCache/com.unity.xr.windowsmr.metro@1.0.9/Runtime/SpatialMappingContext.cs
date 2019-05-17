using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.WSA;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using UnityEngine.Scripting.APIUpdating;
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine.XR.WSA
{
    [MovedFrom("UnityEngine.VR.WSA")]
    public sealed class SpatialMappingContext
    {
        private static readonly SpatialMappingContext instance = new SpatialMappingContext();
        private SpatialMappingContext() {}
        public static SpatialMappingContext Instance
        {
            get { return instance; }
        }

        // The optimal number of surfaces to keep in the work queue is 2.
        // A single surface will stall the queue while it's out for service
        // and more than two increases the chance that one of the surfaces
        // in the queue will be removed or re-updated prior to cooking.
        private const int kIdealInFlightSurfaceCount = 2;

        // Delegate for retrieving the highest priority surface to bake.  This is
        // called by the context when it's looking for work to add to the bake
        // queue.
        public delegate bool GetHighestPriorityCallback(out SurfaceData dataRequest);

        // A record defining a single component.
        struct SMComponentRecord
        {
            public SpatialMappingBase m_Component;  // the component itself
            public SpatialMappingBase.SurfaceDataReadyCallback      m_OnDataReady;  // this component's data ready delegate
            public GetHighestPriorityCallback   m_GetHighestPri;    // called when work queue isn't full
            public SurfaceObserver              m_SurfaceObserver;  // scripting API observer

            SMComponentRecord(
                SpatialMappingBase comp,
                SpatialMappingBase.SurfaceDataReadyCallback onDataReady,
                GetHighestPriorityCallback getHighestPri,
                SurfaceObserver observer)
            {
                m_Component = comp;
                m_OnDataReady = onDataReady;
                m_GetHighestPri = getHighestPri;
                m_SurfaceObserver = observer;
            }

            public void Clear()
            {
                m_Component = null;
                m_OnDataReady = null;
                m_GetHighestPri = null;
                m_SurfaceObserver = null;
            }

            public bool IsClear()
            {
                return m_Component == null
                    && m_OnDataReady == null
                    && m_GetHighestPri == null
                    && m_SurfaceObserver == null;
            }
        }

        // A record containing information about in-flight bakes.
        struct SMBakeRequest
        {
            public SurfaceData          m_RequestData;
            public SMComponentRecord    m_Requester;

            public void Clear()
            {
                m_RequestData.id.handle = 0;
                m_Requester.Clear();
            }

            public bool IsClear()
            {
                return (m_RequestData.id.handle == 0 && m_Requester.IsClear());
            }
        }

        private List<SMComponentRecord> m_Components = new List<SMComponentRecord>();   // registered component list
        private SMBakeRequest[] m_InFlightRequests = new SMBakeRequest[kIdealInFlightSurfaceCount]; // in-flight requests
        private int m_InFlightSurfaces = 0; // count of items currently in the work queue; 0-2
        private int m_NextIndex = 0;    // next index for the m_InFlightRequests array

        // Add the specified component to the list of components with its delegates
        // and scripting API observer.  Components are required to register with
        // the context prior to their first surface bake request.  This method will
        // throw ArgumentException if this component already exists in the list
        // ArgumentNullException if any of the parameters are missing.
        public void RegisterComponent(SpatialMappingBase smComponent, SpatialMappingBase.SurfaceDataReadyCallback onDataReady, GetHighestPriorityCallback getHighestPri, SurfaceObserver observer)
        {
            if (smComponent == null)
            {
                throw new ArgumentNullException("smComponent");
            }
            if (onDataReady == null)
            {
                throw new ArgumentNullException("onDataReady");
            }
            if (getHighestPri == null)
            {
                throw new ArgumentNullException("getHighestPri");
            }
            if (observer == null)
            {
                throw new ArgumentNullException("observer");
            }
            SMComponentRecord findResult = m_Components.Find(record => record.m_Component == smComponent);
            if (findResult.m_Component != null)
            {
                throw new ArgumentException("RegisterComponent on a component already registered!");
            }
            SMComponentRecord rec;
            rec.m_Component = smComponent;
            rec.m_OnDataReady = onDataReady;
            rec.m_GetHighestPri = getHighestPri;
            rec.m_SurfaceObserver = observer;
            m_Components.Add(rec);
        }

        // Remove the specified component from the list of components.  Argument
        // exceptions will be thrown if the specified component cannot be found
        // in the list.
        public void DeregisterComponent(SpatialMappingBase smComponent)
        {
            int removeCount = m_Components.RemoveAll(record => record.m_Component == smComponent);
            if (removeCount == 0)
            {
                throw new ArgumentException("DeregisterComponent for a component not registered!");
            }
        }

        // Delegate called by a SurfaceObserver indicating that a bake request has
        // completed.  This will propagate to some subset of the registered
        // components.
        public void OnSurfaceDataReady(SurfaceData sd, bool outputWritten, float elapsedBakeTimeSeconds)
        {
            int inFlightIdx = GetInFlightIndexFromSD(sd);
            PropagateDataReadyEventToComponents(sd, outputWritten, elapsedBakeTimeSeconds, inFlightIdx);
            UpdateInFlightRecords(inFlightIdx, elapsedBakeTimeSeconds);
            RequestMeshPriorityFromComponents();
        }

        // From the SurfaceData specified, return the index of the in flight
        // requests that matches or -1 if not found.
        private int GetInFlightIndexFromSD(SurfaceData sd)
        {
            for (int inFlightIndex = 0; inFlightIndex < m_InFlightRequests.Length; ++inFlightIndex)
            {
                SMBakeRequest rq = m_InFlightRequests[inFlightIndex];
                // this == might be sketchy
                if (rq.m_RequestData.id.handle == sd.id.handle &&
                    rq.m_RequestData.trianglesPerCubicMeter == sd.trianglesPerCubicMeter &&
                    rq.m_RequestData.bakeCollider == sd.bakeCollider)
                {
                    return inFlightIndex;
                }
            }
            return -1;
        }

        private SpatialMappingBase GetSMComponentFromInFlightIndex(int inFlightIndex)
        {
            if (inFlightIndex < 0)
            {
                return null;
            }

            if (m_InFlightRequests == null ||
                inFlightIndex >= m_InFlightRequests.Length ||
                m_InFlightRequests[inFlightIndex].IsClear())
            {
                return null;
            }

            return m_InFlightRequests[inFlightIndex].m_Requester.m_Component;
        }

        private void PropagateDataReadyEventToComponents(SurfaceData sd, bool outputWritten, float elapsedBakeTimeSeconds, int inFlightIndex)
        {
            SpatialMappingBase.LODType lod = SpatialMappingBase.GetLODFromTPCM(sd.trianglesPerCubicMeter);
            SpatialMappingBase requester = GetSMComponentFromInFlightIndex(inFlightIndex);

            if (outputWritten)
            {
                // prop successes to anyone with a matching LOD; some screening will
                // be needed at the component level.
                foreach (SMComponentRecord comp in m_Components)
                {
                    if (comp.m_Component.lodType == lod && comp.m_Component.bakePhysics == sd.bakeCollider)
                    {
                        comp.m_OnDataReady(requester, sd, outputWritten, elapsedBakeTimeSeconds);
                    }
                }
            }
            else
            {
                // notify ONLY the requester of failure; no one else should care
                if (inFlightIndex != -1)
                {
                    m_InFlightRequests[inFlightIndex].m_Requester.m_OnDataReady(requester, sd, outputWritten, elapsedBakeTimeSeconds);
                }
                else
                {
                    Debug.LogError(System.String.Format("SpatialMappingContext unable to notify a component about a failure to cook surface {0}!", sd.id.handle));
                }
            }
        }

        // Update our records given that we've gotten a data ready event
        // corresponding to the specified index.
        private void UpdateInFlightRecords(int inFlightIndex, float elapsedBakeTimeSeconds)
        {
            if (inFlightIndex == 0 || inFlightIndex == 1)
            {
                if (m_InFlightSurfaces <= 0)
                {
                    Debug.LogError("SMContext:  unexpectedly got a data ready event with too few in flight surfaces!");
                }
                else
                {
                    m_InFlightSurfaces--;
                }

                m_InFlightRequests[inFlightIndex].Clear();
                if (!m_InFlightRequests[inFlightIndex].IsClear())
                {
                    Debug.AssertFormat(false, "Mesh Baking request \"{0}\" should be clear but is not!", inFlightIndex);
                }
                m_NextIndex = inFlightIndex;
            }
            else
            {
                // This isn't a record that we can do anything about.  We don't
                // expect this to happen at all so treat it like an error.
                Debug.LogError(System.String.Format("SMContext:  unable to update in flight record for an invalid index {0}!", inFlightIndex));
            }
        }

        // Components know what work there is to be done so ask the first one in the
        // list for an item of work then drop it to the end of the list so that a
        // really hungry component doesn't starve the rest of them out.
        private void RequestMeshPriorityFromComponents()
        {
            // fixme:  would like to do this twice if needed
            if (m_InFlightSurfaces < kIdealInFlightSurfaceCount)
            {
                for (int ii = 0; ii < m_Components.Count; ++ii)
                {
                    SMComponentRecord comp = m_Components[ii];
                    SurfaceData nextRequest;
                    if (comp.m_GetHighestPri(out nextRequest))
                    {
                        if (-1 == m_NextIndex || !m_InFlightRequests[m_NextIndex].IsClear())
                        {
                            Debug.LogError(System.String.Format("SMContext:  next index {0} may not be clear!", m_NextIndex));
                        }
                        else
                        {
                            if (comp.m_SurfaceObserver.RequestMeshAsync(nextRequest, OnSurfaceDataReady))
                            {
                                //Debug.Log(string.Format("Attempting to Bake \"{0}\" : \"{1}\"", comp.m_Component.name, nextRequest.id.handle));
                                m_InFlightRequests[m_NextIndex].m_RequestData = nextRequest;
                                m_InFlightRequests[m_NextIndex].m_Requester = comp;
                                m_InFlightSurfaces++;
                                m_NextIndex = m_NextIndex == 1 ? 0 : 1;

                                // drop this component to the end of the list
                                // so it can't starve others out.
                                m_Components.RemoveAt(ii);
                                m_Components.Add(comp);
                                break;
                            }
                            else
                            {
                                // if this fires it means that something's
                                // misconfigured, probably in the component.
                                Debug.LogError("SMContext:  unexpected failure requesting mesh bake!");
                            }
                        }
                        break;
                    }
                }
            }
        }

        // Components should call this when they have new data requests available.
        // This will potentially wake a slumbering context.
        public void ComponentHasDataRequests()
        {
            RequestMeshPriorityFromComponents();
        }
    }
}
