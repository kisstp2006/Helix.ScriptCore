using System;
using HelixEngine.Internal;

namespace HelixEngine
{
    /// <summary>A set of layers as a bit mask (bit n: layer n), as cameras, lights and volumes take them.</summary>
    public struct LayerMask
    {
        int m_Mask;

        public static implicit operator int(LayerMask mask) => mask.m_Mask;
        public static implicit operator LayerMask(int intVal) => new LayerMask { m_Mask = intVal };

        public int value { get => m_Mask; set => m_Mask = value; }

        /// <summary>The name of a layer ("" for one without a name).</summary>
        public static string LayerToName(int layer) => LayersNative.LayerToName(layer);

        /// <summary>The layer of a name; -1 when no layer has it.</summary>
        public static int NameToLayer(string layerName) => LayersNative.NameToLayer(layerName ?? "");

        /// <summary>The mask of the named layers (names no layer has are left out).</summary>
        public static int GetMask(params string[] layerNames)
        {
            if (layerNames == null)
                throw new ArgumentNullException(nameof(layerNames));
            int mask = 0;
            foreach (string name in layerNames)
            {
                int layer = NameToLayer(name);
                if (layer != -1)
                    mask |= 1 << layer;
            }
            return mask;
        }
    }
}
