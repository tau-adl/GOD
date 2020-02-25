public struct TelloSdkCoordinates
{
    public float X { get; private set; }
    public float Y { get; private set; }
    public float Z { get; private set; }

    public TelloSdkCoordinates(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public override string ToString()
    {
        return $"({X:F2},{Y:F2},{Z:F2})";
    }
}