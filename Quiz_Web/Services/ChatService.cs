using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Services;

public class ChatService : IChatService
{
    private readonly LearningPlatformContext _context;
    private readonly ICourseAccessService _courseAccessService;

    public ChatService(LearningPlatformContext context, ICourseAccessService courseAccessService)
    {
        _context = context;
        _courseAccessService = courseAccessService;
    }

    public async Task<bool> CanUserChatInCourseAsync(int userId, int courseId)
    {
        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.CourseId == courseId);
        if (course == null) return false;

        // Giảng viên sở hữu khóa học có quyền chat
        if (course.OwnerId == userId) return true;

        // Học viên phải có quyền truy cập khóa học (đã mua hoặc đăng ký)
        return await _courseAccessService.CheckCourseAccessAsync(userId, courseId);
    }

    public async Task<ChatConversation?> GetOrCreateConversationAsync(int studentId, int courseId)
    {
        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.CourseId == courseId);
        if (course == null) return null;

        // Kiểm tra quyền chat của học viên
        bool canChat = await _courseAccessService.CheckCourseAccessAsync(studentId, courseId);
        if (!canChat) return null;

        int instructorId = course.OwnerId;

        // Tìm cuộc hội thoại hiện có
        var conversation = await _context.ChatConversations
            .Include(c => c.Student)
            .Include(c => c.Instructor)
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.StudentId == studentId && c.InstructorId == instructorId && c.CourseId == courseId);

        if (conversation == null)
        {
            conversation = new ChatConversation
            {
                StudentId = studentId,
                InstructorId = instructorId,
                CourseId = courseId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatConversations.Add(conversation);
            await _context.SaveChangesAsync();

            // Load lại đầy đủ thông tin Student, Instructor, Course
            conversation = await _context.ChatConversations
                .Include(c => c.Student)
                .Include(c => c.Instructor)
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.ConversationId == conversation.ConversationId);
        }

        return conversation;
    }

    public async Task<List<ChatConversation>> GetUserConversationsAsync(int userId)
    {
        // Lấy tất cả các hội thoại mà user là Học viên hoặc Giảng viên
        var conversations = await _context.ChatConversations
            .Include(c => c.Student)
            .Include(c => c.Instructor)
            .Include(c => c.Course)
            .Include(c => c.Messages)
            .Where(c => c.StudentId == userId || c.InstructorId == userId)
            .ToListAsync();

        // Sắp xếp các hội thoại theo thời gian tin nhắn mới nhất
        return conversations
            .OrderByDescending(c => c.Messages.Any() ? c.Messages.Max(m => m.CreatedAt) : c.CreatedAt)
            .ToList();
    }

    public async Task<List<ChatMessage>> GetConversationMessagesAsync(int conversationId, int skip, int take)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        // Đảo ngược lại để đúng thứ tự thời gian tăng dần khi hiển thị
        messages.Reverse();
        return messages;
    }

    public async Task<ChatMessage> SaveMessageAsync(int conversationId, int senderId, string content, string messageType, string? fileName = null)
    {
        var message = new ChatMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            MessageType = messageType,
            FileName = fileName,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }

    public async Task MarkMessagesAsReadAsync(int conversationId, int userId)
    {
        var unreadMessages = await _context.ChatMessages
            .Where(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead)
            .ToListAsync();

        if (unreadMessages.Any())
        {
            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
            }
            await _context.SaveChangesAsync();
        }
    }

    public async Task<ChatConversation?> GetConversationByIdAsync(int conversationId)
    {
        return await _context.ChatConversations
            .Include(c => c.Student)
            .Include(c => c.Instructor)
            .Include(c => c.Course)
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId);
    }
}
