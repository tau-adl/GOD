public enum TelloCommandId : ushort
{
    GetSsid = 17, //  pt48
    SetSsid = 18, //  pt68
    GetSsidPassword = 19, //  pt48
    SetSsidPassword = 20, //  pt68
    GetRegion = 21, //  pt48
    SetRegion = 22, //  pt68
    GetVideoSpsPps = 37, //  pt60
    TakePicture = 48, //  pt68
    SwitchPictureVideo = 49, //  pt68
    StartRecording = 50, //  pt68
    ExposureValue = 52, //  pt48
    DateTime = 70, //  pt50
    Stick = 80, //  pt60
    LogHeaderWrite = 4176, //  pt50
    LogDataWrite = 4177, //  rx_o
    LogConfiguration = 4178, //  pt50
    GetWifiSignalPower = 26, //  rx_o
    SetVideoBitRate = 40, //  pt48
    GetVideoBitRate = 32, //  pt68
    GetLightStrength = 53, //  rx_o
    GetVersionString = 69, //  pt48
    GetActivationTime = 71, //  pt48
    GetLoaderVersion = 73, //  pt48
    Status = 86, //  rx_o
    PlaneCalibration = 4180, //  pt68
    GetAltitudeLimit = 4182, //  pt48
    SetAltitudeLimit = 88, //  pt68
    GetLowBatteryThreshold = 4183, //  pt48
    SetLowBatteryThreshold = 4181, //  pt68
    GetAttitudeAngle = 4185, //  pt48
    SetAttitudeAngle = 4184, //  pt68
    SetJpegQuality = 55, //  pt68
    TakeOff = 84, //  pt68
    Land = 85, //  pt68

    Flip = 92, //  pt70
    ThrowFly = 93, //  pt48
    LandOnPalm = 94, //  pt48

    Error1 = 67, //  rx_o
    Error2 = 68, //  rx_o
    GetFileSize = 98, //  pt50
    GetFileData = 99, //  pt50
    GetFileComplete = 100, //  pt48
    HandleImuAngle = 90, //  pt48

    SetDynamicAdjustmentRate = 33, //  pt68
    SetEis = 36, //  pt68
    SmartVideoStart = 128, //  pt68
    SmartVideoStatus = 129, //  pt50
    Bounce = 4179, //  pt68
}