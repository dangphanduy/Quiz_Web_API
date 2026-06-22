using System;
using System.Collections.Generic;

namespace Quiz_Web.Models.Entities;

public partial class ChatConversation
{
    public int ConversationId { get; set; }

    public int StudentId { get; set; }

    public int InstructorId { get; set; }

    public int CourseId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User Student { get; set; } = null!;

    public virtual User Instructor { get; set; } = null!;

    public virtual Course Course { get; set; } = null!;

    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
