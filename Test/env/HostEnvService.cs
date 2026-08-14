

namespace Test.env;

public class HostEnvService : Service
{
    public const uint serviceId = 0xC191DAC9;

    public HostEnvService(ServiceManager mgr) : base(serviceId, mgr)
    {}

    public override ServiceTask? ProcessNewRequest(Packet pkt)
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
