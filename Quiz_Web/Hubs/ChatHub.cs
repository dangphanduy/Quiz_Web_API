using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly LearningPlatformContext _context;

    public ChatHub(IChatService chatService, LearningPlatformContext context)
    {
        _chatService = chatService;
        _context = context;
    }

    public async Task JoinConversation(int conversationId)
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int userId))
        {
            throw new HubException("Không thể xác thực người dùng.");
        }

        var conversation = await _chatService.GetConversationByIdAsync(conversationId);
        if (conversation == null || (conversation.StudentId != userId && conversation.InstructorId != userId))
        {
            throw new HubException("Bạn không có quyền truy cập cuộc hội thoại này.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId.ToString());
    }

    public async Task LeaveConversation(int conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId.ToString());
    }

    public async Task SendMessage(int conversationId, string content, string messageType, string? fileName = null)
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int userId))
        {
            throw new HubException("Không thể xác thực người dùng.");
        }

        var conversation = await _chatService.GetConversationByIdAsync(conversationId);
        if (conversation == null || (conversation.StudentId != userId && conversation.InstructorId != userId))
        {
            throw new HubException("Bạn không có quyền gửi tin nhắn trong cuộc hội thoại này.");
        }

        // Lưu tin nhắn vào DB
        var savedMessage = await _chatService.SaveMessageAsync(conversationId, userId, content, messageType, fileName);

        // Lấy thông tin người gửi
        var senderName = Context.User?.FindFirst(ClaimTypes.Name) ?? Context.User?.FindFirst("FullName");
        string senderNameVal = senderName?.Value ?? "Ai đó";

        // Gửi tin nhắn realtime đến những người trong nhóm (bao gồm cả sender)
        await Clients.Group(conversationId.ToString()).SendAsync(
            "ReceiveMessage",
            conversationId,
            userId,
            senderNameVal,
            savedMessage.MessageId,
            content,
            messageType,
            fileName,
            savedMessage.CreatedAt.ToString("o")
        );

        // Xác định người nhận
        int recipientId = (conversation.StudentId == userId) ? conversation.InstructorId : conversation.StudentId;

        // Đánh dấu người nhận nhận thông báo mới nếu họ không online trong phòng chat.
        // Để đơn giản và chính xác, chúng ta tạo một System Notification cho người nhận
        var notification = new Notification
        {
            UserId = recipientId,
            Type = "NewMessage",
            Title = "Bạn có tin nhắn mới",
            Body = $"{senderNameVal} đã gửi tin nhắn: {(messageType == "Text" ? (content.Length > 50 ? content.Substring(0, 47) + "..." : content) : $"[Tệp đính kèm: {fileName}]")}",
            Data = $"/Chat?conversationId={conversationId}",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // Gửi thông báo realtime về số lượng tin nhắn chưa đọc đến client (nếu họ có kết nối SignalR chung hoặc qua client notification channel)
        await Clients.User(recipientId.ToString()).SendAsync("ReceiveSystemNotification", notification.Title, notification.Body, notification.Data);
    }
}
