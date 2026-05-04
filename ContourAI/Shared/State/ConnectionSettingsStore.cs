/// <summary>
/// Глобальное состояние подключения UI-клиента.
/// Хранит IP сервера без порта в памяти приложения и предоставляет общий доступ для разных экранов.
/// Проект: DevAssistant / ContourAI.
/// </summary>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ContourAI.Shared.State;

public sealed class ConnectionSettingsStore : INotifyPropertyChanged
{
    private string _serverIp = "192.168.3.50";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ServerIp
    {
        get => _serverIp;
        set
        {
            var normalized = Normalize(value);
            if (_serverIp == normalized)
            {
                return;
            }

            _serverIp = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ServerBaseAddress));
            OnPropertyChanged(nameof(ServerIpDisplay));
        }
    }

    public string ServerIpDisplay => string.IsNullOrWhiteSpace(ServerIp) ? "Не задан" : ServerIp;

    public string ServerBaseAddress => $"http://{ServerIp}:5000/";

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        trimmed = trimmed.Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase);
        trimmed = trimmed.Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase);

        var portSeparatorIndex = trimmed.IndexOf(':');
        if (portSeparatorIndex >= 0)
        {
            trimmed = trimmed[..portSeparatorIndex];
        }

        return trimmed.TrimEnd('/');
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
