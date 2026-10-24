namespace Test;

public struct BeaconPacket
{
    private Dictionary<string, string> keys;
    
    public string Spec;
    public string TMA;
    public int TMIPC;
    public string Conn;
    public string HW;
    public string Name;
    public string SN;
    public int BCID;
    public int PS;
    public int PMS;
    public string FW;
    public int CD;

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
        TryGetStr("Spec", out Spec, "Unknown");
        TryGetStr("TMA", out TMA, "Unknown");
        TryGetStr("Conn", out Conn, "Unknown");
        TryGetStr("HW", out HW, "Unknown");
        TryGetStr("Name", out Name, "Unknown");
        TryGetStr("SN", out SN, "Unknown");
        TryGetStr("FW", out FW, "Unknown");

        TryGetInt("TMIPC", out TMIPC, 4);
        TryGetInt("BCID", out BCID, 0);
        TryGetInt("PS", out PS, 0);
        TryGetInt("PMS", out PMS, 0);
        TryGetInt("CD", out CD, 0);
    }

    public bool TryGetStr(string key, out string val, string def = "")
    {
        bool has = keys.TryGetValue(key, out val);
        if (!has)
            val = def;
        return has;
    }

    public bool TryGetInt(string key, out int val, int def = 0)
    {
        bool has = keys.TryGetValue(key, out var tmp);
        val = has ? int.Parse(tmp) : def;
        return has;
    }
}