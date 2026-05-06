/// <summary>
/// Результат запроса истории треда.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System.Collections.Generic;

using System;
using System.Collections.Generic;

namespace ContourAI.Entities.Chat;

/// <param name="Thread">Данные треда.</param>
/// <param name="Messages">Сообщения в хронологическом порядке.</param>
public record GetThreadHistoryResult(
    ChatThreadDto                 Thread,
    IReadOnlyList<ChatMessageDto> Messages);
