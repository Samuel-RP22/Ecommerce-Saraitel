using System.Globalization;
using Saraitel.API.Interfaces;
using Saraitel.API.Models;
using Saraitel.Datos.Repositories;
using Saraitel.Entidades;

namespace Saraitel.API.Services
{
    public class PedidoService : IPedidoService
    {
        private readonly PedidoRepository _pedidoRepo;
        private readonly UsuarioRepository _usuarioRepo;
        private readonly ProductoRepository _productoRepo;
        private readonly IWompiService _wompi;
        private readonly IFacturaService _factura;
        private readonly IColaTareas _cola;

        private const string MotivoVencido = "Se agotó el tiempo para pagar";

        public PedidoService(IWompiService wompi, IFacturaService factura, IColaTareas cola)
        {
            _pedidoRepo = new PedidoRepository();
            _usuarioRepo = new UsuarioRepository();
            _productoRepo = new ProductoRepository();
            _wompi = wompi;
            _factura = factura;
            _cola = cola;
        }

        public List<Pedido> ObtenerPedidos()
        {
            return _pedidoRepo.ObtenerPedidos();
        }

        // HU-18: pedidos de un cliente, del más nuevo al más viejo
        public List<Pedido> ObtenerPedidosDeUsuario(int idUsuario)
        {
            return _pedidoRepo.ObtenerPedidosDeUsuario(idUsuario)
                .OrderByDescending(p => p.Fecha)
                .ToList();
        }

        public Pedido? ObtenerPorId(int id)
        {
            return _pedidoRepo.ObtenerPedidoPorId(id);
        }

        public string AgregarPedido(Pedido pedido)
        {
            _pedidoRepo.AgregarPedido(pedido);
            return "Pedido creado";
        }


        public string CrearPedidoDesdeCarrito(int idUsuario)
        {
            var usuario = _usuarioRepo.ObtenerUsuarioPorId(idUsuario) as Cliente;

            if (usuario == null)
                return "Usuario no encontrado";

            if (usuario.Carrito.ListaProductos.Count == 0)
                return "El carrito está vacío";

            // HU-13: unidades pedidas de cada producto (idProducto -> cantidad)
            var cantidades = usuario.Carrito.ListaProductos
                .GroupBy(p => p.IdProducto)
                .ToDictionary(g => g.Key, g => g.Count());

            // Se valida el stock ANTES de crear el pedido
            foreach (var item in cantidades)
            {
                var productoActual = _productoRepo.ObtenerProductoPorId(item.Key);

                if (productoActual == null)
                {
                    string nombre = usuario.Carrito.ListaProductos
                        .First(p => p.IdProducto == item.Key).Nombre;

                    return $"El producto \"{nombre}\" ya no está disponible";
                }

                if (productoActual.Stock < item.Value)
                    return $"Stock insuficiente para \"{productoActual.Nombre}\". " +
                           $"Disponible: {productoActual.Stock}, en tu carrito: {item.Value}";
            }

            // El id lo asigna la base de datos al guardar
            Pedido pedido = new Pedido
            {
                IdUsuario = usuario.IdUsuario,
                Productos = usuario.Carrito.ListaProductos.ToList(),
                Detalles = usuario.Carrito.ListaProductos
                    .GroupBy(p => p.IdProducto)
                    .Select(g => new DetallePedido
                    {
                        IdProducto = g.Key,
                        Nombre = g.First().Nombre,
                        Imagen = g.First().Imagen,
                        PrecioUnitario = g.First().Precio,
                        Cantidad = g.Count(),
                        Subtotal = g.Sum(p => p.Precio)
                    })
                    .ToList(),

                TotalPedido = usuario.Carrito.ListaProductos.Sum(p => p.Precio),

                EstadoPedido = "Pagado",
                Fecha = DateTime.Now
            };

            // HU-13: guarda el pedido, descuenta el stock y vacía el carrito (todo o nada)
            if (!_pedidoRepo.RegistrarPedido(pedido, cantidades))
                return "Otro cliente acaba de comprar unidades de un producto de tu carrito. Revisa el stock e intenta de nuevo.";

            return $"Compra realizada correctamente. Pedido #{pedido.IdPedido}";
        }

