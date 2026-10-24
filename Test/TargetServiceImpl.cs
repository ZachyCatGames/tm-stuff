using Grpc.Core;

namespace Test;

public class TargetServiceImpl : Test.TargetService.TargetServiceBase
{
    private ITargetManager manager;

    public TargetServiceImpl(ITargetManager manager)
    {
        this.manager = manager;
    }

    public override async Task<TargetCountMsg> QueryTargetCount(QueryTargetCountRequest request, ServerCallContext context)
    {
        int count = await manager.QueryTargetCountAsync();
        return new TargetCountMsg
        {
            Count = count
        };
    }
    
    public override async Task<TargetInfoMsg> QueryTargetInfo(QueryTargetInfoRequest request, ServerCallContext context)
    {
        var info = await manager.QueryTargetInfoAsync(request.Index);
        return ConvertTargetInfo(info, TargetResult.Success);
    }
    
    public override async Task<TargetCountMsg> QueryConnectedTargetCount(QueryConnectedTargetCountRequest request, ServerCallContext context)
    {
        int count = await manager.QueryConnectedTargetCountAsync();
        return new TargetCountMsg
        {
            Count = count
        };
    }
    
    public override async Task<TargetInfoMsg> QueryConnectedTargetInfo(QueryConnectedTargetInfoRequest request, ServerCallContext context)
    {
        var info = await manager.QueryConnectedTargetInfoAsync(request.Index);
        return ConvertTargetInfo(info, TargetResult.Success);
    }

    public override async Task<TargetInfoMsg> ConnectTarget(ConnectTargetRequest request, ServerCallContext context)
    {
        var info = await manager.ConnectTargetAsync(request.SerialNumber);
        if (info == null)
        {
            return GetErrorTargetInfoMsg(TargetResult.TargetNotFound);
        }
        return ConvertTargetInfo((TargetInfo)info, TargetResult.Success);
    }

    private TargetInfoMsg ConvertTargetInfo(TargetInfo info, TargetResult result)
    {
        return new TargetInfoMsg
        {
            Result         = result,
            Address        = info.address,
            SerialNumber   = info.serialNumber,
            ConnectionType = info.connectionType,
        };
    }

    private TargetInfoMsg GetErrorTargetInfoMsg(TargetResult result)
    {
        return new TargetInfoMsg
        {
            Result = result
        };
    }
}