using System.Text.Json;

namespace TurnKeyOps.Services.Interfaces;

public interface IBobContextMinimizer
{
    JsonElement Minimize(object? value, int maxCharacters = 8_000);
}
