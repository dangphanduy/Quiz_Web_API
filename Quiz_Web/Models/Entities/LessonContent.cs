using System;
using System.Collections.Generic;

namespace Quiz_Web.Models.Entities;

public partial class LessonContent
{
    public int ContentId { get; set; }

    public int LessonId { get; set; }

    public string ContentType { get; set; } = null!;

    public int? RefId { get; set; }

    public string? Title { get; set; }

    public string? Body { get; set; }

    public string? VideoUrl { get; set; }

    public string? DocumentUrl { get; set; }

    public int OrderIndex { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Lesson Lesson { get; set; } = null!;
}
