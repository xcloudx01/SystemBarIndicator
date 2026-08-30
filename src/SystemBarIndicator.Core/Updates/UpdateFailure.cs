using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace SystemBarIndicator.Updates
{
    /// <summary>How seriously a failed update check should be taken.</summary>
    public enum UpdateFailureSeverity
    {
        /// <summary>A connectivity or throttling problem: expected in the wild, not our bug.</summary>
        Transient,

        /// <summary>Something that should not happen and needs looking at.</summary>
        Defect
    }

    /// <summary>
    /// A classified update failure. <see cref="Key"/> is a stable identity for the *kind* of
    /// failure — repeated failures are recognised by it rather than by the exception message,
    /// which varies with the address, the OS language and the runtime wording.
    /// </summary>
    public readonly record struct UpdateFailure(UpdateFailureSeverity Severity, string Key)
    {
        public bool IsTransient => Severity == UpdateFailureSeverity.Transient;
    }

    /// <summary>
    /// Decides whether a failed update check is worth an error in the log. The distinction
    /// matters: <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/> reports HTTP errors
    /// through the same <see cref="HttpRequestException"/> as a dead connection, so treating
    /// the whole type as "expected" would hide a genuine breakage — a renamed repository
    /// answering 404, say — behind a warning nobody reads.
    /// </summary>
    public static class UpdateFailures
    {
        /// <param name="cancellationRequested">
        /// Whether the caller's own token was cancelled. Without it a client timeout and a
        /// deliberate shutdown are indistinguishable: both surface as
        /// <see cref="OperationCanceledException"/>.
        /// </param>
        public static UpdateFailure Classify(Exception? exception, bool cancellationRequested)
        {
            switch (exception)
            {
                case null:
                    return new UpdateFailure(UpdateFailureSeverity.Defect, "unknown");

                case OperationCanceledException:
                    // TaskCanceledException derives from this one, and HttpClient reports its
                    // own timeout that way.
                    return cancellationRequested
                        ? new UpdateFailure(UpdateFailureSeverity.Transient, "canceled")
                        : new UpdateFailure(UpdateFailureSeverity.Transient, "timeout");

                case HttpRequestException http:
                    return ClassifyHttp(http);

                case JsonException:
                    // A release payload we cannot read is our problem, not the network's.
                    return new UpdateFailure(UpdateFailureSeverity.Defect, "data");

                default:
                    return new UpdateFailure(UpdateFailureSeverity.Defect, "unknown:" + exception.GetType().Name);
            }
        }

        private static UpdateFailure ClassifyHttp(HttpRequestException exception)
        {
            // No status at all means the request never got an answer: DNS, TLS, refused
            // connection, black-holed host.
            if (exception.StatusCode == null)
                return new UpdateFailure(UpdateFailureSeverity.Transient, "transport");

            int status = (int)exception.StatusCode.Value;
            string key = "http:" + status.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // 403 is how GitHub reports unauthenticated rate limiting, which is a wait, not a bug.
            bool transient = status is 403 or 408 or 429 || status >= 500;
            return new UpdateFailure(
                transient ? UpdateFailureSeverity.Transient : UpdateFailureSeverity.Defect,
                key);
        }
    }
}
