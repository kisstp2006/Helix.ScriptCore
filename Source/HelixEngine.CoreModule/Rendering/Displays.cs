using System;
using HelixEngine.Internal;

namespace HelixEngine
{
    /// <summary>A display the player draws to: display 0 is the main window; the others open when activated (a window
    /// each), and cameras, canvases and UI panels draw to their targetDisplay.</summary>
    public class Display
    {
        public delegate void DisplaysUpdatedDelegate();

        static Display[] s_Displays = { new Display(0) };
        static bool s_Hooked;

        readonly int m_Index;

        Display(int index) { m_Index = index; }

        /// <summary>The displays known (the monitors the host reported, then the windows of tools).</summary>
        public static Display[] displays
        {
            get
            {
                Refresh();
                return s_Displays;
            }
        }

        public static Display main => displays[0];

        public static event DisplaysUpdatedDelegate onDisplaysUpdated
        {
            add
            {
                Refresh();
                s_Updated += value;
            }
            remove => s_Updated -= value;
        }

        static DisplaysUpdatedDelegate s_Updated;

        public bool active => DisplayNative.Active(m_Index);
        public int systemWidth => DisplayNative.SystemWidth(m_Index);
        public int systemHeight => DisplayNative.SystemHeight(m_Index);
        public int renderingWidth => DisplayNative.RenderingWidth(m_Index);
        public int renderingHeight => DisplayNative.RenderingHeight(m_Index);

        /// <summary>Opens the display's window at its own size.</summary>
        public void Activate() => DisplayNative.Activate(m_Index, 0, 0);

        public void Activate(int width, int height, RefreshRate refreshRate) => DisplayNative.Activate(m_Index, width, height);

        /// <summary>The display's window (windowed) at a size and a place on the desktop.</summary>
        public void SetParams(int width, int height, int x, int y) => DisplayNative.SetParams(m_Index, width, height, x, y);

        public void SetRenderingResolution(int w, int h) => DisplayNative.SetRenderingResolution(m_Index, w, h);

        /// <summary>The display under a point of the desktop and the point in it: x and y in the display, z its index
        /// (-1 when no display holds the point).</summary>
        public static Vector3 RelativeMouseAt(Vector3 inputMouseCoordinates) => DisplayNative.RelativeMouseAt(inputMouseCoordinates);

        static void Refresh()
        {
            if (!s_Hooked)
            {
                s_Hooked = true;
                DisplayNative.displaysUpdated += Updated;
            }
            int count = Math.Max(1, DisplayNative.Count());
            if (count == s_Displays.Length)
                return;
            var made = new Display[count];
            for (int i = 0; i < count; i++)
                made[i] = i < s_Displays.Length ? s_Displays[i] : new Display(i);
            s_Displays = made;
        }

        // The engine's Displays changed (a window opened, closed or resized).
        internal static void Updated()
        {
            Refresh();
            s_Updated?.Invoke();
        }
    }
}
