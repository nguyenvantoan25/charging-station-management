using PayOS;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;
using tramsac99.Areas.Admin.Models;

namespace tramsac99.Services
{
    public class PayOsCheckoutService
    {
        private readonly IConfiguration _configuration;

        public PayOsCheckoutService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool IsConfigured()
        {
            var clientId = _configuration["PayOS:ClientId"];
            var apiKey = _configuration["PayOS:ApiKey"];
            var checksumKey = _configuration["PayOS:ChecksumKey"];

            return !string.IsNullOrWhiteSpace(clientId)
                && !string.IsNullOrWhiteSpace(apiKey)
                && !string.IsNullOrWhiteSpace(checksumKey);
        }

        public Task<(long orderCode, string checkoutUrl, bool isFallback)> CreateStationPaymentAsync(
            StationRegistrationRequest request,
            string returnUrl,
            string cancelUrl,
            string fallbackUrl)
        {
            var orderCode = request.PayOsOrderCode ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var description = $"Dang ky tram #{request.Id}";
            return CreatePaymentLinkAsync(orderCode, request.FeeAmount, description, returnUrl, cancelUrl, fallbackUrl);
        }

        public Task<(long orderCode, string checkoutUrl, bool isFallback)> CreateMaintenancePaymentAsync(
            tramsac99.Areas.Admin.Models.PaymentTransaction transaction,
            string returnUrl,
            string cancelUrl,
            string fallbackUrl)
        {
            var orderCode = transaction.PayOsOrderCode ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var description = $"Phi duy tri tram #{transaction.StationId}";
            return CreatePaymentLinkAsync(orderCode, transaction.Amount, description, returnUrl, cancelUrl, fallbackUrl);
        }

        public async Task<(long orderCode, string checkoutUrl, bool isFallback)> CreatePaymentLinkAsync(
            long orderCode,
            decimal amount,
            string description,
            string returnUrl,
            string cancelUrl,
            string fallbackUrl)
        {
            var clientId = _configuration["PayOS:ClientId"];
            var apiKey = _configuration["PayOS:ApiKey"];
            var checksumKey = _configuration["PayOS:ChecksumKey"];

            if (string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(checksumKey))
            {
                return (orderCode, fallbackUrl, true);
            }

            var client = new PayOSClient(clientId, apiKey, checksumKey);
            var safeDescription = string.IsNullOrWhiteSpace(description)
                ? $"Thanh toan #{orderCode}"
                : description.Trim();

            if (safeDescription.Length > 25)
            {
                safeDescription = safeDescription.Substring(0, 25);
            }

            var paymentRequest = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = (int)Math.Round(amount, 0),
                Description = safeDescription,
                ReturnUrl = returnUrl,
                CancelUrl = cancelUrl
            };

            var paymentLink = await client.PaymentRequests.CreateAsync(paymentRequest);
            return (orderCode, paymentLink.CheckoutUrl, false);
        }
    }
}
