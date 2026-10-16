using Saraitel.Entidades;

namespace Saraitel.API.Interfaces
{
    public interface ISoporteService
    {
        List<Soporte> ObtenerSoportes();
        Soporte? ObtenerPorId(int id);
        string CrearSoporte(Soporte soporte);
        string CambiarEstado(int id, string estado);
    }
}