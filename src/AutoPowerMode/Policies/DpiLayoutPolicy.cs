using System.Drawing;

namespace AutoPowerMode;

internal readonly record struct DpiLayoutMetrics(
    Size InitialClientSize,
    Size MinimumClientSize,
    Size MaximumClientSize);

internal static class DpiLayoutPolicy
{
    internal const int BaseDpi = 96;
    internal const int InitialClientWidth = 460;
    internal const int InitialClientHeight = 270;
    internal const int MinimumClientWidth = 400;
    internal const int MinimumClientHeight = 240;
    internal const int WorkingAreaMargin = 12;

    public static DpiLayoutMetrics Calculate(int dpi, Size workingArea, Size nonClientSize)
    {
        var effectiveDpi = Math.Max(BaseDpi, dpi);
        var scaledMargin = Scale(WorkingAreaMargin, effectiveDpi);
        var maximumClientWidth = Math.Max(
            1,
            workingArea.Width - (scaledMargin * 2) - Math.Max(0, nonClientSize.Width));
        var maximumClientHeight = Math.Max(
            1,
            workingArea.Height - (scaledMargin * 2) - Math.Max(0, nonClientSize.Height));
        var maximumClientSize = new Size(maximumClientWidth, maximumClientHeight);

        var minimumClientSize = new Size(
            Math.Min(Scale(MinimumClientWidth, effectiveDpi), maximumClientWidth),
            Math.Min(Scale(MinimumClientHeight, effectiveDpi), maximumClientHeight));
        var initialClientSize = new Size(
            Math.Clamp(Scale(InitialClientWidth, effectiveDpi), minimumClientSize.Width, maximumClientWidth),
            Math.Clamp(Scale(InitialClientHeight, effectiveDpi), minimumClientSize.Height, maximumClientHeight));

        return new DpiLayoutMetrics(initialClientSize, minimumClientSize, maximumClientSize);
    }

    public static Size FitInitialClientSize(DpiLayoutMetrics metrics, Size preferredContentSize)
    {
        return new Size(
            Math.Clamp(
                Math.Max(metrics.InitialClientSize.Width, preferredContentSize.Width),
                metrics.MinimumClientSize.Width,
                metrics.MaximumClientSize.Width),
            Math.Clamp(
                Math.Max(metrics.InitialClientSize.Height, preferredContentSize.Height),
                metrics.MinimumClientSize.Height,
                metrics.MaximumClientSize.Height));
    }

    internal static int Scale(int logicalPixels, int dpi)
    {
        return Math.Max(1, (int)Math.Round(logicalPixels * dpi / (double)BaseDpi));
    }
}
