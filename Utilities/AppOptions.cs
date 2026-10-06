
namespace Exatech_Indotel_API.Utilities
{
    public class AppOptions
    {
        public string SiubenUrl { get; set; } = string.Empty;
        public string SiubenPassword { get; set; } = string.Empty;
        public string SiubenUsername { get; set; } = string.Empty;
        public string WisproClientsUrl { get; set; } = string.Empty;
        public string WisproContractsUrl { get; set; } = string.Empty;
        public string WisproApiKey { get; set; } = string.Empty;
        public string EmailSender { get; set; } = string.Empty;
        public string EmailReciver {  get; set; } = string.Empty;
        public string EmailPassword { get; set; } = string.Empty;
        public string? EmailHost { get; set; } = null;
        public int EmailPort { get; set; }
        public string SecretKey { get; set; } = string.Empty;
        public string DatabaseConnectionString { get; set; } = string.Empty;
        public string ClientCreationTemplate { get; set; } = string.Empty;
        public string ClientUpdateRequestTemplate { get; set; } = string.Empty;
        public string EventHubName {  get; set; } = string.Empty;
        public string EventHubConnectionString {  get; set; } = string.Empty;
        public string BlobContainerName {  get; set; } = string.Empty;
        public string BlobStorageConnectionString { get; set; } = string.Empty;
        public string UserCreationTemplate { get; set; }
    }
}
