using System;

namespace HelixEngine
{
    /// <summary>The order a script class's messages run in among the others (lower first; 0 by default).</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DefaultExecutionOrder : Attribute
    {
        public DefaultExecutionOrder(int order) => this.order = order;

        public int order { get; }
    }

    /// <summary>A private field that scene and prefab files hold (public fields are held without it).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute
    {
    }

    /// <summary>A field holding a reference to an object of a Serializable class (not an Object, not a value type):
    /// null, objects of derived classes, one object in several fields and cycles within the host object.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeReference : Attribute
    {
    }

    /// <summary>Called around serialization: OnBeforeSerialize just before an object is written, OnAfterDeserialize
    /// after its fields are read (the host's before those of its managed references).</summary>
    public interface ISerializationCallbackReceiver
    {
        void OnBeforeSerialize();
        void OnAfterDeserialize();
    }

    /// <summary>A serialized field the Inspector does not show (it is still serialized).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class HideInInspector : Attribute
    {
    }

    /// <summary>A member of an object that was destroyed (its C# object is still referenced).</summary>
    public class MissingReferenceException : SystemException
    {
        public MissingReferenceException() { }
        public MissingReferenceException(string message) : base(message) { }
        public MissingReferenceException(string message, Exception innerException) : base(message, innerException) { }
    }
}