        /* =========================
           HU-15 CONFIRMACIÓN DE PRODUCTOS (CHECKOUT)
        ========================= */

        public (Pedido? Pedido, string Mensaje) CrearPedidoCheckout(
            int idUsuario,
            SolicitudCheckout solicitud)
        {
            var usuario = _usuarioRepo.ObtenerUsuarioPorId(idUsuario) as Cliente;

            if (usuario == null)
                return (null, "Usuario no encontrado");

            if (usuario.Carrito.ListaProductos.Count == 0)
                return (null, "El carrito está vacío");

            if (solicitud?.Envio == null)
                return (null, "Completa los datos de envío");

            // Datos de entrega
            var envio = LimpiarDatos(solicitud.Envio);

            string? error = ValidarDatos(envio, "de envío");
            if (error != null)
                return (null, error);

            // Datos de facturación: los mismos de envío o unos distintos
            DatosEnvio facturacion;

            if (solicitud.FacturacionIgualEnvio)
            {
                facturacion = envio;
            }
            else
            {
                if (solicitud.Facturacion == null)
                    return (null, "Completa la dirección de facturación");

                facturacion = LimpiarDatos(solicitud.Facturacion);

                error = ValidarDatos(facturacion, "de facturación");
                if (error != null)
                    return (null, error);
            }

            // Unidades de cada producto, en el mismo orden del carrito
            var lineas = usuario.Carrito.ListaProductos
                .GroupBy(p => p.IdProducto)
                .Select(g => new { IdProducto = g.Key, Cantidad = g.Count() })
                .ToList();

            // Se valida el stock y se congelan nombre, imagen y precio actuales
            var detalles = new List<DetallePedido>();

            foreach (var linea in lineas)
            {
                // El carrito ya viene de la base de datos con el precio y el stock
                // actuales de cada producto: no hace falta consultarlos otra vez
                var producto = usuario.Carrito.ListaProductos
                    .FirstOrDefault(p => p.IdProducto == linea.IdProducto);

                if (producto == null)
                {
                    string nombre = usuario.Carrito.ListaProductos
                        .First(p => p.IdProducto == linea.IdProducto).Nombre;

                    return (null, $"El producto \"{nombre}\" ya no está disponible. Quítalo del carrito para continuar.");
                }

                if (producto.Stock < linea.Cantidad)
                {
                    return (null, $"Stock insuficiente para \"{producto.Nombre}\". " +
                                  $"Disponible: {producto.Stock}, en tu carrito: {linea.Cantidad}.");
                }

                detalles.Add(new DetallePedido
                {
                    IdProducto = producto.IdProducto,
                    Nombre = producto.Nombre,
                    Imagen = producto.Imagen,
                    PrecioUnitario = producto.Precio,
                    Cantidad = linea.Cantidad,
                    Subtotal = producto.Precio * linea.Cantidad
                });
            }

            decimal subtotal = detalles.Sum(d => d.Subtotal);

            // El id lo asigna la base de datos al guardar
            Pedido pedido = new Pedido
            {
                IdUsuario = usuario.IdUsuario,
                Fecha = DateTime.Now,
                EstadoPedido = Pedido.EstadoPendientePago,
                FechaLimitePago = DateTime.Now.AddMinutes(Pedido.MinutosParaPagar),
                Detalles = detalles,
                Envio = envio,
                Facturacion = facturacion,
                MetodoEnvio = "Estándar",
                MetodoPago = "Wompi",
                Subtotal = subtotal,
                CostoEnvio = Pedido.CostoEnvioEstandar,
                Impuestos = CalcularIvaIncluido(subtotal),
                TotalPedido = subtotal + Pedido.CostoEnvioEstandar
            };

            // Se guarda el pedido, el stock queda reservado mientras se paga y el
            // carrito se vacía, todo en una sola transacción. Si otro cliente compró
            // las últimas unidades un instante antes, no se guarda nada.
            bool registrado = _pedidoRepo.RegistrarPedido(
                pedido, lineas.ToDictionary(l => l.IdProducto, l => l.Cantidad));

            if (!registrado)
                return (null, "Otro cliente acaba de comprar unidades de un producto de tu carrito. " +
                              "Revisa el stock e intenta de nuevo.");

            return (pedido, $"Pedido #{pedido.IdPedido} creado");
        }

