using System.Globalization;
using Saraitel.API.Interfaces;
using Saraitel.API.Models;
using Saraitel.Datos.Repositories;
using Saraitel.Entidades;

namespace Saraitel.API.Services
{
    // HU-19 a HU-23: devoluciones y garantías
    public class PosventaService : IPosventaService
    {
        private readonly PosventaRepository _posventaRepo;
        private readonly PedidoRepository _pedidoRepo;
        private readonly UsuarioRepository _usuarioRepo;
        private readonly ProductoRepository _productoRepo;
        private readonly IColaTareas _cola;

        private static readonly CultureInfo Colombia = new CultureInfo("es-CO");

        public PosventaService(IColaTareas cola)
        {
            _posventaRepo = new PosventaRepository();
            _pedidoRepo = new PedidoRepository();
            _usuarioRepo = new UsuarioRepository();
            _productoRepo = new ProductoRepository();
            _cola = cola;
        }

        /* =========================
           RESUMEN PARA EL CLIENTE
        ========================= */

        public (ResumenPosventaPedido? Resumen, string Mensaje) ObtenerResumenPedido(int idPedido, int idUsuario)
        {
            var pedido = _pedidoRepo.ObtenerPedidoPorId(idPedido);

            if (pedido == null || pedido.IdUsuario != idUsuario)
                return (null, "Pedido no encontrado");

            var devoluciones = _posventaRepo.ObtenerDevolucionesDePedido(idPedido);
            var garantias = _posventaRepo.ObtenerGarantiasDePedido(idPedido);

            var resumen = new ResumenPosventaPedido
            {
                CostoEnvio = pedido.CostoEnvio,
                TotalUnidades = pedido.Detalles.Sum(d => d.Cantidad),
                UnidadesEnDevolucion = devoluciones.Where(d => d.OcupaUnidades()).Sum(d => d.Lineas.Sum(l => l.Cantidad)),
                EsReposicion = pedido.EsReposicion(),
                Devoluciones = devoluciones.OrderByDescending(d => d.Fecha).ToList(),
                Garantias = garantias.OrderByDescending(g => g.Fecha).ToList(),
                Lineas = pedido.Detalles.Select(d => new LineaDisponible
                {
                    IdProducto = d.IdProducto,
                    Nombre = d.Nombre,
                    Imagen = d.Imagen,
                    PrecioUnitario = d.PrecioUnitario,
                    Comprada = d.Cantidad,
                    Disponible = d.Cantidad - UnidadesOcupadas(d.IdProducto, devoluciones, garantias)
                }).ToList()
            };

            bool hayDisponibles = resumen.Lineas.Any(l => l.Disponible > 0);

            if (pedido.EstadoPedido != Pedido.EstadoEntregado)
            {
                resumen.MensajeDevolucion = "Podrás solicitar una devolución cuando el pedido sea entregado.";
                resumen.MensajeGarantia = "Podrás reportar una garantía cuando el pedido sea entregado.";
                return (resumen, "ok");
            }

            DateTime entrega = FechaEntregaDe(pedido);
            resumen.FechaLimiteDevolucion = FinDelDia(SumarDiasHabiles(entrega, Devolucion.DiasHabilesParaDevolver));
            resumen.FechaLimiteGarantia = FinDelDia(entrega.Date.AddMonths(TicketGarantia.MesesDeGarantia));

            // Devolución
            if (pedido.EsReposicion())
                resumen.MensajeDevolucion = "Los pedidos de reposición por garantía no se pueden devolver.";
            else if (DateTime.Now > resumen.FechaLimiteDevolucion)
                resumen.MensajeDevolucion = $"El plazo para devoluciones venció el {Fecha(resumen.FechaLimiteDevolucion.Value)}.";
            else if (!hayDisponibles)
                resumen.MensajeDevolucion = "Todas las unidades de este pedido ya tienen una solicitud.";
            else
                resumen.PuedeDevolver = true;

            // Garantía
            if (DateTime.Now > resumen.FechaLimiteGarantia)
                resumen.MensajeGarantia = $"La garantía venció el {Fecha(resumen.FechaLimiteGarantia.Value)}.";
            else if (!hayDisponibles)
                resumen.MensajeGarantia = "Todas las unidades de este pedido ya tienen una solicitud.";
            else
                resumen.PuedeReportarGarantia = true;

            return (resumen, "ok");
        }

