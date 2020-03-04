public enum TelloErrorCode
{
    NoError,
    PacketTooShort,
    PacketTooLong,
    UnsupportedMagic,
    BadCrc8,
    BadCrc16,
    UnexpectedAsciiSequence,
    InvalidAsciiCharacter,
    UnknownPacketType
}