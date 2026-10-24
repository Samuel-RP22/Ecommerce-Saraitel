using System.Text;
using Npgsql;
using Saraitel.Datos.Conexion;
using Saraitel.Entidades;

namespace Saraitel.Datos.Repositories
{
    // HU-19 a HU-23: devoluciones y tickets de garantía.
    // Rendimiento: cada operación manda sus consultas juntas en un solo comando,
    // que PostgreSQL ejecuta como una sola transacción (todo o nada).
    public class PosventaRepository
    {
        // Id de la devolución / ticket que se acaba de insertar en esta misma conexión
        private const string IdNuevaDevolucion = "currval(pg_get_serial_sequence('devoluciones', 'id_devolucion'))";
        private const string IdNuevoTicket = "currval(pg_get_serial_sequence('garantias', 'id_ticket'))";

        // Siguiente número de un consecutivo con el formato XX-00001
        private const string SqlSiguienteNumero = @"
            WITH siguiente AS (
                INSERT INTO consecutivos (nombre, valor) VALUES (@consecutivo, 1)
                ON CONFLICT (nombre) DO UPDATE SET valor = consecutivos.valor + 1
                RETURNING valor
            )";

        private const string SqlNumero = "@prefijo || LPAD(siguiente.valor::text, GREATEST(5, LENGTH(siguiente.valor::text)), '0')";

        /* =========================
           DEVOLUCIONES
        ========================= */

        public List<Devolucion> ObtenerDevoluciones()
        {
            return CargarDevoluciones("TRUE", _ => { });
        }

        public List<Devolucion> ObtenerDevolucionesDePedido(int idPedido)
        {
            return CargarDevoluciones("d.id_pedido = @idPedido", comando => comando.Parametro("idPedido", idPedido));
        }

        public List<Devolucion> ObtenerDevolucionesDeUsuario(int idUsuario)
        {
            return CargarDevoluciones("d.id_usuario = @idUsuario", comando => comando.Parametro("idUsuario", idUsuario));
        }

        public Devolucion? ObtenerDevolucionPorId(int id)
        {
            return CargarDevoluciones("d.id_devolucion = @id", comando => comando.Parametro("id", id))
                .FirstOrDefault();
        }

