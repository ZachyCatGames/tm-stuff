

namespace Test.hio;

public class HostDirectoryIOService : Service {
    public const uint ServiceId = 0xB04489F2;
    
    readonly HostFilesystemManager hostfsMgr;
    
    public HostDirectoryIOService(ServiceManager mgr, HostFilesystemManager hostfsMgr) : base(ServiceId, mgr)
    {
        this.hostfsMgr = hostfsMgr;
    }

    public override ServiceTask? ProcessNewRequest(Packet pkt)
    {
        pkt.Print();
        ServiceTask? task = null;
        switch (pkt.taskType)
        {
            case TaskType.DirectoryExists:
                break;
            case TaskType.OpenDirectory:
                task = new HostDirectoryIOOpenTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.GetDirectoryEntryCount:
                break;
            case TaskType.ReadDirectory:
                break;
            case TaskType.CloseDirectory:
                task = new HostDirectoryIOCloseTask(this, hostfsMgr, pkt.taskId);
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
