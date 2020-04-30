using UnityEngine;

internal static class GodMessages
{
    public const char KeyValueSeparator = ':';
    public const char FieldSeparator = ' ';
    public const string Update = "GOD-Update";

    public static string ToString(Vector3 v)
    {
        return $"{v.x}{KeyValueSeparator}{v.y}{KeyValueSeparator}{v.z}";
    }

    public static bool TryParse(string text, out Vector3 value)
    {
        if (text != null)
        {
            var segments = text.Split(KeyValueSeparator);
            if (segments.Length == 3 &&
                float.TryParse(segments[0], out var x) &&
                float.TryParse(segments[1], out var y) &&
                float.TryParse(segments[2], out var z))
            {
                value = new Vector3(x, y, z);
                return true;
            }
        }
        value = default;
        return false;
    }

    public static string ToString(GodScore score)
    {
        return score != null ? score.ToString() : "";
    }

    public static bool TryParse(string text, out GodScore value)
    {
        return GodScore.TryParse(text, out value);
    }
}
