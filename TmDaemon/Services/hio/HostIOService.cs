

namespace Test.Services.hio;

public class HostIOService : ServiceBase {
    public const uint ServiceId = 0x5FAE4D7E;

    readonly HostFilesystemManager hostfsMgr;

    public HostIOService(ServiceManager mgr, HostFilesystemManager hostfsMgr) : base(ServiceId, mgr)
    {
        this.hostfsMgr = hostfsMgr;
    }
    
    public override ServiceTask? ProcessNewRequest(Packets.Packet pkt)
    {
        Console.WriteLine("hio recv packet");
        pkt.Print();
        ServiceTask? task = null;
        switch (pkt.taskType) {
            case TaskType.OpenFile:
                task = new HostIOOpenTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.GetFileSize:
                task = new HostIOGetSizeTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.SetFileSize:
                task = new HostIOSetSizeTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.FileExists:
                task = new HostIOExistsTask(this,  pkt.taskId);
                break;
            case TaskType.ReadFile:
                task = new HostIOReadTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.WriteFile:
                task = new HostIOWriteTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.FlushFile:
                task = new HostIOFlushTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.SetPriorityForFile:
                task = new HostIOSetPriorityTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.GetPriorityForFile:
                task = new HostIOGetPriorityTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.CloseFile:
                task = new HostIOCloseTask(this, this.hostfsMgr, pkt.taskId);
                break;
            case TaskType.CreateFile:
                task = new HostIOCreateTask(this, pkt.taskId);
                break;
            case TaskType.DeleteFile:
                task = new HostIODeleteTask(this, pkt.taskId);
                break;
            case TaskType.RenameFile:
                task = new HostIORenameTask(this, pkt.taskId);
                break;
            case TaskType.GetIOType:
                task = new HostIOGetIOTypeTask(this, pkt.taskId);
                break;
            case TaskType.GetFileTimeStamp:
                task = new HostIOGetFileTimeStampTask(this, pkt.taskId);
                break;
            default:
                break;
        }

        return task;
    }
}
