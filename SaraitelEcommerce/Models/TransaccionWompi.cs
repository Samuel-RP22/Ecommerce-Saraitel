using System.Text.Json.Serialization;

namespace Saraitel.API.Models
{
    // Respuesta de Wompi al consultar GET /transactions/{id}
    public class RespuestaTransaccionWompi
    {
        [JsonPropertyName("data")]
        public TransaccionWompi? Data { get; set; }
    }

    public class TransaccionWompi
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        // APPROVED, DECLINED, VOIDED, ERROR o PENDING
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("status_message")]
        public string? StatusMessage { get; set; }

        [JsonPropertyName("reference")]
        public string Reference { get; set; } = string.Empty;

        [JsonPropertyName("amount_in_cents")]
        public long AmountInCents { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        // CARD, PSE, NEQUI, BANCOLOMBIA_TRANSFER, etc.
        [JsonPropertyName("payment_method_type")]
        public string PaymentMethodType { get; set; } = string.Empty;
    }
}
