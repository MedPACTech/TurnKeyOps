using System;

namespace MedInsights.Models
{
    public record ChatTurnRequest(string ChatId, string UserText);
}