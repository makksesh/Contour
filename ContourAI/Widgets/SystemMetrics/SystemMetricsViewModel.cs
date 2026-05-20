using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ContourAI.Shared.Api;

namespace ContourAI.Widgets.SystemMetrics;

/// <summary>
/// ViewModel системных метрик shell-topbar.
/// Опрашивает backend каждые 3 секунды и форматирует CPU / RAM / GPU для UI.
/// </summary>
public sealed partial class SystemMetricsViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly IBrush MutedBrush = Brush.Parse("#968B7E");
    private static readonly IBrush CoolBrush = Brush.Parse("#8D9E73");
    private static readonly IBrush WarmBrush = Brush.Parse("#B88A56");
    private static readonly IBrush HotBrush = Brush.Parse("#C97C74");

    private readonly SystemMetricsService? _metrics;
    private readonly PeriodicTimer? _timer;
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty] private bool _isServerConnected;
    [ObservableProperty] private string _connectionStatus = "Подключение...";

    [ObservableProperty] private string _gpuLoad = "— / — GB";
    [ObservableProperty] private string _gpuTemp = "—";
    [ObservableProperty] private IBrush _gpuTempBrush = MutedBrush;

    [ObservableProperty] private string _cpuLoad = "— / — GHz";
    [ObservableProperty] private string _cpuTemp = "—";
    [ObservableProperty] private IBrush _cpuTempBrush = MutedBrush;

    [ObservableProperty] private string _ramLoad = "— / — GB";

    public SystemMetricsViewModel()
    {
        IsServerConnected = true;
        ConnectionStatus = "Подключено";
        GpuLoad = "1.2 / 8.0 GB";
        GpuTemp = "54°C";
        GpuTempBrush = ResolveTemperatureBrush(54);
        CpuLoad = "12.4% / 4.8 GHz";
        CpuTemp = "54°C";
        CpuTempBrush = ResolveTemperatureBrush(54);
        RamLoad = "8.0 / 16.0 GB";
    }

    public SystemMetricsViewModel(SystemMetricsService metrics)
    {
        _metrics = metrics;
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        _ = StartMetricsLoopAsync();
    }

    private async Task StartMetricsLoopAsync()
    {
        await FetchMetricsAsync();

        try
        {
            while (await _timer!.WaitForNextTickAsync(_cts.Token))
                await FetchMetricsAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task FetchMetricsAsync()
    {
        if (_metrics is null)
            return;

        try
        {
            var metrics = await _metrics.GetAsync(_cts.Token);
            if (metrics is null)
            {
                IsServerConnected = false;
                ConnectionStatus = "Сервер недоступен";
                return;
            }

            IsServerConnected = true;
            ConnectionStatus = "Подключено";

            GpuLoad = $"{metrics.GpuUsedGb:F1} / {metrics.GpuTotalGb:F1} GB";
            GpuTemp = metrics.GpuTemperatureCelsius > 0 ? $"{metrics.GpuTemperatureCelsius:F0}°C" : "—";
            GpuTempBrush = ResolveTemperatureBrush(metrics.GpuTemperatureCelsius);

            CpuLoad = $"{metrics.CpuUsagePercent:F1}% / {metrics.CpuFrequencyGHz:F1} GHz";
            CpuTemp = metrics.CpuTemperatureCelsius > 0 ? $"{metrics.CpuTemperatureCelsius:F0}°C" : "—";
            CpuTempBrush = ResolveTemperatureBrush(metrics.CpuTemperatureCelsius);

            RamLoad = $"{metrics.RamUsedGb:F1} / {metrics.RamTotalGb:F1} GB";
        }
        catch
        {
            IsServerConnected = false;
            ConnectionStatus = "Сервер недоступен";
        }
    }

    private static IBrush ResolveTemperatureBrush(double temperature) => temperature switch
    {
        <= 0 => MutedBrush,
        < 60 => CoolBrush,
        < 80 => WarmBrush,
        _ => HotBrush
    };

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        _timer?.Dispose();
        await ValueTask.CompletedTask;
    }
}
