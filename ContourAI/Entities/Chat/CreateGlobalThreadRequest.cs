namespace ContourAI.Entities.Chat;

/// <summary>POST /api/chat/threads/global — создание глобального треда.</summary>
public sealed record CreateGlobalThreadRequest(string Title);
