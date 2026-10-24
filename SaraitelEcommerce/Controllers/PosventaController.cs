using Microsoft.AspNetCore.Mvc;
using Saraitel.API.Interfaces;
using Saraitel.API.Models;

namespace Saraitel.API.Controllers
{
    // HU-19 a HU-23: devoluciones y garantías
    [ApiController]
    [Route("api/[controller]")]
    public class PosventaController : ControllerBase
    {
        private readonly IPosventaService _service;

        public PosventaController(IPosventaService service)
        {
            _service = service;
        }

        /* ---------- Cliente ---------- */

        // Qué puede hacer el cliente con un pedido (plazos, unidades disponibles y solicitudes)
        [HttpGet("pedido/{idPedido}")]
        public IActionResult ResumenPedido(int idPedido, int idUsuario)
        {
            var (resumen, mensaje) = _service.ObtenerResumenPedido(idPedido, idUsuario);

            if (resumen == null)
                return BadRequest(mensaje);

            return Ok(resumen);
        }

        // Todas las devoluciones y garantías de un cliente
        [HttpGet("usuario/{idUsuario}")]
        public IActionResult DeUsuario(int idUsuario)
        {
            return Ok(_service.ObtenerDeUsuario(idUsuario));
        }

        // HU-19: solicitar devolución
        [HttpPost("devoluciones")]
        public IActionResult SolicitarDevolucion(int idUsuario, SolicitudDevolucion solicitud)
        {
            var (devolucion, mensaje) = _service.SolicitarDevolucion(idUsuario, solicitud);

            if (devolucion == null)
                return BadRequest(mensaje);

            return Ok(devolucion);
        }

        // HU-22: reportar garantía (formulario con fotos opcionales)
        [HttpPost("garantias")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ReportarGarantia([FromForm] SolicitudGarantia solicitud)
        {
            var fotos = new List<ArchivoEvidencia>();

            foreach (var archivo in solicitud.Fotos ?? new List<IFormFile>())
            {
                using var memoria = new MemoryStream();
                await archivo.CopyToAsync(memoria);

                fotos.Add(new ArchivoEvidencia
                {
                    NombreOriginal = archivo.FileName,
                    Contenido = memoria.ToArray()
                });
            }

            var (ticket, mensaje) = _service.ReportarGarantia(
                solicitud.IdUsuario, solicitud.IdPedido, solicitud.IdProducto,
                solicitud.Cantidad, solicitud.Descripcion, fotos);

            if (ticket == null)
                return BadRequest(mensaje);

            return Ok(ticket);
        }

        // Foto de evidencia (la ve el dueño del ticket o un administrador)
        [HttpGet("garantias/{idTicket}/evidencias/{archivo}")]
        public IActionResult Evidencia(int idTicket, string archivo, int idUsuario)
        {
            var (contenido, tipo, mensaje) = _service.ObtenerEvidencia(idTicket, idUsuario, archivo);

            if (contenido == null)
                return NotFound(mensaje);

            return File(contenido, tipo);
        }

        /* ---------- Administrador ---------- */

        // HU-21
        [HttpGet("devoluciones")]
        public IActionResult Devoluciones(int idAdministrador)
        {
            var (lista, mensaje) = _service.ObtenerDevoluciones(idAdministrador);

            if (lista == null)
                return BadRequest(mensaje);

            return Ok(lista);
        }

        // HU-21: accion = aprobar, rechazar, recibir o reembolsar
        [HttpPost("devoluciones/{idDevolucion}/{accion}")]
        public IActionResult CambiarEstadoDevolucion(
            int idDevolucion, string accion, int idAdministrador, string? motivo = null, string? referencia = null)
        {
            var (devolucion, mensaje) = _service.CambiarEstadoDevolucion(
                idDevolucion, idAdministrador, accion, motivo, referencia);

            if (devolucion == null)
                return BadRequest(mensaje);

            return Ok(devolucion);
        }

        // HU-23
        [HttpGet("garantias")]
        public IActionResult Garantias(int idAdministrador)
        {
            var (lista, mensaje) = _service.ObtenerGarantias(idAdministrador);

            if (lista == null)
                return BadRequest(mensaje);

            return Ok(lista);
        }

        // HU-23: accion = revisar, aprobar, rechazar, recibir o enviar-cambio
        [HttpPost("garantias/{idTicket}/{accion}")]
        public IActionResult CambiarEstadoGarantia(
            int idTicket, string accion, int idAdministrador,
            string? motivo = null, string? cubiertoPor = null, int? idProductoReemplazo = null)
        {
            var (ticket, mensaje) = _service.CambiarEstadoGarantia(
                idTicket, idAdministrador, accion, motivo, cubiertoPor, idProductoReemplazo);

            if (ticket == null)
                return BadRequest(mensaje);

            return Ok(ticket);
        }
    }
}
