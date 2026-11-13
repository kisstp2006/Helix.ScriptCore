using System;
using System.Collections.Generic;
using HelixEngine.Internal;

namespace HelixEngine
{
    /// <summary>Values on top of a renderer's materials (not an Object): its native block lives while this does.</summary>
    public sealed class MaterialPropertyBlock
    {
        internal PropertyBlockId m_Block;

        public MaterialPropertyBlock() => m_Block = PropertyBlockNative.Create();

        ~MaterialPropertyBlock() => PropertyBlockNative.Destroy(m_Block);

        public bool isEmpty => PropertyBlockNative.IsEmpty(m_Block);
        public void Clear() => PropertyBlockNative.Clear(m_Block);

        static int Id(string name) => Shader.PropertyToID(name);

        public void SetFloat(string name, float value) => SetFloat(Id(name), value);
        public void SetFloat(int nameID, float value) => PropertyBlockNative.SetFloat(m_Block, nameID, value);
        public void SetInt(string name, int value) => SetInt(Id(name), value);
        public void SetInt(int nameID, int value) => PropertyBlockNative.SetInt(m_Block, nameID, value);
        public void SetInteger(string name, int value) => SetInteger(Id(name), value);
        public void SetInteger(int nameID, int value) => PropertyBlockNative.SetInteger(m_Block, nameID, value);
        public void SetColor(string name, Color value) => SetColor(Id(name), value);
        public void SetColor(int nameID, Color value) => PropertyBlockNative.SetColor(m_Block, nameID, value);
        public void SetVector(string name, Vector4 value) => SetVector(Id(name), value);
        public void SetVector(int nameID, Vector4 value) => PropertyBlockNative.SetVector(m_Block, nameID, value);
        public void SetMatrix(string name, Matrix4x4 value) => SetMatrix(Id(name), value);
        public void SetMatrix(int nameID, Matrix4x4 value) => PropertyBlockNative.SetMatrix(m_Block, nameID, value);
        public void SetTexture(string name, Texture value) => SetTexture(Id(name), value);
        public void SetTexture(int nameID, Texture value) => PropertyBlockNative.SetTexture(m_Block, nameID, value);
        public void SetFloatArray(string name, float[] values) => SetFloatArray(Id(name), values);
        public void SetFloatArray(int nameID, float[] values) => PropertyBlockNative.SetFloatArray(m_Block, nameID, values);
        public void SetFloatArray(string name, List<float> values) => SetFloatArray(Id(name), values.ToArray());
        public void SetFloatArray(int nameID, List<float> values) => SetFloatArray(nameID, values.ToArray());
        public void SetVectorArray(string name, Vector4[] values) => SetVectorArray(Id(name), values);
        public void SetVectorArray(int nameID, Vector4[] values) => PropertyBlockNative.SetVectorArray(m_Block, nameID, values);
        public void SetVectorArray(string name, List<Vector4> values) => SetVectorArray(Id(name), values.ToArray());
        public void SetVectorArray(int nameID, List<Vector4> values) => SetVectorArray(nameID, values.ToArray());
        public void SetMatrixArray(string name, Matrix4x4[] values) => SetMatrixArray(Id(name), values);
        public void SetMatrixArray(int nameID, Matrix4x4[] values) => PropertyBlockNative.SetMatrixArray(m_Block, nameID, values);
        public void SetMatrixArray(string name, List<Matrix4x4> values) => SetMatrixArray(Id(name), values.ToArray());
        public void SetMatrixArray(int nameID, List<Matrix4x4> values) => SetMatrixArray(nameID, values.ToArray());

        public float GetFloat(string name) => GetFloat(Id(name));
        public float GetFloat(int nameID) => PropertyBlockNative.GetFloat(m_Block, nameID);
        public int GetInt(string name) => GetInt(Id(name));
        public int GetInt(int nameID) => PropertyBlockNative.GetInt(m_Block, nameID);
        public int GetInteger(string name) => GetInteger(Id(name));
        public int GetInteger(int nameID) => PropertyBlockNative.GetInteger(m_Block, nameID);
        public Color GetColor(string name) => GetColor(Id(name));
        public Color GetColor(int nameID) => PropertyBlockNative.GetColor(m_Block, nameID);
        public Vector4 GetVector(string name) => GetVector(Id(name));
        public Vector4 GetVector(int nameID) => PropertyBlockNative.GetVector(m_Block, nameID);
        public Matrix4x4 GetMatrix(string name) => GetMatrix(Id(name));
        public Matrix4x4 GetMatrix(int nameID) => PropertyBlockNative.GetMatrix(m_Block, nameID);
        public Texture GetTexture(string name) => GetTexture(Id(name));
        public Texture GetTexture(int nameID) => PropertyBlockNative.GetTexture(m_Block, nameID);
        public float[] GetFloatArray(string name) => GetFloatArray(Id(name));
        public float[] GetFloatArray(int nameID) => PropertyBlockNative.GetFloatArray(m_Block, nameID);
        public void GetFloatArray(string name, List<float> values) => Fill(values, GetFloatArray(name));
        public void GetFloatArray(int nameID, List<float> values) => Fill(values, GetFloatArray(nameID));
        public Vector4[] GetVectorArray(string name) => GetVectorArray(Id(name));
        public Vector4[] GetVectorArray(int nameID) => PropertyBlockNative.GetVectorArray(m_Block, nameID);
        public void GetVectorArray(string name, List<Vector4> values) => Fill(values, GetVectorArray(name));
        public void GetVectorArray(int nameID, List<Vector4> values) => Fill(values, GetVectorArray(nameID));
        public Matrix4x4[] GetMatrixArray(string name) => GetMatrixArray(Id(name));
        public Matrix4x4[] GetMatrixArray(int nameID) => PropertyBlockNative.GetMatrixArray(m_Block, nameID);
        public void GetMatrixArray(string name, List<Matrix4x4> values) => Fill(values, GetMatrixArray(name));
        public void GetMatrixArray(int nameID, List<Matrix4x4> values) => Fill(values, GetMatrixArray(nameID));

