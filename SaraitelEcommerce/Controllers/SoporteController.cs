using Microsoft.AspNetCore.Mvc;
using Saraitel.API.Interfaces;
using Saraitel.Entidades;

namespace Saraitel.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SoporteController : ControllerBase
    {
        private readonly ISoporteService _service;

        public SoporteController(ISoporteService service)
        {
            _service = service;
        }

        [HttpGet]
        public IActionResult Get() => Ok(_service.ObtenerSoportes());

        [HttpPost]
        public IActionResult Post(Soporte soporte)
        {
            return Ok(_service.CrearSoporte(soporte));
        }

        [HttpPut("estado/{id}")]
        public IActionResult CambiarEstado(int id, string estado)
        {
            return Ok(_service.CambiarEstado(id, estado));
        }
    }
}