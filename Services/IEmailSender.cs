namespace tramsac99.Services
{
    public interface IEmailSender
    {
        Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
    }
}
