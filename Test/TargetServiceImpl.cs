using Grpc.Core;

namespace Test;

public class TargetServiceImpl : Test.TargetService.TargetServiceBase
{
    private ITargetManager parent;

    public TargetServiceImpl(ITargetManager parent)
    {
        this.parent = parent;
    }

    public override async Task<TargetCountMsg> QueryTargetCount(QueryTargetCountRequest request, ServerCallContext context)
    {
        int count = await parent.QueryTargetCountAsync();
        return new TargetCountMsg
        {
            Count = count
        };
    }
    
    public override async Task<TargetInfoMsg> QueryTargetInfo(QueryTargetInfoRequest request, ServerCallContext context)
    {
        var info = await parent.QueryTargetInfoAsync(request.Index);
        return new TargetInfoMsg
        {
            Address        = info.address,
            SerialNumber   = info.serialNumber,
            ConnectionType =  info.connectionType,
        };
    }
    
    public override async Task<TargetCountMsg> QueryConnectedTargetCount(QueryConnectedTargetCountRequest request, ServerCallContext context)
    {
        int count = await parent.QueryConnectedTargetCountAsync();
        return new TargetCountMsg
        {
            Count = count
        };
    }
    
    public override async Task<TargetInfoMsg> QueryConnectedTargetInfo(QueryConnectedTargetInfoRequest request, ServerCallContext context)
    {
        var info = await parent.QueryConnectedTargetInfoAsync(request.Index);
        return new TargetInfoMsg
        {
            Address        = info.address,
            SerialNumber   = info.serialNumber,
            ConnectionType =  info.connectionType,
        };
    }
}