        public PosventaUsuario ObtenerDeUsuario(int idUsuario)
        {
            return new PosventaUsuario
            {
                Devoluciones = _posventaRepo.ObtenerDevolucionesDeUsuario(idUsuario)
                    .OrderByDescending(d => d.Fecha)
                    .ToList(),
                Garantias = _posventaRepo.ObtenerGarantiasDeUsuario(idUsuario)
                    .OrderByDescending(g => g.Fecha)
                    .ToList()
            };
        }

        /* =========================
           HU-19 SOLICITAR DEVOLUCIÓN
        ========================= */

        public (Devolucion? Devolucion, string Mensaje) SolicitarDevolucion(int idUsuario, SolicitudDevolucion solicitud)
        {
            if (solicitud == null)
                return (null, "Completa los datos de la devolución.");

            var (resumen, mensaje) = ObtenerResumenPedido(solicitud.IdPedido, idUsuario);

            if (resumen == null)
                return (null, mensaje);

            if (!resumen.PuedeDevolver)
                return (null, resumen.MensajeDevolucion);

            string motivo = (solicitud.Motivo ?? "").Trim();
            string comentario = (solicitud.Comentario ?? "").Trim();

            if (!Devolucion.Motivos.Contains(motivo))
                return (null, "Selecciona el motivo de la devolución.");

            if (motivo == "Otro" && comentario.Length < 5)
                return (null, "Cuéntanos el motivo de la devolución en el comentario.");

            if (comentario.Length > 500)
                return (null, "El comentario puede tener máximo 500 caracteres.");

            // Se agrupan por producto por si llega el mismo producto dos veces
            var pedidas = (solicitud.Lineas ?? new List<LineaSolicitada>())
                .Where(l => l.Cantidad > 0)
                .GroupBy(l => l.IdProducto)
                .Select(g => new { IdProducto = g.Key, Cantidad = g.Sum(l => l.Cantidad) })
                .ToList();

            if (pedidas.Count == 0)
                return (null, "Selecciona al menos un producto para devolver.");

            var lineas = new List<LineaPosventa>();

            foreach (var pedida in pedidas)
            {
                var disponible = resumen.Lineas.FirstOrDefault(l => l.IdProducto == pedida.IdProducto);

                if (disponible == null)
                    return (null, "Uno de los productos no pertenece a este pedido.");

                if (pedida.Cantidad > disponible.Disponible)
                    return (null, $"De \"{disponible.Nombre}\" puedes devolver máximo {disponible.Disponible} unidad(es).");

                lineas.Add(new LineaPosventa
                {
                    IdProducto = disponible.IdProducto,
                    Nombre = disponible.Nombre,
                    Imagen = disponible.Imagen,
                    PrecioUnitario = disponible.PrecioUnitario,
                    Cantidad = pedida.Cantidad,
                    Subtotal = disponible.PrecioUnitario * pedida.Cantidad
                });
            }

            // El envío se reembolsa solo si con esta solicitud se devuelve el pedido completo
            int unidades = lineas.Sum(l => l.Cantidad);
            bool incluyeEnvio = resumen.UnidadesEnDevolucion + unidades == resumen.TotalUnidades;

            var pedido = _pedidoRepo.ObtenerPedidoPorId(solicitud.IdPedido)!;
            var cliente = pedido.Facturacion ?? pedido.Envio ?? new DatosEnvio();

            var devolucion = new Devolucion
            {
                IdPedido = pedido.IdPedido,
                IdUsuario = idUsuario,
                NombreCliente = $"{cliente.Nombre} {cliente.Apellidos}".Trim(),
                DocumentoCliente = $"{DatosEnvio.Abreviatura(cliente.TipoDocumento)} {cliente.Cedula}",
                Fecha = DateTime.Now,
                Estado = Devolucion.EstadoSolicitada,
                Lineas = lineas,
                Motivo = motivo,
                Comentario = comentario,
                IncluyeEnvio = incluyeEnvio,
                ValorProductos = lineas.Sum(l => l.Subtotal),
                ValorEnvio = incluyeEnvio ? resumen.CostoEnvio : 0
            };

            devolucion.ValorReembolso = devolucion.ValorProductos + devolucion.ValorEnvio;
            AgregarHistorial(devolucion.Historial, Devolucion.EstadoSolicitada, $"Motivo: {motivo}");

            _posventaRepo.AgregarDevolucion(devolucion);

            Notificar(idUsuario,
                $"Recibimos tu solicitud de devolución {devolucion.Numero}",
                $"Registramos tu solicitud de devolución del pedido <b>#{pedido.IdPedido}</b> " +
                $"por <b>{Moneda(devolucion.ValorReembolso)}</b>. La revisaremos y te avisaremos por este medio.");

            return (devolucion, "Devolución solicitada");
        }

