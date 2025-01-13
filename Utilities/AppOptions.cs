
namespace Exatech_Indotel_API.Utilities
{
    public class AppOptions
    {
        public string SiubenUrl { get; set; } = string.Empty;
        public string SiubenPassword { get; set; } = string.Empty;
        public string SiubenUsername { get; set; } = string.Empty;
        public string WisproUrl { get; set; } = string.Empty;
        public string WisproApiKey { get; set; } = string.Empty;
        public string EmailSender { get; set; } = string.Empty;
        public string EmailPassword { get; set; } = string.Empty;
        public string? EmailHost { get; set; } = null;
        public int EmailPort { get; set; }
    }
}
