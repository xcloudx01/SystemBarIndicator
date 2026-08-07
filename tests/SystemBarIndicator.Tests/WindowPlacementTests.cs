using System.Collections.Generic;
using System.Drawing;
using SystemBarIndicator.Configuration;
using Xunit;

namespace SystemBarIndicator.Tests
{
    public class WindowPlacementTests
    {
        private static readonly Rectangle Primary = new Rectangle(0, 0, 1920, 1080);
        private static readonly Rectangle Secondary = new Rectangle(1920, 0, 1920, 1080);
        private static readonly IReadOnlyList<Rectangle> OneScreen = new[] { Primary };
        private static readonly IReadOnlyList<Rectangle> TwoScreens = new[] { Primary, Secondary };

        [Fact]
        public void IsUsable_FullyOnScreen()
        {
            Assert.True(WindowPlacement.IsUsable(new Rectangle(1400, 1040, 500, 32), OneScreen));
        }

        [Fact]
        public void IsUsable_OnSecondScreen()
        {
            Assert.True(WindowPlacement.IsUsable(new Rectangle(3300, 1040, 500, 32), TwoScreens));
        }

        [Fact]
        public void IsUsable_FalseWhenSecondScreenDisconnected()
        {
            // Позиция сохранялась на втором мониторе, а он отключён — восстанавливать нельзя.
            Assert.False(WindowPlacement.IsUsable(new Rectangle(3300, 1040, 500, 32), OneScreen));
        }

        [Fact]
        public void IsUsable_FalseWhenBarelyPeeking()
        {
            // Видно 5 px по горизонтали — ухватиться мышью нереально.
            Assert.False(WindowPlacement.IsUsable(new Rectangle(-495, 500, 500, 32), OneScreen));
        }

        [Fact]
        public void IsUsable_TrueAtVisibilityThreshold()
        {
            // Ровно MinVisible пикселей у левого края экрана.
            Assert.True(WindowPlacement.IsUsable(new Rectangle(-500 + WindowPlacement.MinVisible, 500, 500, 32), OneScreen));
        }

        [Fact]
        public void IsUsable_FalseWhenMostlyBelowScreen()
        {
            // По вертикали видно меньше половины высоты бара.
            Assert.False(WindowPlacement.IsUsable(new Rectangle(100, 1070, 500, 32), OneScreen));
        }

        [Fact]
        public void IsUsable_FalseForEmptyScreenListOrDegenerateBar()
        {
            Assert.False(WindowPlacement.IsUsable(new Rectangle(0, 0, 500, 32), new List<Rectangle>()));
            Assert.False(WindowPlacement.IsUsable(new Rectangle(0, 0, 0, 32), OneScreen));
            Assert.False(WindowPlacement.IsUsable(new Rectangle(0, 0, 500, 0), OneScreen));
        }

        [Fact]
        public void ClampToScreens_PullsBarBackInside()
        {
            var clamped = WindowPlacement.ClampToScreens(new Rectangle(1800, 1070, 500, 32), OneScreen);

            Assert.Equal(new Rectangle(1420, 1048, 500, 32), clamped);
            Assert.True(WindowPlacement.IsUsable(clamped, OneScreen));
        }

        [Fact]
        public void ClampToScreens_KeepsBarThatAlreadyFits()
        {
            var bar = new Rectangle(100, 200, 500, 32);
            Assert.Equal(bar, WindowPlacement.ClampToScreens(bar, OneScreen));
        }

        [Fact]
        public void ClampToScreens_UsesScreenWithLargestOverlap()
        {
            // Бар преимущественно на втором мониторе — вдвигаем именно в него.
            var clamped = WindowPlacement.ClampToScreens(new Rectangle(3600, 1070, 500, 32), TwoScreens);

            Assert.True(clamped.X >= Secondary.Left);
            Assert.True(clamped.Right <= Secondary.Right);
            Assert.True(clamped.Bottom <= Secondary.Bottom);
        }

        [Fact]
        public void ClampToScreens_WithoutOverlapFallsBackToFirstScreen()
        {
            var clamped = WindowPlacement.ClampToScreens(new Rectangle(-5000, -5000, 500, 32), TwoScreens);

            Assert.Equal(new Rectangle(0, 0, 500, 32), clamped);
        }

        [Fact]
        public void ClampToScreens_NoScreensLeavesBarAsIs()
        {
            var bar = new Rectangle(10, 20, 500, 32);
            Assert.Equal(bar, WindowPlacement.ClampToScreens(bar, new List<Rectangle>()));
        }

        [Fact]
        public void ClampTo_BarWiderThanScreenIsPinnedToLeftEdge()
        {
            var clamped = WindowPlacement.ClampTo(new Rectangle(100, 10, 3000, 32), Primary);

            Assert.Equal(Primary.Left, clamped.X);
            Assert.Equal(3000, clamped.Width);
        }

        [Fact]
        public void ClampToScreens_HandlesNegativeCoordinateScreen()
        {
            // Монитор слева от основного: координаты отрицательные.
            var screens = new[] { Primary, new Rectangle(-1920, 0, 1920, 1080) };

            var clamped = WindowPlacement.ClampToScreens(new Rectangle(-2100, 500, 500, 32), screens);

            Assert.Equal(-1920, clamped.X);
            Assert.True(WindowPlacement.IsUsable(clamped, screens));
        }
    }
}
