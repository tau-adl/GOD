using System;

namespace Assets.Tello.NativeClient
{
	[Flags]
	public enum TelloNativeAlarmFlags : byte
	{
		None = 0,
		ImuAlarm = 1,
		PressureAlarm = 2,
		DownVisualAlarm = 4,
		PowerAlarm = 8,
		BatteryAlarm = 0x10,
		GravityAlarm = 0x20,
		UnknownAlarm0x40 = 0x40,
		WindAlarm = 0x80
	}

	[Flags]
	public enum TelloNativeStatusFlags : byte
	{
		None = 0,
		Flying = 1,
		OnGround = 2,
		EmOpen = 4,
		DroneHover = 8,
		OutageRecording = 0x10,
		BatteryLow = 0x20,
		BatteryCritical = 0x40,
		FactoryMode = 0x80
	}

	[Flags]
	public enum TelloNativeFrontFlags : byte
	{
		None = 0,
		In = 1,
		Out = 2,
		Lsc = 4
	}

	public class TelloNativeStatus
    {
		public const int BodySize = 24;

		public short Height { get; set; }
		public short NorthSpeed { get; set; }
		public short EastSpeed { get; set; }
		public short VerticalSpeed { get; set; }
		public ushort FlyTime { get; set; }

		public TelloNativeAlarmFlags AlarmFlags { get; set; }
		public TelloNativeStatusFlags StatusFlags { get; set; }
		public TelloNativeFrontFlags FrontFlags { get; set; }
		public sbyte ImuCalibrationState { get; set; }
		public byte BatteryPercent { get; set; }
		public ushort DroneFlyTimeLeft { get; set; }
		public ushort BatteryMilliVolts { get; set; }

		public byte FlightMode { get; set; }
		public byte ThrowFlyTimer { get; set; }
		public byte CameraAlarm { get; set; }
		public byte ElectricalMachineryAlarm { get; set; }


		public bool GeneralAlarm { get; set; }

		public TelloErrorCode Deserialize(byte[] buffer, int count)
		{
			if (count != BodySize)
				return count > BodySize ? TelloErrorCode.PacketTooLong : TelloErrorCode.PacketTooShort;

			Height = unchecked((short)(buffer[0] | (buffer[1] << 8)));
			NorthSpeed = unchecked((short)(buffer[2] | (buffer[3] << 8)));
			EastSpeed = unchecked((short)(buffer[4] | (buffer[5] << 8)));
			VerticalSpeed = unchecked((short)(buffer[6] | (buffer[7] << 8)));
			FlyTime = unchecked((ushort)(buffer[8] | (buffer[9] << 8)));

			AlarmFlags = (TelloNativeAlarmFlags)buffer[10];

			ImuCalibrationState = unchecked((sbyte)buffer[11]);
			BatteryPercent = buffer[12];
			DroneFlyTimeLeft = unchecked((ushort)(buffer[13] | (buffer[14] << 8)));
			BatteryMilliVolts = unchecked((ushort)(buffer[15] | (buffer[16] << 8)));

			StatusFlags = (TelloNativeStatusFlags)buffer[17];

			FlightMode = buffer[18];
			ThrowFlyTimer = buffer[19];
			CameraAlarm = buffer[20];
			ElectricalMachineryAlarm = buffer[21];

			FrontFlags = (TelloNativeFrontFlags)buffer[22];
			GeneralAlarm = (buffer[23] & 1) != 0;

			return TelloErrorCode.NoError;
		}
    }
}
