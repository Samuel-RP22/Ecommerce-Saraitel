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
            return Ok(_service.AgregarProducto(producto));
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            return Ok(_service.EliminarProducto(id));
        }

        [HttpPut]
        public IActionResult Put(Producto producto)
        {
            return Ok(_service.ActualizarProducto(producto));
        }
    }
}