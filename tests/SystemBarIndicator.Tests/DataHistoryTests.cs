using SystemBarIndicator.Models;
using Xunit;

namespace SystemBarIndicator.Tests
{
    public class DataHistoryTests
    {
        private static DataPoint Point(float cpu) => new DataPoint { CpuUsage = cpu };

        [Fact]
        public void NewHistory_IsEmpty()
        {
            var history = new DataHistory(10);
            Assert.Equal(0, history.Count);
            Assert.Empty(history.GetPoints());
        }

        [Fact]
        public void Add_BelowCapacity_KeepsAllPoints()
        {
            var history = new DataHistory(5);
            history.Add(Point(1));
            history.Add(Point(2));

            Assert.Equal(2, history.Count);
            var points = history.GetPoints();
            Assert.Equal(1, points[0].CpuUsage);
            Assert.Equal(2, points[1].CpuUsage);
        }

        [Fact]
        public void Add_BeyondCapacity_EvictsOldestAndKeepsOrder()
        {
            var history = new DataHistory(3);
            for (int i = 1; i <= 5; i++)
                history.Add(Point(i));

            Assert.Equal(3, history.Count);
            var points = history.GetPoints();
            Assert.Equal(3, points[0].CpuUsage); // 1 and 2 were evicted
            Assert.Equal(4, points[1].CpuUsage);
            Assert.Equal(5, points[2].CpuUsage);
        }

        [Fact]
        public void GetPoints_ReturnsIndependentSnapshot()
        {
            var history = new DataHistory(10);
            history.Add(Point(1));

            var snapshot = history.GetPoints();
            history.Add(Point(2));

            Assert.Single(snapshot); // the snapshot does not change when more points are added
            Assert.Equal(2, history.Count);
        }

        [Fact]
        public void Clear_RemovesAllPoints()
        {
            var history = new DataHistory(10);
            history.Add(Point(1));
            history.Add(Point(2));

            history.Clear();

            Assert.Equal(0, history.Count);
            Assert.Empty(history.GetPoints());
        }
    }
}
