
namespace Exatech_Indotel_API.Utilities
{
    public class AppOptions
    {
        public string SiubenUrl { get; set; } = string.Empty;
        public string SiubenPassword { get; set; } = string.Empty;
        public string SiubenUsername { get; set; } = string.Empty;
        public string WisproUrl { get; internal set; }
        public IEnumerable<string?> WisproApiKey { get; internal set; }
        public string EmailSender { get; internal set; }
        public string EmailPassword { get; internal set; }
        public string? EmailHost { get; internal set; }
        public int EmailPort { get; internal set; }
    }
}
