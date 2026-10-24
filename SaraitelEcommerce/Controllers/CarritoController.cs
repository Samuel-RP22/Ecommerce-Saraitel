using Microsoft.AspNetCore.Mvc;
using Saraitel.API.Interfaces;

namespace Saraitel.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CarritoController : ControllerBase
    {
        private readonly ICarritoService _service;

        public CarritoController(ICarritoService service)
        {
            _service = service;
        }


        [HttpGet("{idUsuario}")]
        public IActionResult GetCarrito(int idUsuario)
        {
            var carrito = _service.ObtenerCarrito(idUsuario);

            if (carrito == null)
                return NotFound("Carrito no encontrado");

            return Ok(carrito);
        }


        [HttpPost("agregar")]
        public IActionResult Agregar(int idUsuario, int idProducto, int cantidad)
        {
            var resultado = _service.AgregarProducto(idUsuario, idProducto, cantidad);

            if (resultado != "Producto agregado al carrito")
                return BadRequest(resultado);

            return Ok(resultado);
        }



        [HttpDelete("eliminar")]
        public IActionResult Eliminar(int idUsuario, int idProducto)
        {
            return Ok(_service.EliminarProducto(idUsuario, idProducto));
        }


        [HttpDelete("vaciar/{idUsuario}")]
        public IActionResult Vaciar(int idUsuario)
        {
            return Ok(_service.VaciarCarrito(idUsuario));
        }


        [HttpGet("total/{idUsuario}")]
        public IActionResult Total(int idUsuario)
        {
            return Ok(_service.CalcularTotal(idUsuario));
        }
    }
}