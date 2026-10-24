using Npgsql;

namespace Saraitel.Datos.Conexion
{
    // Conexión a la base de datos PostgreSQL (Neon).
    // Se configura una sola vez al arrancar la API (Program.cs) y todos los
    // repositorios piden aquí sus conexiones. Npgsql las reutiliza (pool),
    // así que abrir una conexión por operación es rápido.
    public static class ConexionBD
    {
        // Esquema donde están las tablas (el mismo del archivo .sql)
        public const string EsquemaPorDefecto = "ecommercesaraitel";

        private static NpgsqlDataSource? _fuente;

        public static void Configurar(string? cadenaConexion)
        {
            if (string.IsNullOrWhiteSpace(cadenaConexion))
                throw new InvalidOperationException(
                    "Falta la cadena de conexión \"ConnectionStrings:Saraitel\". " +
                    "Agrégala en los secretos de usuario (clic derecho en Saraitel.API > Administrar secretos de usuario).");

            cadenaConexion = cadenaConexion.Trim();

            // Neon entrega la cadena como URL (postgresql://usuario:clave@host/base?sslmode=require):
            // se acepta tal cual y se convierte al formato de Npgsql
            if (cadenaConexion.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                cadenaConexion.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            {
                cadenaConexion = ConvertirUrl(cadenaConexion);
            }

            var datos = new NpgsqlConnectionStringBuilder(cadenaConexion);

            // Npgsql ya tiene su propio pool de conexiones. Con el pooler de Neon
            // ("-pooler" en el host) el esquema no se conserva entre consultas,
            // así que se usa la conexión directa
            if (datos.Host != null && datos.Host.Contains("-pooler."))
                datos.Host = datos.Host.Replace("-pooler.", ".");

            // Esquema de las tablas. Neon no aplica el "Search Path" de la cadena de
            // conexión, así que se fija con un SET apenas se abre cada conexión
            string esquema = string.IsNullOrWhiteSpace(datos.SearchPath) ? EsquemaPorDefecto : datos.SearchPath.Trim();
            datos.SearchPath = null;

            if (!esquema.All(c => char.IsLetterOrDigit(c) || c == '_' || c == ',' || c == ' '))
                throw new InvalidOperationException($"El nombre del esquema \"{esquema}\" no es válido.");

            // Npgsql no debe "limpiar" la conexión al devolverla al pool:
            // así el esquema fijado con SET se mantiene mientras la conexión viva
            datos.NoResetOnClose = true;

            if (datos.Host != null && datos.Host.EndsWith(".neon.tech"))
            {
                // Neon exige conexión cifrada
                datos.SslMode = SslMode.Require;

                // Si Neon estaba dormido (plan gratis), tarda unos segundos en despertar
                if (datos.Timeout < 30)
                    datos.Timeout = 30;

                // Rendimiento: abrir una conexión nueva con Neon (cifrado + clave + esquema)
                // tarda casi medio segundo. Se dejan 2 siempre abiertas y las demás
                // se cierran tras 5 minutos sin uso
                datos.MinPoolSize = 2;
                datos.ConnectionIdleLifetime = 300;

                // Cada 30 segundos Npgsql revisa las conexiones abiertas; si Neon
                // cortó alguna, la descarta antes de que una consulta la use
                datos.KeepAlive = 30;
            }

            var constructor = new NpgsqlDataSourceBuilder(datos.ConnectionString);
            string sqlEsquema = $"SET search_path TO {esquema}";

            // Se ejecuta una vez por cada conexión real que se abre con Neon
            constructor.UsePhysicalConnectionInitializer(
                conexion =>
                {
                    using var comando = new NpgsqlCommand(sqlEsquema, conexion);
                    comando.ExecuteNonQuery();
                },
                async conexion =>
                {
                    await using var comando = new NpgsqlCommand(sqlEsquema, conexion);
                    await comando.ExecuteNonQueryAsync();
                });

            _fuente?.Dispose();
            _fuente = constructor.Build();
        }

        // Abre una conexión lista para usar (se cierra con "using")
        public static NpgsqlConnection Abrir()
        {
            if (_fuente == null)
                throw new InvalidOperationException(
                    "La base de datos no está configurada. Revisa ConexionBD.Configurar en Program.cs.");

            return _fuente.OpenConnection();
        }

        // Comprueba la conexión al arrancar, para avisar de una vez si algo está mal
        public static void Probar()
        {
            using var conexion = Abrir();
            using var comando = new NpgsqlCommand("SELECT COUNT(*) FROM consecutivos", conexion);
            comando.ExecuteScalar();
        }

        private static string ConvertirUrl(string url)
        {
            var uri = new Uri(url);
            string[] credenciales = uri.UserInfo.Split(':', 2);

            var datos = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
                Username = Uri.UnescapeDataString(credenciales[0]),
                Password = credenciales.Length > 1 ? Uri.UnescapeDataString(credenciales[1]) : "",
                SslMode = SslMode.Require
            };

            return datos.ConnectionString;
        }
    }
}
