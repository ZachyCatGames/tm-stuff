using Grpc.Core;
using Test.proto;
using Test.protos;

namespace Test;

public class TargetServiceImpl : Test.proto.TargetService.TargetServiceBase
{
    private ITargetManager manager;

    public TargetServiceImpl(ITargetManager manager)
    {
        this.manager = manager;
    }

    public override async Task<TargetCountMsg> QueryTargetCount(QueryTargetCountRequest request, ServerCallContext context)
    {
        int count = await manager.QueryTargetCountAsync(protos.Conversions.QueryTypeToImpl(request.Type));
        return new TargetCountMsg
        {
            Count = count
        };
    }
    
    public override async Task<TargetInfoMsg> QueryTargetInfo(QueryTargetInfoRequest request, ServerCallContext context)
    {
        var info = await manager.QueryTargetInfosAsync(Conversions.QueryTypeToImpl(request.Type), request.Index);
        return Conversions.ConvertTargetInfo(info, TargetResult.Success);
    }
    
    public override async Task<TargetInfoMsg> ConnectTarget(ConnectTargetRequest request, ServerCallContext context)
    {
        var info = await manager.ConnectTargetAsync(request.SerialNumber);
        if (info == null)
        {
            return Conversions.GetErrorTargetInfoMsg(TargetResult.TargetNotFound);
        }
        return Conversions.ConvertTargetInfo((TargetInfo)info, TargetResult.Success);
    }

    public override async Task<DisconnectTargetMsg> DisconnectTarget(DisconnectTargetRequest request, ServerCallContext context)
    {
        await manager.DisconnectTargetAsync(request.SerialNumber);
        return new DisconnectTargetMsg
        {
            Result = TargetResult.Success
        };
    }
}