        /* =========================
           HU-16 SELECCIÓN DE PAGO (WOMPI)
        ========================= */

        // Prepara un intento de pago para el widget de Wompi
        public (DatosPagoWompi? Datos, string Mensaje) IniciarPago(int idPedido, int idUsuario)
        {
            var pedido = _pedidoRepo.ObtenerPedidoPorId(idPedido);

            if (pedido == null || pedido.IdUsuario != idUsuario)
                return (null, "Pedido no encontrado");

            if (pedido.EstaPagado())
                return (null, "Este pedido ya está pagado");

            if (pedido.EstadoPedido != Pedido.EstadoPendientePago)
                return (null, "Este pedido fue cancelado y ya no se puede pagar");

            DateTime fechaLimite = FechaLimiteDe(pedido);

            if (DateTime.Now >= fechaLimite)
            {
                _pedidoRepo.CancelarPedido(pedido, MotivoVencido);

                return (null, "Se agotó el tiempo para pagar. El pedido fue cancelado y " +
                              "los productos volvieron a tu carrito.");
            }

            // Cada intento usa una referencia nueva: Wompi no permite repetirlas
            string referencia = $"SRT-{pedido.IdPedido}-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            long montoEnCentavos = MontoEnCentavos(pedido);

            // Wompi no acepta pagar esta referencia después de la hora límite
            string fechaLimiteUtc = fechaLimite.ToUniversalTime()
                .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

            string firma;

            try
            {
                firma = _wompi.GenerarFirma(referencia, montoEnCentavos, "COP", fechaLimiteUtc);
            }
            catch (InvalidOperationException ex)
            {
                return (null, ex.Message);
            }

            pedido.ReferenciaPago = referencia;
            _pedidoRepo.ActualizarPedido(pedido);

            var datos = new DatosPagoWompi
            {
                IdPedido = pedido.IdPedido,
                LlavePublica = _wompi.LlavePublica,
                Referencia = referencia,
                MontoEnCentavos = montoEnCentavos,
                Moneda = "COP",
                Firma = firma,
                FechaLimite = fechaLimiteUtc
            };

            return (datos, "Pago listo");
        }

        // Consulta en Wompi la transacción y, si fue aprobada, marca el pedido como pagado.
        // El navegador nunca decide si se pagó: siempre se le pregunta a Wompi.
        public async Task<ResultadoPago> ConfirmarPagoAsync(int idPedido, int idUsuario, string idTransaccion)
        {
            var pedido = _pedidoRepo.ObtenerPedidoPorId(idPedido);

            if (pedido == null || pedido.IdUsuario != idUsuario)
                return Resultado(ResultadoPago.Error, "Pedido no encontrado");

            // Si ya se confirmó antes (por ejemplo, con un doble clic), no se repite
            if (pedido.EstaPagado())
                return Resultado(ResultadoPago.Aprobado, "Este pedido ya está pagado", pedido);

            if (string.IsNullOrWhiteSpace(idTransaccion))
                return Resultado(ResultadoPago.Error, "Falta el id de la transacción");

            var transaccion = await _wompi.ConsultarTransaccionAsync(idTransaccion);

            if (transaccion == null)
                return Resultado(ResultadoPago.Error,
                    "No se pudo consultar el pago en Wompi. Intenta de nuevo en unos segundos.");

            // La transacción debe ser de este pedido y por el valor exacto
            bool esDeEstePedido = transaccion.Reference.StartsWith($"SRT-{pedido.IdPedido}-")
                && transaccion.AmountInCents == MontoEnCentavos(pedido)
                && transaccion.Currency == "COP";

            if (!esDeEstePedido)
                return Resultado(ResultadoPago.Error, "La transacción no corresponde a este pedido");

            switch (transaccion.Status)
            {
                case "APPROVED":
                    if (pedido.EstadoPedido == Pedido.EstadoCancelado)
                        return Resultado(ResultadoPago.Error,
                            "El pago fue aprobado, pero el pedido ya estaba cancelado. " +
                            "Comunícate con nosotros para la devolución del dinero.", pedido);

                    pedido.EstadoPedido = Pedido.EstadoPagado;
                    pedido.IdTransaccion = transaccion.Id;
                    pedido.ReferenciaPago = transaccion.Reference;
                    pedido.MedioPago = TraducirMedioPago(transaccion.PaymentMethodType);
                    pedido.FechaPago = DateTime.Now;

                    // HU-17: con el pago aprobado nace la factura (FV-00001, ...)
                    _pedidoRepo.AsignarFactura(pedido);

                    var aprobado = Resultado(ResultadoPago.Aprobado, "¡Pago aprobado!", pedido);
                    aprobado.CorreoFactura = EnviarFacturaPorCorreo(pedido);

                    return aprobado;

                case "PENDING":
                    return Resultado(ResultadoPago.Pendiente,
                        "Tu pago está en proceso. Esto puede tardar unos segundos.", pedido);

                case "DECLINED":
                    return Resultado(ResultadoPago.Rechazado,
                        "El pago fue rechazado por la entidad financiera.", pedido);

                case "VOIDED":
                    return Resultado(ResultadoPago.Rechazado, "El pago fue anulado.", pedido);

                default:
                    return Resultado(ResultadoPago.Rechazado,
                        "Hubo un error al procesar el pago.", pedido);
            }
        }

