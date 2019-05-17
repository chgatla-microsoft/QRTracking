using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.WSA.Input;

namespace UnityEngine.EventSystems
{
    public class HoloLensInput : BaseInput
    {
        ///////////////
        // BaseInput //
        ///////////////

        public override bool mousePresent
        {
            get { return true; }
        }

        public override bool GetMouseButtonDown(int button)
        {
            return button == 0 && !m_IsEmulatedMouseDownPrev && m_IsEmulatedMouseDownCurr;
        }

        public override bool GetMouseButtonUp(int button)
        {
            return button == 0 && m_IsEmulatedMouseDownPrev && !m_IsEmulatedMouseDownCurr;
        }

        public override bool GetMouseButton(int button)
        {
            return button == 0 && m_IsEmulatedMouseDownCurr;
        }

        public override Vector2 mousePosition
        {
            get { return GetGazeAndGestureScreenPosition(); }
        }

        public override Vector2 mouseScrollDelta
        {
            get { return GetGestureScrollDelta(); }
        }

        public override bool touchSupported
        {
            get { return false; }
        }

        public override int touchCount
        {
            get { return 0; }
        }

        ///////////////////
        // MonoBehaviour //
        ///////////////////

        protected override void Awake()
        {
            base.Awake();

            m_Module = GetComponent<HoloLensInputModule>();

            m_GestureRecognizer = new GestureRecognizer();

            m_GestureRecognizer.Tapped += GestureHandler_OnTapped;
            m_GestureRecognizer.NavigationStarted += GestureHandler_OnNavigationStarted;
            m_GestureRecognizer.NavigationUpdated += GestureHandler_OnNavigationUpdated;
            m_GestureRecognizer.NavigationCompleted += GestureHandler_OnNavigationCompleted;
            m_GestureRecognizer.NavigationCanceled += GestureHandler_OnNavigationCanceled;

            m_GestureRecognizer.SetRecognizableGestures(
                GestureSettings.Tap
                | GestureSettings.NavigationX
                | GestureSettings.NavigationY
                | GestureSettings.NavigationZ);
            m_GestureRecognizer.StartCapturingGestures();
        }

        protected override void OnDestroy()
        {
            m_GestureRecognizer.StopCapturingGestures();

            m_GestureRecognizer.Tapped -= GestureHandler_OnTapped;
            m_GestureRecognizer.NavigationStarted -= GestureHandler_OnNavigationStarted;
            m_GestureRecognizer.NavigationUpdated -= GestureHandler_OnNavigationUpdated;
            m_GestureRecognizer.NavigationCompleted -= GestureHandler_OnNavigationCompleted;
            m_GestureRecognizer.NavigationCanceled -= GestureHandler_OnNavigationCanceled;

            base.OnDestroy();
        }

        ///////////////////
        // HoloLensInput //
        ///////////////////

        public void UpdateInput()
        {
            if (MouseEmulationMode.Tap == m_MouseEmulationMode && m_LastTapTime + m_Module.timeToPressOnTap < Time.time)
                m_MouseEmulationMode = MouseEmulationMode.Inactive;

            m_IsEmulatedMouseDownPrev = m_IsEmulatedMouseDownCurr;
            m_IsEmulatedMouseDownCurr = m_MouseEmulationMode != MouseEmulationMode.Inactive;
        }

        public bool AllowDrag()
        {
            return m_MouseEmulationMode == MouseEmulationMode.Navigation;
        }

        private bool TryGetAnchorWorldSpace(out Vector3 anchor)
        {
            GameObject focus = m_Module.Internal_GetCurrentFocusedGameObject();
            if (focus == null)
            {
                anchor = Vector3.zero;
                return false;
            }

            RectTransform rectTransform = focus.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                anchor = Vector3.zero;
                return false;
            }

            return RectTransformUtility.ScreenPointToWorldPointInRectangle(rectTransform, GetGazeScreenPosition(), Camera.main, out anchor);
        }

        private void GestureHandler_OnTapped(TappedEventArgs eventArgs)
        {
            m_Module.Internal_GestureNotifier();
            if (!TryGetAnchorWorldSpace(out m_TapAnchorWorldSpace))
                return;

            m_MouseEmulationMode = MouseEmulationMode.Tap;
            m_LastTapTime = Time.time;
        }

        private void GestureHandler_OnNavigationStarted(NavigationStartedEventArgs eventArgs)
        {
            m_Module.Internal_GestureNotifier();
            if (!TryGetAnchorWorldSpace(out m_NavigationAnchorWorldSpace))
                return;

            m_MouseEmulationMode = MouseEmulationMode.Navigation;
            m_NavigationNormalizedOffset = Vector3.zero;
        }

        private void GestureHandler_OnNavigationUpdated(NavigationUpdatedEventArgs eventArgs)
        {
            m_Module.Internal_GestureNotifier();
            m_NavigationNormalizedOffset = eventArgs.normalizedOffset;
        }

        private void GestureHandler_OnNavigationCompleted(NavigationCompletedEventArgs eventArgs)
        {
            OnNavigationCompletedOrCanceled();
        }

        private void GestureHandler_OnNavigationCanceled(NavigationCanceledEventArgs eventArgs)
        {
            OnNavigationCompletedOrCanceled();
        }

        private void OnNavigationCompletedOrCanceled()
        {
            m_Module.Internal_GestureNotifier();
            m_NavigationNormalizedOffset = Vector3.zero;
            m_MouseEmulationMode = MouseEmulationMode.Inactive;
        }

        private static Vector2 GetGazeScreenPosition()
        {
            return new Vector2(0.5f * Screen.width, 0.5f * Screen.height);
        }

        private Vector2 EmulateMousePosition(Vector3 anchorWorldspace, Vector2 finalOffset)
        {
            Vector2 anchorScreenSpace = Camera.main.WorldToScreenPoint(anchorWorldspace);
            return anchorScreenSpace + finalOffset;
        }

        private Vector2 GetGazeAndGestureScreenPosition()
        {
            switch (m_MouseEmulationMode)
            {
                case MouseEmulationMode.Navigation:
                    return EmulateMousePosition(m_NavigationAnchorWorldSpace, m_Module.normalizedNavigationToScreenOffsetScalar * new Vector2(m_NavigationNormalizedOffset.x, m_NavigationNormalizedOffset.y));

                case MouseEmulationMode.Tap:
                    return EmulateMousePosition(m_TapAnchorWorldSpace, Vector2.zero);

                default:
                    return GetGazeScreenPosition();
            }
        }

        private Vector2 GetGestureScrollDelta()
        {
            return MouseEmulationMode.Navigation == m_MouseEmulationMode
                ? new Vector2(0.0f, m_NavigationNormalizedOffset.z)
                : Vector2.zero;
        }

        private enum MouseEmulationMode
        {
            Inactive,
            Navigation,
            Tap
        }

        private bool m_IsEmulatedMouseDownCurr = false;
        private bool m_IsEmulatedMouseDownPrev = false;

        private MouseEmulationMode m_MouseEmulationMode = MouseEmulationMode.Inactive;
        private Vector3 m_NavigationNormalizedOffset = Vector3.zero;
        private Vector3 m_NavigationAnchorWorldSpace = Vector3.zero;
        private Vector3 m_TapAnchorWorldSpace = Vector3.zero;
        private float m_LastTapTime = 0.0f;

        private HoloLensInputModule m_Module;
        private GestureRecognizer m_GestureRecognizer;
    }
}
