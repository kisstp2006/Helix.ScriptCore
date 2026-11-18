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

    /// <summary>The components a script class needs: adding it adds them first where the GameObject has none (inherited by
    /// the classes deriving from it).</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireComponent : Attribute
    {
        public Type m_Type0;
        public Type m_Type1;
        public Type m_Type2;

        public RequireComponent(Type requiredComponent) => m_Type0 = requiredComponent;

        public RequireComponent(Type requiredComponent, Type requiredComponent2)
        {
            m_Type0 = requiredComponent;
            m_Type1 = requiredComponent2;
        }

        public RequireComponent(Type requiredComponent, Type requiredComponent2, Type requiredComponent3)
        {
            m_Type0 = requiredComponent;
            m_Type1 = requiredComponent2;
            m_Type2 = requiredComponent3;
        }
    }

    /// <summary>Where the editor's Add Component list shows a component class ("UI/Button"; "" leaves it out) and its
    /// place among its group's.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AddComponentMenu : Attribute
    {
        public AddComponentMenu(string menuName) => componentMenu = menuName;

        public AddComponentMenu(string menuName, int order)
        {
            componentMenu = menuName;
            componentOrder = order;
        }

        public string componentMenu { get; }
        public int componentOrder { get; }
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
