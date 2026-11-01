namespace Test;

public class TargetEvent
{
    public TargetInfo Info { get; set; }
    public TargetEventType EventType { get; set; }

    public TargetEvent(TargetInfo info, TargetEventType type)
    {
        Info      = info;
        EventType = type;
    }
}