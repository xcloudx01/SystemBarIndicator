using SystemBarIndicator.Monitoring;
using Xunit;

namespace SystemBarIndicator.Tests
{
    public class CpuLoadTests
    {
        [Theory]
        [InlineData(0UL, 100UL, 100f)]   // nothing idle: fully busy
        [InlineData(100UL, 100UL, 0f)]   // all idle
        [InlineData(25UL, 100UL, 75f)]
        [InlineData(75UL, 100UL, 25f)]
        public void FromDeltas_IsTheBusyShareOfTheTotal(ulong idle, ulong total, float expected)
        {
            Assert.Equal(expected, CpuLoad.FromDeltas(idle, total), 3);
        }

        [Fact]
        public void FromDeltas_ZeroTotalIsZero()
        {
            // Two samples taken within the same counter granularity.
            Assert.Equal(0f, CpuLoad.FromDeltas(0, 0));
            Assert.Equal(0f, CpuLoad.FromDeltas(50, 0));
        }

        [Theory]
        [InlineData(101UL, 100UL)]
        [InlineData(ulong.MaxValue, 100UL)]
        public void FromDeltas_IdleAboveTotalIsZeroNotOneHundred(ulong idle, ulong total)
        {
            // The subtraction is unsigned: done in the wrong order it wraps around to an
            // enormous number and clamps to 100 %, reporting a fully loaded CPU while it
            // is in fact idle.
            Assert.Equal(0f, CpuLoad.FromDeltas(idle, total));
        }

        [Fact]
        public void FromDeltas_HandlesRealisticCounterMagnitudes()
        {
            // 100 ns ticks accumulated over months of uptime: far beyond float precision,
            // which is why the division is done in double.
            const ulong total = 4_000_000_000_000UL;
            const ulong idle = 3_000_000_000_000UL;
            Assert.Equal(25f, CpuLoad.FromDeltas(idle, total), 3);
        }

        [Fact]
        public void FromSamples_UsesTheDifferenceBetweenCumulativeCounters()
        {
            Assert.Equal(40f, CpuLoad.FromSamples(1000, 2000, 1600, 3000), 3);
        }

        [Theory]
        [InlineData(1000UL, 2000UL, 900UL, 3000UL)]  // idle went backwards
        [InlineData(1000UL, 2000UL, 1600UL, 1900UL)] // total went backwards
        public void FromSamples_RegressedCountersReportZeroInsteadOfASpike(
            ulong previousIdle, ulong previousTotal, ulong currentIdle, ulong currentTotal)
        {
            Assert.Equal(0f, CpuLoad.FromSamples(previousIdle, previousTotal, currentIdle, currentTotal));
        }

        [Fact]
        public void FromSamples_IdenticalSamplesReportZero()
        {
            Assert.Equal(0f, CpuLoad.FromSamples(1000, 2000, 1000, 2000));
        }
    }
}
