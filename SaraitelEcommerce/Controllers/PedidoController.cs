using Microsoft.AspNetCore.Mvc;
using Saraitel.API.Interfaces;
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

        [HttpPost("comprar/{idUsuario}")]
        public IActionResult Comprar(int idUsuario)
        {
            var resultado = _service.CrearPedidoDesdeCarrito(idUsuario);
            return Ok(resultado);
        }
    }
}