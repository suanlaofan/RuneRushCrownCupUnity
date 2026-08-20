using System.Collections.Generic;
using RuneRush;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace RuneRush.UnityRuntime
{
    public enum TouchRaceAction
    {
        SteerLeft,
        SteerRight,
        Throttle,
        Brake,
        Drift,
        Nitro,
        UseItem,
        Reset
    }

    public sealed class RaceInputSource : MonoBehaviour
    {
        private readonly Dictionary<TouchRaceAction, bool> _touch = new();
        private readonly List<XRInputDevice> _leftXr = new();
        private readonly List<XRInputDevice> _rightXr = new();

        private bool _previousUseItem;
        private bool _previousReset;
        private bool _previousPause;
        private bool _previousMenuPrimary;
        private bool _previousMenuSecondary;
        private bool _previousMenuLeft;
        private bool _previousMenuRight;
        private float _nextXrRefresh;

        public bool PausePressedThisFrame { get; private set; }
        public bool MenuPrimaryPressedThisFrame { get; private set; }
        public bool MenuSecondaryPressedThisFrame { get; private set; }
        public bool MenuLeftPressedThisFrame { get; private set; }
        public bool MenuRightPressedThisFrame { get; private set; }

        public RaceInputFrame ReadFrame()
        {
            float steer = 0f;
            float throttle = 0f;
            float brake = 0f;
            bool drift = false;
            bool nitro = false;
            bool useItem = false;
            bool reset = false;
            bool pause = false;
            bool menuPrimary = false;
            bool menuSecondary = false;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                steer += BoolAxis(keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
                    keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
                throttle = Mathf.Max(throttle, BoolValue(keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed));
                brake = Mathf.Max(brake, BoolValue(keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed));
                drift |= keyboard.spaceKey.isPressed;
                nitro |= keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                useItem |= keyboard.eKey.isPressed || keyboard.enterKey.isPressed;
                reset |= keyboard.rKey.isPressed;
                pause |= keyboard.escapeKey.isPressed || keyboard.pKey.isPressed;
                menuPrimary |= keyboard.enterKey.isPressed || keyboard.spaceKey.isPressed;
                menuSecondary |= keyboard.backspaceKey.isPressed;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                steer += gamepad.leftStick.x.ReadValue();
                throttle = Mathf.Max(throttle, Mathf.Max(gamepad.rightTrigger.ReadValue(), BoolValue(gamepad.buttonSouth.isPressed)));
                brake = Mathf.Max(brake, Mathf.Max(gamepad.leftTrigger.ReadValue(), BoolValue(gamepad.buttonEast.isPressed)));
                drift |= gamepad.buttonWest.isPressed;
                nitro |= gamepad.leftShoulder.isPressed;
                useItem |= gamepad.rightShoulder.isPressed;
                reset |= gamepad.buttonNorth.isPressed;
                pause |= gamepad.startButton.isPressed;
                menuPrimary |= gamepad.buttonSouth.isPressed;
                menuSecondary |= gamepad.buttonEast.isPressed;
            }

            ReadXr(ref steer, ref throttle, ref brake, ref drift, ref nitro, ref useItem, ref reset, ref pause,
                ref menuPrimary, ref menuSecondary);

            steer += BoolAxis(IsTouching(TouchRaceAction.SteerLeft), IsTouching(TouchRaceAction.SteerRight));
            throttle = Mathf.Max(throttle, BoolValue(IsTouching(TouchRaceAction.Throttle)));
            brake = Mathf.Max(brake, BoolValue(IsTouching(TouchRaceAction.Brake)));
            drift |= IsTouching(TouchRaceAction.Drift);
            nitro |= IsTouching(TouchRaceAction.Nitro);
            useItem |= IsTouching(TouchRaceAction.UseItem);
            reset |= IsTouching(TouchRaceAction.Reset);

            bool useEdge = useItem && !_previousUseItem;
            bool resetEdge = reset && !_previousReset;
            PausePressedThisFrame = pause && !_previousPause;
            bool menuLeft = steer < -0.6f;
            bool menuRight = steer > 0.6f;
            MenuPrimaryPressedThisFrame = menuPrimary && !_previousMenuPrimary;
            MenuSecondaryPressedThisFrame = menuSecondary && !_previousMenuSecondary;
            MenuLeftPressedThisFrame = menuLeft && !_previousMenuLeft;
            MenuRightPressedThisFrame = menuRight && !_previousMenuRight;
            _previousUseItem = useItem;
            _previousReset = reset;
            _previousPause = pause;
            _previousMenuPrimary = menuPrimary;
            _previousMenuSecondary = menuSecondary;
            _previousMenuLeft = menuLeft;
            _previousMenuRight = menuRight;

            return new RaceInputFrame
            {
                Throttle = Mathf.Clamp01(throttle),
                Brake = Mathf.Clamp01(brake),
                Steer = Mathf.Clamp(steer, -1f, 1f),
                Drift = drift,
                Nitro = nitro,
                UseItem = useEdge,
                Reset = resetEdge
            };
        }

        public void SetTouch(TouchRaceAction action, bool pressed)
        {
            _touch[action] = pressed;
        }

        private void ReadXr(ref float steer, ref float throttle, ref float brake, ref bool drift,
            ref bool nitro, ref bool useItem, ref bool reset, ref bool pause,
            ref bool menuPrimary, ref bool menuSecondary)
        {
            if (Time.unscaledTime >= _nextXrRefresh)
            {
                _nextXrRefresh = Time.unscaledTime + 1f;
                _leftXr.Clear();
                _rightXr.Clear();
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, _leftXr);
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, _rightXr);
            }

            if (_leftXr.Count > 0 && _leftXr[0].isValid)
            {
                XRInputDevice left = _leftXr[0];
                if (left.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 stick))
                {
                    steer += stick.x;
                }

                if (left.TryGetFeatureValue(XRCommonUsages.trigger, out float leftTrigger))
                {
                    brake = Mathf.Max(brake, leftTrigger);
                }

                drift |= ReadBool(left, XRCommonUsages.primaryButton);
                reset |= ReadBool(left, XRCommonUsages.secondaryButton);
                pause |= ReadBool(left, XRCommonUsages.menuButton);
            }

            if (_rightXr.Count > 0 && _rightXr[0].isValid)
            {
                XRInputDevice right = _rightXr[0];
                if (right.TryGetFeatureValue(XRCommonUsages.trigger, out float rightTrigger))
                {
                    throttle = Mathf.Max(throttle, rightTrigger);
                }

                nitro |= ReadBool(right, XRCommonUsages.gripButton);
                useItem |= ReadBool(right, XRCommonUsages.primaryButton);
                reset |= ReadBool(right, XRCommonUsages.secondaryButton);
                menuPrimary |= ReadBool(right, XRCommonUsages.primaryButton);
                menuSecondary |= ReadBool(right, XRCommonUsages.secondaryButton);
            }
        }

        private bool IsTouching(TouchRaceAction action)
        {
            return _touch.TryGetValue(action, out bool pressed) && pressed;
        }

        private static bool ReadBool(XRInputDevice device, InputFeatureUsage<bool> usage)
        {
            return device.TryGetFeatureValue(usage, out bool value) && value;
        }

        private static float BoolAxis(bool negative, bool positive)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        private static float BoolValue(bool value)
        {
            return value ? 1f : 0f;
        }
    }
}
