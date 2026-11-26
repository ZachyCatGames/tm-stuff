

namespace Test.Services.env;

public class HostEnvService : ServiceBase
{
    public const uint ServiceId = 0xC191DAC9;

    public HostEnvService(ServiceManager mgr) : base(ServiceId, mgr)
    {}

    public override ServiceTask? ProcessNewRequest(Packets.Packet pkt)
    {
        Console.WriteLine("Here");
        pkt.Print();
        ServiceTask? task = null;
        switch (pkt.taskType)
        {
            case TaskType.GetVar:
                task = new HostEnvGetVarTask(this, pkt.taskId);
                break;
        }
        
        return task;
    }
}