        // Le asigna número consecutivo (DV-00001) y la guarda con sus productos e
        // historial, todo en un solo viaje
        public Devolucion AgregarDevolucion(Devolucion devolucion)
        {
            var sql = new StringBuilder($@"
                {SqlSiguienteNumero}
                INSERT INTO devoluciones (
                    numero, id_pedido, id_usuario, nombre_cliente, documento_cliente, fecha, estado,
                    motivo, comentario, incluye_envio, valor_productos, valor_envio, valor_reembolso,
                    motivo_rechazo, referencia_reembolso, fecha_reembolso)
                SELECT
                    {SqlNumero}, @idPedido, @idUsuario, @nombreCliente, @documentoCliente, @fecha, @estado,
                    @motivo, @comentario, @incluyeEnvio, @valorProductos, @valorEnvio, @valorReembolso,
                    @motivoRechazo, @referenciaReembolso, @fechaReembolso
                FROM siguiente
                RETURNING id_devolucion, numero;");

            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand { Connection = conexion };

            ParametrosDevolucion(comando, devolucion);
            comando.Parametro("consecutivo", "Devolucion");
            comando.Parametro("prefijo", "DV-");

            for (int i = 0; i < devolucion.Lineas.Count; i++)
            {
                var l = devolucion.Lineas[i];

                sql.Append($@"
                    INSERT INTO devolucion_lineas (id_devolucion, id_producto, nombre, imagen, precio_unitario, cantidad, subtotal)
                    VALUES ({IdNuevaDevolucion}, @lIdProducto{i}, @lNombre{i}, @lImagen{i}, @lPrecio{i}, @lCantidad{i}, @lSubtotal{i});");

                comando.Parametro($"lIdProducto{i}", l.IdProducto);
                comando.Parametro($"lNombre{i}", l.Nombre ?? "");
                comando.Parametro($"lImagen{i}", l.Imagen ?? "");
                comando.Parametro($"lPrecio{i}", l.PrecioUnitario);
                comando.Parametro($"lCantidad{i}", l.Cantidad);
                comando.Parametro($"lSubtotal{i}", l.Subtotal);
            }

            AgregarHistorial(sql, comando, "devolucion_historial", "id_devolucion", IdNuevaDevolucion, devolucion.Historial);

            comando.CommandText = sql.ToString();
            comando.EjecutarTodo(lector =>
            {
                devolucion.IdDevolucion = lector.Entero("id_devolucion");
                devolucion.Numero = lector.Texto("numero");
            });

            return devolucion;
        }

        public void ActualizarDevolucion(Devolucion devolucion)
        {
            using var conexion = ConexionBD.Abrir();

            GuardarDevolucion(conexion, devolucion, devolverStock: false);
        }

        // HU-21: el producto devuelto llega a la tienda en buen estado y vuelve al stock.
        // El estado y el stock se guardan en el mismo comando (todo o nada).
        public void RecibirDevolucion(Devolucion devolucion)
        {
            using var conexion = ConexionBD.Abrir();

            GuardarDevolucion(conexion, devolucion, devolverStock: true);

            ProductoRepository.LimpiarCache();
        }

        /* =========================
           GARANTÍAS
        ========================= */

        public List<TicketGarantia> ObtenerGarantias()
        {
            return CargarGarantias("TRUE", _ => { });
        }

        public List<TicketGarantia> ObtenerGarantiasDePedido(int idPedido)
        {
            return CargarGarantias("g.id_pedido = @idPedido", comando => comando.Parametro("idPedido", idPedido));
        }

        public List<TicketGarantia> ObtenerGarantiasDeUsuario(int idUsuario)
        {
            return CargarGarantias("g.id_usuario = @idUsuario", comando => comando.Parametro("idUsuario", idUsuario));
        }

        public TicketGarantia? ObtenerGarantiaPorId(int id)
        {
            return CargarGarantias("g.id_ticket = @id", comando => comando.Parametro("id", id))
                .FirstOrDefault();
        }

        // Le asigna número consecutivo (GT-00001) y lo guarda con su historial y sus
        // fotos, todo en un solo viaje. Las fotos se nombran con el número del ticket:
        // GT-00001-1.jpg, GT-00001-2.png...
        public TicketGarantia AgregarGarantia(TicketGarantia ticket, List<(string Extension, byte[] Contenido)> fotos)
        {
            var sql = new StringBuilder($@"
                {SqlSiguienteNumero}
                INSERT INTO garantias (
                    numero, id_pedido, id_usuario, nombre_cliente, documento_cliente, fecha, estado,
                    id_producto, nombre_producto, imagen_producto, precio_unitario, cantidad, subtotal,
                    descripcion_falla, cubierto_por, motivo_rechazo, id_pedido_reposicion, producto_reposicion)
                SELECT
                    {SqlNumero}, @idPedido, @idUsuario, @nombreCliente, @documentoCliente, @fecha, @estado,
                    @idProducto, @nombreProducto, @imagenProducto, @precioUnitario, @cantidad, @subtotal,
                    @descripcionFalla, @cubiertoPor, @motivoRechazo, @idPedidoReposicion, @productoReposicion
                FROM siguiente
                RETURNING id_ticket, numero;");

            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand { Connection = conexion };

            ParametrosGarantia(comando, ticket);
            comando.Parametro("consecutivo", "Garantia");
            comando.Parametro("prefijo", "GT-");

            AgregarHistorial(sql, comando, "garantia_historial", "id_ticket", IdNuevoTicket, ticket.Historial);

            // Nombre de cada foto: número del ticket + "-1.jpg", "-2.png"...
            var sufijos = new List<string>();

            for (int i = 0; i < fotos.Count; i++)
            {
                string sufijo = $"-{i + 1}{fotos[i].Extension}";
                sufijos.Add(sufijo);

                sql.Append($@"
                    INSERT INTO garantia_evidencias (id_ticket, archivo, tipo_contenido, contenido)
                    SELECT id_ticket, numero || @eSufijo{i}, @eTipo{i}, @eContenido{i}
                    FROM garantias WHERE id_ticket = {IdNuevoTicket};");

                comando.Parametro($"eSufijo{i}", sufijo);
                comando.Parametro($"eTipo{i}", fotos[i].Extension == ".png" ? "image/png" : "image/jpeg");
                comando.Parametro($"eContenido{i}", fotos[i].Contenido);
            }

            comando.CommandText = sql.ToString();
            comando.EjecutarTodo(lector =>
            {
                ticket.IdTicket = lector.Entero("id_ticket");
                ticket.Numero = lector.Texto("numero");
            });

            ticket.Evidencias = sufijos.Select(s => ticket.Numero + s).ToList();

            return ticket;
        }

        public void ActualizarGarantia(TicketGarantia ticket)
        {
            using var conexion = ConexionBD.Abrir();

            GuardarGarantia(conexion, null, ticket);
        }

        // HU-23: crea el pedido de reposición por $0, descuenta del stock el producto
        // nuevo y actualiza el ticket, todo en una sola transacción.
        // Devuelve null si ya no hay stock suficiente.
        public Pedido? CrearReposicion(TicketGarantia ticket, Pedido reposicion, int idProductoReemplazo)
        {
            using var conexion = ConexionBD.Abrir();
            using var transaccion = conexion.BeginTransaction();

            var cantidades = new Dictionary<int, int> { [idProductoReemplazo] = ticket.Linea.Cantidad };

            if (!ProductoRepository.DescontarStock(conexion, transaccion, cantidades))
                return null; // sin Commit: no se guarda nada

            PedidoRepository.Insertar(conexion, transaccion, reposicion);

            ticket.IdPedidoReposicion = reposicion.IdPedido;

            GuardarGarantia(conexion, transaccion, ticket);

            transaccion.Commit();

            ProductoRepository.LimpiarCache();

            return reposicion;
        }

        // Foto de evidencia guardada en la base de datos
        public (byte[]? Contenido, string TipoContenido) ObtenerEvidencia(int idTicket, string archivo)
        {
            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand(@"
                SELECT tipo_contenido, contenido FROM garantia_evidencias
                WHERE id_ticket = @idTicket AND archivo = @archivo", conexion);

            comando.Parametro("idTicket", idTicket);
            comando.Parametro("archivo", archivo);

            using var lector = comando.ExecuteReader();

            if (!lector.Read())
                return (null, "");

            return ((byte[])lector["contenido"], lector.Texto("tipo_contenido"));
        }

        /* =========================
           AYUDAS
        ========================= */

        // Actualiza los datos de la devolución y reescribe su historial; si se pide,
        // también devuelve sus productos al stock. Todo en un solo comando.
        private static void GuardarDevolucion(NpgsqlConnection conexion, Devolucion devolucion, bool devolverStock)
        {
            var sql = new StringBuilder(@"
                UPDATE devoluciones SET
                    numero = @numero, id_pedido = @idPedido, id_usuario = @idUsuario,
                    nombre_cliente = @nombreCliente, documento_cliente = @documentoCliente,
                    fecha = @fecha, estado = @estado, motivo = @motivo, comentario = @comentario,
                    incluye_envio = @incluyeEnvio, valor_productos = @valorProductos,
                    valor_envio = @valorEnvio, valor_reembolso = @valorReembolso,
                    motivo_rechazo = @motivoRechazo, referencia_reembolso = @referenciaReembolso,
                    fecha_reembolso = @fechaReembolso
                WHERE id_devolucion = @id;
                DELETE FROM devolucion_historial WHERE id_devolucion = @id;");

            using var comando = new NpgsqlCommand { Connection = conexion };

            ParametrosDevolucion(comando, devolucion);
            comando.Parametro("numero", devolucion.Numero ?? "");
            comando.Parametro("id", devolucion.IdDevolucion);

            AgregarHistorial(sql, comando, "devolucion_historial", "id_devolucion", "@id", devolucion.Historial);

            if (devolverStock)
            {
                // Si el producto ya no existe en el catálogo, simplemente no se actualiza nada
                for (int i = 0; i < devolucion.Lineas.Count; i++)
                {
                    sql.Append($@"
                        UPDATE productos SET stock = stock + @sCantidad{i} WHERE id_producto = @sIdProducto{i};");

                    comando.Parametro($"sIdProducto{i}", devolucion.Lineas[i].IdProducto);
                    comando.Parametro($"sCantidad{i}", devolucion.Lineas[i].Cantidad);
                }
            }

            comando.CommandText = sql.ToString();
            comando.ExecuteNonQuery();
        }

        // Actualiza los datos del ticket y reescribe su historial (las fotos no cambian)
        private static void GuardarGarantia(NpgsqlConnection conexion, NpgsqlTransaction? transaccion, TicketGarantia ticket)
        {
            var sql = new StringBuilder(@"
                UPDATE garantias SET
                    numero = @numero, id_pedido = @idPedido, id_usuario = @idUsuario,
                    nombre_cliente = @nombreCliente, documento_cliente = @documentoCliente,
                    fecha = @fecha, estado = @estado,
                    id_producto = @idProducto, nombre_producto = @nombreProducto, imagen_producto = @imagenProducto,
                    precio_unitario = @precioUnitario, cantidad = @cantidad, subtotal = @subtotal,
                    descripcion_falla = @descripcionFalla, cubierto_por = @cubiertoPor,
                    motivo_rechazo = @motivoRechazo, id_pedido_reposicion = @idPedidoReposicion,
                    producto_reposicion = @productoReposicion
                WHERE id_ticket = @id;
                DELETE FROM garantia_historial WHERE id_ticket = @id;");

            using var comando = new NpgsqlCommand { Connection = conexion, Transaction = transaccion };

            ParametrosGarantia(comando, ticket);
            comando.Parametro("numero", ticket.Numero ?? "");
            comando.Parametro("id", ticket.IdTicket);

            AgregarHistorial(sql, comando, "garantia_historial", "id_ticket", "@id", ticket.Historial);

            comando.CommandText = sql.ToString();
            comando.ExecuteNonQuery();
        }

        // Agrega al lote los INSERT del historial de estados.
        // "id" es el SQL del id dueño: "@id" o el id recién insertado
        private static void AgregarHistorial(StringBuilder sql, NpgsqlCommand comando, string tabla, string columnaId, string id, List<HistorialEstado> historial)
        {
            for (int i = 0; i < historial.Count; i++)
            {
                sql.Append($@"
                    INSERT INTO {tabla} ({columnaId}, estado, fecha, nota)
                    VALUES ({id}, @hEstado{i}, @hFecha{i}, @hNota{i});");

                comando.Parametro($"hEstado{i}", historial[i].Estado ?? "");
                comando.ParametroFecha($"hFecha{i}", historial[i].Fecha);
                comando.Parametro($"hNota{i}", historial[i].Nota ?? "");
            }
        }

        // Datos de la devolución (el número se agrega aparte: al crearla lo genera la base de datos)
        private static void ParametrosDevolucion(NpgsqlCommand comando, Devolucion d)
        {
            comando.Parametro("idPedido", d.IdPedido);
            comando.Parametro("idUsuario", d.IdUsuario);
            comando.Parametro("nombreCliente", d.NombreCliente ?? "");
            comando.Parametro("documentoCliente", d.DocumentoCliente ?? "");
            comando.ParametroFecha("fecha", d.Fecha);
            comando.Parametro("estado", d.Estado ?? "");
            comando.Parametro("motivo", d.Motivo ?? "");
            comando.Parametro("comentario", d.Comentario ?? "");
            comando.Parametro("incluyeEnvio", d.IncluyeEnvio);
            comando.Parametro("valorProductos", d.ValorProductos);
            comando.Parametro("valorEnvio", d.ValorEnvio);
            comando.Parametro("valorReembolso", d.ValorReembolso);
            comando.Parametro("motivoRechazo", d.MotivoRechazo ?? "");
            comando.Parametro("referenciaReembolso", d.ReferenciaReembolso ?? "");
            comando.ParametroFecha("fechaReembolso", d.FechaReembolso);
        }

        // Datos del ticket (el número se agrega aparte: al crearlo lo genera la base de datos)
        private static void ParametrosGarantia(NpgsqlCommand comando, TicketGarantia g)
        {
            comando.Parametro("idPedido", g.IdPedido);
            comando.Parametro("idUsuario", g.IdUsuario);
            comando.Parametro("nombreCliente", g.NombreCliente ?? "");
            comando.Parametro("documentoCliente", g.DocumentoCliente ?? "");
            comando.ParametroFecha("fecha", g.Fecha);
            comando.Parametro("estado", g.Estado ?? "");
            comando.Parametro("idProducto", g.Linea.IdProducto);
            comando.Parametro("nombreProducto", g.Linea.Nombre ?? "");
            comando.Parametro("imagenProducto", g.Linea.Imagen ?? "");
            comando.Parametro("precioUnitario", g.Linea.PrecioUnitario);
            comando.Parametro("cantidad", g.Linea.Cantidad);
            comando.Parametro("subtotal", g.Linea.Subtotal);
            comando.Parametro("descripcionFalla", g.DescripcionFalla ?? "");
            comando.Parametro("cubiertoPor", g.CubiertoPor ?? "");
            comando.Parametro("motivoRechazo", g.MotivoRechazo ?? "");
            comando.Parametro("idPedidoReposicion", g.IdPedidoReposicion);
            comando.Parametro("productoReposicion", g.ProductoReposicion ?? "");
        }

        // Devoluciones con sus productos e historial (tres consultas en un solo viaje)
        private static List<Devolucion> CargarDevoluciones(string filtro, Action<NpgsqlCommand> parametros)
        {
            string sql = $@"
                SELECT d.* FROM devoluciones d WHERE {filtro} ORDER BY d.id_devolucion;

                SELECT l.* FROM devolucion_lineas l
                JOIN devoluciones d ON d.id_devolucion = l.id_devolucion
                WHERE {filtro} ORDER BY l.id_linea;

                SELECT h.* FROM devolucion_historial h
                JOIN devoluciones d ON d.id_devolucion = h.id_devolucion
                WHERE {filtro} ORDER BY h.id_historial;";

            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand(sql, conexion);

            parametros(comando);

            using var lector = comando.ExecuteReader();

            var devoluciones = new List<Devolucion>();
            var porId = new Dictionary<int, Devolucion>();

            while (lector.Read())
            {
                var d = new Devolucion
                {
                    IdDevolucion = lector.Entero("id_devolucion"),
                    Numero = lector.Texto("numero"),
                    IdPedido = lector.Entero("id_pedido"),
                    IdUsuario = lector.Entero("id_usuario"),
                    NombreCliente = lector.Texto("nombre_cliente"),
                    DocumentoCliente = lector.Texto("documento_cliente"),
                    Fecha = lector.Fecha("fecha"),
                    Estado = lector.Texto("estado"),
                    Motivo = lector.Texto("motivo"),
                    Comentario = lector.Texto("comentario"),
                    IncluyeEnvio = lector.Booleano("incluye_envio"),
                    ValorProductos = lector.Numero("valor_productos"),
                    ValorEnvio = lector.Numero("valor_envio"),
                    ValorReembolso = lector.Numero("valor_reembolso"),
                    MotivoRechazo = lector.Texto("motivo_rechazo"),
                    ReferenciaReembolso = lector.Texto("referencia_reembolso"),
                    FechaReembolso = lector.FechaNula("fecha_reembolso")
                };

                devoluciones.Add(d);
                porId[d.IdDevolucion] = d;
            }

            lector.NextResult();

            while (lector.Read())
            {
                porId[lector.Entero("id_devolucion")].Lineas.Add(new LineaPosventa
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
                porId[lector.Entero("id_devolucion")].Historial.Add(LeerHistorial(lector));

            return devoluciones;
        }

        // Tickets con su historial y los nombres de sus fotos (tres consultas en un solo viaje)
        private static List<TicketGarantia> CargarGarantias(string filtro, Action<NpgsqlCommand> parametros)
        {
            string sql = $@"
                SELECT g.* FROM garantias g WHERE {filtro} ORDER BY g.id_ticket;

                SELECT h.* FROM garantia_historial h
                JOIN garantias g ON g.id_ticket = h.id_ticket
                WHERE {filtro} ORDER BY h.id_historial;

                SELECT ev.id_ticket, ev.archivo FROM garantia_evidencias ev
                JOIN garantias g ON g.id_ticket = ev.id_ticket
                WHERE {filtro} ORDER BY ev.id_evidencia;";

            using var conexion = ConexionBD.Abrir();
            using var comando = new NpgsqlCommand(sql, conexion);

            parametros(comando);

            using var lector = comando.ExecuteReader();

            var garantias = new List<TicketGarantia>();
            var porId = new Dictionary<int, TicketGarantia>();

            while (lector.Read())
            {
                var g = new TicketGarantia
                {
                    IdTicket = lector.Entero("id_ticket"),
                    Numero = lector.Texto("numero"),
                    IdPedido = lector.Entero("id_pedido"),
                    IdUsuario = lector.Entero("id_usuario"),
                    NombreCliente = lector.Texto("nombre_cliente"),
                    DocumentoCliente = lector.Texto("documento_cliente"),
                    Fecha = lector.Fecha("fecha"),
                    Estado = lector.Texto("estado"),
                    Linea = new LineaPosventa
                    {
                        IdProducto = lector.Entero("id_producto"),
                        Nombre = lector.Texto("nombre_producto"),
                        Imagen = lector.Texto("imagen_producto"),
                        PrecioUnitario = lector.Numero("precio_unitario"),
                        Cantidad = lector.Entero("cantidad"),
                        Subtotal = lector.Numero("subtotal")
                    },
                    DescripcionFalla = lector.Texto("descripcion_falla"),
                    CubiertoPor = lector.Texto("cubierto_por"),
                    MotivoRechazo = lector.Texto("motivo_rechazo"),
                    IdPedidoReposicion = lector.EnteroNulo("id_pedido_reposicion"),
                    ProductoReposicion = lector.Texto("producto_reposicion")
                };

                garantias.Add(g);
                porId[g.IdTicket] = g;
            }

            lector.NextResult();

            while (lector.Read())
                porId[lector.Entero("id_ticket")].Historial.Add(LeerHistorial(lector));

            lector.NextResult();

            while (lector.Read())
                porId[lector.Entero("id_ticket")].Evidencias.Add(lector.Texto("archivo"));

            return garantias;
        }

        private static HistorialEstado LeerHistorial(NpgsqlDataReader lector)
        {
            return new HistorialEstado
            {
                Estado = lector.Texto("estado"),
                Fecha = lector.Fecha("fecha"),
                Nota = lector.Texto("nota")
            };
        }
    }
}
