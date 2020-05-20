using System;
using System.Globalization;
using System.Text;
using UnityEngine;

public sealed class GodUpdateDatagram
    : GodDatagram
{
    #region Constants

    /// <summary>
    /// The maximum datagram size in bytes when serialized.
    /// </summary>
    public const int MaxSize = 256;

    private const char KeyValueSeparator = GodMessages.KeyValueSeparator;
    private const char FieldSeparator = GodMessages.FieldSeparator;

    private static class Fields
    {
        public const string BallPosition = "ball-p";
        public const string BallVelocity = "ball-s";
        public const string DronePosition = "drone-p";
        public const string Score = "score";
        public const string GameStatus = "status";
    }

    #endregion Constants

    #region Properties

    public uint SequenceNumber { get; set; }
    public Vector3? BallPosition { get; set; }
    public Vector3? BallVelocity { get; set; }
    public Vector3? DronePosition { get; set; }
    public GodScore Score { get; set; }
    public GameStatusFlags GameStatus { get; set; }

    #endregion Properties

    #region Methods

    public GodUpdateDatagram()
        : base(GodDatagramType.Update)
    {
    }

    private static bool TryParse(string text, GodUpdateDatagram datagram)
    {
        if (text == null || !text.StartsWith(GodMessages.Update, StringComparison.Ordinal))
            return false;
        var pairs = text.Split(FieldSeparator);
        if (pairs.Length <= 2)
            return true; // packet does not have a body.
        // parse sequence number:
        if (uint.TryParse(pairs[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequenceNumber))
            datagram.SequenceNumber = sequenceNumber;
        else return false;
        // parse other fields:
        for (var i = 2; i < pairs.Length; ++i)
        {
            var keyLength = pairs[i].IndexOf(KeyValueSeparator);
            if (keyLength <= 0)
                return false; // invalid key.
            var key = pairs[i].Substring(0, keyLength);
            var valueText = pairs[i].Substring(keyLength + 1);
            switch (key)
            {
                case Fields.BallPosition:
                {
                    if (!GodMessages.TryParse(valueText, out Vector3 value))
                        return false;
                    datagram.BallPosition = value;
                    break;
                }
                case Fields.BallVelocity:
                {
                    if (!GodMessages.TryParse(valueText, out Vector3 value))
                        return false;
                    datagram.BallVelocity = value;
                    break;
                }
                case Fields.DronePosition:
                {
                    if (!GodMessages.TryParse(valueText, out Vector3 value))
                        return false;
                    datagram.DronePosition = value;
                    break;
                }
                case Fields.Score:
                {
                    if (!GodMessages.TryParse(valueText, out GodScore value))
                        return false;
                    datagram.Score = value;
                    break;
                }
                case Fields.GameStatus:
                {
                    if (!int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                        return false;
                    datagram.GameStatus = (GameStatusFlags) value;
                    break;
                }
                default:
                    continue; // skip unknown key.
            }
        }

        return true;
    }

    public static bool TryParse(string text, out GodUpdateDatagram datagram)
    {
        datagram = new GodUpdateDatagram();
        return TryParse(text, datagram);
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(GodMessages.Update);
        builder.Append(' ');
        builder.Append(SequenceNumber.ToString("D"));
        if (BallPosition.HasValue)
        {
            builder.Append($" {Fields.BallPosition}{KeyValueSeparator}");
            builder.Append(GodMessages.ToString(BallPosition.Value));
        }

        if (BallVelocity.HasValue)
        {
            builder.Append($" {Fields.BallVelocity}{KeyValueSeparator}");
            builder.Append(GodMessages.ToString(BallVelocity.Value));
        }

        if (DronePosition.HasValue)
        {
            builder.Append($" {Fields.DronePosition}{KeyValueSeparator}");
            builder.Append(GodMessages.ToString(DronePosition.Value));
        }

        if (Score != null)
        {
            builder.Append($" {Fields.Score}{KeyValueSeparator}");
            builder.Append(GodMessages.ToString(Score));
        }

        // append game status:
        var gameStatus = (int) (GameStatus & ~GameStatusFlags.ValueChanged);
        builder.Append($" {Fields.GameStatus}{KeyValueSeparator}");
        builder.Append(gameStatus.ToString("D"));
        // return generated string:
        return builder.ToString();
    }

    public static bool TryDeserialize(byte[] buffer, int offset, int count, out GodUpdateDatagram datagram)
    {
        var text = Encoding.ASCII.GetString(buffer, 0, count);
        return TryParse(text, out datagram);
    }

    public static bool TryDeserialize(byte[] buffer, out GodUpdateDatagram datagram)
    {
        return TryDeserialize(buffer, 0, buffer.Length, out datagram);
    }

    #endregion Methods
}