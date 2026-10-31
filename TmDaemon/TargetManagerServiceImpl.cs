namespace Test;
using Grpc.Core;

public class TargetManagerServiceImpl : TargetManagerService.TargetManagerServiceBase
{
    private ITargetManager manager;
    
    public TargetManagerServiceImpl(ITargetManager manager)
    {
        this.manager = manager;
    }
    

    public override async Task<QueryTargetCountReply> QueryTargetCount(QueryTargetCountRequest request, ServerCallContext context)
    {
        int count = await manager.QueryTargetCountAsync(request.Type);
        return new QueryTargetCountReply
        {
            Result = TargetResult.Success,
            Count  = count
        };
    }
    
    public override async Task<QueryTargetInfoReply> QueryTargetInfo(QueryTargetInfoRequest request, ServerCallContext context)
    {
        var info = await manager.QueryTargetInfosAsync(request.Type, request.MaxCount);

        var t = new QueryTargetInfoReply
        {
            Result = TargetResult.Success,
        };
        t.Targets.AddRange(info);

        return t;
    }

    public override async Task<QueryTargetInfoBySerialReply> QueryTargetInfoBySerial(QueryTargetInfoBySerialRequest request, ServerCallContext context)
    {
        var info = await manager.QueryTargetInfoBySerialAsync(request.SerialNumber);

        if (info == null)
        {
            return new QueryTargetInfoBySerialReply()
            {
                Result = TargetResult.TargetNotFound
            };
        }

        return new QueryTargetInfoBySerialReply()
        {
            Result = TargetResult.Success,
            Info = info
        };
    }
}