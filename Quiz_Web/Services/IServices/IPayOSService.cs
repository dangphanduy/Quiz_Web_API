using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace Quiz_Web.Services.IServices
{
    public interface IPayOSService
    {
        Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(decimal amount, string description, int orderId);
        Task<PaymentLink> GetPaymentLinkInformationAsync(int orderId);
        Task<WebhookData> VerifyWebhookDataAsync(Webhook webhookBody);
    }
}