        /* =========================
           HU-21 GESTIONAR ESTADO DE LA DEVOLUCIÓN
        ========================= */

        public (List<Devolucion>? Devoluciones, string Mensaje) ObtenerDevoluciones(int idAdministrador)
        {
            if (!EsAdministrador(idAdministrador))
                return (null, "Solo un administrador puede ver las devoluciones");

            return (_posventaRepo.ObtenerDevoluciones().OrderByDescending(d => d.Fecha).ToList(), "ok");
        }

        public (Devolucion? Devolucion, string Mensaje) CambiarEstadoDevolucion(
            int idDevolucion, int idAdministrador, string accion, string? motivo, string? referencia)
        {
            if (!EsAdministrador(idAdministrador))
                return (null, "Solo un administrador puede gestionar devoluciones");

            var devolucion = _posventaRepo.ObtenerDevolucionPorId(idDevolucion);

            if (devolucion == null)
                return (null, "Devolución no encontrada");

            string estado = devolucion.Estado;
            string asunto;
            string mensajeCliente;

            switch ((accion ?? "").ToLowerInvariant())
            {
                case "aprobar":
                    if (estado != Devolucion.EstadoSolicitada)
                        return (null, "Solo se pueden aprobar devoluciones solicitadas");

                    devolucion.Estado = Devolucion.EstadoAprobada;
                    AgregarHistorial(devolucion.Historial, devolucion.Estado, "");
                    _posventaRepo.ActualizarDevolucion(devolucion);

                    asunto = $"Aprobamos tu devolución {devolucion.Numero}";
                    mensajeCliente = "Tu devolución fue aprobada. Envía los productos en buen estado y con sus accesorios " +
                                     "a nuestra dirección; el costo del envío de regreso corre por tu cuenta. " +
                                     "Cuando los recibamos haremos el reembolso.";
                    break;

                case "rechazar":
                    if (estado != Devolucion.EstadoSolicitada)
                        return (null, "Solo se pueden rechazar devoluciones solicitadas");

                    string motivoRechazo = (motivo ?? "").Trim();
                    if (motivoRechazo.Length < 5 || motivoRechazo.Length > 300)
                        return (null, "Escribe el motivo del rechazo (entre 5 y 300 caracteres)");

                    devolucion.Estado = Devolucion.EstadoRechazada;
                    devolucion.MotivoRechazo = motivoRechazo;
                    AgregarHistorial(devolucion.Historial, devolucion.Estado, motivoRechazo);
                    _posventaRepo.ActualizarDevolucion(devolucion);

                    asunto = $"Tu devolución {devolucion.Numero} fue rechazada";
                    mensajeCliente = $"No pudimos aprobar tu devolución. Motivo: {motivoRechazo}";
                    break;

                case "recibir":
                    if (estado != Devolucion.EstadoAprobada)
                        return (null, "Solo se pueden recibir devoluciones aprobadas");

                    devolucion.Estado = Devolucion.EstadoProductoRecibido;
                    AgregarHistorial(devolucion.Historial, devolucion.Estado, "Las unidades volvieron al stock");

                    // El producto en buen estado vuelve al stock (misma escritura)
                    _posventaRepo.RecibirDevolucion(devolucion);

                    asunto = $"Recibimos los productos de tu devolución {devolucion.Numero}";
                    mensajeCliente = "Recibimos los productos que devolviste. Pronto haremos el reembolso.";
                    break;

                case "reembolsar":
                    if (estado != Devolucion.EstadoProductoRecibido)
                        return (null, "Solo se pueden reembolsar devoluciones con el producto recibido");

                    string referenciaReembolso = (referencia ?? "").Trim();
                    if (referenciaReembolso.Length < 3 || referenciaReembolso.Length > 60)
                        return (null, "Escribe la referencia del reembolso (entre 3 y 60 caracteres)");

                    devolucion.Estado = Devolucion.EstadoReembolsada;
                    devolucion.ReferenciaReembolso = referenciaReembolso;
                    devolucion.FechaReembolso = DateTime.Now;
                    AgregarHistorial(devolucion.Historial, devolucion.Estado, $"Referencia: {referenciaReembolso}");
                    _posventaRepo.ActualizarDevolucion(devolucion);

                    asunto = $"Reembolsamos tu devolución {devolucion.Numero}";
                    mensajeCliente = $"Hicimos el reembolso de <b>{Moneda(devolucion.ValorReembolso)}</b> al medio de pago " +
                                     $"de tu compra. Referencia: <b>{referenciaReembolso}</b>.";
                    break;

                default:
                    return (null, "Acción no válida");
            }

            Notificar(devolucion.IdUsuario, asunto, mensajeCliente);

            return (devolucion, "Devolución actualizada");
        }

