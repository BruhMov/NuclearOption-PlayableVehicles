using Rewired;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class GameActions
    {
        public const string Fire = "Fire";
        public const string NextWeapon = "Next Weapon";
        public const string PreviousWeapon = "Previous Weapon";
        public const string TargetSelect = "Select";
        public const string TargetCancel = "Cancel";
        public const string PanView = "Pan View";
        public const string TiltView = "Tilt View";
        public const string ZoomView = "Zoom View";
        public const string FreeLook = "Free Look";
        public const string MoveLongitudinal = "Move Longitudinal";
        public const string MoveLateral = "Move Lateral";
    }

    internal static class InputGate
    {
        public static bool PauseMenuOpen => GameplayUI.GameIsPaused || !GameManager.flightControlsEnabled;

        public static bool TextInputActive => NuclearOption.MissionEditorScripts.InputFieldChecker.InsideInputField
            || CursorManager.GetFlag(CursorFlags.Chat | CursorFlags.Dialogue);

        public static bool GameplayInputAllowed => !PauseMenuOpen && !DynamicMap.mapMaximized && !TextInputActive
            && !VanillaWindow.AnyOpen && !VehicleSelectionMenu.IsOpen;

        public static bool MouseLookAllowed => GameplayInputAllowed && !Cursor.visible;

        public static bool ActionHeld(string action)
        {
            Player player = GameManager.playerInput;
            return player != null && player.GetButton(action);
        }

        public static bool ActionClicked(string action)
        {
            Player player = GameManager.playerInput;
            return player != null && player.GetButtonTimedPressUp(action, 0f, PlayerSettings.clickDelay);
        }

        public static float ActionAxis(string action)
        {
            Player player = GameManager.playerInput;
            return player != null ? player.GetAxis(action) : 0f;
        }

        public static bool KeyDown(KeyCode key)
        {
            if (TextInputActive) return false;
            if (ReInput.isReady && ReInput.controllers != null && ReInput.controllers.Keyboard != null)
            {
                return ReInput.controllers.Keyboard.GetKeyDown(key);
            }
            return Input.GetKeyDown(key);
        }

        public static bool KeyHeld(KeyCode key)
        {
            if (TextInputActive || key == KeyCode.None) return false;
            if (ReInput.isReady && ReInput.controllers != null && ReInput.controllers.Keyboard != null)
            {
                return ReInput.controllers.Keyboard.GetKey(key);
            }
            return Input.GetKey(key);
        }

        public static float KeyAxis(KeyCode positive, KeyCode negative)
        {
            return (KeyHeld(positive) ? 1f : 0f) - (KeyHeld(negative) ? 1f : 0f);
        }

        public static float JoystickAxis(string action, float deadZone)
        {
            if (TextInputActive) return 0f;
            Player player = GameManager.playerInput;
            if (player == null || !ReInput.isReady) return 0f;
            float value = 0f;
            try
            {
                foreach (Joystick joystick in player.controllers.Joysticks)
                {
                    if (joystick == null || !joystick.enabled) continue;
                    foreach (ActionElementMap map in player.controllers.maps.ElementMapsWithAction(ControllerType.Joystick, joystick.id, action, true))
                    {
                        float element = ElementValue(joystick, map);
                        if (Mathf.Abs(element) > Mathf.Abs(value)) value = element;
                    }
                }
            }
            catch (System.Exception e)
            {
                JoystickErrors.Report(e);
                return 0f;
            }
            return ApplyDeadZone(Mathf.Clamp(value, -1f, 1f), deadZone);
        }

        private static readonly ErrorThrottle JoystickErrors = new ErrorThrottle("InputGate.JoystickAxis");

        private static float ElementValue(Joystick joystick, ActionElementMap map)
        {
            float sign = map.axisContribution == Pole.Negative ? -1f : 1f;
            if (map.elementType == ControllerElementType.Button)
            {
                return joystick.GetButton(map.elementIndex) ? sign : 0f;
            }
            float axis = joystick.GetAxis(map.elementIndex);
            if (map.invert) axis = -axis;
            switch (map.axisRange)
            {
                case AxisRange.Positive: return Mathf.Max(axis, 0f) * sign;
                case AxisRange.Negative: return -Mathf.Min(axis, 0f) * sign;
                default: return axis;
            }
        }

        public static float ApplyDeadZone(float value, float deadZone)
        {
            deadZone = Mathf.Clamp(deadZone, 0f, 0.95f);
            float magnitude = Mathf.Abs(value);
            if (magnitude <= deadZone) return 0f;
            return Mathf.Sign(value) * (magnitude - deadZone) / (1f - deadZone);
        }
    }
}
