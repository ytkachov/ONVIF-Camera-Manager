using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;
using OnvifManager.Web.Contracts;

namespace OnvifManager.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    private static readonly DateTime StartTimeUtc = DateTime.UtcNow;

    private static readonly string AssemblyVersion =
        typeof(HealthController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(HealthController).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    [HttpGet]
    public ActionResult<HealthDto> Get()
    {
        var uptime = DateTime.UtcNow - StartTimeUtc;
        return Ok(new HealthDto(
            Status: "ok",
            Version: AssemblyVersion,
            Runtime: RuntimeInformation.FrameworkDescription,
            Uptime: uptime));
    }
}
