using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Options;
using Microsoft.CognitiveServices.Speech.Transcription;
using MedInsights.AzureServices.Interfaces;
using MedInsights.AzureServices.Lib;

namespace MedInsights.Services
{

    public class AzureSpeechService : IAzureSpeechService
    {
        private readonly string _subscriptionKey;
        private readonly string _region;

        private readonly List<RedactionHit> _lastAudit = new();
        public IReadOnlyList<RedactionHit> LastAudit => _lastAudit;

        public AzureSpeechService(IOptions<AzureSpeechSettings> options)
        {
            _subscriptionKey = options.Value.Key;
            _region = options.Value.Region;
        }

        public async Task<string> TranscribeDictationAsync(Stream audioStream, string locale = "en-US", CancellationToken ct = default)
        {
            _lastAudit.Clear();

            var config = SpeechConfig.FromSubscription(_subscriptionKey, _region);
            config.SpeechRecognitionLanguage = locale;
            config.SetProfanity(ProfanityOption.Raw); // SDK 1.45+ recommended
            config.SetProperty(PropertyId.SpeechServiceResponse_RequestWordLevelTimestamps, "true");
            config.OutputFormat = OutputFormat.Detailed;

            // Helpful for pauses (tune as needed: ms)
            config.SetProperty("SpeechServiceConnection_InitialSilenceTimeoutMs", "7000");
            config.SetProperty("SpeechServiceConnection_EndSilenceTimeoutMs", "3000");

            // If you already have WAV PCM 16k mono, push directly:
            var format = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
            using var pushStream = AudioInputStream.CreatePushStream(format);
            using var audioConfig = AudioConfig.FromStreamInput(pushStream);
            using var recognizer = new SpeechRecognizer(config, audioConfig);

            var sb = new StringBuilder();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            recognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
                {
                    var rawText = e.Result.Text;
                    var json = e.Result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult);
                    var words = ExtractWordTiming(json);

                    // Single-speaker transcription has no diarized speaker; use a neutral label.
                    var redacted = RedactText(rawText, _lastAudit, "Speaker", words);
                    sb.AppendLine(redacted);
                }
            };
            recognizer.Canceled += (s, e) => tcs.TrySetResult(true);
            recognizer.SessionStopped += (s, e) => tcs.TrySetResult(true);

            // Feed the audio into the push stream
            var buffer = new byte[4096];
            int read;
            while ((read = await audioStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                pushStream.Write(buffer, read);
            }
            pushStream.Close(); // important: tells SDK there’s no more audio

            await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
            await tcs.Task.ConfigureAwait(false);
            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);

