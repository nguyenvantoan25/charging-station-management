namespace tramsac99.Services
{
    public class SmtpEmailSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = "TramSac99";
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
