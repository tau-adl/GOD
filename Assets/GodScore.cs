using System.Runtime.Remoting.Messaging;

public sealed class GodScore
{
    private const char ValueSeparator = GodMessages.KeyValueSeparator;

    public uint MyScore { get; set; }
    public uint TheirScore { get; set; }

    public GodScore()
    {
    }

    public GodScore(uint myScore, uint theirScore)
    {
        MyScore = myScore;
        TheirScore = theirScore;
    }

    public override string ToString()
    {
        return $"{MyScore}{ValueSeparator}{TheirScore}";
    }

    public static bool TryParse(string text, out GodScore value)
    {
        if (text != null)
        {
            var colonIndex = text.IndexOf(ValueSeparator);
            if (colonIndex > 0)
            {
                var myScoreText = text.Substring(0, colonIndex);
                var theirScoreText = text.Substring(colonIndex + 1);
                if (uint.TryParse(myScoreText, out uint myScore) &&
                    uint.TryParse(theirScoreText, out uint theirScore))
                {
                    value = new GodScore(myScore, theirScore);
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    public bool ScoreEquals(GodScore other)
    {
        return other != null && MyScore == other.MyScore && TheirScore == other.TheirScore;
    }
}