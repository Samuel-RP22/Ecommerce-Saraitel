using System.Text;
using Npgsql;
using Saraitel.Datos.Conexion;
using Saraitel.Entidades;

namespace Saraitel.Datos.Repositories
{
    // Rendimiento: la base de datos está en internet (Neon), así que lo que más
    // tarda es cada ida y vuelta. Por eso cada operación manda sus consultas
    // juntas en un solo comando siempre que se puede.
    public class PedidoRepository
    {
        // Id del pedido que se acaba de insertar en esta misma conexión
        private const string IdNuevoPedido = "currval(pg_get_serial_sequence('pedidos', 'id_pedido'))";

        // Columnas del pedido (sin el id, que lo genera la base de datos)
        private const string SqlInsertar = @"
            INSERT INTO pedidos (
                id_usuario, fecha, estado, tipo_pedido, metodo_envio, metodo_pago,
                subtotal, costo_envio, impuestos, total,
                fecha_limite_pago, referencia_pago, id_transaccion, medio_pago, fecha_pago, motivo_cancelacion,
                numero_factura, fecha_factura, transportadora, numero_guia, fecha_envio, fecha_entrega,
                id_pedido_original, numero_garantia)
            VALUES (
                @idUsuario, @fecha, @estado, @tipoPedido, @metodoEnvio, @metodoPago,
                @subtotal, @costoEnvio, @impuestos, @total,
                @fechaLimitePago, @referenciaPago, @idTransaccion, @medioPago, @fechaPago, @motivoCancelacion,
                @numeroFactura, @fechaFactura, @transportadora, @numeroGuia, @fechaEnvio, @fechaEntrega,
                @idPedidoOriginal, @numeroGarantia)
            RETURNING id_pedido;";

        // Todo lo que puede cambiar de un pedido, menos el número de factura
        private const string SqlCamposActualizar = @"
                id_usuario = @idUsuario, fecha = @fecha, estado = @estado, tipo_pedido = @tipoPedido,
                metodo_envio = @metodoEnvio, metodo_pago = @metodoPago,
                subtotal = @subtotal, costo_envio = @costoEnvio, impuestos = @impuestos, total = @total,
                fecha_limite_pago = @fechaLimitePago, referencia_pago = @referenciaPago,
                id_transaccion = @idTransaccion, medio_pago = @medioPago, fecha_pago = @fechaPago,
                motivo_cancelacion = @motivoCancelacion,
                transportadora = @transportadora, numero_guia = @numeroGuia,
                fecha_envio = @fechaEnvio, fecha_entrega = @fechaEntrega,
                id_pedido_original = @idPedidoOriginal, numero_garantia = @numeroGarantia";

        /* ---------- Consultas ---------- */

        public List<Pedido> ObtenerPedidos()
        {
            return Cargar("TRUE", _ => { });
        }

        // HU-18: pedidos de un cliente
        public List<Pedido> ObtenerPedidosDeUsuario(int idUsuario)
        {
            return Cargar("p.id_usuario = @idUsuario", comando => comando.Parametro("idUsuario", idUsuario));
        }

        public Pedido? ObtenerPedidoPorId(int id)
        {
            return Cargar("p.id_pedido = @idPedido", comando => comando.Parametro("idPedido", id))
                .FirstOrDefault();
        }

        // HU-16: pedidos sin pagar cuyo tiempo ya se acabó (los de antes de la HU-16
        // no guardaban fecha límite: se toman 20 minutos desde que se crearon)
        public List<Pedido> ObtenerPedidosVencidos(DateTime ahora)
        {
            return Cargar(
                $"p.estado = @pendiente AND COALESCE(p.fecha_limite_pago, p.fecha + INTERVAL '{Pedido.MinutosParaPagar} minutes') <= @ahora",
                comando =>
                {
                    comando.Parametro("pendiente", Pedido.EstadoPendientePago);
                    comando.ParametroFecha("ahora", ahora);
                });
        }

        /* ---------- Escritura ---------- */