        public bool HasProperty(string name) => HasProperty(Id(name));
        public bool HasProperty(int nameID) => PropertyBlockNative.HasProperty(m_Block, nameID);
        public bool HasFloat(string name) => HasFloat(Id(name));
        public bool HasFloat(int nameID) => PropertyBlockNative.HasFloat(m_Block, nameID);
        public bool HasInt(string name) => HasFloat(name);
        public bool HasInt(int nameID) => HasFloat(nameID);
        public bool HasInteger(string name) => HasInteger(Id(name));
        public bool HasInteger(int nameID) => PropertyBlockNative.HasInteger(m_Block, nameID);
        public bool HasColor(string name) => HasVector(name);
        public bool HasColor(int nameID) => HasVector(nameID);
        public bool HasVector(string name) => HasVector(Id(name));
        public bool HasVector(int nameID) => PropertyBlockNative.HasVector(m_Block, nameID);
        public bool HasMatrix(string name) => HasMatrix(Id(name));
        public bool HasMatrix(int nameID) => PropertyBlockNative.HasMatrix(m_Block, nameID);
        public bool HasTexture(string name) => HasTexture(Id(name));
        public bool HasTexture(int nameID) => PropertyBlockNative.HasTexture(m_Block, nameID);

        static void Fill<T>(List<T> to, T[] from)
        {
            to.Clear();
            if (from != null)
                to.AddRange(from);
        }
    }

    // The List overloads of Material.
    public partial class Material
    {
        public void SetFloatArray(string name, List<float> values) => SetFloatArray(name, values.ToArray());
        public void SetFloatArray(int nameID, List<float> values) => SetFloatArray(nameID, values.ToArray());
        public void SetColorArray(string name, List<Color> values) => SetColorArray(name, values.ToArray());
        public void SetColorArray(int nameID, List<Color> values) => SetColorArray(nameID, values.ToArray());
        public void SetVectorArray(string name, List<Vector4> values) => SetVectorArray(name, values.ToArray());
        public void SetVectorArray(int nameID, List<Vector4> values) => SetVectorArray(nameID, values.ToArray());
        public void SetMatrixArray(string name, List<Matrix4x4> values) => SetMatrixArray(name, values.ToArray());
        public void SetMatrixArray(int nameID, List<Matrix4x4> values) => SetMatrixArray(nameID, values.ToArray());
        public void GetFloatArray(string name, List<float> values) => Fill(values, GetFloatArray(name));
        public void GetFloatArray(int nameID, List<float> values) => Fill(values, GetFloatArray(nameID));
        public void GetColorArray(string name, List<Color> values) => Fill(values, GetColorArray(name));
        public void GetColorArray(int nameID, List<Color> values) => Fill(values, GetColorArray(nameID));
        public void GetVectorArray(string name, List<Vector4> values) => Fill(values, GetVectorArray(name));
        public void GetVectorArray(int nameID, List<Vector4> values) => Fill(values, GetVectorArray(nameID));
        public void GetMatrixArray(string name, List<Matrix4x4> values) => Fill(values, GetMatrixArray(name));
        public void GetMatrixArray(int nameID, List<Matrix4x4> values) => Fill(values, GetMatrixArray(nameID));
        public void GetTexturePropertyNames(List<string> outNames) => Fill(outNames, GetTexturePropertyNames());
        public void GetTexturePropertyNameIDs(List<int> outNames) => Fill(outNames, GetTexturePropertyNameIDs());

        static void Fill<T>(List<T> to, T[] from)
        {
            to.Clear();
            if (from != null)
                to.AddRange(from);
        }
    }
}
