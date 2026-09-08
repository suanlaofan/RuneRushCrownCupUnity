using UnityEngine;
using UnityEngine.UI;

namespace RuneRush.UnityRuntime
{
    /// <summary>Shows the menu against black, then restores the exact race camera and canvas.</summary>
    public sealed class FullscreenMenuPresentation
    {
        public const int UiLayer = 5;
        private readonly Canvas _canvas;
        private readonly Camera _camera;
        private bool _active;
        private bool _xr;
        private int _cameraMask;
        private CameraClearFlags _clearFlags;
        private Color _background;
        private Transform _parent;
        private Vector3 _position, _scale;
        private Quaternion _rotation;
        private Vector2 _size, _anchorMin, _anchorMax, _pivot;
        private RenderMode _renderMode;
        private CanvasScaler.ScaleMode _scaleMode;
        private float _scaleFactor;

        public FullscreenMenuPresentation(Canvas canvas, Camera camera)
        {
            _canvas = canvas;
            _camera = camera;
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = UiLayer;
        }

        public void SetVisible(bool visible, bool xr)
        {
            if (_camera == null || _canvas == null) return;
            if (_active && (!visible || xr != _xr)) Restore();
            if (!visible) return;
            if (!_active)
            {
                _cameraMask = _camera.cullingMask;
                _clearFlags = _camera.clearFlags;
                _background = _camera.backgroundColor;
                RectTransform rect = (RectTransform)_canvas.transform;
                _parent = rect.parent;
                _position = rect.localPosition;
                _rotation = rect.localRotation;
                _scale = rect.localScale;
                _size = rect.sizeDelta;
                _anchorMin = rect.anchorMin;
                _anchorMax = rect.anchorMax;
                _pivot = rect.pivot;
                _renderMode = _canvas.renderMode;
                CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
                if (scaler != null) { _scaleMode = scaler.uiScaleMode; _scaleFactor = scaler.scaleFactor; }
                _active = true;
                _xr = xr;
                Debug.Log("RUNE_RUSH_MENU_FULLSCREEN: visible=true; world=black; xr=" + xr);
            }
            _camera.cullingMask = 1 << UiLayer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            if (xr) FitHeadsetView();
        }

        private void FitHeadsetView()
        {
            const float distance = 2.1f;
            float halfHeight = Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * .5f);
            float halfWidth = halfHeight * _camera.aspect;
            if (_camera.stereoEnabled)
            {
                // Use the common visible region of both asymmetric eye projections.
                halfWidth = float.MaxValue;
                halfHeight = float.MaxValue;
                for (int eye = 0; eye < 2; eye++)
                {
                    Matrix4x4 p = _camera.GetStereoProjectionMatrix((Camera.StereoscopicEye)eye);
                    halfWidth = Mathf.Min(halfWidth, (1f - Mathf.Abs(p.m02)) / Mathf.Abs(p.m00));
                    halfHeight = Mathf.Min(halfHeight, (1f - Mathf.Abs(p.m12)) / Mathf.Abs(p.m11));
                }
            }
            float scale = .98f * 2f * distance * Mathf.Min(halfWidth / 1920f, halfHeight / 1080f);
            RectTransform rect = (RectTransform)_canvas.transform;
            rect.SetParent(_camera.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = new Vector2(1920f, 1080f);
            rect.localPosition = new Vector3(0f, 0f, distance);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * scale;
            _canvas.renderMode = RenderMode.WorldSpace;
            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
            }
        }

        public void Restore()
        {
            if (!_active) return;
            _active = false;
            if (_camera != null)
            {
                _camera.cullingMask = _cameraMask;
                _camera.clearFlags = _clearFlags;
                _camera.backgroundColor = _background;
            }
            if (_canvas != null)
            {
                RectTransform rect = (RectTransform)_canvas.transform;
                rect.SetParent(_parent, false);
                rect.anchorMin = _anchorMin;
                rect.anchorMax = _anchorMax;
                rect.pivot = _pivot;
                rect.sizeDelta = _size;
                rect.localPosition = _position;
                rect.localRotation = _rotation;
                rect.localScale = _scale;
                _canvas.renderMode = _renderMode;
                CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
                if (scaler != null) { scaler.uiScaleMode = _scaleMode; scaler.scaleFactor = _scaleFactor; }
            }
            Debug.Log("RUNE_RUSH_MENU_FULLSCREEN: visible=false; world=restored");
        }
    }
}
