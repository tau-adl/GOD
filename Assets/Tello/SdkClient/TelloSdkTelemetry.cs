using System;

/// <summary>
/// Represents drone telemetry using the SDK API.
/// </summary>
public class TelloSdkTelemetry
{
    private short pitchDegrees;
    private short rollDegrees;
    private short yawDegrees;
    private int height;
    private int missionPadId;
    private TelloSdkCoordinates missionPadCoordinates;
    private short measuredTemperatureHigh;
    private short measuredTemperatureLow;
    private byte batteryPercent;
    private uint timeOfFlight;
    private uint motorTime;
    private float barometer;

    /// <summary>
    /// Gets drone pitch degrees.
    /// </summary>
    public short PitchDegrees { get => pitchDegrees; private set => pitchDegrees = value; }
    /// <summary>
    /// Gets drone roll degrees.
    /// </summary>
    public short RollDegrees { get => rollDegrees; private set => rollDegrees = value; }
    /// <summary>
    /// Gets drone yaw degrees.
    /// </summary>
    public short YawDegrees { get => yawDegrees; private set => yawDegrees = value; }
    /// <summary>
    /// Gets the drone height above ground in centimeters.
    /// </summary>
    public int Height { get => height; private set => height = value; }

    /// <summary>
    /// Gets the identifier of the detected mission pad.
    /// The value -1 indicates no mission pad has been detected.
    /// </summary>
    public int MissionPadId { get => missionPadId; private set => missionPadId = value; }
    /// <summary>
    /// Gets the coordinates on the detected mission pad.
    /// </summary>
    public TelloSdkCoordinates MissionPadCoordinates { get => missionPadCoordinates; private set => missionPadCoordinates = value; }
    /// <summary>
    /// Gets the 3D velocity vector.
    /// </summary>
    public TelloSdkCoordinates VelocityVector { get; private set; }

    /// <summary>
    /// Gets the measured temperature high value (Celsius).
    /// </summary>
    public short MeasuredTemperatureHigh { get => measuredTemperatureHigh; private set => measuredTemperatureHigh = value; }
    /// <summary>
    /// Gets the measured temperature low value (Celsius).
    /// </summary>
    public short MeasuredTemperatureLow { get => measuredTemperatureLow; private set => measuredTemperatureLow = value; }

    /// <summary>
    /// Gets the time-of-flight distance in centimeters.
    /// </summary>
    public uint TimeOfFlight { get => timeOfFlight; private set => timeOfFlight = value; }

    /// <summary>
    /// Gets drone remaining battery percent.
    /// </summary>
    public byte BatteryPercent { get => batteryPercent; private set => batteryPercent = value; }

    /// <summary>
    /// Gets barometer reading in centimeters.
    /// </summary>
    public float Barometer { get => barometer; private set => barometer = value; }
    /// <summary>
    /// Gets the amount of time the motors have been used.
    /// </summary>
    public uint MotorTime { get => motorTime; private set => motorTime = value; }

    /// <summary>
    /// Gets acceleration vector.
    /// </summary>
    public TelloSdkCoordinates AccelerationVector { get; private set; }

    public static bool TryParse(string text, out TelloSdkTelemetry result)
    {
        if (string.IsNullOrEmpty(text))
        {
            result = null;
            return false;
        }
        var keyValuePairs = text.Split(';');
        float vgx = 0, vgy = 0, vgz = 0;
        float agx = 0, agy = 0, agz = 0;
        float mpx = 0, mpy = 0, mpz = 0;
        result = new TelloSdkTelemetry();
        bool ok = true;
        foreach (var pair in keyValuePairs)
        {
            var keyLength = pair.IndexOf(':');
            if (keyLength < 0 || keyLength + 1 >= pair.Length)
                continue;
            var key = pair.Substring(0, keyLength).Trim();
            var value = pair.Substring(keyLength + 1).Trim();
            switch (key)
            {
                case "mid":
                    ok &= int.TryParse(value, out result.missionPadId);
                    break;
                case "x":
                    ok &= float.TryParse(value, out mpx);
                    break;
                case "y":
                    ok &= float.TryParse(value, out mpy);
                    break;
                case "z":
                    ok &= float.TryParse(value, out mpz);
                    break;
                case "pitch":
                    ok &= short.TryParse(value, out result.pitchDegrees);
                    break;
                case "roll":
                    ok &= short.TryParse(value, out result.rollDegrees);
                    break;
                case "yaw":
                    ok &= short.TryParse(value, out result.yawDegrees);
                    break;
                case "vgx":
                    ok &= float.TryParse(value, out vgx);
                    break;
                case "vgy":
                    ok &= float.TryParse(value, out vgy);
                    break;
                case "vgz":
                    ok &= float.TryParse(value, out vgz);
                    break;
                case "templ":
                    ok &= short.TryParse(value, out result.measuredTemperatureLow);
                    break;
                case "temph":
                    ok &= short.TryParse(value, out result.measuredTemperatureHigh);
                    break;
                case "tof":
                    ok &= uint.TryParse(value, out result.timeOfFlight);
                    break;
                case "h":
                    ok &= int.TryParse(value, out result.height);
                    break;
                case "bat":
                    ok &= byte.TryParse(value, out result.batteryPercent);
                    break;
                case "baro":
                    ok &= float.TryParse(value, out result.barometer);
                    break;
                case "time":
                    ok &= uint.TryParse(value, out result.motorTime);
                    break;
                case "agx":
                    ok &= float.TryParse(value, out agx);
                    break;
                case "agy":
                    ok &= float.TryParse(value, out agy);
                    break;
                case "agz":
                    ok &= float.TryParse(value, out agz);
                    break;
                default:
                    continue;
            }
        }
        result.VelocityVector = new TelloSdkCoordinates(vgx, vgy, vgz);
        result.AccelerationVector = new TelloSdkCoordinates(agx, agy, agz);
        result.MissionPadCoordinates = new TelloSdkCoordinates(mpx, mpy, mpz);
        return ok;
    }

    public override string ToString()
    {
        return $"mid:{MissionPadId};" +
            $"x:{MissionPadCoordinates.X:F2};" +
            $"y:{MissionPadCoordinates.Y:F2};" +
            $"z:{MissionPadCoordinates.Z:F2};" +
            $"pitch:{PitchDegrees};" +
            $"roll:{RollDegrees};" +
            $"yaw:{YawDegrees};" +
            $"vgx:{VelocityVector.X:F2};" +
            $"vgy:{VelocityVector.Y:F2};" +
            $"vgz:{VelocityVector.Z:F2};" +
            $"templ:{MeasuredTemperatureLow};" +
            $"temph:{MeasuredTemperatureHigh};" +
            $"tof:{TimeOfFlight};" +
            $"h:{Height};" +
            $"bat:{BatteryPercent};" +
            $"baro:{Barometer:F2};" +
            $"time:{MotorTime};" +
            $"agx:{AccelerationVector.X:F2};" +
            $"agy:{AccelerationVector.Y:F2};" +
            $"agz:{AccelerationVector.Z:F2};";
    }
}