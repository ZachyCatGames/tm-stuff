

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
                break;
            case TaskType.FileExists:
                break;
            case TaskType.ReadFile:
                task = new HostIOReadTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.WriteFile:
                break;
            case TaskType.FlushFile:
                break;
            case TaskType.SetPriorityForFile:
                break;
            case TaskType.GetPriorityForFile:
                break;
            case TaskType.CloseFile:
                task = new HostIOCloseTask(this, hostfsMgr, pkt.taskId);
                break;
            case TaskType.CreateFile:
                break;
            case TaskType.DeleteFile:
                break;
            case TaskType.RenameFile:
                break;
            case TaskType.GetIOType:
                break;
            case TaskType.GetFileTimeStamp:
                break;
            default:
                break;
        }

        return task;
    }
}
