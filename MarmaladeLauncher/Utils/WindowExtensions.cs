using Avalonia.Controls;
using Avalonia.Media;

namespace MarmaladeLauncher.Utils;

public static class WindowExtensions {
    public static void SetRTL(this Window window, bool isRTL) {
        window.FlowDirection = isRTL ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }
}