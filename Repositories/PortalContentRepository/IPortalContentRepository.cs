namespace Exatech_Indotel_API.Repositories.PortalContentRepository;

public interface IPortalContentRepository
{
    Task<IReadOnlyDictionary<string, string>> GetAll();
    Task Save(IReadOnlyDictionary<string, string> messages, int userId);
}
