using Npgsql;
using NpgsqlTypes;

namespace Saraitel.Datos.Conexion
{
    // Atajos para pasar parámetros y leer columnas sin repetir código en cada repositorio
    internal static class AyudasSql
    {
        /* ---------- Parámetros ---------- */

        // Texto, números y booleanos (null se guarda como NULL)
        public static void Parametro(this NpgsqlCommand comando, string nombre, object? valor)
        {
            comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        }

        // Fechas de la tienda (hora de Colombia, columnas TIMESTAMP)
        public static void ParametroFecha(this NpgsqlCommand comando, string nombre, DateTime? fecha)
        {
            if (fecha == null)
            {
                comando.Parameters.AddWithValue(nombre, NpgsqlDbType.Timestamp, DBNull.Value);
                return;
            }

            DateTime local = fecha.Value.Kind == DateTimeKind.Utc ? fecha.Value.ToLocalTime() : fecha.Value;

            comando.Parameters.AddWithValue(nombre, NpgsqlDbType.Timestamp,
                DateTime.SpecifyKind(local, DateTimeKind.Unspecified));
        }

        // Fechas en UTC (columnas TIMESTAMPTZ, como el vencimiento del código de recuperación)
        public static void ParametroFechaUtc(this NpgsqlCommand comando, string nombre, DateTime? fecha)
        {
            if (fecha == null)
            {
                comando.Parameters.AddWithValue(nombre, NpgsqlDbType.TimestampTz, DBNull.Value);
                return;
            }

            DateTime utc = fecha.Value.Kind == DateTimeKind.Utc ? fecha.Value : fecha.Value.ToUniversalTime();

            comando.Parameters.AddWithValue(nombre, NpgsqlDbType.TimestampTz, utc);
        }

        /* ---------- Ejecución ---------- */

        // Ejecuta un comando con varias consultas, lee la primera fila del primer
        // resultado (por ejemplo, el id que devolvió un INSERT ... RETURNING) y
        // recorre el resto, para que si alguna consulta falla se note aquí mismo
        public static void EjecutarTodo(this NpgsqlCommand comando, Action<NpgsqlDataReader> leerPrimeraFila)
        {
            using var lector = comando.ExecuteReader();

            if (lector.Read())
                leerPrimeraFila(lector);

            while (lector.NextResult())
            {
            }
        }

        /* ---------- Lectura de columnas ---------- */

        public static string Texto(this NpgsqlDataReader lector, string columna)
        {
            int i = lector.GetOrdinal(columna);
            return lector.IsDBNull(i) ? string.Empty : lector.GetString(i);
        }

        public static string? TextoNulo(this NpgsqlDataReader lector, string columna)
        {
            int i = lector.GetOrdinal(columna);
            return lector.IsDBNull(i) ? null : lector.GetString(i);
        }

        public static int Entero(this NpgsqlDataReader lector, string columna)
        {
            int i = lector.GetOrdinal(columna);
            return lector.IsDBNull(i) ? 0 : lector.GetInt32(i);
        }

        public static int? EnteroNulo(this NpgsqlDataReader lector, string columna)
        {
            int i = lector.GetOrdinal(columna);
            return lector.IsDBNull(i) ? null : lector.GetInt32(i);
        }

        public static decimal Numero(this NpgsqlDataReader lector, string columna)
        {
            int i = lector.GetOrdinal(columna);
            return lector.IsDBNull(i) ? 0 : lector.GetDecimal(i);
        }

        public static bool Booleano(this NpgsqlDataReader lector, string columna)
        {
            int i = lector.GetOrdinal(columna);
            return !lector.IsDBNull(i) && lector.GetBoolean(i);
        }

        // Las fechas se devuelven como hora local, igual que DateTime.Now
        public static DateTime Fecha(this NpgsqlDataReader lector, string columna)
        {
            return lector.FechaNula(columna) ?? DateTime.MinValue;
        }

        public static DateTime? FechaNula(this NpgsqlDataReader lector, string columna)
        {
            int i = lector.GetOrdinal(columna);

            if (lector.IsDBNull(i))
                return null;

            return DateTime.SpecifyKind(lector.GetDateTime(i), DateTimeKind.Local);
        }

        public static DateTime? FechaUtcNula(this NpgsqlDataReader lector, string columna)
        {
            int i = lector.GetOrdinal(columna);

            if (lector.IsDBNull(i))
                return null;

            return DateTime.SpecifyKind(lector.GetDateTime(i), DateTimeKind.Utc);
        }
    }
}