        // El cliente decide no pagar: se cancela y el stock vuelve al catálogo
        public string CancelarPedido(int idPedido, int idUsuario)
        {
            var pedido = _pedidoRepo.ObtenerPedidoPorId(idPedido);

            if (pedido == null || pedido.IdUsuario != idUsuario)
                return "Pedido no encontrado";

            if (pedido.EstadoPedido != Pedido.EstadoPendientePago)
                return "Solo se pueden cancelar pedidos pendientes de pago";

            string motivo = DateTime.Now >= FechaLimiteDe(pedido)
                ? MotivoVencido
                : "Cancelado por el cliente";

            _pedidoRepo.CancelarPedido(pedido, motivo);

            return "Pedido cancelado";
        }

        // Lo llama el servicio en segundo plano cada minuto.
        // Devuelve cuántos pedidos se cancelaron.
        public int CancelarPedidosVencidos()
        {
            // La base de datos devuelve solo los pendientes cuyo tiempo ya pasó
            var vencidos = _pedidoRepo.ObtenerPedidosVencidos(DateTime.Now);

            int cancelados = 0;

            foreach (var pedido in vencidos)
            {
                if (_pedidoRepo.CancelarPedido(pedido, MotivoVencido))
                    cancelados++;
            }

            return cancelados;
        }

        /* =========================
           HU-17 FACTURACIÓN
        ========================= */

        // Devuelve el PDF de la factura de un pedido pagado
        public (byte[]? Pdf, string NombreArchivo, string Mensaje) ObtenerFacturaPdf(int idPedido, int idUsuario)
        {
            var pedido = _pedidoRepo.ObtenerPedidoPorId(idPedido);

            // El cliente descarga sus facturas; el administrador, las de todos
            if (pedido == null || (pedido.IdUsuario != idUsuario && !EsAdministrador(idUsuario)))
                return (null, "", "Pedido no encontrado");

            if (!pedido.EstaPagado())
                return (null, "", "Solo los pedidos pagados tienen factura");

            if (pedido.EsReposicion())
                return (null, "", "Los pedidos de reposición por garantía no tienen factura");

            // Los pedidos de antes del checkout (HU-15) no guardan el detalle necesario
            if (pedido.Detalles.Count == 0)
                return (null, "", "Este pedido es anterior al sistema de facturación");

            // Pedidos pagados antes de existir la facturación: se les asigna número ahora
            if (string.IsNullOrEmpty(pedido.NumeroFactura))
                _pedidoRepo.AsignarFactura(pedido);

            var cliente = _usuarioRepo.ObtenerUsuarioPorId(pedido.IdUsuario);

            try
            {
                byte[] pdf = _factura.GenerarPdf(pedido, cliente?.Correo ?? "");

                return (pdf, $"Factura-{pedido.NumeroFactura}.pdf", "Factura generada");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR AL GENERAR LA FACTURA: {ex}");
                return (null, "", "No se pudo generar la factura. Revisa la consola de la API.");
            }
        }

