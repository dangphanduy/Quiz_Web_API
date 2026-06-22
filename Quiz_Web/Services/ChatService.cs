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
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        LearningPlatformContext context,
        ICourseAccessService courseAccessService,
        ILogger<ChatService> logger)
    {
        _context = context;
        _courseAccessService = courseAccessService;
        _logger = logger;
    }

    public async Task<bool> CanUserChatInCourseAsync(int userId, int courseId)
    {
        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.CourseId == courseId);
        if (course == null)
        {
            _logger.LogWarning("User {UserId} attempted to chat in missing course {CourseId}", userId, courseId);
            return false;
        }

        // Giảng viên sở hữu khóa học có quyền chat
        if (course.OwnerId == userId) return true;

        // Học viên phải có quyền truy cập khóa học (đã mua hoặc đăng ký)
        var hasAccess = await _courseAccessService.CheckCourseAccessAsync(userId, courseId);
        if (!hasAccess)
        {
            _logger.LogWarning("User {UserId} does not have chat access for course {CourseId}", userId, courseId);
        }

        return hasAccess;
    }

    public async Task<ChatConversation?> GetOrCreateConversationAsync(int studentId, int courseId)
    {
        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.CourseId == courseId);
        if (course == null)
        {
            _logger.LogWarning("Cannot create chat conversation because course {CourseId} was not found", courseId);
            return null;
        }

        // Kiểm tra quyền chat của học viên
        bool canChat = await _courseAccessService.CheckCourseAccessAsync(studentId, courseId);
        if (!canChat)
        {
            _logger.LogWarning("Student {StudentId} cannot create chat conversation for course {CourseId}", studentId, courseId);
            return null;
        }

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
                CreatedAt = DateTimeHelper.Now
            };

            _context.ChatConversations.Add(conversation);
            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "Created chat conversation {ConversationId} for student {StudentId}, instructor {InstructorId}, course {CourseId}",
                conversation.ConversationId,
                studentId,
                instructorId,
                courseId);

            // Load lại đầy đủ thông tin Student, Instructor, Course
            conversation = await _context.ChatConversations
                .Include(c => c.Student)
                .Include(c => c.Instructor)
                .Include(c => c.Course)
                .FirstOrDefaultAsync(c => c.ConversationId == conversation.ConversationId);
        }

        _logger.LogInformation(
            "Resolved chat conversation {ConversationId} for student {StudentId}, instructor {InstructorId}, course {CourseId}",
            conversation?.ConversationId,
            studentId,
            instructorId,
            courseId);

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
            CreatedAt = DateTimeHelper.Now
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();
        _logger.LogInformation(
            "Saved chat message {MessageId} in conversation {ConversationId} from sender {SenderId}; message type {MessageType}; has file {HasFile}",
            message.MessageId,
            conversationId,
            senderId,
            messageType,
            !string.IsNullOrWhiteSpace(fileName));

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
            _logger.LogInformation(
                "Marked {UnreadCount} chat messages as read in conversation {ConversationId} for user {UserId}",
                unreadMessages.Count,
                conversationId,
                userId);
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
