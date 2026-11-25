namespace Test.Services.powermanagement;

public enum PmState
{
    FullAwake = 0,
    MinimumAwake = 1,
    SleepReady = 2,
    EssentialServicesSleepReady = 3,
    EssentialServicesAwake = 4,
    ShutdownReady = 5,
    Invalid = 6,
}
