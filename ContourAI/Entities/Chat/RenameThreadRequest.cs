namespace ContourAI.Entities.Chat;

/// <summary>PUT /api/chat/threads/{id} — переименование треда.</summary>
public sealed record RenameThreadRequest(string Title);
