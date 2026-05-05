/// <summary>
/// Запрос на создание нового треда чата.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;

namespace ContourAI.Entities.Chat;

public sealed record CreateThreadRequest(
    string    Title,
    ChatScope Scope,
    Guid?     ProjectId);
