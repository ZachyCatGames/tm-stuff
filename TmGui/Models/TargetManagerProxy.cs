using System.Linq;

namespace TmGui.Models;

using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Net.Client;

public class TargetManagerProxy
{
    private static TargetManagerService.TargetManagerServiceClient client;

    public static void Initialize(GrpcChannel channel)
    {
        client = new  TargetManagerService.TargetManagerServiceClient(channel);
    }
    
    public TargetManagerProxy() {}

    public async Task<(TargetResult, int)> QueryTargetCount(QueryType type)
    {
        var reply = await client.QueryTargetCountAsync(new QueryTargetCountRequest { Type = type });
        return (reply.Result, reply.Count);
    }

    public async Task<(TargetResult, List<TargetInfo>)> QueryTargetInfo(QueryType type, int maxCount)
    {
        var reply = await client.QueryTargetInfoAsync(new QueryTargetInfoRequest { Type = type, MaxCount = maxCount });
        return (reply.Result, reply.Targets.ToList());
    }
}