public sealed class GodScore
{
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
        return $"{MyScore}:{TheirScore}";
    }

    public static bool TryParse(string text, out GodScore value)
    {
        if (text != null)
        {
            var colonIndex = text.IndexOf(':');
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
}