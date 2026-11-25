using Test.Target;

namespace Test.TargetManager;

public interface ITargetEventCallback
{
    public void Notify(TargetEvent ev);
}