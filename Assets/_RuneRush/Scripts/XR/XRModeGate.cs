using System;
using UnityEngine;

namespace RuneRush.XR
{
    [DisallowMultipleComponent]
    public sealed class XRModeGate : MonoBehaviour
    {
        public enum RuntimeMode
        {
            Auto,
            Desktop,
            XR
        }

        [SerializeField] private RuntimeMode mode = RuntimeMode.Auto;
        [SerializeField] private Transform trackedHead;
        [SerializeField] private bool applyHeadPose;
        [SerializeField] private GameObject[] xrOnlyObjects = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] desktopOnlyObjects = Array.Empty<GameObject>();
        [SerializeField] private Behaviour[] desktopOnlyBehaviours = Array.Empty<Behaviour>();

        private bool isXrMode;

        public bool IsXrMode => isXrMode;
        public XRVehicleInputSnapshot VehicleInput => XRRuntimeState.ReadVehicleInput();
        public event Action<bool> ModeChanged;

        private void OnEnable()
        {
            ApplyMode(ResolveMode(), true);
        }

        private void Update()
        {
            ApplyMode(ResolveMode(), false);

            if (isXrMode && applyHeadPose && trackedHead != null && XRRuntimeState.TryGetHeadPose(out Pose pose))
            {
                trackedHead.SetLocalPositionAndRotation(pose.position, pose.rotation);
            }
        }

        public void SetMode(RuntimeMode nextMode)
        {
            mode = nextMode;
            ApplyMode(ResolveMode(), true);
        }

        private bool ResolveMode()
        {
            return mode switch
            {
                RuntimeMode.Desktop => false,
                RuntimeMode.XR => true,
                _ => XRRuntimeState.IsHeadsetActive
            };
        }

        private void ApplyMode(bool nextIsXrMode, bool force)
        {
            if (!force && nextIsXrMode == isXrMode)
            {
                return;
            }

            isXrMode = nextIsXrMode;
            SetObjectsActive(xrOnlyObjects, isXrMode);
            SetObjectsActive(desktopOnlyObjects, !isXrMode);

            foreach (Behaviour behaviour in desktopOnlyBehaviours)
            {
                if (behaviour != null && behaviour != this)
                {
                    behaviour.enabled = !isXrMode;
                }
            }

            ModeChanged?.Invoke(isXrMode);
        }

        private void SetObjectsActive(GameObject[] objects, bool active)
        {
            foreach (GameObject target in objects)
            {
                if (target != null && target != gameObject)
                {
                    target.SetActive(active);
                }
            }
        }
    }
}