        // Envía la factura al correo del cliente. El PDF y el correo se hacen en
        // segundo plano para que el pago se confirme de inmediato; si el correo
        // falla, el pago igual queda aprobado (solo se registra el error).
        private string EnviarFacturaPorCorreo(Pedido pedido)
        {
            var cliente = _usuarioRepo.ObtenerUsuarioPorId(pedido.IdUsuario);

            if (cliente == null || string.IsNullOrWhiteSpace(cliente.Correo))
                return "";

            string correo = cliente.Correo;
            string cuerpo = CuerpoCorreoFactura(pedido);

            _cola.Encolar($"Factura {pedido.NumeroFactura}", servicios =>
            {
                byte[] pdf = servicios.GetRequiredService<IFacturaService>().GenerarPdf(pedido, correo);

                servicios.GetRequiredService<IEmailService>().EnviarCorreoConAdjunto(
                    correo,
                    $"Tu factura {pedido.NumeroFactura} - Saraitel",
                    cuerpo,
                    $"Factura-{pedido.NumeroFactura}.pdf",
                    pdf);
            });

            return correo;
        }

        private static string CuerpoCorreoFactura(Pedido pedido)
        {
            var colombia = new CultureInfo("es-CO");
            string nombre = pedido.Envio?.Nombre ?? "";
            string total = "$ " + pedido.TotalPedido.ToString("N0", colombia);

            return $@"
                <div style='font-family:Arial,sans-serif;max-width:520px;margin:auto;background:#000;color:#fff;padding:32px;border-radius:12px'>
                    <h2 style='margin:0 0 6px;letter-spacing:2px'>SARAITEL</h2>
                    <p style='color:#a3a3a3;margin:0 0 24px'>¡Gracias por tu compra, {nombre}!</p>
                    <p>Tu pago del pedido <b>#{pedido.IdPedido}</b> por <b>{total}</b> fue aprobado.</p>
                    <p>Adjuntamos tu factura <b>{pedido.NumeroFactura}</b> en PDF.</p>
                    <p style='color:#6b6b6b;font-size:12px;margin-top:28px'>
                        Documento de prueba (Wompi Sandbox) — no válido como factura electrónica DIAN.
                    </p>
                </div>";
        }

        /* =========================
           ENVÍO MANUAL (ADMINISTRADOR)
        ========================= */

        // El administrador despacha un pedido pagado con su transportadora y número de guía
        public string MarcarEnviado(int idPedido, int idAdministrador, string transportadora, string numeroGuia)
        {
            if (!EsAdministrador(idAdministrador))
                return "Solo un administrador puede actualizar el envío";

            var pedido = _pedidoRepo.ObtenerPedidoPorId(idPedido);

            if (pedido == null)
                return "Pedido no encontrado";

            if (pedido.EstadoPedido != Pedido.EstadoPagado)
                return "Solo se pueden enviar pedidos pagados que aún no han salido";

            transportadora = (transportadora ?? "").Trim();
            numeroGuia = (numeroGuia ?? "").Trim().ToUpperInvariant();

            if (!Transportadoras.Lista.Contains(transportadora))
                return "Selecciona una transportadora de la lista";

            if (numeroGuia.Length < 4 || numeroGuia.Length > 30 ||
                !numeroGuia.All(c => char.IsLetterOrDigit(c) || c == '-'))
                return "El número de guía debe tener entre 4 y 30 letras, números o guiones";

            pedido.EstadoPedido = Pedido.EstadoEnviado;
            pedido.Transportadora = transportadora;
            pedido.NumeroGuia = numeroGuia;
            pedido.FechaEnvio = DateTime.Now;

            _pedidoRepo.ActualizarPedido(pedido);

            NotificarCliente(pedido,
                $"Tu pedido #{pedido.IdPedido} va en camino",
                $"Despachamos tu pedido con <b>{transportadora}</b>.<br>" +
                $"Número de guía: <b>{numeroGuia}</b><br><br>" +
                "Con ese número puedes rastrearlo en la página de la transportadora.");

            return "Pedido marcado como enviado";
        }

