using System;

namespace UnityEngine.EventSystems
{
    public partial class HoloLensInputModule
    {
        [Obsolete("This method was never intended for public consumption - if you needed it as a workaround for something, please report the accompanying bug.", true)]
        public void HoloLensInput_GestureNotifier()
        {
        }

        [Obsolete("This method was never intended for public consumption - if you needed it as a workaround for something, please report the accompanying bug.")]
        public EventSystem HoloLensInput_GetEventSystem()
        {
            return eventSystem;
        }

        [Obsolete("HoloLensInput_GetScreenOffsetScalar has been deprecated. Use normalizedNavigationToScreenOffsetScalar instead. (UnityUpgradable) -> normalizedNavigationToScreenOffsetScalar")]
        public float HoloLensInput_GetScreenOffsetScalar()
        {
            return normalizedNavigationToScreenOffsetScalar;
        }

        [Obsolete("HoloLensInput_GetTimeToPressOnTap has been deprecated. Use timeToPressOnTap instead. (UnityUpgradable) -> timeToPressOnTap")]
        public float HoloLensInput_GetTimeToPressOnTap()
        {
            return timeToPressOnTap;
        }
    }
}
