using System;
using UnityEngine;

namespace ButterflyEffect.Prototype
{
    /// <summary>Optional scene-owned permissions shared by desktop, XR, and direct interaction entry points.</summary>
    public static class PrototypeInteractionPolicy
    {
        public static Func<PrototypeItem, bool> GrabHandler { get; set; }
        public static Func<PrototypeItem, bool> UseHandler { get; set; }
        public static Func<PrototypeButton, bool> PressHandler { get; set; }
        public static Func<FireworkStation, bool> LaunchHandler { get; set; }
        public static Func<Vector3, Vector3, bool> MoveHandler { get; set; }
        public static Func<bool> ReturnHandler { get; set; }
        public static bool ReturnInProgress { get; private set; }

        public static bool CanGrab(PrototypeItem item) => item != null && (GrabHandler == null || GrabHandler(item));
        public static bool CanUse(PrototypeItem item) => item != null && (UseHandler == null || UseHandler(item));
        public static bool CanPress(PrototypeButton button) => button != null && (PressHandler == null || PressHandler(button));
        public static bool CanLaunch(FireworkStation station) => station != null && (LaunchHandler == null || LaunchHandler(station));
        public static bool CanMove(Vector3 from, Vector3 to) => Finite(from) && Finite(to)
            && (MoveHandler == null || MoveHandler(from, to));
        public static bool CanReturn() => ReturnHandler == null || ReturnHandler();

        public static bool TryBeginReturn()
        {
            if (ReturnInProgress || !CanReturn()) return false;
            ReturnInProgress = true;
            return true;
        }

        public static void CancelReturn() { ReturnInProgress = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            GrabHandler = null;
            UseHandler = null;
            PressHandler = null;
            LaunchHandler = null;
            MoveHandler = null;
            ReturnHandler = null;
            ReturnInProgress = false;
        }

        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
