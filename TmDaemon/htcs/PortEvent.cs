namespace Test.htcs;

public class PortEvent
{
    public PortInfo PortInfo { get; set; }
    public PortEventType EventType { get; set; }

    public PortEvent(PortInfo info, PortEventType eventType)
    {
        PortInfo  = info;
        EventType = eventType;
    }
}