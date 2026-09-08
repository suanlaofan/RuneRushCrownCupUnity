using NUnit.Framework;
using RuneRush.UnityRuntime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RuneRush.Tests
{
    public sealed class XrPointerInteractionTests
    {
        private GameObject _root;
        private RuneRushXrPointerModule _module;
        private Button _button;
        private Camera _camera;
        private Canvas _canvas;
        private int _clicks;

        [Test]
        public void PointerVisualMaterialIsBundledForPlayerBuilds()
        {
            var material = Resources.Load<Material>("RuneRushUiPointer");
            Assert.That(material, Is.Not.Null, "The player must retain the pointer shader through a Resources material.");
            Assert.That(material.shader, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
        }

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("XR pointer test");
            var es = new GameObject("Test EventSystem", typeof(EventSystem));
            es.transform.SetParent(_root.transform);
            _module = es.AddComponent<RuneRushXrPointerModule>();
            var cameraObject = new GameObject("Test Camera", typeof(Camera));
            cameraObject.transform.SetParent(_root.transform);
            _camera = cameraObject.GetComponent<Camera>();
            var go = new GameObject("Test Canvas", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(_root.transform);
            Canvas canvas = go.GetComponent<Canvas>();
            _canvas = canvas;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _camera;
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1200, 800);
            go.transform.localPosition = new Vector3(0, 0, 2.1f);
            go.transform.localScale = Vector3.one * 0.0013f;
            var target = new GameObject("Start", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            target.transform.SetParent(go.transform, false);
            target.GetComponent<RectTransform>().sizeDelta = new Vector2(330, 76);
            _button = target.GetComponent<Button>();
            _button.targetGraphic = target.GetComponent<Image>();
            _button.onClick.AddListener(() => _clicks++);
            _module.Configure(canvas, _root.transform);
            _module.SetInteractionEnabled(true);
            RenderCanvas();
            Sample(false);
        }

        private void RenderCanvas()
        {
            Canvas.ForceUpdateCanvases();
            var target = RenderTexture.GetTemporary(512, 512, 24);
            _camera.targetTexture = target;
            _camera.Render();
            _camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_root); _clicks = 0; }

        private void Sample(bool pressed, bool miss = false)
        {
            Vector3 origin = _camera.transform.position;
            Vector3 target = _button.transform.position + (miss ? _camera.transform.right * 3 : Vector3.zero);
            _module.ProcessSample(origin, Quaternion.LookRotation(target - origin, _camera.transform.up), pressed);
        }

        [Test]
        public void SpatialRayHitsButtonAndLongPressClicksExactlyOnce()
        {
            Assert.That(_module.HoveredButton, Is.SameAs(_button));
            Sample(true); Sample(true); Sample(true);
            Assert.That(_clicks, Is.Zero);
            Sample(false); Sample(false);
            Assert.That(_clicks, Is.EqualTo(1));
        }

        [Test]
        public void ReleaseOutsideDoesNotClick()
        {
            Sample(true); Sample(true, true); Sample(false, true);
            Assert.That(_clicks, Is.Zero);
        }

        [Test]
        public void DisabledButtonDoesNotClick()
        {
            _button.interactable = false;
            Sample(true); Sample(false);
            Assert.That(_clicks, Is.Zero);
        }

        [Test]
        public void HeldTriggerAfterReopenMustReleaseBeforeNextClick()
        {
            Sample(true);
            _module.SetInteractionEnabled(false);
            _module.SetInteractionEnabled(true);
            Sample(true); Sample(false);
            Assert.That(_clicks, Is.Zero);
            Sample(true); Sample(false);
            Assert.That(_clicks, Is.EqualTo(1));
        }

        [Test]
        public void TrackingCancellationNeverReplaysOldClick()
        {
            Sample(true); _module.CancelPointer(); Sample(true); Sample(false);
            Assert.That(_clicks, Is.Zero);
        }

        [Test]
        public void VehicleTranslationAndYawKeepRayAligned()
        {
            _root.transform.SetPositionAndRotation(new Vector3(137, 4, -52), Quaternion.Euler(0, 124, 0));
            RenderCanvas();
            Sample(false); Sample(true); Sample(false);
            Assert.That(_clicks, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedClicksDoNotAccumulateCallbacks()
        {
            for (int i = 0; i < 20; i++) { Sample(true); Sample(false); }
            Assert.That(_clicks, Is.EqualTo(20));
        }

        [Test]
        public void UiTriggerCannotCarryThrottleIntoRace()
        {
            var gate = new DrivingInputGate();
            var throttle = new RaceInputFrame { Throttle = 1, UseItem = true };
            Assert.That(gate.Filter(throttle, true).Throttle, Is.Zero);
            Assert.That(gate.Filter(throttle, false).Throttle, Is.Zero);
            Assert.That(gate.Filter(RaceInputFrame.Idle, false).UseItem, Is.False);
            Assert.That(gate.Filter(throttle, false).Throttle, Is.EqualTo(1));
            gate.RequireRelease();
            Assert.That(gate.Filter(throttle, false).UseItem, Is.False);
        }

        [Test]
        public void FullscreenMenuHidesWorldAndRestoresCameraAndHudAcrossRepeatedVisits()
        {
            _camera.cullingMask = (1 << 0) | (1 << 5) | (1 << 8);
            _camera.clearFlags = CameraClearFlags.Skybox;
            _camera.backgroundColor = Color.cyan;
            Transform parent = _canvas.transform.parent;
            Vector3 position = _canvas.transform.localPosition;
            Vector3 scale = _canvas.transform.localScale;
            Vector2 size = ((RectTransform)_canvas.transform).sizeDelta;
            var presentation = new FullscreenMenuPresentation(_canvas, _camera);
            for (int visit = 0; visit < 3; visit++)
            {
                presentation.SetVisible(true, true);
                presentation.SetVisible(true, true); // LateUpdate must not replace the saved race state.
                Assert.That(_camera.cullingMask, Is.EqualTo(1 << 5));
                Assert.That(_camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
                Assert.That(_camera.backgroundColor, Is.EqualTo(Color.black));
                Assert.That(_button.gameObject.layer, Is.EqualTo(5));
                presentation.SetVisible(false, true); // Start / countdown.
                Assert.That(_camera.cullingMask, Is.EqualTo((1 << 0) | (1 << 5) | (1 << 8)));
                Assert.That(_camera.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
                Assert.That(_camera.backgroundColor, Is.EqualTo(Color.cyan));
                Assert.That(_canvas.transform.parent, Is.SameAs(parent));
                Assert.That(_canvas.transform.localPosition, Is.EqualTo(position));
                Assert.That(_canvas.transform.localScale, Is.EqualTo(scale));
                Assert.That(((RectTransform)_canvas.transform).sizeDelta, Is.EqualTo(size));
            }
        }

        [Test]
        public void FullscreenMenuFitsViewAndSpatialButtonStillClicksAfterHeadMovement()
        {
            _camera.fieldOfView = 90;
            _camera.aspect = 1;
            var presentation = new FullscreenMenuPresentation(_canvas, _camera);
            presentation.SetVisible(true, true);
            Assert.That(_canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(_canvas.transform.parent, Is.SameAs(_camera.transform));
            var corners = new Vector3[4];
            ((RectTransform)_canvas.transform).GetWorldCorners(corners);
            Assert.That(_camera.WorldToViewportPoint(corners[0]).x, Is.EqualTo(.01f).Within(.002f));
            Assert.That(_camera.WorldToViewportPoint(corners[2]).x, Is.EqualTo(.99f).Within(.002f));
            _camera.transform.SetPositionAndRotation(new Vector3(5, 2, 9), Quaternion.Euler(12, 47, 0));
            RenderCanvas();
            Sample(false); Sample(true); Sample(false);
            Assert.That(_clicks, Is.EqualTo(1));
            presentation.Restore();
        }

        [Test]
        public void DesktopMenuKeepsScreenSpaceLayoutWhileWorldIsBlack()
        {
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            var presentation = new FullscreenMenuPresentation(_canvas, _camera);
            presentation.SetVisible(true, false);
            Assert.That(_canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(_camera.cullingMask, Is.EqualTo(1 << 5));
            presentation.Restore();
            Assert.That(_camera.cullingMask, Is.EqualTo(-1));
        }
    }
}
