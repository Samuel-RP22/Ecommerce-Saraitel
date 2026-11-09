using Npgsql;
using Saraitel.Datos.Conexion;
using Saraitel.Entidades;

namespace Saraitel.Datos.Repositories
{
    public class CategoriaRepository
    {
        // Rendimiento: las categorías casi nunca cambian, así que se guardan en
        // memoria. Agregar o eliminar una desde este programa las vuelve a leer;
        // los cambios hechos desde otro PC se ven a más tardar en 1 minuto.
        private static readonly TimeSpan DuracionCache = TimeSpan.FromMinutes(1);
        private static readonly object _bloqueoCache = new object();
        private static List<Categoria>? _cache;
        private static DateTime _cacheVence;

        public List<Categoria> ObtenerCategorias()
        {
            lock (_bloqueoCache)
            {
                if (_cache != null && DateTime.UtcNow < _cacheVence)
                    return _cache.Select(Copiar).ToList();
            }

            var categorias = new List<Categoria>();

            using (var conexion = ConexionBD.Abrir())
            using (var comando = new NpgsqlCommand(
                "SELECT id_categoria, nombre_categoria FROM categorias ORDER BY id_categoria", conexion))
            using (var lector = comando.ExecuteReader())
            {
                while (lector.Read())
                    categorias.Add(LeerCategoria(lector));
            }

            lock (_bloqueoCache)
            {
                _cache = categorias;
                _cacheVence = DateTime.UtcNow.Add(DuracionCache);
            }

            return categorias.Select(Copiar).ToList();
        }

        // Guarda la categoría y le asigna el id que generó la base de datos
        public void AgregarCategoria(Categoria categoria)
        {
            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand(
                "INSERT INTO categorias (nombre_categoria) VALUES (@nombre) RETURNING id_categoria", conexion);

            comando.Parametro("nombre", categoria.NombreCategoria.Trim());

            categoria.IdCategoria = (int)comando.ExecuteScalar()!;

            LimpiarCache();
        }

        // Cuántos productos usan esta categoría (consulta directa, sin caché)
        public int ContarProductos(int idCategoria)
        {
            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand(
                "SELECT COUNT(*) FROM productos WHERE id_categoria = @id", conexion);

            comando.Parametro("id", idCategoria);

            return Convert.ToInt32(comando.ExecuteScalar());
        }

        // Elimina la categoría SOLO si ningún producto la usa.
        // La condición va dentro del mismo DELETE: si alguien le asigna un producto
        // justo en ese instante, no se borra. Devuelve false si no se eliminó
        public bool EliminarCategoriaSinProductos(int id)
        {
            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand(@"
                DELETE FROM categorias
                WHERE id_categoria = @id
                  AND NOT EXISTS (SELECT 1 FROM productos WHERE id_categoria = @id)", conexion);

            comando.Parametro("id", id);

            int filas;

            try
            {
                filas = comando.ExecuteNonQuery();
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                // La base de datos también lo impide (ON DELETE RESTRICT)
                return false;
            }

            LimpiarCache();

            return filas > 0;
        }

        public Categoria? ObtenerCategoriaPorId(int id)
        {
            return ObtenerCategorias().FirstOrDefault(c => c.IdCategoria == id);
        }

        private static void LimpiarCache()
        {
            lock (_bloqueoCache)
            {
                _cache = null;
            }
        }

        private static Categoria Copiar(Categoria c)
        {
            return new Categoria { IdCategoria = c.IdCategoria, NombreCategoria = c.NombreCategoria };
        }

        private static Categoria LeerCategoria(NpgsqlDataReader lector)
        {
            return new Categoria
            {
                IdCategoria = lector.Entero("id_categoria"),
                NombreCategoria = lector.Texto("nombre_categoria")
            };
        }
    }
}