        // El administrador confirma que el cliente recibió el pedido
        public string MarcarEntregado(int idPedido, int idAdministrador)
        {
            if (!EsAdministrador(idAdministrador))
                return "Solo un administrador puede actualizar el envío";

            var pedido = _pedidoRepo.ObtenerPedidoPorId(idPedido);

            if (pedido == null)
                return "Pedido no encontrado";

            if (pedido.EstadoPedido != Pedido.EstadoEnviado)
                return "Solo se pueden marcar como entregados los pedidos enviados";

            pedido.EstadoPedido = Pedido.EstadoEntregado;
            pedido.FechaEntrega = DateTime.Now;

            _pedidoRepo.ActualizarPedido(pedido);

            NotificarCliente(pedido,
                $"Tu pedido #{pedido.IdPedido} fue entregado",
                "Tu pedido llegó a su destino. ¡Esperamos que lo disfrutes!");

            return "Pedido marcado como entregado";
        }

        // El administrador cancela un pedido que quedó pendiente de pago:
        // el stock vuelve al catálogo y los productos al carrito del cliente
        public string CancelarPedidoAdmin(int idPedido, int idAdministrador)
        {
            if (!EsAdministrador(idAdministrador))
                return "Solo un administrador puede cancelar pedidos";

            var pedido = _pedidoRepo.ObtenerPedidoPorId(idPedido);

            if (pedido == null)
                return "Pedido no encontrado";

            if (pedido.EstadoPedido != Pedido.EstadoPendientePago)
                return "Solo se pueden cancelar pedidos pendientes de pago";

            // Si justo en ese momento el cliente pagó, el pedido ya no está pendiente
            if (!_pedidoRepo.CancelarPedido(pedido, "Cancelado por el administrador"))
                return "El pedido cambió de estado. Actualiza la página.";

            NotificarCliente(pedido,
                $"Tu pedido #{pedido.IdPedido} fue cancelado",
                "Cancelamos tu pedido porque no se completó el pago. " +
                "No se hizo ningún cobro y los productos volvieron a tu carrito, " +
                "por si quieres intentarlo de nuevo.");

            return "Pedido cancelado";
        }

        /* =========================
           CONSULTA DE COMPRAS (por criterios)
        ========================= */

        public const string MensajeFechasInvalidas = "La fecha inicial no puede ser mayor a la fecha final.";

