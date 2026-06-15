namespace LocalLLM.Core;

public sealed record TimeWindow(TimeOnly Start, TimeOnly End, string DisplayText)
{
    public bool Contains(DateTime timestamp)
    {
        var time = TimeOnly.FromDateTime(timestamp);

        if (Start <= End)
        {
            return time >= Start && time <= End;
        }

        return time >= Start || time <= End;
    }
}
