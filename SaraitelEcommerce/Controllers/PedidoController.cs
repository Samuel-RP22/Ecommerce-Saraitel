using Microsoft.AspNetCore.Mvc;
using Saraitel.API.Interfaces;
using Saraitel.API.Models;
using Saraitel.Entidades;

namespace Saraitel.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PedidoController : ControllerBase
    {
        private readonly IPedidoService _service;

        public PedidoController(IPedidoService service)
        {
            _service = service;
        }

        [HttpGet]
        public IActionResult Get() => Ok(_service.ObtenerPedidos());

        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var p = _service.ObtenerPorId(id);
            if (p == null) return NotFound();
            return Ok(p);
        }

        [HttpPost]
        public IActionResult Post(Pedido pedido)
        {
            return Ok(_service.AgregarPedido(pedido));
        }

        // HU-18: historial de pedidos de un cliente (Mis pedidos)
        [HttpGet("usuario/{idUsuario}")]
        public IActionResult PedidosDeUsuario(int idUsuario)
        {
            return Ok(_service.ObtenerPedidosDeUsuario(idUsuario));
        }

        // Consulta de compras por criterios (fechas, factura y, para el administrador, documento).
        // Fechas en formato yyyy-MM-dd.
        [HttpGet("compras")]
        public IActionResult ConsultarCompras(
            int idUsuario,
            DateTime? fechaInicial,
            DateTime? fechaFinal,
            string? numeroFactura = null,
            string? tipoDocumento = null,
            string? numeroDocumento = null)
        {
            var (compras, mensaje) = _service.ConsultarCompras(
                idUsuario, fechaInicial, fechaFinal, numeroFactura, tipoDocumento, numeroDocumento);

            if (compras == null)
                return BadRequest(mensaje);

            return Ok(compras);
        }

        // HU-15: costo de envío e IVA que usa el resumen del checkout
        [HttpGet("tarifas")]
        public IActionResult Tarifas()
        {
            return Ok(new
            {
                costoEnvio = Pedido.CostoEnvioEstandar,
                tasaIva = Pedido.TasaIva
            });
        }

        // HU-15: confirma los productos del carrito y los datos de entrega.
        // Crea el pedido en estado "Pendiente de pago".
        [HttpPost("checkout/{idUsuario}")]
        public IActionResult Checkout(int idUsuario, SolicitudCheckout solicitud)
        {
            var (pedido, mensaje) = _service.CrearPedidoCheckout(idUsuario, solicitud);

            if (pedido == null)
                return BadRequest(mensaje);

            return Ok(pedido);
        }

        // HU-16: prepara un intento de pago (referencia, monto y firma para Wompi)
        [HttpPost("{idPedido}/iniciar-pago")]
        public IActionResult IniciarPago(int idPedido, int idUsuario)
        {
            var (datos, mensaje) = _service.IniciarPago(idPedido, idUsuario);

            if (datos == null)
                return BadRequest(mensaje);

            return Ok(datos);
        }

        // HU-16: verifica con Wompi el resultado de una transacción
        [HttpPost("{idPedido}/confirmar-pago")]
        public async Task<IActionResult> ConfirmarPago(int idPedido, int idUsuario, string idTransaccion)
        {
            var resultado = await _service.ConfirmarPagoAsync(idPedido, idUsuario, idTransaccion);

            if (resultado.Estado == ResultadoPago.Error)
                return BadRequest(resultado.Mensaje);

            return Ok(resultado);
        }

        // HU-16: el cliente cancela un pedido que no ha pagado
        [HttpPost("{idPedido}/cancelar")]
        public IActionResult Cancelar(int idPedido, int idUsuario)
        {
            var resultado = _service.CancelarPedido(idPedido, idUsuario);

            if (resultado != "Pedido cancelado")
                return BadRequest(resultado);

            return Ok(resultado);
        }

        // HU-17: descarga la factura en PDF de un pedido pagado
        [HttpGet("{idPedido}/factura")]
        public IActionResult DescargarFactura(int idPedido, int idUsuario)
        {
            var (pdf, nombreArchivo, mensaje) = _service.ObtenerFacturaPdf(idPedido, idUsuario);

            if (pdf == null)
                return BadRequest(mensaje);

            return File(pdf, "application/pdf", nombreArchivo);
        }

        // Envío manual: lista de transportadoras para el formulario del administrador
        [HttpGet("transportadoras")]
        public IActionResult ObtenerTransportadoras()
        {
            return Ok(Transportadoras.Lista);
        }

        // Envío manual: el administrador despacha el pedido
        [HttpPost("{idPedido}/enviar")]
        public IActionResult MarcarEnviado(int idPedido, int idAdministrador, string transportadora, string numeroGuia)
        {
            var resultado = _service.MarcarEnviado(idPedido, idAdministrador, transportadora, numeroGuia);

            if (resultado != "Pedido marcado como enviado")
                return BadRequest(resultado);

            return Ok(resultado);
        }

        // Envío manual: el administrador confirma la entrega
        [HttpPost("{idPedido}/entregar")]
        public IActionResult MarcarEntregado(int idPedido, int idAdministrador)
        {
            var resultado = _service.MarcarEntregado(idPedido, idAdministrador);

            if (resultado != "Pedido marcado como entregado")
                return BadRequest(resultado);

            return Ok(resultado);
        }

        // El administrador cancela un pedido pendiente de pago
        [HttpPost("{idPedido}/cancelar-admin")]
        public IActionResult CancelarPedidoAdmin(int idPedido, int idAdministrador)
        {
            var resultado = _service.CancelarPedidoAdmin(idPedido, idAdministrador);

            if (resultado != "Pedido cancelado")
                return BadRequest(resultado);

            return Ok(resultado);
        }

        [HttpPost("comprar/{idUsuario}")]
        public IActionResult Comprar(int idUsuario)
        {
            var resultado = _service.CrearPedidoDesdeCarrito(idUsuario);

            if (!resultado.Contains("correctamente"))
                return BadRequest(resultado);

            return Ok(resultado);
        }
    }
}