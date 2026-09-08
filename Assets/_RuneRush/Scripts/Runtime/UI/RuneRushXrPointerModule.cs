using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR;

namespace RuneRush.UnityRuntime
{
    /// <summary>Button-only XR input using the same Unity XR device poses as the racer.
    /// One active pointer owns each press. Desktop input remains on its own module.</summary>
    public sealed class RuneRushXrPointerModule : BaseInputModule
    {
        private readonly List<RaycastResult> _hits = new();
        private ExtendedPointerEventData _pointer;
        private EventSystem _system;
        private TrackedDeviceRaycaster _raycaster;
        private Transform _trackingSpace;
        private bool _requested;
        private bool _wasPressed;
        private bool _needsRelease = true;
        private bool _focused = true;
        private int _activeHand = 1;
        private bool _lastLeft;
        private bool _lastRight;
        private LineRenderer _line;
        private GameObject _dot;
        private Material _lineMaterial;
        private Material _dotMaterial;
        private bool _reportedPose;
        public Button HoveredButton { get; private set; }
        public int ClickCount { get; private set; }
        public bool InteractionEnabled => _requested;

        public void Configure(Canvas canvas, Transform trackingSpace)
        {
            CancelPointer();
            _trackingSpace = trackingSpace;
            _system = GetComponent<EventSystem>();
            _raycaster = canvas.GetComponent<TrackedDeviceRaycaster>();
            if (_raycaster == null) _raycaster = canvas.gameObject.AddComponent<TrackedDeviceRaycaster>();
            _raycaster.maxDistance = 8f;
            _raycaster.checkFor3DOcclusion = false;
            _raycaster.checkFor2DOcclusion = false;
            _raycaster.ignoreReversedGraphics = true;
            _pointer = new ExtendedPointerEventData(_system)
            {
                pointerId = -101,
                pointerType = UIPointerType.Tracked,
                button = PointerEventData.InputButton.Left
            };
            if (Application.isPlaying && _line == null) BuildVisuals();
            Debug.Log("RUNE_RUSH_XR_POINTER_READY: buttonOnly=true; trackedRaycaster=true; material=" + (_lineMaterial != null));
        }

        public void SetInteractionEnabled(bool value)
        {
            if (_requested == value) return;
            _requested = value;
            CancelPointer();
        }

        public override bool ShouldActivateModule() => enabled && gameObject.activeInHierarchy;

        public override void Process()
        {
            if (!_requested || !_focused || _trackingSpace == null || _raycaster == null)
            {
                CancelPointer();
                return;
            }
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool hasLeft = ReadPose(left, out Vector3 lp, out Quaternion lr, out bool lt);
            bool hasRight = ReadPose(right, out Vector3 rp, out Quaternion rr, out bool rt);
            int previousHand = _activeHand;
            if (!_wasPressed)
            {
                if (hasLeft && lt && !_lastLeft && !(hasRight && rt && !_lastRight)) _activeHand = 0;
                else if (hasRight && rt && !_lastRight) _activeHand = 1;
                else if (_activeHand == 1 && !hasRight && hasLeft) _activeHand = 0;
                else if (_activeHand == 0 && !hasLeft && hasRight) _activeHand = 1;
            }
            _lastLeft = lt;
            _lastRight = rt;
            bool valid = _activeHand == 0 ? hasLeft : hasRight;
            if (!valid) { CancelPointer(); return; }
            if (_activeHand != previousHand) ClearHover();
            Vector3 position = _activeHand == 0 ? lp : rp;
            Quaternion rotation = _activeHand == 0 ? lr : rr;
            position = _trackingSpace.TransformPoint(position);
            rotation = _trackingSpace.rotation * rotation;
            if (!_reportedPose)
            {
                _reportedPose = true;
                Debug.Log("RUNE_RUSH_XR_POINTER_POSE: hand=" + _activeHand + "; device=" + (_activeHand == 0 ? left.name : right.name));
            }
            ProcessSample(position, rotation, _activeHand == 0 ? lt : rt);
        }

