namespace Saraitel.API.Interfaces
{
    public interface IEmailService
    {
        void EnviarCorreo(string destino, string asunto, string cuerpoHtml);
    }
}