        /* =========================
           HU-22 REPORTAR PRODUCTO POR GARANTÍA
        ========================= */

        public (TicketGarantia? Ticket, string Mensaje) ReportarGarantia(
            int idUsuario, int idPedido, int idProducto, int cantidad, string descripcion, List<ArchivoEvidencia> fotos)
        {
            var (resumen, mensaje) = ObtenerResumenPedido(idPedido, idUsuario);

            if (resumen == null)
                return (null, mensaje);

            if (!resumen.PuedeReportarGarantia)
                return (null, resumen.MensajeGarantia);

            var linea = resumen.Lineas.FirstOrDefault(l => l.IdProducto == idProducto);

            if (linea == null)
                return (null, "Selecciona un producto de este pedido.");

            if (cantidad < 1 || cantidad > linea.Disponible)
                return (null, $"De \"{linea.Nombre}\" puedes reportar entre 1 y {linea.Disponible} unidad(es).");

            string falla = (descripcion ?? "").Trim();
            if (falla.Length < 10 || falla.Length > 1000)
                return (null, "Describe la falla del producto (entre 10 y 1000 caracteres).");

            fotos ??= new List<ArchivoEvidencia>();

            if (fotos.Count > TicketGarantia.MaximoEvidencias)
                return (null, $"Puedes adjuntar máximo {TicketGarantia.MaximoEvidencias} fotos.");

            foreach (var foto in fotos)
            {
                if (foto.Contenido.Length == 0 || foto.Contenido.Length > TicketGarantia.TamanoMaximoEvidencia)
                    return (null, $"La foto \"{foto.NombreOriginal}\" debe pesar máximo 5 MB.");

                if (ExtensionDeImagen(foto.Contenido) == null)
                    return (null, $"La foto \"{foto.NombreOriginal}\" debe ser JPG o PNG.");
            }

            var pedido = _pedidoRepo.ObtenerPedidoPorId(idPedido)!;
            var cliente = pedido.Facturacion ?? pedido.Envio ?? new DatosEnvio();

            var ticket = new TicketGarantia
            {
                IdPedido = idPedido,
                IdUsuario = idUsuario,
                NombreCliente = $"{cliente.Nombre} {cliente.Apellidos}".Trim(),
                DocumentoCliente = $"{DatosEnvio.Abreviatura(cliente.TipoDocumento)} {cliente.Cedula}",
                Fecha = DateTime.Now,
                Estado = TicketGarantia.EstadoRecibido,
                DescripcionFalla = falla,
                Linea = new LineaPosventa
                {
                    IdProducto = linea.IdProducto,
                    Nombre = linea.Nombre,
                    Imagen = linea.Imagen,
                    PrecioUnitario = linea.PrecioUnitario,
                    Cantidad = cantidad,
                    Subtotal = linea.PrecioUnitario * cantidad
                }
            };

            AgregarHistorial(ticket.Historial, TicketGarantia.EstadoRecibido, "");

            // El ticket y sus fotos se guardan juntos en la base de datos; las fotos
            // se nombran con el número del ticket (GT-00001-1.jpg) para verlas desde cualquier PC
            var evidencias = fotos
                .Select(f => (ExtensionDeImagen(f.Contenido)!, f.Contenido))
                .ToList();

            _posventaRepo.AgregarGarantia(ticket, evidencias);

            Notificar(idUsuario,
                $"Recibimos tu reporte de garantía {ticket.Numero}",
                $"Registramos el reporte de garantía de <b>{ticket.Linea.Nombre}</b> del pedido <b>#{idPedido}</b>. " +
                "Lo revisaremos y te avisaremos por este medio.");

            return (ticket, "Garantía reportada");
        }

