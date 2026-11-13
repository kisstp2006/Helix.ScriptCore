using System;
using System.Collections.Generic;
using HelixEngine.Internal;

namespace HelixEngine.Rendering
{
    /// <summary>A keyword of a shader's keyword space.</summary>
    public partial struct LocalKeyword : IEquatable<LocalKeyword>
    {
        /// <summary>The keyword of that name in the shader's space; an error is logged when there is none.</summary>
        public LocalKeyword(Shader shader, string name) => this = KeywordNative.Find(shader, name);

        public string name => KeywordNative.Name(this);
        public bool isValid => KeywordNative.IsValid(this);
        public bool isDynamic => KeywordNative.IsDynamic(this);
        public bool isOverridable => KeywordNative.IsOverridable(this);
        public ShaderKeywordType type => isValid ? ShaderKeywordType.UserDefined : ShaderKeywordType.None;

        public static bool operator ==(LocalKeyword lhs, LocalKeyword rhs) => lhs.Equals(rhs);
        public static bool operator !=(LocalKeyword lhs, LocalKeyword rhs) => !lhs.Equals(rhs);
        public bool Equals(LocalKeyword rhs) => m_Shader == rhs.m_Shader && m_Index == rhs.m_Index;
        public override bool Equals(object o) => o is LocalKeyword k && Equals(k);
        public override int GetHashCode() => HashCode.Combine(m_Shader, m_Index);
        public override string ToString() => name;
    }

    /// <summary>A global shader keyword.</summary>
    public partial struct GlobalKeyword
    {
        /// <summary>The global keyword of that name.</summary>
        public GlobalKeyword(string name) => this = KeywordNative.Global(name);

        /// <summary>The global keyword of that name, added to the list when it is not there.</summary>
        public static GlobalKeyword Create(string name) => KeywordNative.Global(name);

        public string name => KeywordNative.GlobalName(this);
        public override string ToString() => name;
    }

    /// <summary>The local keywords of a shader.</summary>
    public partial struct LocalKeywordSpace : IEquatable<LocalKeywordSpace>
    {
        public uint keywordCount => KeywordNative.SpaceCount(this);

        public LocalKeyword[] keywords
        {
            get
            {
                var all = new LocalKeyword[keywordCount];
                for (uint i = 0; i < all.Length; i++)
                    all[i] = KeywordNative.SpaceAt(this, i);
                return all;
            }
        }

        public string[] keywordNames
        {
            get
            {
                LocalKeyword[] all = keywords;
                var names = new string[all.Length];
                for (int i = 0; i < all.Length; i++)
                    names[i] = all[i].name;
                return names;
            }
        }

        /// <summary>The keyword of that name; isValid is false when it is not in the space.</summary>
        public LocalKeyword FindKeyword(string name) => KeywordNative.SpaceFind(this, name);

        public static bool operator ==(LocalKeywordSpace lhs, LocalKeywordSpace rhs) => lhs.Equals(rhs);
        public static bool operator !=(LocalKeywordSpace lhs, LocalKeywordSpace rhs) => !lhs.Equals(rhs);
        public bool Equals(LocalKeywordSpace rhs) => m_Shader == rhs.m_Shader;
        public override bool Equals(object o) => o is LocalKeywordSpace s && Equals(s);
        public override int GetHashCode() => m_Shader.GetHashCode();
    }

    /// <summary>A shader tag name or value.</summary>
    public partial struct ShaderTagId : IEquatable<ShaderTagId>
    {
        public static readonly ShaderTagId none = default;

        public ShaderTagId(string name) => this = KeywordNative.Tag(name);

        public string name => KeywordNative.TagName(this);

        public static explicit operator ShaderTagId(string name) => new ShaderTagId(name);
        public static explicit operator string(ShaderTagId tagId) => tagId.name;
        public static bool operator ==(ShaderTagId tag1, ShaderTagId tag2) => tag1.m_Id == tag2.m_Id;
        public static bool operator !=(ShaderTagId tag1, ShaderTagId tag2) => tag1.m_Id != tag2.m_Id;
        public bool Equals(ShaderTagId other) => m_Id == other.m_Id;
        public override bool Equals(object obj) => obj is ShaderTagId t && Equals(t);
        public override int GetHashCode() => m_Id;
    }
}

namespace HelixEngine
{
    // The List overloads of the global arrays.
    public partial class Shader
    {
        public static void SetGlobalFloatArray(string name, List<float> values) => SetGlobalFloatArray(name, values.ToArray());
        public static void SetGlobalFloatArray(int nameID, List<float> values) => SetGlobalFloatArray(nameID, values.ToArray());
        public static void SetGlobalVectorArray(string name, List<Vector4> values) => SetGlobalVectorArray(name, values.ToArray());
        public static void SetGlobalVectorArray(int nameID, List<Vector4> values) => SetGlobalVectorArray(nameID, values.ToArray());
        public static void SetGlobalMatrixArray(string name, List<Matrix4x4> values) => SetGlobalMatrixArray(name, values.ToArray());
        public static void SetGlobalMatrixArray(int nameID, List<Matrix4x4> values) => SetGlobalMatrixArray(nameID, values.ToArray());

        public static void GetGlobalFloatArray(string name, List<float> values) => Fill(values, GetGlobalFloatArray(name));
        public static void GetGlobalFloatArray(int nameID, List<float> values) => Fill(values, GetGlobalFloatArray(nameID));
        public static void GetGlobalVectorArray(string name, List<Vector4> values) => Fill(values, GetGlobalVectorArray(name));
        public static void GetGlobalVectorArray(int nameID, List<Vector4> values) => Fill(values, GetGlobalVectorArray(nameID));
        public static void GetGlobalMatrixArray(string name, List<Matrix4x4> values) => Fill(values, GetGlobalMatrixArray(name));
        public static void GetGlobalMatrixArray(int nameID, List<Matrix4x4> values) => Fill(values, GetGlobalMatrixArray(nameID));

        static void Fill<T>(List<T> to, T[] from)
        {
            to.Clear();
            if (from != null)
                to.AddRange(from);
        }
    }
}
