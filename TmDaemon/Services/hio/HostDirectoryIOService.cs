

namespace Test.Services.hio;

public class HostDirectoryIOService : ServiceBase {
    public const uint ServiceId = 0xB04489F2;
    
    readonly HostFilesystemManager hostfsMgr;
    
    public HostDirectoryIOService(ServiceManager mgr, HostFilesystemManager hostfsMgr) : base(ServiceId, mgr)
    {
        this.hostfsMgr = hostfsMgr;
    }

    public override ServiceTask? ProcessNewRequest(Packets.Packet pkt)
    {
        pkt.Print();
        ServiceTask? task = null;
        switch (pkt.taskType)
        {
            case TaskType.DirectoryExists:
                task = new HostDirectoryIOExistsTask(this, pkt.taskId);
                break;
            case TaskType.OpenDirectory:
                task = new HostDirectoryIOOpenTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.GetDirectoryEntryCount:
                task = new HostDirectoryIOGetEntryCountTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.ReadDirectory:
                task = new HostDirectoryIOReadTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.CloseDirectory:
                task = new HostDirectoryIOCloseTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.SetPriorityForDirectory:
                task = new HostDirectoryIOSetPriorityTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.GetPriorityForDirectory:
                task = new HostDirectoryIOGetPriorityTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.CreateDirectory:
                task = new HostDirectoryIOCreateTask(this, pkt.taskId);
                break;
            case TaskType.DeleteDirectory:
                task = new HostDirectoryIODeleteTask(this, pkt.taskId);
                break;
            case TaskType.RenameDirectory:
                task = new HostDirectoryIORenameTask(this, pkt.taskId);
                break;
        }
        return task;   
    }
}
