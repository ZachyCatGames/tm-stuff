
using Test.htcs;

namespace Test.hio;

public class HioException : Exception {
    public HioErrorCode error;   
    
    public HioException(HioErrorCode err)
    {
        this.error = err;
    }
}
