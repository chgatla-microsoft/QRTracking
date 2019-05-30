using System;
using System.Collections;

using System.Collections.Generic;

using UnityEngine;

using QRCodesTrackerPlugin;
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
        public QRCodesTrackerPlugin.QRTrackerStartResult StartResult { get; private set; }

        public event EventHandler<bool> QRCodesTrackingStateChanged;
        public event EventHandler<QRCodeEventArgs<QRCodesTrackerPlugin.QRCode>> QRCodeAdded;
        public event EventHandler<QRCodeEventArgs<QRCodesTrackerPlugin.QRCode>> QRCodeUpdated;
        public event EventHandler<QRCodeEventArgs<QRCodesTrackerPlugin.QRCode>> QRCodeRemoved;

        private System.Collections.Generic.SortedDictionary<System.Guid, QRCodesTrackerPlugin.QRCode> qrCodesList = new SortedDictionary<System.Guid, QRCodesTrackerPlugin.QRCode>();

        private QRTracker qrTracker;
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
                    if (ite.Value.Code == qrCodeData)
                    {
                        return ite.Key;
                    }
                }
            }
            return new System.Guid();
        }

        public System.Collections.Generic.IList<QRCodesTrackerPlugin.QRCode> GetList()
        {
            lock (qrCodesList)
            {
                return new List<QRCodesTrackerPlugin.QRCode>(qrCodesList.Values);
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
                qrTracker = new QRTracker();
                IsTrackerRunning = false;
                qrTracker.Added += QrTracker_Added;
                qrTracker.Updated += QrTracker_Updated;
                qrTracker.Removed += QrTracker_Removed;
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

        public QRTrackerStartResult StartQRTracking()
        {
            if (qrTracker != null && !IsTrackerRunning)
            {
                int tries = 0;
                do
                {
                    Debug.Log("QRCodesManager starting qrtracker");
                    StartResult = (qrTracker.Start());
                    Debug.Log("QRCodesManager starting qrtracker result:" + StartResult);
                    if (StartResult == QRTrackerStartResult.DeviceNotConnected)
                    {
                        System.Threading.Thread.Sleep(500);
                    }
                } while (++tries <= 3);

                if (StartResult == QRTrackerStartResult.Success)
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
                    StartResult = QRTrackerStartResult.AccessDenied;
                    Debug.Log("QRCodesManager starting qrtracker result:" + StartResult);
                }
#endif
            }
            return StartResult;
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
                StartResult = QRTrackerStartResult.DeviceNotConnected;
                var handlers = QRCodesTrackingStateChanged;
                if (handlers != null)
                {
                    handlers(this, false);
                }
            }
        }

        private void QrTracker_Removed(QRCodeRemovedEventArgs args)
        {
            Debug.Log("QRCodesManager QrTracker_Removed");

            bool found = false;
            lock (qrCodesList)
            {
                if (qrCodesList.ContainsKey(args.Code.Id))
                {
                    qrCodesList.Remove(args.Code.Id);
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

        private void QrTracker_Updated(QRCodeUpdatedEventArgs args)
        {
            Debug.Log("QRCodesManager QrTracker_Updated");

            bool found = false;
            lock (qrCodesList)
            {
                if (qrCodesList.ContainsKey(args.Code.Id))
                {
                    found = true;
                    qrCodesList[args.Code.Id] = args.Code;
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

        private void QrTracker_Added(QRCodeAddedEventArgs args)
        {
            Debug.Log("QRCodesManager QrTracker_Added");

            lock (qrCodesList)
            {
                qrCodesList[args.Code.Id] = args.Code;
            }
            var handlers = QRCodeAdded;
            if (handlers != null)
            {
                handlers(this, QRCodeEventArgs.Create(args.Code));
            }
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