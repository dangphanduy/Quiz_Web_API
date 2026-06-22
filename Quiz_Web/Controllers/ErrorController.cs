using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Quiz_Web.Models;

namespace Quiz_Web.Controllers;

[AllowAnonymous]
[Route("Error")]
public class ErrorController : Controller
{
    [AcceptVerbs("GET", "POST", "PUT", "DELETE", "PATCH")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index()
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

        Response.StatusCode = StatusCodes.Status500InternalServerError;

        if (IsApiRequest(exceptionFeature?.Path))
        {
            return new JsonResult(new
            {
                success = false,
                message = "Hệ thống đang gặp lỗi. Vui lòng thử lại sau.",
                traceId = requestId
            });
        }

        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel { RequestId = requestId });
    }

    private bool IsApiRequest(string? originalPath)
    {
        return originalPath?.StartsWith("/api", StringComparison.OrdinalIgnoreCase) == true
            || Request.Headers.Accept.Any(value =>
                value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
    }
}
