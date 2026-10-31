namespace TmGui.ViewModels;

using System.Threading.Tasks;
using System.Collections.Generic;
using Models;

public class TargetViewModel
{
    private TargetProxy proxy;

    private TargetInfo? cachedInfo;

    public TargetViewModel(TargetProxy proxy)
    {
        this.proxy = proxy;
    }


}