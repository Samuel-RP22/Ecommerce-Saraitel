using Microsoft.AspNetCore.Mvc;
using Saraitel.API.Interfaces;
using Saraitel.Entidades;

namespace Saraitel.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriaController : ControllerBase
    {
        private readonly ICategoriaService _service;

        public CategoriaController(ICategoriaService service)
        {
            _service = service;
        }

        [HttpGet]
        public IActionResult Get() => Ok(_service.ObtenerCategorias());

        [HttpPost]
        public IActionResult Post(Categoria categoria)
        {
            return Ok(_service.AgregarCategoria(categoria));
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var resultado = _service.EliminarCategoria(id);

            if (resultado == "Categoría no encontrada")
                return NotFound(resultado);

            if (resultado.Contains("correctamente"))
                return Ok(resultado);

            // 409 Conflict: la categoría existe pero tiene productos que dependen de ella
            return Conflict(resultado);
        }
    }
}