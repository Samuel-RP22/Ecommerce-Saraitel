using System.Collections.Concurrent;
using System.Text;
using Npgsql;
using Saraitel.Datos.Conexion;
using Saraitel.Entidades;

namespace Saraitel.Datos.Repositories
{
    public class UsuarioRepository
    {
        // Productos del carrito de un usuario, en el orden en que los agregó
        private const string SqlCarrito = @"
            SELECT ci.cantidad, p.*, c.nombre_categoria
            FROM carrito_items ci
            JOIN productos p ON p.id_producto = ci.id_producto
            LEFT JOIN categorias c ON c.id_categoria = p.id_categoria
            WHERE ci.id_usuario = {0}
            ORDER BY ci.posicion";

        // Rendimiento: el tipo de cada cuenta (cliente o administrador) nunca cambia,
        // así que se recuerda para no consultarlo en cada acción del administrador
        private static readonly ConcurrentDictionary<int, bool> _esAdministrador = new();

        public bool EsAdministrador(int idUsuario)
        {
            if (_esAdministrador.TryGetValue(idUsuario, out bool esAdmin))
                return esAdmin;

            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand("SELECT tipo FROM usuarios WHERE id_usuario = @id", conexion);

            comando.Parametro("id", idUsuario);

            var tipo = comando.ExecuteScalar() as string;

            // Un id que no existe no se recuerda (podría crearse después)
            if (tipo == null)
                return false;

            esAdmin = tipo == "Administrador";
            _esAdministrador[idUsuario] = esAdmin;

            return esAdmin;
        }

        // Lista de usuarios (sin carritos): para listados del administrador
        public List<Usuario> ObtenerUsuarios()
        {
            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand("SELECT * FROM usuarios ORDER BY id_usuario", conexion);
            using var lector = comando.ExecuteReader();

            var usuarios = new List<Usuario>();

            while (lector.Read())
                usuarios.Add(LeerUsuario(lector));

            return usuarios;
        }

        // Usuario con su carrito, en una sola ida a la base de datos
        public Usuario? ObtenerUsuarioPorId(int id)
        {
            string sql = "SELECT * FROM usuarios WHERE id_usuario = @id;" +
                         string.Format(SqlCarrito, "@id");

            return CargarUsuario(sql, comando => comando.Parametro("id", id));
        }

        public Usuario? ObtenerUsuarioPorCorreo(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
                return null;

            string sql = "SELECT * FROM usuarios WHERE LOWER(correo) = LOWER(@correo);" +
                         string.Format(SqlCarrito, "(SELECT id_usuario FROM usuarios WHERE LOWER(correo) = LOWER(@correo))");

            return CargarUsuario(sql, comando => comando.Parametro("correo", correo.Trim()));
        }

