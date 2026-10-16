using Saraitel.API.Interfaces;
using Saraitel.Datos.Repositories;
using Saraitel.Entidades;

namespace Saraitel.API.Services
{
    public class SoporteService : ISoporteService
    {
        private readonly SoporteRepository _repo = new();

        public List<Soporte> ObtenerSoportes() => _repo.ObtenerSolicitudes();

        public Soporte? ObtenerPorId(int id) => _repo.ObtenerSoportePorId(id);

        public string CrearSoporte(Soporte soporte)
        {
            int nuevoId = ObtenerSoportes().Any() ?
                ObtenerSoportes().Max(s => s.IdSoporte) + 1 : 1;

            soporte.IdSoporte = nuevoId;
            soporte.EstadoSolicitud = "Pendiente"; 

            _repo.AgregarSolicitud(soporte);
            return "Soporte creado";
        }

        public string CambiarEstado(int id, string estado)
        {
            _repo.CambiarEstado(id, estado);
            return "Estado actualizado";
        }
    }
}