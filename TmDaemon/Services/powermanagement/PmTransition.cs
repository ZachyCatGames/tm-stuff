namespace Test.Services.powermanagement;

public enum PmTransition
{
    Suspending = 0,
    Suspended = 1,
    Resuming = 2,
    Resumed = 3,
    
    Unknown = -1
}