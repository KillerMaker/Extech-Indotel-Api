namespace Exatech_Indotel_API.Services.Siuben;

public interface ISiubenApiProxyResolver
{
    ISiubenApiProxy Resolve(string locality);
}
