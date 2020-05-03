using System;
using UnityEngine;

public static class GodSettings
{
    #region Constants

    public static class Keys
    {
        public const string DronePositionScaleFactor = "DronePositionScaleFactor";
        public const string DronePositionBias = "DronePositionBias";
        public const string DemoMode = "DemoMode";
        public const string JoystickSensitivity = "JoystickSensitivity";
        public const string Gravity = "Gravity";
        public const string BallSpeed = "BallSpeed";
        public const string BallMovementSmoothingFactor = "BallMovementSmoothingFactor";
    }

    public static class Defaults
    {
        public static readonly Vector3 DronePositionScaleFactor = new Vector3(1.5F, 1.5F, 1);
        public static readonly Vector3 DronePositionBias = new Vector3(0, 0, 0.5F);
        public const bool DemoMode = false;
        public const byte JoystickSensitivity = 50;
        public const float Gravity = -9.81F;
        public const float BallSpeed = 10;
        public const float BallMovementSmoothingFactor = 1;
    }

    #endregion Constants

    #region Private Methods

    private static string GetVector3Text(string key, Vector3 defaultValue)
    {
        var defaultValueText = GodMessages.ToString(defaultValue);
        var valueText = PlayerPrefs.GetString(key, defaultValueText);
        return '(' + valueText.Replace(':', ' ') + ')';
    }

    private static Vector3 GetVector3(string key, Vector3 defaultValue)
    {
        var defaultValueText = GodMessages.ToString(defaultValue);
        var valueText = PlayerPrefs.GetString(key, defaultValueText);
        return GodMessages.TryParse(valueText, out Vector3 value)
            ? value
            : defaultValue;
    }

    private static void SetVector3(string key, Vector3 value)
    {
        var valueText = GodMessages.ToString(value);
        PlayerPrefs.SetString(key, valueText);
    }

    private static bool SetVector3(string key, string valueText)
    {
        if (valueText == null)
            return false;
        valueText = valueText.Trim('(', ')', ' ', '\t', '\v', '\n', '\r');
        valueText = string.Join(":", valueText.Split(
            new[] {' '}, StringSplitOptions.RemoveEmptyEntries));
        if (GodMessages.TryParse(valueText, out Vector3 value))
        {
            SetVector3(key, value);
            return true;
        }

        return false;
    }

    private static bool TryParseBoolean(string text, out bool value)
    {
        if (text == null)
        {
            value = default;
            return false;
        }

        text = text.Trim().ToLower();
        switch (text)
        {
            case "0":
            case "false":
            case "off":
            case "disable":
            case "disabled":
            case "no":
                value = false;
                return true;
            case "1":
            case "true":
            case "on":
            case "enable":
            case "enabled":
            case "yes":
                value = true;
                return true;
            default:
                return bool.TryParse(text, out value);
        }
    }

    private static bool GetBoolean(string key, bool defaultValue)
    {
        var valueText = PlayerPrefs.GetString(key, defaultValue ? "1" : "0");
        return TryParseBoolean(valueText, out var value)
            ? value
            : defaultValue;
    }

    private static bool TrySetBoolean(string key, string text)
    {
        if (TryParseBoolean(text, out _))
        {
            PlayerPrefs.SetString(key, text.Trim());
            return true;
        }

        return false;
    }

    private static byte GetPercent(string key, byte defaultValue)
    {
        var valueText = PlayerPrefs.GetString(key, defaultValue.ToString("D"));
        return byte.TryParse(valueText, out var value) && value <= 100 ? value : defaultValue;
    }

    private static bool TrySetPercent(string key, byte value)
    {
        if (value <= 100)
        {
            PlayerPrefs.SetString(key, value.ToString("D"));
            return true;
        }
        return false;
    }

    private static bool TryParsePercent(string text, out byte value)
    {
        return byte.TryParse(text, out value);
    }


    private static bool TrySetPercent(string key, string text)
    {
        return TryParsePercent(text, out var value) &&
               TrySetPercent(key, value);
    }

    #endregion Private Methods

    #region Public Methods

    public static Vector3 GetDronePositionBias()
    {
        return GetVector3(Keys.DronePositionBias, Defaults.DronePositionBias);
    }

    public static string GetDronePositionBiasText()
    {
        return GetVector3Text(Keys.DronePositionBias, Defaults.DronePositionBias);
    }

