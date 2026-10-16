using System.Collections.Generic;
using System.Linq;
using Saraitel.Datos.Managers;
using Saraitel.Entidades;

namespace Saraitel.Datos.Repositories
{
    public class SoporteRepository
    {
        private readonly JsonManager _jsonManager;

        public SoporteRepository()
        {
            _jsonManager = JsonManager.Instance;
        }

        public List<Soporte> ObtenerSolicitudes()
        {
            return _jsonManager.LeerDatos().Soportes;
        }

        public void AgregarSolicitud(Soporte soporte)
        {
            var data = _jsonManager.LeerDatos();
            data.Soportes.Add(soporte);
            _jsonManager.GuardarDatos(data);
        }

        public void CambiarEstado(int idSoporte, string nuevoEstado)
        {
            var data = _jsonManager.LeerDatos();
            var soporte = data.Soportes.FirstOrDefault(s => s.IdSoporte == idSoporte);

            if (soporte != null)
            {
                soporte.EstadoSolicitud = nuevoEstado;
                _jsonManager.GuardarDatos(data);
            }
        }

        public Soporte? ObtenerSoportePorId(int id)
        {
            return ObtenerSolicitudes().FirstOrDefault(p => p.IdSoporte == id);
        }
    }
}