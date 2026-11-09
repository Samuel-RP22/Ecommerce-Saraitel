using Microsoft.AspNetCore.Mvc;
using Saraitel.API.Interfaces;
using Saraitel.Entidades;

namespace Saraitel.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductoController : ControllerBase
    {
        private readonly IProductoService _service;

        public ProductoController(IProductoService service)
        {
            _service = service;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(_service.ObtenerProductos());
        }

        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var producto = _service.ObtenerPorId(id);
            if (producto == null) return NotFound();
            return Ok(producto);
        }

        [HttpPost]
        public IActionResult Post(Producto producto)
        {
            return Responder(_service.AgregarProducto(producto));
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            return Responder(_service.EliminarProducto(id));
        }

        [HttpPut]
        public IActionResult Put(Producto producto)
        {
            return Responder(_service.ActualizarProducto(producto));
        }

        // El servicio devuelve un texto: si salió bien es 200,
        // si el producto no existe 404, y si un dato está mal 400 con el motivo
        private IActionResult Responder(string resultado)
        {
            if (resultado == "Producto no encontrado")
                return NotFound(resultado);

            if (resultado.Contains("correctamente"))
                return Ok(resultado);

            return BadRequest(resultado);
        }
    }
}