        /* =========================
           HU-23 REVISIÓN Y CONTROL DEL TICKET DE GARANTÍA
        ========================= */

        public (List<TicketGarantia>? Garantias, string Mensaje) ObtenerGarantias(int idAdministrador)
        {
            if (!EsAdministrador(idAdministrador))
                return (null, "Solo un administrador puede ver las garantías");

            return (_posventaRepo.ObtenerGarantias().OrderByDescending(g => g.Fecha).ToList(), "ok");
        }

        public (TicketGarantia? Ticket, string Mensaje) CambiarEstadoGarantia(
            int idTicket, int idAdministrador, string accion, string? motivo, string? cubiertoPor, int? idProductoReemplazo)
        {
            if (!EsAdministrador(idAdministrador))
                return (null, "Solo un administrador puede gestionar garantías");

            var ticket = _posventaRepo.ObtenerGarantiaPorId(idTicket);

            if (ticket == null)
                return (null, "Ticket de garantía no encontrado");

            string estado = ticket.Estado;
            bool enRevisionOPendiente = estado == TicketGarantia.EstadoRecibido || estado == TicketGarantia.EstadoEnRevision;
            string asunto;
            string mensajeCliente;

            switch ((accion ?? "").ToLowerInvariant())
            {
                case "revisar":
                    if (estado != TicketGarantia.EstadoRecibido)
                        return (null, "Solo se pueden poner en revisión los tickets recibidos");

                    ticket.Estado = TicketGarantia.EstadoEnRevision;
                    AgregarHistorial(ticket.Historial, ticket.Estado, "");
                    _posventaRepo.ActualizarGarantia(ticket);

                    asunto = $"Estamos revisando tu garantía {ticket.Numero}";
                    mensajeCliente = "Nuestro equipo está revisando tu reporte de garantía.";
                    break;

                case "aprobar":
                    if (!enRevisionOPendiente)
                        return (null, "Solo se pueden aprobar tickets recibidos o en revisión");

                    string cubre = (cubiertoPor ?? "").Trim();
                    if (cubre != TicketGarantia.CubreTienda && cubre != TicketGarantia.CubreFabricante)
                        return (null, "Indica si el daño lo cubre la tienda o el fabricante");

                    ticket.Estado = TicketGarantia.EstadoAprobado;
                    ticket.CubiertoPor = cubre;
                    AgregarHistorial(ticket.Historial, ticket.Estado, $"Cubre: {cubre}");
                    _posventaRepo.ActualizarGarantia(ticket);

                    asunto = $"Aprobamos tu garantía {ticket.Numero}";
                    mensajeCliente = "Tu garantía fue aprobada. Envíanos el producto defectuoso a nuestra dirección " +
                                     "(el envío corre por nuestra cuenta) y te mandaremos uno nuevo sin costo.";
                    break;

                case "rechazar":
                    if (!enRevisionOPendiente)
                        return (null, "Solo se pueden rechazar tickets recibidos o en revisión");

                    string motivoRechazo = (motivo ?? "").Trim();
                    if (motivoRechazo.Length < 5 || motivoRechazo.Length > 300)
                        return (null, "Escribe el motivo del rechazo (entre 5 y 300 caracteres)");

                    ticket.Estado = TicketGarantia.EstadoRechazado;
                    ticket.MotivoRechazo = motivoRechazo;
                    AgregarHistorial(ticket.Historial, ticket.Estado, motivoRechazo);
                    _posventaRepo.ActualizarGarantia(ticket);

                    asunto = $"Tu garantía {ticket.Numero} fue rechazada";
                    mensajeCliente = $"No pudimos aprobar tu garantía. Motivo: {motivoRechazo}";
                    break;

                case "recibir":
                    if (estado != TicketGarantia.EstadoAprobado)
                        return (null, "Solo se puede recibir el producto de tickets aprobados");

                    // La unidad defectuosa NO vuelve al stock: no se puede vender
                    ticket.Estado = TicketGarantia.EstadoProductoRecibido;
                    AgregarHistorial(ticket.Historial, ticket.Estado, "Unidad defectuosa fuera de inventario");
                    _posventaRepo.ActualizarGarantia(ticket);

                    asunto = $"Recibimos el producto de tu garantía {ticket.Numero}";
                    mensajeCliente = "Recibimos el producto defectuoso. Pronto te enviaremos el producto nuevo.";
                    break;

                case "enviar-cambio":
                    if (estado != TicketGarantia.EstadoProductoRecibido)
                        return (null, "Primero marca el producto defectuoso como recibido");

                    var (reposicion, error) = CrearReposicion(ticket, idProductoReemplazo ?? ticket.Linea.IdProducto);

                    if (reposicion == null)
                        return (null, error);

                    asunto = $"Enviaremos el cambio de tu garantía {ticket.Numero}";
                    mensajeCliente = $"Creamos el pedido <b>#{reposicion.IdPedido}</b> para enviarte " +
                                     $"<b>{ticket.ProductoReposicion}</b> sin costo. Te avisaremos cuando salga con la transportadora.";
                    break;

                default:
                    return (null, "Acción no válida");
            }

            Notificar(ticket.IdUsuario, asunto, mensajeCliente);

            return (ticket, "Garantía actualizada");
        }