        // Compras = pedidos pagados (también enviados o entregados), que son los que tienen factura.
        // El cliente ve solo las suyas; el administrador, las de todos los clientes.
        public (List<ResumenCompra>? Compras, string Mensaje) ConsultarCompras(
            int idUsuario,
            DateTime? fechaInicial,
            DateTime? fechaFinal,
            string? numeroFactura,
            string? tipoDocumento,
            string? numeroDocumento)
        {
            var usuario = _usuarioRepo.ObtenerUsuarioPorId(idUsuario);

            if (usuario == null)
                return (null, "Usuario no encontrado");

            // Paso 4: las fechas deben ser válidas
            if (fechaInicial == null || fechaFinal == null)
                return (null, "Ingresa la fecha inicial y la fecha final.");

            if (fechaInicial.Value.Date > fechaFinal.Value.Date)
                return (null, MensajeFechasInvalidas);

            bool esAdministrador = usuario is Administrador;

            // Paso 5: compras según el rol
            // El cliente solo trae de la base de datos sus propios pedidos
            var pedidosBase = esAdministrador
                ? _pedidoRepo.ObtenerPedidos()
                : _pedidoRepo.ObtenerPedidosDeUsuario(idUsuario);

            var compras = pedidosBase
                .Where(p => p.EstaPagado() && p.Detalles.Count > 0 && !p.EsReposicion())
                .Where(p => esAdministrador || p.IdUsuario == idUsuario);

            // Fecha de la compra = cuando se aprobó el pago
            compras = compras.Where(p =>
            {
                DateTime fecha = (p.FechaPago ?? p.Fecha).Date;
                return fecha >= fechaInicial.Value.Date && fecha <= fechaFinal.Value.Date;
            });

            // Número de factura (opcional): acepta "FV-00007", "fv-7" o solo "7"
            string factura = NormalizarNumeroFactura(numeroFactura);
            if (factura != "")
                compras = compras.Where(p => p.NumeroFactura.Equals(factura, StringComparison.OrdinalIgnoreCase));

            // Documento del cliente: solo lo usa el administrador
            if (esAdministrador)
            {
                string tipo = (tipoDocumento ?? "").Trim().ToUpperInvariant();
                string numero = DatosEnvio.NormalizarNumero(tipo, numeroDocumento);

                if (tipo != "")
                    compras = compras.Where(p => DatosCliente(p).TipoDocumento == tipo);

                if (numero != "")
                    compras = compras.Where(p => DatosCliente(p).Cedula == numero);
            }

            var resultado = compras
                .OrderByDescending(p => p.FechaPago ?? p.Fecha)
                .Select(p =>
                {
                    var cliente = DatosCliente(p);

                    return new ResumenCompra
                    {
                        IdPedido = p.IdPedido,
                        NumeroFactura = p.NumeroFactura,
                        FechaCompra = p.FechaPago ?? p.Fecha,
                        // El nombre del cliente solo se envía al administrador
                        Cliente = esAdministrador ? $"{cliente.Nombre} {cliente.Apellidos}".Trim() : "",
                        TipoDocumento = esAdministrador ? cliente.TipoDocumento : "",
                        NumeroDocumento = esAdministrador ? cliente.Cedula : "",
                        TotalPedido = p.TotalPedido,
                        MedioPago = p.MedioPago,
                        EstadoPedido = p.EstadoPedido
                    };
                })
                .ToList();

            // Paso 5.1: si no hay compras, la lista va vacía y la pantalla muestra el mensaje
            return (resultado, resultado.Count == 0
                ? "No se encontraron compras con los criterios ingresados."
                : $"{resultado.Count} compras encontradas");
        }

        // El cliente de una compra es quien aparece en la factura
        private static DatosEnvio DatosCliente(Pedido pedido)
        {
            return pedido.Facturacion ?? pedido.Envio ?? new DatosEnvio();
        }

        private static string NormalizarNumeroFactura(string? texto)
        {
            string valor = (texto ?? "").Trim().ToUpperInvariant();

            if (valor == "")
                return "";

            string digitos = SoloDigitos(valor);

            // "7", "FV7" o "FV-7" -> "FV-00007"
            if (digitos != "" && int.TryParse(digitos, out int numero))
                return $"FV-{numero:D5}";

            return valor;
        }

        private bool EsAdministrador(int idUsuario)
        {
            // El tipo de cada cuenta se recuerda en memoria: casi nunca va a la base de datos
            return _usuarioRepo.EsAdministrador(idUsuario);
        }

        // Correo al cliente con cada cambio de estado. Todo (buscar su correo, armar
        // el mensaje y enviarlo) se hace en segundo plano, así el administrador no
        // espera; si falla, el cambio igual queda guardado: solo se registra el error.
        private void NotificarCliente(Pedido pedido, string asunto, string mensajeHtml)
        {
            _cola.Encolar(asunto, servicios =>
            {
                var cliente = new UsuarioRepository().ObtenerUsuarioPorId(pedido.IdUsuario);

                if (cliente == null || string.IsNullOrWhiteSpace(cliente.Correo))
                    return;

                string nombre = pedido.Envio?.Nombre ?? cliente.Nombre;

                string cuerpo = $@"
                    <div style='font-family:Arial,sans-serif;max-width:520px;margin:auto;background:#000;color:#fff;padding:32px;border-radius:12px'>
                        <h2 style='margin:0 0 6px;letter-spacing:2px'>SARAITEL</h2>
                        <p style='color:#a3a3a3;margin:0 0 24px'>Hola, {nombre}</p>
                        <h3 style='margin:0 0 12px'>{asunto}</h3>
                        <p style='line-height:1.6'>{mensajeHtml}</p>
                        <p style='color:#6b6b6b;font-size:12px;margin-top:28px'>
                            Puedes ver el estado de tu pedido en Saraitel, en el menú de tu nombre → Mis pedidos.
                        </p>
                    </div>";

                servicios.GetRequiredService<IEmailService>().EnviarCorreo(cliente.Correo, $"{asunto} - Saraitel", cuerpo);
            });
        }

