using System;

using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace WLMClient.Config
{
    /// <summary>
    /// The colours the application paints itself with. The built in controls follow Avalonia's own
    /// light and dark variants; everything this application draws by hand reads its colours here.
    /// </summary>
    static class Theme
    {
        private static bool isDark;

        /// <summary>Raised after the theme changes so open windows can repaint.</summary>
        public static event Action Changed;

        public static bool IsDark
        {
            get { return isDark; }
        }

        /// <summary>Switches theme and tells the rest of the interface to repaint.</summary>
        public static void Load(bool dark)
        {
            isDark = dark;

            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            }

            Action handler = Changed;

            if (handler != null)
            {
                handler();
            }
        }

        private static IBrush Pick(uint light, uint dark)
        {
            return new SolidColorBrush(Color.FromUInt32(isDark ? dark : light));
        }

        private static Color PickColor(uint light, uint dark)
        {
            return Color.FromUInt32(isDark ? dark : light);
        }

        #region Surfaces

        /// <summary>Behind the contact list and the chat transcript.</summary>
        public static IBrush ListBackground { get { return Pick(0xFFFCFCFC, 0xFF232428); } }

        /// <summary>Chat windows and dialogs.</summary>
        public static IBrush WindowBackground { get { return Pick(0xFFFFFFFF, 0xFF26282C); } }

        /// <summary>The message composing area at the foot of a conversation.</summary>
        public static IBrush ComposeBackground { get { return Pick(0xFFF9F9F9, 0xFF2A2C31); } }

        public static IBrush DialogBackground { get { return Pick(0xFFFFFFFF, 0xFF26282C); } }

        /// <summary>Hairlines between areas.</summary>
        public static IBrush Separator { get { return Pick(0xFFDDDDDD, 0xFF3A3D42); } }

        #endregion

        #region Text

        public static IBrush TextPrimary { get { return Pick(0xFF333333, 0xFFE6E6E6); } }

        public static IBrush TextSecondary { get { return Pick(0xFF888888, 0xFF9A9A9A); } }

        /// <summary>Headings and links.</summary>
        public static IBrush Accent { get { return Pick(0xFF355A88, 0xFF7FB3E8); } }

        public static IBrush VersionText { get { return Pick(0xFF6C8A9C, 0xFF6F7C86); } }

        /// <summary>The "X says" line above a message.</summary>
        public static IBrush ChatFrom { get { return Pick(0xFFA5A5A5, 0xFF8F8F8F); } }

        public static IBrush ChatText { get { return Pick(0xFF000000, 0xFFE6E6E6); } }

        public static IBrush NudgeText { get { return Pick(0xFF29292B, 0xFFD8D8D8); } }

        public static IBrush NudgeBorder { get { return Pick(0xFFEAEAEA, 0xFF3A3D42); } }

        /// <summary>Links inside messages.</summary>
        public static IBrush Link { get { return Pick(0xFF0000FF, 0xFF7FB3E8); } }

        /// <summary>Face of the ordinary dialog buttons.</summary>
        public static IBrush ButtonBackground { get { return Pick(0xFFF5F5F5, 0xFF34373D); } }

        /// <summary>The emphasised button, which the designer file drew in black.</summary>
        public static IBrush ButtonStrongBackground { get { return Pick(0xFF000000, 0xFF4A4F59); } }

        public static IBrush ButtonStrongForeground { get { return Brushes.White; } }

        #endregion

        #region Contact rows

        public static Color RowHoverFrom { get { return PickColor(0xFFEBF3FD, 0xFF2E3542); } }

        public static Color RowHoverTo { get { return PickColor(0xFFFCFDFE, 0xFF272B32); } }

        public static IBrush RowHoverBorder { get { return Pick(0xFFB8D6FB, 0xFF3E4C63); } }

        public static Color RowSelectedFrom { get { return PickColor(0xFFEBF4FE, 0xFF35455D); } }

        public static Color RowSelectedTo { get { return PickColor(0xFFCFE4FE, 0xFF2B3646); } }

        public static IBrush RowSelectedBorder { get { return Pick(0xFF84ACDD, 0xFF5A7BA8); } }

        #endregion

        #region Games

        public static IBrush GameCell { get { return Pick(0xFFFFFFFF, 0xFF2A2C31); } }

        public static IBrush GameCellHover { get { return Pick(0xFFEBF3FD, 0xFF2E3542); } }

        public static IBrush GameCellBorder { get { return Pick(0xFFB8D6FB, 0xFF3E4C63); } }

        /// <summary>Behind the three cells that won.</summary>
        public static IBrush GameHighlight { get { return Pick(0xFFFFF4C2, 0xFF4A4426); } }

        public static IBrush GameMarkX { get { return Pick(0xFF355A88, 0xFF7FB3E8); } }

        public static IBrush GameMarkO { get { return Pick(0xFFC0504D, 0xFFE8857F); } }

        #endregion

        /// <summary>
        /// Laid over the photographic header artwork in dark mode. The artwork is a fixed bitmap,
        /// so it is toned down rather than recoloured.
        /// </summary>
        public static IBrush ArtworkDimmer
        {
            get
            {
                return isDark
                    ? new SolidColorBrush(Color.FromArgb(0x8A, 0x12, 0x14, 0x18))
                    : Brushes.Transparent;
            }
        }

        /// <summary>The gradient filling the strip at the foot of the main window.</summary>
        public static IBrush FooterBackground
        {
            get
            {
                return Compat.BrushHelper.VerticalGradient(
                    PickColor(0xFFD9EBF4, 0xFF262A30),
                    PickColor(0xFFC5E3F0, 0xFF1F2227));
            }
        }

        public static IBrush FooterBorder { get { return Pick(0xFFC1D3E1, 0xFF3A3D42); } }

        /// <summary>
        /// The panel under the sign in form. It starts at the page background so there is no seam
        /// where it begins, and only picks up colour towards the very bottom of the window.
        /// </summary>
        public static IBrush LoginFooterBackground
        {
            get
            {
                return Compat.BrushHelper.VerticalGradient(
                    PickColor(0xFFFFFFFF, 0xFF26282C),
                    PickColor(0xFFC9E7F3, 0xFF1F2227));
            }
        }
    }
}
