using Exatech_Indotel_API.Repositories.PortalContentRepository;

namespace Exatech_Indotel_API.Simulation;

public sealed class SimulationPortalContent : IPortalContentRepository
{
    private readonly Dictionary<string, string> _messages = new();
    public Task<IReadOnlyDictionary<string, string>> GetAll()
    {
        lock (_messages) return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(_messages));
    }
    public Task Save(IReadOnlyDictionary<string, string> messages, int userId)
    {
        lock (_messages) foreach (var item in messages) _messages[item.Key] = item.Value;
        return Task.CompletedTask;
    }
}
