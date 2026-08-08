

using System.Runtime.CompilerServices;

namespace Test.hio;

public class HostIOService : Service {
    public const uint ServiceId = 0x5FAE4D7E;
    
    public HostIOService(ServiceManager mgr) : base(ServiceId, mgr) {}
    
    public override ServiceTask? ProcessNewRequest(Packet pkt)
    {
        ServiceTask? task = null;
        switch (pkt.taskType) {
            case TaskType.OpenFile:
                break;
            case TaskType.GetFileSize:
                break;
            case TaskType.SetFileSize:
                break;
            case TaskType.FileExists:
                break;
            case TaskType.ReadFile:
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
