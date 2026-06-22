using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Controllers.API;

[Authorize]
[Route("api/chat")]
[ApiController]
public class ChatApiController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IStorageService _storageService;

    public ChatApiController(IChatService chatService, IStorageService storageService)
    {
        _chatService = chatService;
        _storageService = storageService;
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

        var conversations = await _chatService.GetUserConversationsAsync(userId);
        
        var result = conversations.Select(c => new
        {
            conversationId = c.ConversationId,
            studentId = c.StudentId,
            studentName = c.Student.FullName,
            studentAvatar = c.Student.AvatarUrl,
            instructorId = c.InstructorId,
            instructorName = c.Instructor.FullName,
            instructorAvatar = c.Instructor.AvatarUrl,
            courseId = c.CourseId,
            courseTitle = c.Course.Title,
            createdAt = c.CreatedAt,
            lastMessage = c.Messages.OrderByDescending(m => m.CreatedAt).Select(m => new
            {
                senderId = m.SenderId,
                content = m.Content,
                messageType = m.MessageType,
                createdAt = m.CreatedAt
            }).FirstOrDefault(),
            unreadCount = c.Messages.Count(m => m.SenderId != userId && !m.IsRead)
        });

        return Ok(new { success = true, conversations = result });
    }

    [HttpGet("history/{conversationId:int}")]
    public async Task<IActionResult> GetHistory(int conversationId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

        var conversation = await _chatService.GetConversationByIdAsync(conversationId);
        if (conversation == null || (conversation.StudentId != userId && conversation.InstructorId != userId))
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Không có quyền xem lịch sử cuộc trò chuyện này." });

        var messages = await _chatService.GetConversationMessagesAsync(conversationId, skip, take);
        
        var result = messages.Select(m => new
        {
            messageId = m.MessageId,
            conversationId = m.ConversationId,
            senderId = m.SenderId,
            content = m.Content,
            messageType = m.MessageType,
            fileName = m.FileName,
            isRead = m.IsRead,
            createdAt = m.CreatedAt
        });

        return Ok(new { success = true, messages = result });
    }

    [HttpPost("get-or-create")]
    public async Task<IActionResult> GetOrCreateConversation([FromQuery] int courseId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

        var canChat = await _chatService.CanUserChatInCourseAsync(userId, courseId);
        if (!canChat)
            return BadRequest(new { success = false, message = "Bạn cần mua khóa học này để nhắn tin cho giảng viên." });

        var conversation = await _chatService.GetOrCreateConversationAsync(userId, courseId);
        if (conversation == null)
            return BadRequest(new { success = false, message = "Không thể khởi tạo cuộc trò chuyện." });

        return Ok(new
        {
            success = true,
            conversationId = conversation.ConversationId,
            studentId = conversation.StudentId,
            studentName = conversation.Student.FullName,
            instructorId = conversation.InstructorId,
            instructorName = conversation.Instructor.FullName,
            courseId = conversation.CourseId,
            courseTitle = conversation.Course.Title
        });
    }

    [HttpPost("read/{conversationId:int}")]
    public async Task<IActionResult> MarkAsRead(int conversationId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

        var conversation = await _chatService.GetConversationByIdAsync(conversationId);
        if (conversation == null || (conversation.StudentId != userId && conversation.InstructorId != userId))
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Không có quyền truy cập." });

        await _chatService.MarkMessagesAsReadAsync(conversationId, userId);
        return Ok(new { success = true });
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10_485_760)] // 10MB limit
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "Không nhận được tệp tin." });

        try
        {
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".docx", ".doc", ".zip", ".rar", ".txt" };
            var ext = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
            {
                return BadRequest(new { success = false, message = "Định dạng tệp đính kèm không được hỗ trợ." });
            }

            var fileUrl = await _storageService.UploadFileAsync(file, "uploads/chat");
            return Ok(new
            {
                success = true,
                fileUrl = fileUrl,
                fileName = file.FileName
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Lỗi khi tải tệp lên: " + ex.Message });
        }
    }
}
