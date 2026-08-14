

namespace Test.env;

public static class HostEnvManager
{
    public static string? GetEnvironmentVariable(string name)
    {
        try {
            return Environment.GetEnvironmentVariable(name);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