        // El cambio es un pedido nuevo por $0 que sigue el flujo normal de envío
        private (Pedido? Reposicion, string Mensaje) CrearReposicion(TicketGarantia ticket, int idProductoReemplazo)
        {
            var original = _pedidoRepo.ObtenerPedidoPorId(ticket.IdPedido);

            if (original == null)
                return (null, "No se encontró el pedido original");

            var reemplazo = _productoRepo.ObtenerProductoPorId(idProductoReemplazo);

            if (reemplazo == null)
                return (null, "El producto de reemplazo no existe");

            // Si se cambia por otro producto, debe valer lo mismo o menos que el original
            if (idProductoReemplazo != ticket.Linea.IdProducto)
            {
                decimal precioOriginal = ticket.Linea.PrecioUnitario > 0
                    ? ticket.Linea.PrecioUnitario
                    : _productoRepo.ObtenerProductoPorId(ticket.Linea.IdProducto)?.Precio ?? 0;

                if (reemplazo.Precio > precioOriginal)
                    return (null, $"El producto de reemplazo debe costar máximo {Moneda(precioOriginal)}");
            }

            if (reemplazo.Stock < ticket.Linea.Cantidad)
                return (null, $"No hay stock suficiente de \"{reemplazo.Nombre}\". Elige otro producto.");

            var reposicion = new Pedido
            {
                IdUsuario = ticket.IdUsuario,
                Fecha = DateTime.Now,
                EstadoPedido = Pedido.EstadoPagado,
                TipoPedido = Pedido.TipoReposicion,
                IdPedidoOriginal = original.IdPedido,
                NumeroGarantia = ticket.Numero,
                Envio = original.Envio,
                Facturacion = original.Facturacion,
                MetodoEnvio = "Estándar",
                MetodoPago = "Garantía",
                MedioPago = "Garantía",
                FechaPago = DateTime.Now,
                Detalles = new List<DetallePedido>
                {
                    new DetallePedido
                    {
                        IdProducto = reemplazo.IdProducto,
                        Nombre = reemplazo.Nombre,
                        Imagen = reemplazo.Imagen,
                        PrecioUnitario = 0,
                        Cantidad = ticket.Linea.Cantidad,
                        Subtotal = 0
                    }
                }
            };

            ticket.Estado = TicketGarantia.EstadoCambioEnviado;
            ticket.ProductoReposicion = reemplazo.Nombre;

            // Crea el pedido, descuenta el stock y guarda el ticket en una sola escritura
            var creado = _posventaRepo.CrearReposicion(ticket, reposicion, reemplazo.IdProducto);

            if (creado == null)
                return (null, $"No hay stock suficiente de \"{reemplazo.Nombre}\". Elige otro producto.");

            AgregarHistorial(ticket.Historial, ticket.Estado, $"Pedido de reposición #{creado.IdPedido}: {reemplazo.Nombre}");
            _posventaRepo.ActualizarGarantia(ticket);

            return (creado, "ok");
        }