        // Guarda el usuario y le asigna el id que generó la base de datos
        public void AgregarUsuario(Usuario usuario)
        {
            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand(@"
                INSERT INTO usuarios (tipo, nombre, correo, telefono, contrasena, direccion, token_recuperacion, fecha_expiracion_token)
                VALUES (@tipo, @nombre, @correo, @telefono, @contrasena, @direccion, @token, @fechaToken)
                RETURNING id_usuario", conexion);

            AgregarParametros(comando, usuario);
            comando.Parametro("tipo", usuario is Administrador ? "Administrador" : "Cliente");

            usuario.IdUsuario = (int)comando.ExecuteScalar()!;
        }

        // Perfil, contraseña y código de recuperación (el carrito se guarda aparte)
        public void ActualizarUsuario(Usuario usuario)
        {
            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand(@"
                UPDATE usuarios SET
                    nombre = @nombre,
                    correo = @correo,
                    telefono = @telefono,
                    contrasena = @contrasena,
                    direccion = @direccion,
                    token_recuperacion = @token,
                    fecha_expiracion_token = @fechaToken
                WHERE id_usuario = @id", conexion);

            AgregarParametros(comando, usuario);
            comando.Parametro("id", usuario.IdUsuario);

            comando.ExecuteNonQuery();
        }

        // Reemplaza el carrito guardado por el que tiene el cliente en memoria.
        // El carrito guarda una entrada por unidad; en la tabla va una fila por producto.
        // El borrado y la inserción viajan en un solo comando, que PostgreSQL
        // ejecuta como una sola transacción (si algo falla, no cambia nada)
        public void GuardarCarrito(Cliente cliente)
        {
            var lineas = cliente.Carrito.ListaProductos
                .GroupBy(p => p.IdProducto)
                .Select(g => new { IdProducto = g.Key, Cantidad = g.Count() })
                .ToList();

            var sql = new StringBuilder("DELETE FROM carrito_items WHERE id_usuario = @idUsuario;");

            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand { Connection = conexion };

            comando.Parametro("idUsuario", cliente.IdUsuario);

            if (lineas.Count > 0)
            {
                sql.Append(" INSERT INTO carrito_items (id_usuario, id_producto, cantidad, posicion) VALUES ");

                for (int i = 0; i < lineas.Count; i++)
                {
                    if (i > 0)
                        sql.Append(", ");

                    sql.Append($"(@idUsuario, @idProducto{i}, @cantidad{i}, {i + 1})");
                    comando.Parametro($"idProducto{i}", lineas[i].IdProducto);
                    comando.Parametro($"cantidad{i}", lineas[i].Cantidad);
                }

                sql.Append(';');
            }

            comando.CommandText = sql.ToString();
            comando.ExecuteNonQuery();
        }

        /* ---------- Ayudas ---------- */

        private static Usuario? CargarUsuario(string sql, Action<NpgsqlCommand> parametros)
        {
            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand(sql, conexion);

            parametros(comando);

            using var lector = comando.ExecuteReader();

            if (!lector.Read())
                return null;

            var usuario = LeerUsuario(lector);

            // Segunda consulta: el carrito
            lector.NextResult();

            if (usuario is Cliente cliente)
            {
                while (lector.Read())
                {
                    var producto = ProductoRepository.LeerProducto(lector);
                    int cantidad = lector.Entero("cantidad");

                    for (int i = 0; i < cantidad; i++)
                        cliente.Carrito.ListaProductos.Add(producto);
                }

                cliente.Carrito.IdCarrito = cliente.IdUsuario;
                cliente.Carrito.CantidadProductos = cliente.Carrito.ListaProductos.Count;
                cliente.Carrito.TotalCompra = cliente.Carrito.ListaProductos.Sum(p => p.Precio);
            }

            return usuario;
        }

        private static Usuario LeerUsuario(NpgsqlDataReader lector)
        {
            Usuario usuario = lector.Texto("tipo") == "Administrador"
                ? new Administrador()
                : new Cliente { Direccion = lector.Texto("direccion") };

            usuario.IdUsuario = lector.Entero("id_usuario");
            usuario.Nombre = lector.Texto("nombre");
            usuario.Correo = lector.Texto("correo");
            usuario.Telefono = lector.Texto("telefono");
            usuario.Contrasena = lector.Texto("contrasena");
            usuario.TokenRecuperacion = lector.TextoNulo("token_recuperacion");
            usuario.FechaExpiracionToken = lector.FechaUtcNula("fecha_expiracion_token");

            return usuario;
        }

        private static void AgregarParametros(NpgsqlCommand comando, Usuario usuario)
        {
            comando.Parametro("nombre", usuario.Nombre ?? "");
            comando.Parametro("correo", (usuario.Correo ?? "").Trim());
            comando.Parametro("telefono", usuario.Telefono ?? "");
            comando.Parametro("contrasena", usuario.Contrasena ?? "");
            comando.Parametro("direccion", usuario is Cliente cliente ? cliente.Direccion ?? "" : "");
            comando.Parametro("token", usuario.TokenRecuperacion);
            comando.ParametroFechaUtc("fechaToken", usuario.FechaExpiracionToken);
        }
    }
}
