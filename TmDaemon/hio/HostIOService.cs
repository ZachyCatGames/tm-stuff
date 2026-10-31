

using System.Runtime.CompilerServices;

namespace Test.hio;

public class HostIOService : Service {
    public const uint ServiceId = 0x5FAE4D7E;

    readonly HostFilesystemManager hostfsMgr;

    public HostIOService(ServiceManager mgr, HostFilesystemManager hostfsMgr) : base(ServiceId, mgr)
    {
        this.hostfsMgr = hostfsMgr;
    }
    
    public override ServiceTask? ProcessNewRequest(Packet pkt)
    {
        Console.WriteLine("hio recv packet");
        pkt.Print();
        ServiceTask? task = null;
        switch (pkt.taskType) {
            case TaskType.OpenFile:
                task = new HostIOOpenTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.GetFileSize:
                task = new HostIOGetSizeTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.SetFileSize:
                task = new HostIOSetSizeTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.FileExists:
                task = new HostIOExistsTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.ReadFile:
                task = new HostIOReadTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.WriteFile:
                task = new HostIOWriteTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.FlushFile:
                task = new HostIOFlushTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.SetPriorityForFile:
                task = new HostIOSetPriorityTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.GetPriorityForFile:
                task = new HostIOGetPriorityTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.CloseFile:
                task = new HostIOCloseTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.CreateFile:
                task = new HostIOCreateTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.DeleteFile:
                task = new HostIODeleteTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.RenameFile:
                task = new HostIORenameTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.GetIOType:
                task = new HostIOGetIOTypeTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.GetFileTimeStamp:
                task = new HostIOGetFileTimeStampTask(this, hostfsMgr, pkt.taskId);
                break;
            default:
                break;
        }

        return task;
    }
}
