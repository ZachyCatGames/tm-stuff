

namespace Test.hio;

public class HostDirectoryIOService : Service {
    public const uint ServiceId = 0xB04489F2;
    
    public HostDirectoryIOService(ServiceManager mgr) : base(ServiceId, mgr) {}
    
    public override ServiceTask? ProcessNewRequest(Packet pkt)
    {
        ServiceTask? task = null;
        switch (pkt.taskType)
        {
            case TaskType.DirectoryExists:
                break;
            case TaskType.OpenDirectory:
                break;
            case TaskType.GetDirectoryEntryCount:
                break;
            case TaskType.ReadDirectory:
                break;
            case TaskType.CloseDirectory:
                break;
            case TaskType.SetPriorityForDirectory:
                break;
            case TaskType.GetPriorityForDirectory:
                break;
            case TaskType.CreateDirectory:
                break;
            case TaskType.DeleteDirectory:
                break;
            case TaskType.RenameDirectory:
                break;
        }
        return task;   
    }
}
