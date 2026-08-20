using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace RuneRush.XR
{
    public readonly struct XRVehicleInputSnapshot
    {
        public XRVehicleInputSnapshot(
            float steer,
            float throttle,
            float brake,
            bool drift,
            bool useItem,
            bool boost)
        {
            Steer = steer;
            Throttle = throttle;
            Brake = brake;
            Drift = drift;
            UseItem = useItem;
            Boost = boost;
        }

        public float Steer { get; }
        public float Throttle { get; }
        public float Brake { get; }
        public bool Drift { get; }
        public bool UseItem { get; }
        public bool Boost { get; }
    }

    public static class XRRuntimeState
    {
        private static readonly List<InputDevice> Headsets = new List<InputDevice>(2);

        public static bool IsHeadsetActive
        {
            get
            {
                if (!XRSettings.isDeviceActive) return false;

                Headsets.Clear();
                InputDevices.GetDevicesWithCharacteristics(
                    InputDeviceCharacteristics.HeadMounted | InputDeviceCharacteristics.TrackedDevice,
                    Headsets);
                foreach (InputDevice headset in Headsets)
                {
                    if (!headset.isValid) continue;
                    bool isTracked = headset.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
                    bool hasTrackingState = headset.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState state)
                        && (state & (InputTrackingState.Position | InputTrackingState.Rotation)) != 0;
                    if (isTracked || hasTrackingState) return true;
                }

                return false;
            }
        }

        public static bool IsPicoXrPresentation(Camera camera)
        {
            if (IsHeadsetActive) return true;
            if (!Application.isMobilePlatform || camera == null) return false;
            Transform current = camera.transform;
            while (current != null)
            {
                if (current.GetComponent("XROrigin") != null) return true;
                current = current.parent;
            }
            return false;
        }

        public static bool TryGetHeadPose(out Pose pose)
        {
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool hasPosition = head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position);
            bool hasRotation = head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation);

            pose = new Pose(position, rotation);
            return head.isValid && hasPosition && hasRotation;
        }

        public static XRVehicleInputSnapshot ReadVehicleInput()
        {
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            Vector2 leftAxis = ReadAxis(left);
            Vector2 rightAxis = ReadAxis(right);
            float steer = Mathf.Abs(leftAxis.x) >= Mathf.Abs(rightAxis.x) ? leftAxis.x : rightAxis.x;

            return new XRVehicleInputSnapshot(
                Mathf.Clamp(steer, -1f, 1f),
                ReadFloat(right, CommonUsages.trigger),
                ReadFloat(left, CommonUsages.trigger),
                ReadButton(left, CommonUsages.gripButton),
                ReadButton(right, CommonUsages.primaryButton),
                ReadButton(right, CommonUsages.secondaryButton));
        }

        private static Vector2 ReadAxis(InputDevice device)
        {
            return device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 value)
                ? value
                : Vector2.zero;
        }

        private static float ReadFloat(InputDevice device, InputFeatureUsage<float> usage)
        {
            return device.TryGetFeatureValue(usage, out float value) ? Mathf.Clamp01(value) : 0f;
        }

        private static bool ReadButton(InputDevice device, InputFeatureUsage<bool> usage)
        {
            return device.TryGetFeatureValue(usage, out bool value) && value;
        }
    }
}
