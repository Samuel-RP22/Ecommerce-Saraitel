using System.Collections.Generic;
using System.Linq;
using Saraitel.Datos.Managers;
using Saraitel.Entidades;

namespace Saraitel.Datos.Repositories
{
    public class CategoriaRepository
    {
        private readonly JsonManager _jsonManager;

        public CategoriaRepository()
        {
            _jsonManager = JsonManager.Instance;
        }

        public List<Categoria> ObtenerCategorias()
        {
            return _jsonManager.LeerDatos().Categorias;
        }

        public void AgregarCategoria(Categoria categoria)
        {
            var data = _jsonManager.LeerDatos();
            data.Categorias.Add(categoria);
            _jsonManager.GuardarDatos(data);
        }

        public void EliminarCategoria(int id)
        {
            var data = _jsonManager.LeerDatos();
            var categoria = data.Categorias.FirstOrDefault(c => c.IdCategoria == id);

            if (categoria != null)
            {
                data.Categorias.Remove(categoria);
                _jsonManager.GuardarDatos(data);
            }
        }

        public Categoria? ObtenerCategoriaPorId(int id)
        {
            return ObtenerCategorias().FirstOrDefault(c => c.IdCategoria == id);
        }
    }
}