        // El administrador o el dueño del ticket pueden ver sus fotos
        public (byte[]? Contenido, string TipoContenido, string Mensaje) ObtenerEvidencia(int idTicket, int idUsuario, string archivo)
        {
            var ticket = _posventaRepo.ObtenerGarantiaPorId(idTicket);

            if (ticket == null || (ticket.IdUsuario != idUsuario && !EsAdministrador(idUsuario)))
                return (null, "", "Ticket no encontrado");

            // Solo se entregan archivos registrados en el ticket (nunca rutas libres)
            if (!ticket.Evidencias.Contains(archivo))
                return (null, "", "Foto no encontrada");

            var (contenido, tipo) = _posventaRepo.ObtenerEvidencia(idTicket, archivo);

            if (contenido == null)
                return (null, "", "Foto no encontrada");

            return (contenido, tipo, "ok");
        }

        /* =========================
           AYUDAS
        ========================= */

        // Unidades de un producto que ya están en una devolución o garantía no rechazada
        private static int UnidadesOcupadas(int idProducto, List<Devolucion> devoluciones, List<TicketGarantia> garantias)
        {
            int enDevoluciones = devoluciones
                .Where(d => d.OcupaUnidades())
                .Sum(d => d.Lineas.Where(l => l.IdProducto == idProducto).Sum(l => l.Cantidad));

            int enGarantias = garantias
                .Where(g => g.OcupaUnidades() && g.Linea.IdProducto == idProducto)
                .Sum(g => g.Linea.Cantidad);

            return enDevoluciones + enGarantias;
        }

