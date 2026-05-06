using System;

namespace ContourAI.Entities.Chat;

/// <summary>POST /api/chat/threads — создание треда проекта.</summary>
public sealed record CreateThreadRequest(Guid ProjectId, string Title);
