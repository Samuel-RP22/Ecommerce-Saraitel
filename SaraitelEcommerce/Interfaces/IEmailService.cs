namespace Saraitel.API.Interfaces
{
    public interface IEmailService
    {
        void EnviarCorreo(string destino, string asunto, string cuerpoHtml);

        // HU-17: para enviar la factura en PDF
        void EnviarCorreoConAdjunto(string destino, string asunto, string cuerpoHtml,
                                    string nombreArchivo, byte[] contenidoArchivo);
    }
}