        // Los pedidos entregados antes de guardar la fecha de entrega usan la fecha más cercana
        private static DateTime FechaEntregaDe(Pedido pedido)
        {
            return pedido.FechaEntrega ?? pedido.FechaEnvio ?? pedido.FechaPago ?? pedido.Fecha;
        }

        // Suma días hábiles (lunes a viernes)
        private static DateTime SumarDiasHabiles(DateTime fecha, int dias)
        {
            DateTime resultado = fecha.Date;
            int sumados = 0;

            while (sumados < dias)
            {
                resultado = resultado.AddDays(1);

                if (resultado.DayOfWeek != DayOfWeek.Saturday && resultado.DayOfWeek != DayOfWeek.Sunday)
                    sumados++;
            }

            return resultado;
        }

        private static DateTime FinDelDia(DateTime fecha)
        {
            return fecha.Date.AddDays(1).AddTicks(-1);
        }

        // Revisa los primeros bytes del archivo: así no basta con cambiarle el nombre
        private static string? ExtensionDeImagen(byte[] contenido)
        {
            if (contenido.Length >= 8 &&
                contenido[0] == 0x89 && contenido[1] == 0x50 && contenido[2] == 0x4E && contenido[3] == 0x47)
                return ".png";

            if (contenido.Length >= 3 &&
                contenido[0] == 0xFF && contenido[1] == 0xD8 && contenido[2] == 0xFF)
                return ".jpg";

            return null;
        }

        private static void AgregarHistorial(List<HistorialEstado> historial, string estado, string nota)
        {
            historial.Add(new HistorialEstado { Estado = estado, Fecha = DateTime.Now, Nota = nota });
        }

        private bool EsAdministrador(int idUsuario)
        {
            // El tipo de cada cuenta se recuerda en memoria: casi nunca va a la base de datos
            return _usuarioRepo.EsAdministrador(idUsuario);
        }

        private static string Moneda(decimal valor)
        {
            return "$ " + valor.ToString("N0", Colombia);
        }

        private static string Fecha(DateTime fecha)
        {
            return fecha.ToString("d 'de' MMMM 'de' yyyy", Colombia);
        }

        // Correo al cliente con cada cambio. Se envía en segundo plano;
        // si falla, el cambio igual queda guardado.
        private void Notificar(int idUsuario, string asunto, string mensajeHtml)
        {
            // Buscar el correo, armar el mensaje y enviarlo: todo en segundo plano
            _cola.Encolar(asunto, servicios =>
            {
                var cliente = new UsuarioRepository().ObtenerUsuarioPorId(idUsuario);

                if (cliente == null || string.IsNullOrWhiteSpace(cliente.Correo))
                    return;

                string cuerpo = $@"
                    <div style='font-family:Arial,sans-serif;max-width:520px;margin:auto;background:#000;color:#fff;padding:32px;border-radius:12px'>
                        <h2 style='margin:0 0 6px;letter-spacing:2px'>SARAITEL</h2>
                        <p style='color:#a3a3a3;margin:0 0 24px'>Hola, {cliente.Nombre}</p>
                        <h3 style='margin:0 0 12px'>{asunto}</h3>
                        <p style='line-height:1.6'>{mensajeHtml}</p>
                        <p style='color:#6b6b6b;font-size:12px;margin-top:28px'>
                            Puedes ver el estado de tu solicitud en Saraitel, en Mis pedidos → detalle del pedido.
                        </p>
                    </div>";

                servicios.GetRequiredService<IEmailService>().EnviarCorreo(cliente.Correo, $"{asunto} - Saraitel", cuerpo);
            });
        }
    }
}
