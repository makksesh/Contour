using System.Text.Json.Serialization;

namespace ContourAI.Entities.SystemMetrics;

public sealed record SystemMetricsResponse(
    [property: JsonPropertyName("gpuUsedGb")] double GpuUsedGb,
    [property: JsonPropertyName("gpuTotalGb")] double GpuTotalGb,
    [property: JsonPropertyName("gpuTemperatureCelsius")] double GpuTemperatureCelsius,
    [property: JsonPropertyName("cpuUsagePercent")] double CpuUsagePercent,
    [property: JsonPropertyName("cpuFrequencyGHz")] double CpuFrequencyGHz,
    [property: JsonPropertyName("cpuTemperatureCelsius")] double CpuTemperatureCelsius,
    [property: JsonPropertyName("ramUsedGb")] double RamUsedGb,
    [property: JsonPropertyName("ramTotalGb")] double RamTotalGb);
