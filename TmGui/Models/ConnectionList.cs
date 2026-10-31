namespace TmGui.Models;

using System.Collections.Generic;

public class ConnectionList
{
    private static readonly ConnectionList s_inst = new();

    public static ConnectionList Get() => s_inst;

    private readonly Dictionary<string, TargetProxy> targets = new();

    public TargetProxy? GetTarget(string serial) => targets.GetValueOrDefault(serial);

    public bool Register(string serial, TargetProxy target)
    {
        if (targets.ContainsKey(serial))
            return false;
        
        targets.Add(serial, target);
        return true;
    }

    public bool Unregister(string serial) => targets.Remove(serial);

    public bool IsConnected(string serial) => targets.ContainsKey(serial);
}