            return sb.ToString().Trim();
        }

        public async Task<string> TranscribeConversationAsync(Stream audioStream, string locale = "en-US", CancellationToken ct = default)
        {
            _lastAudit.Clear();

            var config = SpeechConfig.FromSubscription(_subscriptionKey, _region);
            config.SpeechRecognitionLanguage = locale;
            config.OutputFormat = OutputFormat.Detailed;
            config.SetProfanity(ProfanityOption.Raw); // SDK 1.45+ recommended
            config.SetProperty(PropertyId.SpeechServiceResponse_RequestWordLevelTimestamps, "true");

            // Adjust silence timeouts for longer pauses between speakers
            config.SetProperty("SpeechServiceConnection_InitialSilenceTimeoutMs", "10000");
            config.SetProperty("SpeechServiceConnection_EndSilenceTimeoutMs", "5000");

            // Prepare 16-kHz mono PCM push stream
            var format = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
            using var pushStream = AudioInputStream.CreatePushStream(format);
            using var audioConfig = AudioConfig.FromStreamInput(pushStream);
            using var transcriber = new ConversationTranscriber(config, audioConfig);

            var sb = new StringBuilder();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            transcriber.Transcribed += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
                {
                    // Azure will auto-assign “Speaker1”, “Speaker2”, etc.
                    var speaker = string.IsNullOrEmpty(e.Result.SpeakerId) ? "Unknown" : e.Result.SpeakerId;
                    var rawText = e.Result.Text;

                    // Parse service JSON for word timings
                    var json = e.Result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult);
                    var words = ExtractWordTiming(json);

                    // Redact for display + audit
                    var display = RedactText(rawText, _lastAudit, speaker, words);
                    sb.AppendLine($"[{speaker}] {display}");
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    sb.AppendLine("[NoMatch]");
                }
            };

            transcriber.Canceled += (s, e) =>
            {
                Console.WriteLine($"Transcription canceled: {e.Reason} — {e.ErrorDetails}");
                tcs.TrySetResult(true);
            };

            transcriber.SessionStopped += (s, e) =>
            {
                Console.WriteLine("Transcription session stopped.");
                tcs.TrySetResult(true);
            };

            // Push the audio into the stream
            var buffer = new byte[4096];
            int read;
            while ((read = await audioStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                pushStream.Write(buffer, read);
            }
            pushStream.Close(); // Important: signals “end of audio” to SDK

            // Start, wait for completion, stop
            await transcriber.StartTranscribingAsync().ConfigureAwait(false);
            await tcs.Task.ConfigureAwait(false);
            await transcriber.StopTranscribingAsync().ConfigureAwait(false);

            return sb.ToString().Trim();
        }

        // ---------- Redaction & auditing ----------

        // Example rules (expand per policy; keep them neutral).
        // Do not embed raw slurs here—load policy-driven patterns from config if needed.
        private static readonly RedactionRule[] _redactionRules = new[]
        {
            // Masked racial slur pattern (e.g., N***, n*****)
            new RedactionRule { Pattern = new Regex(@"\bN\*+\b", RegexOptions.IgnoreCase), Placeholder = "N-word" },

            // Pure asterisk tokens (common generic masking)
            new RedactionRule { Pattern = new Regex(@"\b\*{3,}\b"), Placeholder = "[redacted slur]" },

            // Add additional neutral mappings here (without the raw terms)
        };

        private static string RedactText(
            string text,
            List<RedactionHit> auditHits,
            string speakerId,
            IEnumerable<(string word, long offset, long duration)> wordTiming)
        {
            var redacted = text;
            var alreadyLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rule in _redactionRules)
            {
                var matches = rule.Pattern.Matches(redacted);
                foreach (Match m in matches)
                {
                    var token = m.Value;

                    // Replace in display text
                    redacted = rule.Pattern.Replace(redacted, rule.Placeholder);

                    // Best-effort alignment to word timing for auditing
                    var timed = wordTiming.FirstOrDefault(w => string.Equals(w.word, token, StringComparison.OrdinalIgnoreCase));
                    var key = $"{speakerId}|{token}|{timed.offset}";
                    if (alreadyLogged.Add(key))
                    {
                        auditHits.Add(new RedactionHit
                        {
                            SpeakerId = speakerId,
                            Placeholder = rule.Placeholder,
                            OffsetTicks = timed.offset,
                            DurationTicks = timed.duration,
                            MaskedToken = token
                        });
                    }
                }
            }

            return redacted;
        }

        // ---------- JSON parsing helpers ----------

        private static IEnumerable<(string word, long offset, long duration)> ExtractWordTiming(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                yield break;

            SpeechJsonResult? root = null;
            try
            {
                root = JsonSerializer.Deserialize<SpeechJsonResult>(json);
            }
            catch
            {
                yield break;
            }

            var words = root?.NBest?.FirstOrDefault()?.Words ?? new List<WordInfo>();
            foreach (var w in words)
                yield return (w.Word, w.Offset, w.Duration);
        }

        // Move into internal classes section?

        private sealed class SpeechJsonResult
        {
            public List<NBestEntry>? NBest { get; set; }
        }

        private sealed class NBestEntry
        {
            public List<WordInfo>? Words { get; set; }
        }

        private sealed class WordInfo
        {
            public string Word { get; set; } = "";
            public long Offset { get; set; } // 100-ns ticks
            public long Duration { get; set; }
        }
    }
}
