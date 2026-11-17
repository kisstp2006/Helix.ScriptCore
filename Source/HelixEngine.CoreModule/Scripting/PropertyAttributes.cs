using System;

namespace HelixEngine
{
    /// <summary>The color space colors are given in (Gamma: sRGB values; Linear: linear values).</summary>
    public enum ColorSpace
    {
        Uninitialized = -1,
        Gamma = 0,
        Linear = 1,
    }

    /// <summary>The base of the attributes that tell the Inspector how to show a serialized field (its drawers and
    /// decorators run in their order).</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public abstract class PropertyAttribute : Attribute
    {
        protected PropertyAttribute() : this(false) { }

        protected PropertyAttribute(bool applyToCollection) => this.applyToCollection = applyToCollection;

        public int order { get; set; }

        /// <summary>Whether the attribute applies to a list or array field as a whole (not to each element).</summary>
        public bool applyToCollection { get; }
    }

    /// <summary>A number field shown as a slider between min and max.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class RangeAttribute : PropertyAttribute
    {
        public readonly float min;
        public readonly float max;

        public RangeAttribute(float min, float max)
        {
            this.min = min;
            this.max = max;
        }
    }

    /// <summary>A number or vector field kept at min or more.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class MinAttribute : PropertyAttribute
    {
        public readonly float min;

        public MinAttribute(float min) => this.min = min;
    }

    /// <summary>A string field edited in a box of several lines.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class MultilineAttribute : PropertyAttribute
    {
        public readonly int lines;

        public MultilineAttribute() => lines = 3;

        public MultilineAttribute(int lines) => this.lines = lines;
    }

    /// <summary>A string field edited in a text area that grows from minLines to maxLines, then scrolls.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class TextAreaAttribute : PropertyAttribute
    {
        public readonly int minLines;
        public readonly int maxLines;

        public TextAreaAttribute()
        {
            minLines = 3;
            maxLines = 3;
        }

        public TextAreaAttribute(int minLines, int maxLines)
        {
            this.minLines = minLines;
            this.maxLines = maxLines;
        }
    }

    /// <summary>The tooltip of a field's label.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class TooltipAttribute : PropertyAttribute
    {
        public readonly string tooltip;

        public TooltipAttribute(string tooltip) => this.tooltip = tooltip;
    }

    /// <summary>A header above a field.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = true)]
    public class HeaderAttribute : PropertyAttribute
    {
        public readonly string header;

        public HeaderAttribute(string header) => this.header = header;
    }

    /// <summary>Room above a field (8 pixels by default).</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct, Inherited = true, AllowMultiple = true)]
    public class SpaceAttribute : PropertyAttribute
    {
        public readonly float height;

        public SpaceAttribute() => height = 8;

        public SpaceAttribute(float height) => this.height = height;
    }

    /// <summary>How a color field's picker shows: with its alpha, and HDR values.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class ColorUsageAttribute : PropertyAttribute
    {
        public readonly bool showAlpha = true;
        public readonly bool hdr;

        public ColorUsageAttribute(bool showAlpha) => this.showAlpha = showAlpha;

        public ColorUsageAttribute(bool showAlpha, bool hdr)
        {
            this.showAlpha = showAlpha;
            this.hdr = hdr;
        }
    }

    /// <summary>How a gradient field's editor shows: HDR colors, and the color space they are shown in.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class GradientUsageAttribute : PropertyAttribute
    {
        public readonly bool hdr;
        public readonly ColorSpace colorSpace = ColorSpace.Gamma;

        public GradientUsageAttribute(bool hdr) => this.hdr = hdr;

        public GradientUsageAttribute(bool hdr, ColorSpace colorSpace)
        {
            this.hdr = hdr;
            this.colorSpace = colorSpace;
        }
    }

    /// <summary>An entry of a field's context menu that calls a method of the script by name.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = true)]
    public class ContextMenuItemAttribute : PropertyAttribute
    {
        public readonly string name;
        public readonly string function;

        public ContextMenuItemAttribute(string name, string function)
        {
            this.name = name;
            this.function = function;
        }
    }

    /// <summary>A number or text field whose value is set when its editing ends (not with each key).</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class DelayedAttribute : PropertyAttribute
    {
    }

    /// <summary>The name an enum value shows in the Inspector.</summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public class InspectorNameAttribute : PropertyAttribute
    {
        public readonly string displayName;

        public InspectorNameAttribute(string displayName) => this.displayName = displayName;
    }

    /// <summary>The displays an OnGui method draws on (all by default).</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class GuiTargetAttribute : Attribute
    {
        internal int displayMask;

        public GuiTargetAttribute() => displayMask = -1;

        public GuiTargetAttribute(int displayIndex) => displayMask = 1 << displayIndex;

        public GuiTargetAttribute(int displayIndex, int displayIndex1) => displayMask = (1 << displayIndex) | (1 << displayIndex1);

        public GuiTargetAttribute(int displayIndex, int displayIndex1, params int[] displayIndexList)
        {
            displayMask = (1 << displayIndex) | (1 << displayIndex1);
            foreach (int i in displayIndexList)
                displayMask |= 1 << i;
        }
    }
}