        // Los pedidos creados antes de la HU-16 no tienen fecha límite guardada
        private static DateTime FechaLimiteDe(Pedido pedido)
        {
            return pedido.FechaLimitePago ?? pedido.Fecha.AddMinutes(Pedido.MinutosParaPagar);
        }

        private static long MontoEnCentavos(Pedido pedido)
        {
            return (long)Math.Round(pedido.TotalPedido * 100);
        }

        private static string TraducirMedioPago(string tipo)
        {
            return tipo switch
            {
                "CARD" => "Tarjeta",
                "PSE" => "PSE",
                "NEQUI" => "Nequi",
                "BANCOLOMBIA_TRANSFER" => "Botón Bancolombia",
                "BANCOLOMBIA_QR" => "QR Bancolombia",
                _ => tipo
            };
        }

        private static ResultadoPago Resultado(string estado, string mensaje, Pedido? pedido = null)
        {
            return new ResultadoPago { Estado = estado, Mensaje = mensaje, Pedido = pedido };
        }

        // Los precios ya incluyen IVA: se calcula cuánto de ese valor es impuesto
        private static decimal CalcularIvaIncluido(decimal valorConIva)
        {
            decimal baseSinIva = valorConIva / (1 + Pedido.TasaIva);

            return Math.Round(valorConIva - baseSinIva, 2);
        }

        // Quita espacios sobrantes y deja solo los caracteres válidos en documento y teléfono
        private static DatosEnvio LimpiarDatos(DatosEnvio datos)
        {
            string tipoDocumento = (datos.TipoDocumento ?? "CC").Trim().ToUpperInvariant();

            return new DatosEnvio
            {
                Pais = "Colombia",
                Nombre = (datos.Nombre ?? "").Trim(),
                Apellidos = (datos.Apellidos ?? "").Trim(),
                TipoDocumento = tipoDocumento,
                Cedula = DatosEnvio.NormalizarNumero(tipoDocumento, datos.Cedula),
                Direccion = (datos.Direccion ?? "").Trim(),
                Complemento = (datos.Complemento ?? "").Trim(),
                Ciudad = (datos.Ciudad ?? "").Trim(),
                Departamento = (datos.Departamento ?? "").Trim(),
                CodigoPostal = SoloDigitos(datos.CodigoPostal),
                Telefono = SoloDigitos(datos.Telefono)
            };
        }

        private static string SoloDigitos(string? texto)
        {
            return new string((texto ?? "").Where(char.IsDigit).ToArray());
        }

        // Devuelve el primer error encontrado, o null si todo está bien
        private static string? ValidarDatos(DatosEnvio datos, string tipo)
        {
            if (datos.Nombre == "")
                return $"Datos {tipo}: escribe el nombre.";

            if (datos.Apellidos == "")
                return $"Datos {tipo}: escribe los apellidos.";

            if (!DatosEnvio.TiposDocumento.Contains(datos.TipoDocumento))
                return $"Datos {tipo}: selecciona el tipo de documento.";

            string? errorDocumento = DatosEnvio.ValidarNumero(datos.TipoDocumento, datos.Cedula);
            if (errorDocumento != null)
                return $"Datos {tipo}: {errorDocumento}.";

            if (datos.Direccion == "")
                return $"Datos {tipo}: escribe la dirección.";

            if (datos.Ciudad == "")
                return $"Datos {tipo}: escribe la ciudad.";

            if (datos.Departamento == "")
                return $"Datos {tipo}: selecciona el departamento.";

            if (datos.Telefono.Length != 10)
                return $"Datos {tipo}: el teléfono debe tener 10 números.";

            return null;
        }
    }
}