    public static void SetDronePositionBias(Vector3 value)
    {
        SetVector3(Keys.DronePositionBias, value);
    }

    public static bool TrySetDronePositionBias(string text)
    {
        return SetVector3(Keys.DronePositionBias, text);
    }

    public static Vector3 GetDronePositionScaleFactor()
    {
        return GetVector3(Keys.DronePositionScaleFactor, Defaults.DronePositionScaleFactor);
    }

    public static string GetDronePositionScaleFactorText()
    {
        return GetVector3Text(Keys.DronePositionScaleFactor, Defaults.DronePositionScaleFactor);
    }

    public static void SetDronePositionScaleFactor(Vector3 value)
    {
        SetVector3(Keys.DronePositionScaleFactor, value);
    }

    public static bool TrySetDronePositionScaleFactor(string text)
    {
        return SetVector3(Keys.DronePositionScaleFactor, text);
    }

    public static bool GetDemoMode()
    {
        return GetBoolean(Keys.DemoMode, Defaults.DemoMode);
    }

    public static void SetDemoMode(bool value)
    {
        var valueCode = value ? "1" : "0";
        PlayerPrefs.SetString(Keys.DemoMode, valueCode);
    }

    public static bool TrySetDemoMode(string text)
    {
        return TrySetBoolean(Keys.DemoMode, text);
    }

    public static byte GetJoystickSensitivity()
    {
        return GetPercent(Keys.JoystickSensitivity, Defaults.JoystickSensitivity);
    }

    public static string GetJoystickSensitivityText()
    {
        var value = GetPercent(Keys.JoystickSensitivity, Defaults.JoystickSensitivity);
        return value.ToString("D");
    }

    public static bool TrySetJoystickSensitivity(int value)
    {
        return value > 0 && value <= 100 && TrySetPercent(Keys.JoystickSensitivity, (byte) value);
    }

    public static bool TrySetJoystickSensitivity(byte value)
    {
        return value > 0 && TrySetPercent(Keys.JoystickSensitivity, value);
    }

    public static bool TrySetJoystickSensitivity(string text)
    {
        return TryParsePercent(text, out var value) &&
               TrySetJoystickSensitivity(value);
    }

    public static float GetGravity()
    {
        return PlayerPrefs.GetFloat(Keys.Gravity, Defaults.Gravity);
    }

    public static string GetGravityText()
    {
        var value = GetGravity();
        return value.ToString("F2");
    }

    public static void SetGravity(float gravity)
    {
        PlayerPrefs.SetFloat(Keys.Gravity, gravity);
    }

    public static bool TrySetGravity(string text)
    {
        if (float.TryParse(text, out var gravity))
        {
            SetGravity(gravity);
            return true;
        }
        return false;
    }

    public static float GetBallMovementSmoothingFactor()
    {
        return PlayerPrefs.GetFloat(Keys.BallMovementSmoothingFactor, Defaults.BallMovementSmoothingFactor);
    }

    public static string GetBallMovementSmoothingFactorText()
    {
        var factor = GetBallMovementSmoothingFactor();
        return factor.ToString("F2");
    }

    public static bool TrySetBallMovementSmoothingFactor(float factor)
    {
        if (factor > 0)
        {
            PlayerPrefs.SetFloat(Keys.BallMovementSmoothingFactor, factor);
            return true;
        }
        return false;
    }

    public static bool TrySetBallMovementSmoothingFactor(string text)
    {
        return float.TryParse(text, out var factor) &&
               TrySetBallMovementSmoothingFactor(factor);
    }

    public static float GetBallSpeed()
    {
        return PlayerPrefs.GetFloat(Keys.BallSpeed, Defaults.BallSpeed);
    }

    public static string GetBallSpeedText()
    {
        var speed = GetBallSpeed();
        return speed.ToString("F2");
    }

    public static bool TrySetBallSpeed(float speed)
    {
        if (speed > 0)
        {
            PlayerPrefs.SetFloat(Keys.BallSpeed, speed);
            return true;
        }
        return false;
    }

    public static bool TrySetBallSpeed(string text)
    {
        return float.TryParse(text, out var speed) &&
               TrySetBallSpeed(speed);
    }

    #endregion Public Methods
}
