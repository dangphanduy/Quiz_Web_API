using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Quiz_Web.Controllers;

[Authorize]
[Route("[controller]")]
public class ChatController : Controller
{
    private readonly ILogger<ChatController> _logger;

    public ChatController(ILogger<ChatController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index([FromQuery] int? conversationId = null)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Unauthenticated user attempted to open chat page");
            return Challenge();
        }

        ViewBag.CurrentUserId = userId;
        ViewBag.InitialConversationId = conversationId;

        _logger.LogInformation(
            "User {UserId} opened chat page with initial conversation {ConversationId}",
            userId,
            conversationId);
        
        return View();
    }
}
