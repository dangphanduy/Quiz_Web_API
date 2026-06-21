using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using Quiz_Web.Models.PayOSPayment;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Services
{
    public class PayOSService : IPayOSService
    {
        private readonly PayOSClient _payOSClient;
        private readonly PayOSSettings _settings;
        private readonly ILogger<PayOSService> _logger;

        public PayOSService(
            PayOSClient payOSClient,
            IOptions<PayOSSettings> settings,
            ILogger<PayOSService> logger)
        {
            _payOSClient = payOSClient;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(decimal amount, string description, int orderId)
        {
            try
            {
                var amountLong = (long)Math.Round(amount, 0, MidpointRounding.AwayFromZero);
                
                // Tiêu đề thanh toán (tối đa 25 ký tự theo quy định ngân hàng của PayOS)
                var cleanDescription = description.Length > 25 ? description.Substring(0, 25) : description;

                var paymentRequest = new CreatePaymentLinkRequest
                {
                    OrderCode = orderId,
                    Amount = amountLong,
                    Description = cleanDescription,
                    CancelUrl = _settings.CancelUrl,
                    ReturnUrl = _settings.ReturnUrl
                };

                _logger.LogInformation("Creating PayOS payment link for OrderId: {OrderId}, Amount: {Amount}", orderId, amountLong);
                
                var result = await _payOSClient.PaymentRequests.CreateAsync(paymentRequest);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating PayOS payment link for OrderId: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<PaymentLink> GetPaymentLinkInformationAsync(int orderId)
        {
            try
            {
                _logger.LogInformation("Querying PayOS payment status for OrderId: {OrderId}", orderId);
                var result = await _payOSClient.PaymentRequests.GetAsync(orderId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying PayOS payment status for OrderId: {OrderId}", orderId);
                throw;
            }
        }

        public async Task<WebhookData> VerifyWebhookDataAsync(Webhook webhookBody)
        {
            try
            {
                var result = await _payOSClient.Webhooks.VerifyAsync(webhookBody);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying PayOS webhook signature");
                throw;
            }
        }
    }
}
