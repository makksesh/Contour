using System;

namespace ContourAI.Entities.Chat;

/// <summary>POST /api/chat/threads/{id}/attach — привязка треда к проекту.</summary>
public sealed record AttachThreadToProjectRequest(Guid ProjectId);
