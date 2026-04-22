
namespace MedInsights.Lib.Dtos
{
    public sealed record RealtimeSessionRequestDto(
        string? Model,
        string? Mode,
        string? Voice,
        string? Instructions
    );
}