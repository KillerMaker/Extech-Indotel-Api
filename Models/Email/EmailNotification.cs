namespace Exatech_Indotel_API.Models.Email
{
    public class EmailNotification
    {
        public required string To { get; set; }
        public required string Subject { get; set; }
        public required string Body { get; set; }
        public IEnumerable<string>? CC { get; set; }
    }
}
