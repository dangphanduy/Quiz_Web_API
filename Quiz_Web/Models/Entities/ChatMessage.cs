using System;

namespace Quiz_Web.Models.Entities;

public partial class ChatMessage
{
    public int MessageId { get; set; }

    public int ConversationId { get; set; }

    public int SenderId { get; set; }

    public string Content { get; set; } = null!;

    public string MessageType { get; set; } = "Text"; // "Text", "Image", "File"

    public string? FileName { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ChatConversation Conversation { get; set; } = null!;

    public virtual User Sender { get; set; } = null!;
}
