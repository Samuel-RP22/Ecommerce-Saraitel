using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Saraitel.API.Interfaces;
using Saraitel.API.Models;

namespace Saraitel.API.Services
{
    // HU-16: conexión con la pasarela de pagos Wompi (ambiente Sandbox)
    public class WompiService : IWompiService
    {
        private readonly HttpClient _http;
        private readonly string _urlApi;
        private readonly string _llavePublica;
        private readonly string _secretoIntegridad;

        public WompiService(HttpClient http, IConfiguration configuracion)
        {
            _http = http;

            // La URL y la llave pública están en appsettings.json.
            // El secreto de integridad está en los secretos de usuario.
            _urlApi = (configuracion["Wompi:UrlApi"] ?? "https://sandbox.wompi.co/v1").TrimEnd('/');
            _llavePublica = configuracion["Wompi:LlavePublica"] ?? string.Empty;
            _secretoIntegridad = configuracion["Wompi:SecretoIntegridad"] ?? string.Empty;
        }

        public string LlavePublica => _llavePublica;

        // Firma de integridad: SHA-256 de referencia + monto + moneda + fecha límite + secreto.
        // Se calcula aquí para que el secreto nunca llegue al navegador.
        public string GenerarFirma(string referencia, long montoEnCentavos, string moneda, string fechaLimite)
        {
            if (string.IsNullOrWhiteSpace(_secretoIntegridad))
                throw new InvalidOperationException(
                    "Falta Wompi:SecretoIntegridad en los secretos de usuario.");

            string cadena = $"{referencia}{montoEnCentavos}{moneda}{fechaLimite}{_secretoIntegridad}";

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(cadena));

            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        // Consulta en Wompi el estado real de una transacción
        public async Task<TransaccionWompi?> ConsultarTransaccionAsync(string idTransaccion)
        {
            try
            {
                var respuesta = await _http.GetAsync(
                    $"{_urlApi}/transactions/{Uri.EscapeDataString(idTransaccion)}");

                if (!respuesta.IsSuccessStatusCode)
                    return null;

                string json = await respuesta.Content.ReadAsStringAsync();

                return JsonSerializer.Deserialize<RespuestaTransaccionWompi>(json)?.Data;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"ERROR WOMPI: {ex.Message}");
                return null;
            }
        }
    }
}
