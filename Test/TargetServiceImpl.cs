using Grpc.Core;

namespace Test;

public class TargetServiceImpl : Test.TargetService.TargetServiceBase
{
    private ITargetManager manager;
    private Target? target;

    public TargetServiceImpl(ITargetManager manager)
    {
        this.manager = manager;
    }
    
    public override async Task<ConnectTargetReply> Connect(ConnectTargetRequest request, ServerCallContext context)
    {
        var info = await manager.ConnectTargetAsync(request.SerialNumber);
        if (info == null)
            return new ConnectTargetReply { Result = TargetResult.TargetNotFound };

        return new ConnectTargetReply
        {
            Result = TargetResult.Success,
            Info   = info
        };
    }

    public override async Task<DisconnectTargetReply> Disconnect(DisconnectTargetRequest request, ServerCallContext context)
    {
        if (target == null)
            return new DisconnectTargetReply { Result = TargetResult.NotConnected };
        
        /* Disconnect the target. */
        await target.DisconnectAsync();
        
        return new DisconnectTargetReply
        {
            Result = TargetResult.Success
        };
    }

    public override async Task<CloseTargetReply> Close(CloseTargetRequest request, ServerCallContext context)
    {
        if (target == null)
            return new CloseTargetReply { Result = TargetResult.NotConnected };
        
        /* Close this reference. */
        target.Close();
        
        return new CloseTargetReply { 
            Result = TargetResult.Success
        };
    }

    public override async Task<QueryHtcsPortByNameReply> QueryHtcsPortByName(QueryHtcsPortByNameRequest request, ServerCallContext context)
    {
        if (target == null)
            return new QueryHtcsPortByNameReply { Result = TargetResult.NotConnected };
        
        /* Call on target. */
        var info = target.QueryHtcsPortByName(request.PortName);

        if (info == null)
            return new QueryHtcsPortByNameReply { Result = TargetResult.PortNotFound };
        
        /* Setup our reply. */
        return new QueryHtcsPortByNameReply { Result = TargetResult.Success, Info = info };
    }
    
    public override async Task<QueryHtcsPortCountReply> QueryHtcsPortCount(QueryHtcsPortCountRequest request, ServerCallContext context)
    {
        if (target == null)
            return new QueryHtcsPortCountReply { Result = TargetResult.NotConnected };
        
        /* Call on target. */
        int count = target.QueryHtcsPortCount();
        
        /* Setup our reply. */
        return new QueryHtcsPortCountReply { Result = TargetResult.Success, Count = count };
    }
    
    public override async Task<QueryHtcsPortInfosReply> QueryHtcsPortInfos(QueryHtcsPortInfosRequest request, ServerCallContext context)
    {
        if (target == null)
            return new QueryHtcsPortInfosReply { Result = TargetResult.NotConnected };

        if (request.MaxCount < 0)
            return new QueryHtcsPortInfosReply { Result = TargetResult.InvalidSize };
        
        /* Call on target. */
        var infos = target.QueryHtcsPortInfos(request.MaxCount);

        /* Setup our reply. */
        var reply = new QueryHtcsPortInfosReply { Result = TargetResult.Success };
        reply.Infos.AddRange(infos);
        return reply;
    }
}