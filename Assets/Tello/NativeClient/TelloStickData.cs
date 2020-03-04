using System;

public class TelloStickData
{
    public const short MinValue = -165;
    public const short MaxValue = +165;

    private short _fast;
    private short _throttle;
    private short _roll;
    private short _pitch;
    private short _yaw;

    public short Fast
    {
        get => _fast; set
        {
            if (value < MinValue || value > MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"{GetType().Name}.{nameof(Fast)} must be between {MinValue} and {MaxValue}.");
            _fast = value;
        }
    }

    public short Throttle
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
    public short Roll
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
    public short Pitch
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
    public short Yaw
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
                Roll = value ? TelloStickData.MinValue : (short)0;
        }
    }

    public bool MoveRight
    {
        get => Roll > 0;
        set
        {
            if (MoveRight != value)
                Roll = value ? TelloStickData.MaxValue : (short)0;
        }
    }

    public bool MoveBackward
    {
        get => Pitch < 0;
        set
        {
            if (MoveBackward != value)
                Pitch = value ? TelloStickData.MinValue : (short)0;
        }
    }

    public bool MoveForward
    {
        get => Pitch > 0;
        set
        {
            if (MoveForward != value)
                Pitch = value ? TelloStickData.MaxValue : (short)0;
        }
    }

    public bool MoveDown
    {
        get => Throttle < 0;
        set
        {
            if (MoveDown != value)
                Throttle = value ? TelloStickData.MinValue : (short)0;
        }
    }

    public bool MoveUp 
    {
        get => Throttle > 0;
        set
        {
            if (MoveUp != value)
                Throttle = value ? TelloStickData.MaxValue : (short)0;
        }
    }

    public bool TurnLeft
    {
        get => Yaw < 0;
        set
        {
            if (TurnLeft != value)
                Yaw = value ? TelloStickData.MinValue : (short)0;
        }
    }

    public bool TurnRight
    {
        get => Yaw > 0;
        set
        {
            if (TurnRight != value)
                Yaw = value ? TelloStickData.MaxValue : (short)0;
        }
    }
}