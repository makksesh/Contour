namespace ContourAI.Entities.Chat;

/// <summary>
/// Один SSE-фрейм: event-имя + данные.
/// Парсится построчно из потока сервера.
/// </summary>
public sealed record SseFrame(string EventName, string Data);
