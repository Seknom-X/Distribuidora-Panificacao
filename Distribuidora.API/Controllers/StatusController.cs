
using Microsoft.AspNetCore.Mvc;

namespace Distribuidora.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Mensagem = "API da distribuidora funcionando!",
            Sistema = "Distribuidora de Panificacao",
            Versao = "1.0"
        });
    }
}