        private static bool ReadPose(InputDevice device, out Vector3 position, out Quaternion rotation, out bool trigger)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            trigger = false;
            if (!device.isValid) return false;
            if (device.TryGetFeatureValue(CommonUsages.trigger, out float amount)) trigger = amount >= 0.65f;
            else device.TryGetFeatureValue(CommonUsages.triggerButton, out trigger);
            if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && !tracked) return false;
            return device.TryGetFeatureValue(CommonUsages.devicePosition, out position)
                && device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
        }

        // Shared by real devices and integration tests. Tests supply poses, never invoke Button.onClick directly.
        public void ProcessSample(Vector3 worldPosition, Quaternion worldRotation, bool pressed)
        {
            if (!_requested || !_focused || _pointer == null || _raycaster == null) return;
            _pointer.trackedDevicePosition = worldPosition;
            _pointer.trackedDeviceOrientation = worldRotation;
            _hits.Clear();
            _raycaster.Raycast(_pointer, _hits);
            RaycastResult hit = default;
            Button button = null;
            foreach (RaycastResult candidate in _hits)
            {
                Graphic graphic = candidate.gameObject.GetComponent<Graphic>();
                if (graphic == null || !graphic.raycastTarget) continue;
                button = candidate.gameObject.GetComponentInParent<Button>();
                if (button == null || !button.gameObject.activeInHierarchy) continue;
                hit = candidate;
                break;
            }
            HoveredButton = button;
            _pointer.pointerCurrentRaycast = hit;
            _pointer.position = hit.screenPosition;
            HandlePointerExitAndEnter(_pointer, button != null ? button.gameObject : null);
            DrawPointer(worldPosition, worldRotation * Vector3.forward, hit, button);
            if (_needsRelease)
            {
                if (!pressed) _needsRelease = false;
                _wasPressed = pressed;
                return;
            }
            if (pressed && !_wasPressed)
            {
                _pointer.pressPosition = _pointer.position;
                _pointer.pointerPressRaycast = hit;
                _pointer.eligibleForClick = button != null && button.IsInteractable();
                _pointer.clickCount = 1;
                _pointer.clickTime = Time.unscaledTime;
                if (_pointer.eligibleForClick)
                {
                    _system.SetSelectedGameObject(button.gameObject, _pointer);
                    _pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(button.gameObject, _pointer, ExecuteEvents.pointerDownHandler);
                    if (_pointer.pointerPress == null) _pointer.pointerPress = button.gameObject;
                    _pointer.rawPointerPress = button.gameObject;
                }
            }
            if (!pressed && _wasPressed)
            {
                GameObject pressTarget = _pointer.pointerPress;
                bool click = _pointer.eligibleForClick && button != null && button.IsInteractable()
                    && pressTarget == button.gameObject;
                _pointer.eligibleForClick = false;
                _pointer.pointerPress = null;
                _pointer.rawPointerPress = null;
                if (pressTarget != null) ExecuteEvents.Execute(pressTarget, _pointer, ExecuteEvents.pointerUpHandler);
                if (click)
                {
                    ClickCount++;
                    InputDevice device = InputDevices.GetDeviceAtXRNode(_activeHand == 0 ? XRNode.LeftHand : XRNode.RightHand);
                    if (device.isValid) device.SendHapticImpulse(0, 0.22f, 0.035f);
                    Debug.Log("RUNE_RUSH_XR_UI_CLICK: hand=" + _activeHand + "; button=" + button.name);
                    ExecuteEvents.Execute(pressTarget, _pointer, ExecuteEvents.pointerClickHandler);
                }
            }
            _wasPressed = pressed;
        }

        public void CancelPointer()
        {
            if (_pointer != null)
            {
                GameObject target = _pointer.pointerPress;
                _pointer.eligibleForClick = false;
                _pointer.pointerPress = null;
                _pointer.rawPointerPress = null;
                if (target != null) ExecuteEvents.Execute(target, _pointer, ExecuteEvents.pointerUpHandler);
                ClearHover();
            }
            _wasPressed = false;
            _needsRelease = true;
            if (_line != null) _line.enabled = false;
            if (_dot != null) _dot.SetActive(false);
        }

        private void ClearHover()
        {
            if (_pointer != null) HandlePointerExitAndEnter(_pointer, null);
            HoveredButton = null;
        }

        private void OnApplicationFocus(bool focus) { _focused = focus; CancelPointer(); }
        private void OnApplicationPause(bool paused) { _focused = !paused; CancelPointer(); }
        protected override void OnDisable() { CancelPointer(); base.OnDisable(); }

        private void BuildVisuals()
        {
            // An authored Resources material retains its shader in the Android player build.
            Material template = Resources.Load<Material>("RuneRushUiPointer");
            if (template == null || template.shader == null)
            {
                Debug.LogError("RUNE_RUSH_XR_POINTER_MATERIAL_MISSING: RuneRushUiPointer Resources material is required.");
                return;
            }
            var root = new GameObject("Controller UI Pointer");
            root.layer = FullscreenMenuPresentation.UiLayer;
            root.transform.SetParent(transform, false);
            _line = root.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.startWidth = 0.003f;
            _line.endWidth = 0.0015f;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _lineMaterial = new Material(template);
            _lineMaterial.renderQueue = 4000;
            _line.sharedMaterial = _lineMaterial;
            _dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _dot.name = "UI Hit Point";
            _dot.layer = FullscreenMenuPresentation.UiLayer;
            _dot.transform.SetParent(root.transform, false);
            Destroy(_dot.GetComponent<Collider>());
            _dotMaterial = new Material(_lineMaterial);
            _dot.GetComponent<Renderer>().sharedMaterial = _dotMaterial;
            _dot.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            CancelPointer();
        }

        private void DrawPointer(Vector3 origin, Vector3 direction, RaycastResult hit, Button button)
        {
            if (_line == null) return;
            bool valid = button != null && button.IsInteractable();
            Color color = valid ? new Color(0.35f, 1f, 0.78f) : new Color(0.8f, 0.86f, 0.94f);
            Vector3 end = button != null ? hit.worldPosition - direction * 0.009f : origin + direction * 2.8f;
            _line.enabled = true;
            _line.SetPosition(0, origin);
            _line.SetPosition(1, end);
            _lineMaterial.SetColor("_BaseColor", color);
            _dot.SetActive(button != null);
            _dot.transform.position = end;
            _dot.transform.localScale = Vector3.one * (valid ? 0.014f : 0.009f);
            _dotMaterial.SetColor("_BaseColor", color);
        }

        protected override void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
            if (_dotMaterial != null) Destroy(_dotMaterial);
            base.OnDestroy();
        }
    }
}
