namespace Exatech_Indotel_API.Utilities
{
    public class EmailQueueOptions
    {
        public Confluent.Kafka.SecurityProtocol SecurityProtocol { get; set; } = Confluent.Kafka.SecurityProtocol.Plaintext;
        public Confluent.Kafka.SaslMechanism? SaslMechanism { get; set; }
        public string? SaslUsername { get; set; }
        public string? SaslPassword { get; set; }
        public const string SectionName = "EmailQueue";
        public string BootstrapServers { get; set; } = string.Empty;
        public string Topic { get; set; } = "email.requests";
        public string System { get; set; } = "exatech-indotel-api";
    }
}
