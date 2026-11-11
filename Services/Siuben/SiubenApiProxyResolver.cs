using Exatech_Indotel_API.Services.Siuben.Phase1;
using Exatech_Indotel_API.Services.Siuben.Phase2;

namespace Exatech_Indotel_API.Services.Siuben;

public class SiubenApiProxyResolver(IServiceProvider services, SiubenLocalities localities) : ISiubenApiProxyResolver
{
    public ISiubenApiProxy Resolve(string locality) => localities.GetPhase(locality) switch
    {
        SiubenPhase.Phase1 => services.GetRequiredService<SiubenPhase1ApiProxy>(),
        SiubenPhase.Phase2 => services.GetRequiredService<SiubenPhase2ApiProxy>(),
        _ => throw new ArgumentException("Unsupported locality.", nameof(locality))
    };
}
