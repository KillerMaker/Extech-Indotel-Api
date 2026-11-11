namespace Exatech_Indotel_API.Utilities
{
    public class EmailQueueOptions
    {
        public const string SectionName = "EmailQueue";
        public string BootstrapServers { get; set; } = string.Empty;
        public string Topic { get; set; } = "email.requests";
        public string System { get; set; } = "exatech-indotel-api";
    }
}
