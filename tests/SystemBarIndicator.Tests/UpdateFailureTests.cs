using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SystemBarIndicator.Updates;
using Xunit;

namespace SystemBarIndicator.Tests
{
    public class UpdateFailureTests
    {
        private static UpdateFailure Classify(Exception ex, bool cancellationRequested = false) =>
            UpdateFailures.Classify(ex, cancellationRequested);

        [Fact]
        public void TransportFailureIsTransient()
        {
            // No status code at all: DNS, TLS, refused or black-holed connection.
            var failure = Classify(new HttpRequestException("no such host"));

            Assert.True(failure.IsTransient);
            Assert.Equal("transport", failure.Key);
        }

        [Theory]
        [InlineData(HttpStatusCode.RequestTimeout)]
        [InlineData(HttpStatusCode.TooManyRequests)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        [InlineData(HttpStatusCode.Forbidden)] // GitHub reports rate limiting this way
        public void RetryableStatusesAreTransient(HttpStatusCode status)
        {
            var failure = Classify(new HttpRequestException("http", null, status));

            Assert.True(failure.IsTransient);
            Assert.Equal("http:" + (int)status, failure.Key);
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]     // repository or endpoint renamed — our bug
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.BadRequest)]
        public void OtherHttpStatusesAreDefects(HttpStatusCode status)
        {
            // EnsureSuccessStatusCode throws the same exception type as a dead connection, so
            // classifying by type alone would bury a genuine breakage in the warnings.
            var failure = Classify(new HttpRequestException("http", null, status));

            Assert.False(failure.IsTransient);
            Assert.Equal("http:" + (int)status, failure.Key);
        }

        [Fact]
        public void ClientTimeoutIsTransient()
        {
            var failure = Classify(new TaskCanceledException("timed out"), cancellationRequested: false);

            Assert.True(failure.IsTransient);
            Assert.Equal("timeout", failure.Key);
        }

        [Fact]
        public void DeliberateCancellationIsDistinguishedFromATimeout()
        {
            var failure = Classify(new OperationCanceledException(), cancellationRequested: true);

            Assert.True(failure.IsTransient);
            Assert.Equal("canceled", failure.Key);
        }

        [Fact]
        public void UnreadablePayloadIsADefect()
        {
            var failure = Classify(new JsonException("unexpected token"));

            Assert.False(failure.IsTransient);
            Assert.Equal("data", failure.Key);
        }

        [Fact]
        public void UnknownExceptionsAreDefectsAndKeyedByType()
        {
            var failure = Classify(new InvalidOperationException("boom"));

            Assert.False(failure.IsTransient);
            Assert.Equal("unknown:InvalidOperationException", failure.Key);
        }

        [Fact]
        public void NullIsHandled()
        {
            Assert.False(Classify(null!).IsTransient);
        }

        [Fact]
        public void KeysAreStableAcrossDifferingMessages()
        {
            // De-duplication compares keys, not messages: the text carries the address and
            // varies with the OS language, so the same outage would otherwise log forever.
            var first = Classify(new HttpRequestException("No such host is known. (api.github.com:443)"));
            var second = Classify(new HttpRequestException("Nome host sconosciuto. (api.github.com:443)"));

            Assert.Equal(first.Key, second.Key);
            Assert.Equal(first.Severity, second.Severity);
        }

        [Fact]
        public void TransportAndHttpFailuresDoNotShareAKey()
        {
            var transport = Classify(new HttpRequestException("dead"));
            var notFound = Classify(new HttpRequestException("http", null, HttpStatusCode.NotFound));

            Assert.NotEqual(transport.Key, notFound.Key);
        }
    }
}
