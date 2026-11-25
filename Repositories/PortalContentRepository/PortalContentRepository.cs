using System.Data;
using Dapper;
using Exatech_Indotel_API.Utilities.Factories;

namespace Exatech_Indotel_API.Repositories.PortalContentRepository;

public sealed class PortalContentRepository(IDatabaseConnectionFactory factory) : IPortalContentRepository
{
    public async Task<IReadOnlyDictionary<string, string>> GetAll()
    {
        using var connection = factory.GetOpenConnection();
        var rows = await connection.QueryAsync<ContentRow>("SELECT [Key], [Value] FROM dbo.PortalContent");
        return rows.ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
    }

    public async Task Save(IReadOnlyDictionary<string, string> messages, int userId)
    {
        using var connection = factory.GetOpenConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        foreach (var message in messages.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            await connection.ExecuteAsync("""
                UPDATE dbo.PortalContent WITH (UPDLOCK) SET [Value]=@Value, ModifiedAt=SYSUTCDATETIME(), ModifiedByUserId=@UserId WHERE [Key]=@Key;
                IF @@ROWCOUNT=0 INSERT INTO dbo.PortalContent ([Key], [Value], ModifiedByUserId) VALUES (@Key, @Value, @UserId);
                """, new { message.Key, message.Value, UserId = userId }, transaction);
        }
        transaction.Commit();
    }

    private sealed class ContentRow
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
    }
}
