using System;
using System.Collections.Generic;
using System.Drawing;

namespace SystemBarIndicator.Configuration
{
    /// <summary>
    /// Проверка и починка сохранённого положения окна: монитор мог быть отключён,
    /// а разрешение — измениться, и тогда бар оказался бы за пределами видимой области.
    /// Логика чистая (System.Drawing.Primitives, без WinForms) — покрыта тестами.
    /// </summary>
    public static class WindowPlacement
    {
        /// <summary>Сколько пикселей окна должно остаться видимым, чтобы за него можно было ухватиться.</summary>
        public const int MinVisible = 24;

        /// <summary>
        /// Виден ли бар настолько, чтобы им можно было пользоваться: хотя бы на одном экране
        /// пересечение шире <paramref name="minVisible"/> и покрывает половину высоты бара.
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
        /// Вдвигает бар целиком в тот экран, с которым он пересекается сильнее всего
        /// (если не пересекается ни с одним — в первый). Размер не меняется.
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

        /// <summary>Вдвигает прямоугольник внутрь заданного экрана; при нехватке места прижимает к левому/верхнему краю.</summary>
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
