using System.Text;
using Npgsql;
using Saraitel.Datos.Conexion;
using Saraitel.Entidades;

namespace Saraitel.Datos.Repositories
{
    public class ProductoRepository
    {
        // Producto con el nombre de su categoría
        internal const string SqlSeleccionar = @"
            SELECT p.*, c.nombre_categoria
            FROM productos p
            LEFT JOIN categorias c ON c.id_categoria = p.id_categoria";

        // La categoría se busca por id y, si no llega el id, por nombre.
        // Si no existe, el producto queda sin categoría (NULL)
        private const string SqlIdCategoria = @"
            COALESCE(
                (SELECT id_categoria FROM categorias WHERE id_categoria = @idCategoria),
                (SELECT id_categoria FROM categorias WHERE LOWER(nombre_categoria) = LOWER(@nombreCategoria)))";

        /* ---------- Catálogo en memoria ----------
           Rendimiento: el catálogo se consulta muchísimo (tienda, carrito, filtros)
           y cambia poco, así que se guarda en memoria unos segundos. Cualquier cambio
           hecho desde este programa lo borra al instante; los cambios hechos desde otro
           PC se ven a más tardar en 30 segundos. Las compras SIEMPRE validan el stock
           real en la base de datos, así que nunca se vende de más. */

        private static readonly TimeSpan DuracionCache = TimeSpan.FromSeconds(30);
        private static readonly object _bloqueoCache = new object();
        private static List<Producto>? _cache;
        private static DateTime _cacheVence;

        // Se llama después de cualquier cambio en productos o en su stock
        public static void LimpiarCache()
        {
            lock (_bloqueoCache)
            {
                _cache = null;
            }
        }

        public List<Producto> ObtenerProductos()
        {
            // Se entregan copias: si alguien modifica un producto, no daña la memoria
            return Catalogo().Select(Copiar).ToList();
        }

        public Producto? ObtenerProductoPorId(int id)
        {
            var producto = Catalogo().FirstOrDefault(p => p.IdProducto == id);

            return producto == null ? null : Copiar(producto);
        }

        private static List<Producto> Catalogo()
        {
            lock (_bloqueoCache)
            {
                if (_cache != null && DateTime.UtcNow < _cacheVence)
                    return _cache;
            }

            var productos = new List<Producto>();

            using (var conexion = ConexionBD.Abrir())
            using (var comando = new NpgsqlCommand(SqlSeleccionar + " ORDER BY p.id_producto", conexion))
            using (var lector = comando.ExecuteReader())
            {
                while (lector.Read())
                    productos.Add(LeerProducto(lector));
            }

            lock (_bloqueoCache)
            {
                _cache = productos;
                _cacheVence = DateTime.UtcNow.Add(DuracionCache);
            }

            return productos;
        }

        // Guarda el producto y le asigna el id que generó la base de datos
        public void AgregarProducto(Producto producto)
        {
            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand($@"
                INSERT INTO productos (nombre, descripcion, precio, stock, imagen, id_categoria)
                VALUES (@nombre, @descripcion, @precio, @stock, @imagen, {SqlIdCategoria})
                RETURNING id_producto", conexion);

            AgregarParametros(comando, producto);

            producto.IdProducto = (int)comando.ExecuteScalar()!;

            LimpiarCache();
        }

        // Al borrarlo también sale de los carritos (ON DELETE CASCADE).
        // Los pedidos no cambian: guardan su propia copia del producto
        public void EliminarProducto(int id)
        {
            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand("DELETE FROM productos WHERE id_producto = @id", conexion);

            comando.Parametro("id", id);
            comando.ExecuteNonQuery();

            LimpiarCache();
        }

        public void ActualizarProducto(Producto producto)
        {
            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand($@"
                UPDATE productos SET
                    nombre = @nombre,
                    descripcion = @descripcion,
                    precio = @precio,
                    stock = @stock,
                    imagen = @imagen,
                    id_categoria = {SqlIdCategoria}
                WHERE id_producto = @id", conexion);

            AgregarParametros(comando, producto);
            comando.Parametro("id", producto.IdProducto);

            comando.ExecuteNonQuery();

            LimpiarCache();
        }

        /* ---------- Usados dentro de las transacciones de pedidos y posventa ---------- */

        // Descuenta las unidades de varios productos en una sola consulta, solo si
        // alcanzan todas. Si otro cliente compró primero, devuelve false y quien
        // llama deshace la transacción completa
        internal static bool DescontarStock(NpgsqlConnection conexion, NpgsqlTransaction transaccion, Dictionary<int, int> cantidadesPorProducto)
        {
            if (cantidadesPorProducto.Count == 0)
                return true;

            var valores = new StringBuilder();
            using var comando = new NpgsqlCommand { Connection = conexion, Transaction = transaccion };

            int i = 0;
            foreach (var item in cantidadesPorProducto)
            {
                if (i > 0)
                    valores.Append(", ");

                valores.Append($"(@id{i}::int, @cantidad{i}::int)");
                comando.Parametro($"id{i}", item.Key);
                comando.Parametro($"cantidad{i}", item.Value);
                i++;
            }

            comando.CommandText = $@"
                UPDATE productos p SET stock = p.stock - v.cantidad
                FROM (VALUES {valores}) AS v(id, cantidad)
                WHERE p.id_producto = v.id AND p.stock >= v.cantidad";

            // Debe actualizar una fila por cada producto; si falta alguna, no alcanzó el stock
            return comando.ExecuteNonQuery() == cantidadesPorProducto.Count;
        }

        internal static Producto LeerProducto(NpgsqlDataReader lector)
        {
            int? idCategoria = lector.EnteroNulo("id_categoria");

            return new Producto
            {
                IdProducto = lector.Entero("id_producto"),
                Nombre = lector.Texto("nombre"),
                Descripcion = lector.Texto("descripcion"),
                Precio = lector.Numero("precio"),
                Stock = lector.Entero("stock"),
                Imagen = lector.Texto("imagen"),
                // Sin categoría: id 0 y nombre vacío, como antes
                Categoria = idCategoria == null
                    ? new Categoria()
                    : new Categoria { IdCategoria = idCategoria.Value, NombreCategoria = lector.Texto("nombre_categoria") }
            };
        }

        private static Producto Copiar(Producto p)
        {
            return new Producto
            {
                IdProducto = p.IdProducto,
                Nombre = p.Nombre,
                Descripcion = p.Descripcion,
                Precio = p.Precio,
                Stock = p.Stock,
                Imagen = p.Imagen,
                Categoria = new Categoria
                {
                    IdCategoria = p.Categoria?.IdCategoria ?? 0,
                    NombreCategoria = p.Categoria?.NombreCategoria ?? ""
                }
            };
        }

        private static void AgregarParametros(NpgsqlCommand comando, Producto producto)
        {
            comando.Parametro("nombre", producto.Nombre ?? "");
            comando.Parametro("descripcion", producto.Descripcion ?? "");
            comando.Parametro("precio", producto.Precio);
            comando.Parametro("stock", producto.Stock);
            comando.Parametro("imagen", producto.Imagen ?? "");
            comando.Parametro("idCategoria", producto.Categoria?.IdCategoria ?? 0);
            comando.Parametro("nombreCategoria", producto.Categoria?.NombreCategoria ?? "");
        }
    }
}
