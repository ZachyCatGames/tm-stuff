

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
                task = new HostDirectoryIOExistsTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.OpenDirectory:
                task = new HostDirectoryIOOpenTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.GetDirectoryEntryCount:
                task = new HostDirectoryIOGetEntryCountTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.ReadDirectory:
                task = new HostDirectoryIOReadTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.CloseDirectory:
                task = new HostDirectoryIOCloseTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.SetPriorityForDirectory:
                task = new HostDirectoryIOSetPriorityTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.GetPriorityForDirectory:
                task = new HostDirectoryIOGetPriorityTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.CreateDirectory:
                task = new HostDirectoryIOCreateTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.DeleteDirectory:
                task = new HostDirectoryIODeleteTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.RenameDirectory:
                task = new HostDirectoryIORenameTask(this, hostfsMgr, pkt.taskId);
                break;
        }
        return task;   
    }
}
