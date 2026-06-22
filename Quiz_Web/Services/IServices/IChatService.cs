using System.Collections.Generic;
using System.Threading.Tasks;
using Quiz_Web.Models.Entities;

namespace Quiz_Web.Services.IServices;

public interface IChatService
{
    Task<ChatConversation?> GetOrCreateConversationAsync(int studentId, int courseId);
    Task<List<ChatConversation>> GetUserConversationsAsync(int userId);
    Task<List<ChatMessage>> GetConversationMessagesAsync(int conversationId, int skip, int take);
    Task<ChatMessage> SaveMessageAsync(int conversationId, int senderId, string content, string messageType, string? fileName = null);
    Task MarkMessagesAsReadAsync(int conversationId, int userId);
    Task<bool> CanUserChatInCourseAsync(int userId, int courseId);
    Task<ChatConversation?> GetConversationByIdAsync(int conversationId);
}
