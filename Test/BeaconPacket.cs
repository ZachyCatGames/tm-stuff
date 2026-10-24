namespace Test;

public struct BeaconPacket
{
    private Dictionary<string, string> keys;
    
    public string? Spec;
    public string? TMA;
    public int? TMIPC;
    public string? Conn;
    public string? HW;
    public string? Name;
    public string? SN;
    public int? BCID;
    public int? PS;
    public int? PMS;
    public string? FW;
    public int? CD;

    public BeaconPacket(string beacon)
    {
        keys = new Dictionary<string, string>();
        
        using (var read = new StringReader(beacon))
        {
            string line;
            while ((line = read.ReadLine()) != null)
            {
                var parts = line.Split(':');

                keys[parts[0].Trim()] = parts[1].Trim();
            }
        }

        /* Parse known fields. */
        keys.TryGetValue("Spec", out Spec);
        keys.TryGetValue("TMA", out TMA);
        keys.TryGetValue("Conn", out Conn);
        keys.TryGetValue("HW", out HW);
        keys.TryGetValue("Name", out Name);
        keys.TryGetValue("SN", out SN);
        keys.TryGetValue("FW", out FW);

        TryGetInt("TMIPC", out TMIPC);
        TryGetInt("BCID", out BCID);
        TryGetInt("PS", out PS);
        TryGetInt("PMS", out PMS);
        TryGetInt("CD", out CD);
    }

    public bool TryGetInt(string key, out int? val)
    {
        bool has = keys.TryGetValue(key, out var tmp);
        val = has ? int.Parse(tmp) : null;
        return has;
    }
}