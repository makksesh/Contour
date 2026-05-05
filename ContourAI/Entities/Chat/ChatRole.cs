/// <summary>
/// Роль участника сообщения: User или Assistant.
/// Проект: DevAssistant / ContourAI.
/// </summary>

namespace ContourAI.Entities.Chat;

public enum ChatRole
{
    User      = 0,
    Assistant = 1,
    System    = 2
}
