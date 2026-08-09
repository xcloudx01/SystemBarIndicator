using System;
using System.Collections.Generic;
using System.Drawing;

namespace SystemBarIndicator.Configuration
{
    /// <summary>
    /// Validates and repairs a saved window position: a monitor may have been unplugged or
    /// the resolution changed, which would leave the bar outside the visible area.
    /// The logic is pure (System.Drawing.Primitives, no WinForms) and is covered by tests.
    /// </summary>
    public static class WindowPlacement
    {
        /// <summary>How many pixels of the window must stay visible for it to be grabbable.</summary>
        public const int MinVisible = 24;

        /// <summary>
        /// Whether the bar is visible enough to be usable: on at least one screen the
        /// intersection is wider than <paramref name="minVisible"/> and covers half its height.
        /// </summary>
        public static bool IsUsable(Rectangle bar, IReadOnlyList<Rectangle> screens, int minVisible = MinVisible)
        {
            if (screens == null || screens.Count == 0 || bar.Width <= 0 || bar.Height <= 0)
                return false;

            int needHeight = Math.Max(1, Math.Min(bar.Height, bar.Height / 2));
            int needWidth = Math.Min(minVisible, bar.Width);

            foreach (var screen in screens)
            {
                var hit = Rectangle.Intersect(bar, screen);
                if (hit.Width >= needWidth && hit.Height >= needHeight)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Moves the bar fully into the screen it overlaps most (or the first screen if it
        /// overlaps none). The size is unchanged.
        /// </summary>
        public static Rectangle ClampToScreens(Rectangle bar, IReadOnlyList<Rectangle> screens)
        {
            if (screens == null || screens.Count == 0)
                return bar;

            Rectangle target = screens[0];
            long bestArea = -1;
            foreach (var screen in screens)
            {
                var hit = Rectangle.Intersect(bar, screen);
                long area = (long)Math.Max(hit.Width, 0) * Math.Max(hit.Height, 0);
                if (area > bestArea)
                {
                    bestArea = area;
                    target = screen;
                }
            }

            return ClampTo(bar, target);
        }

        /// <summary>Moves a rectangle inside the given screen; if it does not fit, pins it to the left/top edge.</summary>
        public static Rectangle ClampTo(Rectangle bar, Rectangle screen)
        {
            int x = bar.X;
            int y = bar.Y;

            if (bar.Right > screen.Right) x = screen.Right - bar.Width;
            if (x < screen.Left) x = screen.Left;

            if (bar.Bottom > screen.Bottom) y = screen.Bottom - bar.Height;
            if (y < screen.Top) y = screen.Top;

            return new Rectangle(x, y, bar.Width, bar.Height);
        }
    }
}
