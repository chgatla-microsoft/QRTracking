using System;
using System.Collections;

using System.Collections.Generic;

using UnityEngine;

using Microsoft.MixedReality.QR;
namespace QRTracking
{
    public static class QRCodeEventArgs
    {
        public static QRCodeEventArgs<TData> Create<TData>(TData data)
        {
            return new QRCodeEventArgs<TData>(data);
        }
    }

    [Serializable]
    public class QRCodeEventArgs<TData> : EventArgs
    {
        public TData Data { get; private set; }

        public QRCodeEventArgs(TData data)
        {
            Data = data;
        }
    }

    public class QRCodesManager : Singleton<QRCodesManager>
    {
        [Tooltip("Determines if the QR codes scanner should be automatically started.")]
        public bool AutoStartQRTracking = true;

        public bool IsTrackerRunning { get; private set; }
        public Microsoft.MixedReality.QR.QRCodeWatcherStartResult StartResult { get; private set; }

        public event EventHandler<bool> QRCodesTrackingStateChanged;
        public event EventHandler<QRCodeEventArgs<Microsoft.MixedReality.QR.QRCode>> QRCodeAdded;
        public event EventHandler<QRCodeEventArgs<Microsoft.MixedReality.QR.QRCode>> QRCodeUpdated;
        public event EventHandler<QRCodeEventArgs<Microsoft.MixedReality.QR.QRCode>> QRCodeRemoved;

        private System.Collections.Generic.SortedDictionary<System.Guid, Microsoft.MixedReality.QR.QRCode> qrCodesList = new SortedDictionary<System.Guid, Microsoft.MixedReality.QR.QRCode>();

        private QRCodeWatcher qrTracker;
        private bool capabilityInitialized = false;
#if WINDOWS_UWP
        private Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus accessStatus;
#endif

        public System.Guid GetIdForQRCode(string qrCodeData)
        {
            lock (qrCodesList)
            {
                foreach (var ite in qrCodesList)
                {
                    if (ite.Value.Data == qrCodeData)
                    {
                        return ite.Key;
                    }
                }
            }
            return new System.Guid();
        }

        public System.Collections.Generic.IList<Microsoft.MixedReality.QR.QRCode> GetList()
        {
            lock (qrCodesList)
            {
                return new List<Microsoft.MixedReality.QR.QRCode>(qrCodesList.Values);
            }
        }
        protected void Awake()
        {

        }
#if WINDOWS_UWP
        async private void RequestCapability()
        {
            Windows.Security.Authorization.AppCapabilityAccess.AppCapability cap = Windows.Security.Authorization.AppCapabilityAccess.AppCapability.Create("webcam");
            accessStatus = await cap.RequestAccessAsync();
            capabilityInitialized = true;
        }
#endif
        // Use this for initialization
        protected virtual void Start()
        {
#if WINDOWS_UWP
            RequestCapability();
#endif
        }

        private void SetupQRTracking()
        {
            try
            {
                qrTracker = new QRCodeWatcher();
                IsTrackerRunning = false;
                qrTracker.Added += QRCodeWatcher_Added;
                qrTracker.Updated += QRCodeWatcher_Updated;
                qrTracker.Removed += QRCodeWatcher_Removed;
                qrTracker.EnumerationCompleted += QrTracker_EnumerationCompleted;
            }
            catch (Exception ex)
            {
                Debug.Log("QRCodesManager : exception starting the tracker " + ex.ToString());
            }

            if (AutoStartQRTracking)
            {
                StartQRTracking();
            }
        }

        public void StartQRTracking()
        {
            if (qrTracker != null && !IsTrackerRunning)
            {
                int tries = 0;
                do
                {
                    Debug.Log("QRCodesManager starting QRCodeWatcher");
                    StartResult = (qrTracker.Start());
                    Debug.Log("QRCodesManager starting QRCodeWatcher result:" + StartResult);

                    if (StartResult == QRCodeWatcherStartResult.DeviceNotConnected)
                    {
                        System.Threading.Thread.Sleep(500);
                    }
                    else
                    {
                        break;
                    }
                } while (++tries <= 3);

                if (StartResult == QRCodeWatcherStartResult.Success)
                {
                    IsTrackerRunning = true;
                    
                    var handlers = QRCodesTrackingStateChanged;
                    if (handlers != null)
                    {
                        handlers(this, true);
                    }
                }
            }
            else
            {
#if WINDOWS_UWP
                if (accessStatus == Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus.DeniedByUser ||
                    accessStatus == Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus.DeniedBySystem)
                {
                    StartResult = QRCodeWatcherStartResult.AccessDenied;
                    Debug.Log("QRCodesManager starting QRCodeWatcher result:" + StartResult);
                }
#endif
            }

        }

        public void StopQRTracking()
        {
            if (IsTrackerRunning)
            {
                IsTrackerRunning = false;
                if (qrTracker != null)
                {
                    qrTracker.Stop();
                    qrCodesList.Clear();
                }
                StartResult = QRCodeWatcherStartResult.DeviceNotConnected;
                var handlers = QRCodesTrackingStateChanged;
                if (handlers != null)
                {
                    handlers(this, false);
                }
            }
        }

        private void QRCodeWatcher_Removed(QRCodeRemovedEventArgs args)
        {
            Debug.Log("QRCodesManager QRCodeWatcher_Removed");

            bool found = false;
            lock (qrCodesList)
            {
                if (qrCodesList.ContainsKey(args.Code.NodeId))
                {
                    qrCodesList.Remove(args.Code.NodeId);
                }
            }
            if (found)
            {
                var handlers = QRCodeRemoved;
                if (handlers != null)
                {
                    handlers(this, QRCodeEventArgs.Create(args.Code));
                }
            }
        }

        private void QRCodeWatcher_Updated(QRCodeUpdatedEventArgs args)
        {
            Debug.Log("QRCodesManager QRCodeWatcher_Updated");

            bool found = false;
            lock (qrCodesList)
            {
                if (qrCodesList.ContainsKey(args.Code.NodeId))
                {
                    found = true;
                    qrCodesList[args.Code.NodeId] = args.Code;
                }
            }
            if (found)
            {
                var handlers = QRCodeUpdated;
                if (handlers != null)
                {
                    handlers(this, QRCodeEventArgs.Create(args.Code));
                }
            }
        }

        private void QRCodeWatcher_Added(QRCodeAddedEventArgs args)
        {
            Debug.Log("QRCodesManager QRCodeWatcher_Added");

            lock (qrCodesList)
            {
                qrCodesList[args.Code.NodeId] = args.Code;
            }
            var handlers = QRCodeAdded;
            if (handlers != null)
            {
                handlers(this, QRCodeEventArgs.Create(args.Code));
            }
        }

        private void QrTracker_EnumerationCompleted()
        {
            Debug.Log("QRCodesManager QrTracker_EnumerationCompleted");
        }

        private void Update()
        {
            if (qrTracker == null && capabilityInitialized)
            {
#if WINDOWS_UWP
                if (accessStatus == Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus.Allowed)
                {
#endif
                    SetupQRTracking();
#if WINDOWS_UWP
                }
                else
                {  
                    Debug.Log("Webcam capability is needed : " + accessStatus);
                }
#endif
            }
        }
    }

}