        // Guarda un pedido con sus productos y datos de envío (un solo viaje); le asigna su id
        public void AgregarPedido(Pedido pedido)
        {
            using var conexion = ConexionBD.Abrir();

            Insertar(conexion, null, pedido);
        }

        // HU-15: crea el pedido, aparta el stock y vacía el carrito del cliente,
        // todo o nada. Devuelve false si otro cliente se llevó primero las unidades.
        public bool RegistrarPedido(Pedido pedido, Dictionary<int, int> cantidadesPorProducto)
        {
            using var conexion = ConexionBD.Abrir();
            using var transaccion = conexion.BeginTransaction();

            // 1er viaje: apartar el stock de todos los productos a la vez
            if (!ProductoRepository.DescontarStock(conexion, transaccion, cantidadesPorProducto))
                return false; // sin Commit: se deshace todo

            // 2do viaje: pedido, productos, datos de envío y carrito vacío
            Insertar(conexion, transaccion, pedido,
                "DELETE FROM carrito_items WHERE id_usuario = @idUsuario;");

            transaccion.Commit();

            ProductoRepository.LimpiarCache();
            return true;
        }

        // HU-16: guarda los cambios de un pedido (estado, pago, envío...)
        public void ActualizarPedido(Pedido pedido)
        {
            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand($@"
                UPDATE pedidos SET {SqlCamposActualizar},
                    numero_factura = @numeroFactura, fecha_factura = @fechaFactura
                WHERE id_pedido = @idPedido", conexion);

            AgregarParametros(comando, pedido);
            comando.Parametro("idPedido", pedido.IdPedido);

            comando.ExecuteNonQuery();
        }

        // HU-16: cancela un pedido pendiente en un solo viaje y de forma atómica:
        // cambia el estado, devuelve el stock reservado y regresa los productos al
        // carrito del cliente (solo los que siguen en el catálogo).
        // Devuelve false si el pedido ya no estaba pendiente (por ejemplo, ya se pagó).
        public bool CancelarPedido(Pedido pedido, string motivo)
        {
            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand(@"
                WITH cancelado AS (
                    UPDATE pedidos SET estado = @cancelado, motivo_cancelacion = @motivo
                    WHERE id_pedido = @idPedido AND estado = @pendiente
                    RETURNING id_pedido, id_usuario
                ),
                lineas AS (
                    SELECT d.id_producto, SUM(d.cantidad)::int AS cantidad
                    FROM detalle_pedido d
                    JOIN cancelado c ON c.id_pedido = d.id_pedido
                    GROUP BY d.id_producto
                ),
                stock AS (
                    UPDATE productos p SET stock = p.stock + l.cantidad
                    FROM lineas l
                    WHERE p.id_producto = l.id_producto
                ),
                carrito AS (
                    INSERT INTO carrito_items (id_usuario, id_producto, cantidad, posicion)
                    SELECT c.id_usuario, l.id_producto, l.cantidad,
                           COALESCE((SELECT MAX(ci.posicion) FROM carrito_items ci WHERE ci.id_usuario = c.id_usuario), 0)
                           + ROW_NUMBER() OVER (ORDER BY l.id_producto)
                    FROM cancelado c
                    CROSS JOIN lineas l
                    JOIN productos p ON p.id_producto = l.id_producto
                    ON CONFLICT (id_usuario, id_producto)
                    DO UPDATE SET cantidad = carrito_items.cantidad + EXCLUDED.cantidad
                )
                SELECT COUNT(*)::int FROM cancelado", conexion);

            comando.Parametro("cancelado", Pedido.EstadoCancelado);
            comando.Parametro("motivo", motivo);
            comando.Parametro("idPedido", pedido.IdPedido);
            comando.Parametro("pendiente", Pedido.EstadoPendientePago);

            if ((int)comando.ExecuteScalar()! == 0)
                return false;

            ProductoRepository.LimpiarCache();

            pedido.EstadoPedido = Pedido.EstadoCancelado;
            pedido.MotivoCancelacion = motivo;

            return true;
        }

        // HU-17: le asigna al pedido el siguiente número de factura y lo guarda,
        // en un solo viaje y de forma atómica: nunca quedan dos facturas con el
        // mismo número ni huecos. Si el pedido ya tenía factura, conserva la misma
        // (por ejemplo, si llegan dos confirmaciones del pago a la vez).
        public void AsignarFactura(Pedido pedido)
        {
            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand($@"
                WITH actual AS (
                    SELECT numero_factura FROM pedidos WHERE id_pedido = @idPedido FOR UPDATE
                ),
                siguiente AS (
                    INSERT INTO consecutivos (nombre, valor)
                    SELECT 'Factura', 1
                    WHERE NOT EXISTS (SELECT 1 FROM actual WHERE numero_factura <> '')
                    ON CONFLICT (nombre) DO UPDATE SET valor = consecutivos.valor + 1
                    RETURNING valor
                )
                UPDATE pedidos SET {SqlCamposActualizar},
                    numero_factura = COALESCE(
                        (SELECT 'FV-' || LPAD(valor::text, GREATEST(5, LENGTH(valor::text)), '0') FROM siguiente),
                        NULLIF(pedidos.numero_factura, ''),
                        @numeroFactura),
                    fecha_factura = CASE WHEN EXISTS (SELECT 1 FROM siguiente) THEN @ahora
                                         ELSE COALESCE(pedidos.fecha_factura, @fechaFactura) END
                WHERE id_pedido = @idPedido
                RETURNING numero_factura, fecha_factura", conexion);

            AgregarParametros(comando, pedido);
            comando.Parametro("idPedido", pedido.IdPedido);
            comando.ParametroFecha("ahora", DateTime.Now);

            using var lector = comando.ExecuteReader();

            if (lector.Read())
            {
                pedido.NumeroFactura = lector.Texto("numero_factura");
                pedido.FechaFactura = lector.FechaNula("fecha_factura");
            }
        }

        /* ---------- Ayudas (también las usa PosventaRepository) ---------- */

        // Inserta el pedido, sus productos y sus datos de envío/facturación en un
        // solo comando (PostgreSQL lo ejecuta todo o nada). "sqlExtra" se agrega al
        // final, por ejemplo para vaciar el carrito en el mismo viaje.
        internal static void Insertar(NpgsqlConnection conexion, NpgsqlTransaction? transaccion, Pedido pedido, string sqlExtra = "")
        {
            var sql = new StringBuilder(SqlInsertar);
            using var comando = new NpgsqlCommand { Connection = conexion, Transaction = transaccion };

            AgregarParametros(comando, pedido);

            for (int i = 0; i < pedido.Detalles.Count; i++)
            {
                var d = pedido.Detalles[i];

                sql.Append($@"
                    INSERT INTO detalle_pedido (id_pedido, id_producto, nombre, imagen, precio_unitario, cantidad, subtotal)
                    VALUES ({IdNuevoPedido}, @dIdProducto{i}, @dNombre{i}, @dImagen{i}, @dPrecio{i}, @dCantidad{i}, @dSubtotal{i});");

                comando.Parametro($"dIdProducto{i}", d.IdProducto);
                comando.Parametro($"dNombre{i}", d.Nombre ?? "");
                comando.Parametro($"dImagen{i}", d.Imagen ?? "");
                comando.Parametro($"dPrecio{i}", d.PrecioUnitario);
                comando.Parametro($"dCantidad{i}", d.Cantidad);
                comando.Parametro($"dSubtotal{i}", d.Subtotal);
            }

            AgregarDatosEnvio(sql, comando, "Envio", pedido.Envio);
            AgregarDatosEnvio(sql, comando, "Facturacion", pedido.Facturacion);

            sql.Append(sqlExtra);

            comando.CommandText = sql.ToString();

            // El primer resultado es el id del pedido nuevo
            comando.EjecutarTodo(lector => pedido.IdPedido = lector.Entero("id_pedido"));
        }

        private static void AgregarDatosEnvio(StringBuilder sql, NpgsqlCommand comando, string tipo, DatosEnvio? datos)
        {
            if (datos == null)
                return;

            string p = tipo; // prefijo de los parámetros: @Envio_nombre, @Facturacion_nombre...

            sql.Append($@"
                INSERT INTO datos_envio (id_pedido, tipo, pais, nombre, apellidos, tipo_documento, cedula,
                                         direccion, complemento, ciudad, departamento, codigo_postal, telefono)
                VALUES ({IdNuevoPedido}, @{p}_tipo, @{p}_pais, @{p}_nombre, @{p}_apellidos, @{p}_tipoDocumento, @{p}_cedula,
                        @{p}_direccion, @{p}_complemento, @{p}_ciudad, @{p}_departamento, @{p}_codigoPostal, @{p}_telefono);");

            comando.Parametro($"{p}_tipo", tipo);
            comando.Parametro($"{p}_pais", datos.Pais ?? "");
            comando.Parametro($"{p}_nombre", datos.Nombre ?? "");
            comando.Parametro($"{p}_apellidos", datos.Apellidos ?? "");
            comando.Parametro($"{p}_tipoDocumento", datos.TipoDocumento ?? "");
            comando.Parametro($"{p}_cedula", datos.Cedula ?? "");
            comando.Parametro($"{p}_direccion", datos.Direccion ?? "");
            comando.Parametro($"{p}_complemento", datos.Complemento ?? "");
            comando.Parametro($"{p}_ciudad", datos.Ciudad ?? "");
            comando.Parametro($"{p}_departamento", datos.Departamento ?? "");
            comando.Parametro($"{p}_codigoPostal", datos.CodigoPostal ?? "");
            comando.Parametro($"{p}_telefono", datos.Telefono ?? "");
        }

        // Pedidos que cumplen el filtro, con sus productos y datos de envío.
        // Las tres consultas viajan juntas: una sola ida a la base de datos
        private static List<Pedido> Cargar(string filtro, Action<NpgsqlCommand> parametros)
        {
            string sql = $@"
                SELECT p.* FROM pedidos p WHERE {filtro} ORDER BY p.id_pedido;

                SELECT d.* FROM detalle_pedido d
                JOIN pedidos p ON p.id_pedido = d.id_pedido
                WHERE {filtro} ORDER BY d.id_detalle;

                SELECT e.* FROM datos_envio e
                JOIN pedidos p ON p.id_pedido = e.id_pedido
                WHERE {filtro};";

            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand(sql, conexion);

            parametros(comando);

            using var lector = comando.ExecuteReader();

            var pedidos = new List<Pedido>();
            var porId = new Dictionary<int, Pedido>();

            while (lector.Read())
            {
                var pedido = LeerPedido(lector);
                pedidos.Add(pedido);
                porId[pedido.IdPedido] = pedido;
            }

            lector.NextResult();

            while (lector.Read())
            {
                porId[lector.Entero("id_pedido")].Detalles.Add(new DetallePedido
                {
                    IdProducto = lector.Entero("id_producto"),
                    Nombre = lector.Texto("nombre"),
                    Imagen = lector.Texto("imagen"),
                    PrecioUnitario = lector.Numero("precio_unitario"),
                    Cantidad = lector.Entero("cantidad"),
                    Subtotal = lector.Numero("subtotal")
                });
            }

            lector.NextResult();

            while (lector.Read())
            {
                var datos = new DatosEnvio
                {
                    Pais = lector.Texto("pais"),
                    Nombre = lector.Texto("nombre"),
                    Apellidos = lector.Texto("apellidos"),
                    TipoDocumento = lector.Texto("tipo_documento"),
                    Cedula = lector.Texto("cedula"),
                    Direccion = lector.Texto("direccion"),
                    Complemento = lector.Texto("complemento"),
                    Ciudad = lector.Texto("ciudad"),
                    Departamento = lector.Texto("departamento"),
                    CodigoPostal = lector.Texto("codigo_postal"),
                    Telefono = lector.Texto("telefono")
                };

                var pedido = porId[lector.Entero("id_pedido")];

                if (lector.Texto("tipo") == "Envio")
                    pedido.Envio = datos;
                else
                    pedido.Facturacion = datos;
            }

            return pedidos;
        }

        private static Pedido LeerPedido(NpgsqlDataReader lector)
        {
            return new Pedido
            {
                IdPedido = lector.Entero("id_pedido"),
                IdUsuario = lector.Entero("id_usuario"),
                Fecha = lector.Fecha("fecha"),
                EstadoPedido = lector.Texto("estado"),
                TipoPedido = lector.Texto("tipo_pedido"),
                MetodoEnvio = lector.Texto("metodo_envio"),
                MetodoPago = lector.Texto("metodo_pago"),
                Subtotal = lector.Numero("subtotal"),
                CostoEnvio = lector.Numero("costo_envio"),
                Impuestos = lector.Numero("impuestos"),
                TotalPedido = lector.Numero("total"),
                FechaLimitePago = lector.FechaNula("fecha_limite_pago"),
                ReferenciaPago = lector.Texto("referencia_pago"),
                IdTransaccion = lector.Texto("id_transaccion"),
                MedioPago = lector.Texto("medio_pago"),
                FechaPago = lector.FechaNula("fecha_pago"),
                MotivoCancelacion = lector.Texto("motivo_cancelacion"),
                NumeroFactura = lector.Texto("numero_factura"),
                FechaFactura = lector.FechaNula("fecha_factura"),
                Transportadora = lector.Texto("transportadora"),
                NumeroGuia = lector.Texto("numero_guia"),
                FechaEnvio = lector.FechaNula("fecha_envio"),
                FechaEntrega = lector.FechaNula("fecha_entrega"),
                IdPedidoOriginal = lector.EnteroNulo("id_pedido_original"),
                NumeroGarantia = lector.Texto("numero_garantia")
            };
        }

        private static void AgregarParametros(NpgsqlCommand comando, Pedido pedido)
        {
            comando.Parametro("idUsuario", pedido.IdUsuario);
            comando.ParametroFecha("fecha", pedido.Fecha);
            comando.Parametro("estado", pedido.EstadoPedido ?? "");
            comando.Parametro("tipoPedido", string.IsNullOrEmpty(pedido.TipoPedido) ? Pedido.TipoVenta : pedido.TipoPedido);
            comando.Parametro("metodoEnvio", pedido.MetodoEnvio ?? "");
            comando.Parametro("metodoPago", pedido.MetodoPago ?? "");
            comando.Parametro("subtotal", pedido.Subtotal);
            comando.Parametro("costoEnvio", pedido.CostoEnvio);
            comando.Parametro("impuestos", pedido.Impuestos);
            comando.Parametro("total", pedido.TotalPedido);
            comando.ParametroFecha("fechaLimitePago", pedido.FechaLimitePago);
            comando.Parametro("referenciaPago", pedido.ReferenciaPago ?? "");
            comando.Parametro("idTransaccion", pedido.IdTransaccion ?? "");
            comando.Parametro("medioPago", pedido.MedioPago ?? "");
            comando.ParametroFecha("fechaPago", pedido.FechaPago);
            comando.Parametro("motivoCancelacion", pedido.MotivoCancelacion ?? "");
            comando.Parametro("numeroFactura", pedido.NumeroFactura ?? "");
            comando.ParametroFecha("fechaFactura", pedido.FechaFactura);
            comando.Parametro("transportadora", pedido.Transportadora ?? "");
            comando.Parametro("numeroGuia", pedido.NumeroGuia ?? "");
            comando.ParametroFecha("fechaEnvio", pedido.FechaEnvio);
            comando.ParametroFecha("fechaEntrega", pedido.FechaEntrega);
            comando.Parametro("idPedidoOriginal", pedido.IdPedidoOriginal);
            comando.Parametro("numeroGarantia", pedido.NumeroGarantia ?? "");
        }
    }
}
