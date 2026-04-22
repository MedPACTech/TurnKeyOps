using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MedInsights.Lib
{
    public sealed class SessionMessageDto
    {
        public required string Role { get; init; }
        public required string Content { get; init; }
        public DateTimeOffset At { get; init; }
    }
}
