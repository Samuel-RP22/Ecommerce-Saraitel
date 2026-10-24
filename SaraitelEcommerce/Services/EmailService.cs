using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Saraitel.API.Interfaces;

namespace Saraitel.API.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuracion;

        public EmailService(IConfiguration configuracion)
        {
            _configuracion = configuracion;
        }

        public void EnviarCorreo(string destino, string asunto, string cuerpoHtml)
        {
            string? host = _configuracion["Email:Host"];
            string puertoTexto = _configuracion["Email:Puerto"] ?? "587";
            string? remitente = _configuracion["Email:Remitente"];
            string nombreRemitente = _configuracion["Email:NombreRemitente"] ?? "Saraitel";
            string? clave = _configuracion["Email:Clave"];

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(remitente) ||
                string.IsNullOrWhiteSpace(clave))
            {
                throw new InvalidOperationException(
                    "Falta la configuración de correo (Email:Host, Email:Remitente o Email:Clave).");
            }

            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(nombreRemitente, remitente));
            mensaje.To.Add(MailboxAddress.Parse(destino));
            mensaje.Subject = asunto;
            mensaje.Body = new TextPart("html") { Text = cuerpoHtml };

            using var cliente = new SmtpClient();

            int puerto = int.Parse(puertoTexto);

            // 587 usa STARTTLS; 465 usa conexión segura directa
            cliente.Connect(
                host,
                puerto,
                puerto == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls);

            // Google muestra la clave con espacios; se los quitamos por si acaso
            cliente.Authenticate(remitente, clave.Replace(" ", ""));

            cliente.Send(mensaje);
            cliente.Disconnect(true);
        }
    }
}
