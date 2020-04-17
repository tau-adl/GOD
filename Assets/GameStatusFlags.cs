using System;
using System.Threading;

[Flags]
public enum GameStatusFlags
{
    None = 0,
    ValueChanged = 1,
    VuforiaReady = 2,
    DroneReady = 4,
    PartnerReady = 8,
    PartnerConnected = 0x10,
    UserReady = 0x20,
    DroneAirborne = 0x40,

    AllClear = UserReady | VuforiaReady | DroneReady | PartnerReady
}

public static class GameStatusFlagsExtensions
{
    /// <summary>
    /// Safely set a flag on the backing field of a <see cref="GameStatusFlags"/> property.
    /// </summary>
    /// <param name="field">The field to be changed</param>
    /// <param name="flag">the flag to set on the field.</param>
    /// <returns>The new value of the field.</returns>
    public static GameStatusFlags SetOnField(this GameStatusFlags flag, ref int field)
    {
        int original, desired, result;
        flag |= GameStatusFlags.ValueChanged;
        do
        {
            original = field;
            desired = original | (int)flag;
            result = Interlocked.CompareExchange(
                ref field, desired, original);
        } while (result != original);
        return (GameStatusFlags)desired;
    }

    /// <summary>
    /// Safely unset a flag on the backing field of a <see cref="GameStatusFlags"/> property.
    /// </summary>
    /// <param name="field">The field to be changed</param>
    /// <param name="flag">the flag to unset on the field.</param>
    /// <returns>The new value of the field.</returns>
    public static GameStatusFlags UnsetOnField(this GameStatusFlags flag, ref int field)
    {
        int original, desired, result;
        var negFlag = (int)~flag;
        do
        {
            original = field;
            desired = (original & negFlag) | (int)GameStatusFlags.ValueChanged;
            result = Interlocked.CompareExchange(
                ref field, desired, original);
        } while (result != original);
        return (GameStatusFlags)desired;
    }

    /// <summary>
    /// Safely set or unset a flag on the backing field of a <see cref="GameStatusFlags"/> property.
    /// </summary>
    /// <param name="field">The field to be changed</param>
    /// <param name="flagValue">The new value for the flag.</param>
    /// <param name="flag">the flag to be changed on the field.</param>
    /// <returns>The new value of the field.</returns>
    public static GameStatusFlags ChangeOnField(this GameStatusFlags flag, bool flagValue, ref int field)
    {
        return flagValue ? SetOnField(flag, ref field) : UnsetOnField(flag, ref field);
    }

    /// <summary>
    /// Safely get the value of a backing field of a <see cref="GameStatusFlags"/> property and reset the <seealso cref="GameStatusFlags.ValueChanged"/> flag.
    /// </summary>
    /// <returns>The original value of the field, before the <seealso cref="GameStatusFlags.ValueChanged"/> flag was reset.</returns>
    public static GameStatusFlags GetAndResetChanges(ref int field)
    {
        const int notChanged = (int)~GameStatusFlags.ValueChanged;
        int original, result;
        do
        {
            original = field;
            var desired = original & notChanged;
            result = Interlocked.CompareExchange(
                ref field, desired, original);
        } while (result != original);

        return (GameStatusFlags)original;
    }
}
