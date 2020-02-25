using System;

public class TelloSdkStickData
{
    public const sbyte MinValue = -100;
    public const sbyte MaxValue = +100;

    private sbyte _throttle;
    private sbyte _pitch;
    private sbyte _roll;
    private sbyte _yaw;
    private float _moveSpeedFactor = 1;

    public float MoveSpeedFactor
    {
        get => _moveSpeedFactor;
        set
        {
            if (value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(nameof(value), $"The value of '{nameof(MoveSpeedFactor)}' must be between 0 and 1.");
            _moveSpeedFactor = value;
        }
    }

    public sbyte Throttle
    {
        get => _throttle;
        set
        {
            if (value < MinValue || value > MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"{GetType().Name}.{nameof(Throttle)} must be between {MinValue} and {MaxValue}.");
            _throttle = value;
        }
    }
    public sbyte Pitch
    {
        get => _pitch;
        set
        {
            if (value < MinValue || value > MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"{GetType().Name}.{nameof(Pitch)} must be between {MinValue} and {MaxValue}.");
            _pitch = value;
        }
    }
    public sbyte Roll
    {
        get => _roll;
        set
        {
            if (value < MinValue || value > MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"{GetType().Name}.{nameof(Roll)} must be between {MinValue} and {MaxValue}.");
            _roll = value;
        }
    }
    public sbyte Yaw
    {
        get => _yaw;
        set
        {
            if (value < MinValue || value > MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"{GetType().Name}.{nameof(Yaw)} must be between {MinValue} and {MaxValue}.");
            _yaw = value;
        }
    }

    public bool HoldingPosition
    {
        get
        {
            return _throttle == 0 &&
                   _roll == 0 &&
                   _pitch == 0 &&
                   _yaw == 0;
        }
    }

    public void HoldPosition()
    {
        _throttle = 0;
        _roll = 0;
        _pitch = 0;
        _yaw = 0;
    }

    public bool MoveLeft
    {
        get => Roll < 0;
        set
        {
            if (MoveLeft != value)
                Roll = value ? (sbyte)(MoveSpeedFactor*MinValue) : (sbyte)0;
        }
    }

    public bool MoveRight
    {
        get => Roll > 0;
        set
        {
            if (MoveRight != value)
                Roll = value ? (sbyte)(MoveSpeedFactor * MaxValue) : (sbyte)0;
        }
    }

    public bool MoveBackward
    {
        get => Pitch < 0;
        set
        {
            if (MoveBackward != value)
                Pitch = value ? (sbyte)(MoveSpeedFactor * MinValue) : (sbyte)0;
        }
    }

    public bool MoveForward
    {
        get => Pitch > 0;
        set
        {
            if (MoveForward != value)
                Pitch = value ? (sbyte)(MoveSpeedFactor * MaxValue) : (sbyte)0;
        }
    }

    public bool MoveDown
    {
        get => Throttle < 0;
        set
        {
            if (MoveDown != value)
                Throttle = value ? (sbyte)(MoveSpeedFactor * MinValue) : (sbyte)0;
        }
    }

    public bool MoveUp 
    {
        get => Throttle > 0;
        set
        {
            if (MoveUp != value)
                Throttle = value ? (sbyte)(MoveSpeedFactor * MaxValue) : (sbyte)0;
        }
    }

    public bool TurnLeft
    {
        get => Yaw < 0;
        set
        {
            if (TurnLeft != value)
                Yaw = value ? (sbyte)(MoveSpeedFactor * MinValue) : (sbyte)0;
        }
    }

    public bool TurnRight
    {
        get => Yaw > 0;
        set
        {
            if (TurnRight != value)
                Yaw = value ? (sbyte)(MoveSpeedFactor * MaxValue) : (sbyte)0;
        }
    }
}