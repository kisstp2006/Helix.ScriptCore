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

    /// <summary>A member of an object that was destroyed (its C# object is still referenced).</summary>
    public class MissingReferenceException : SystemException
    {
        public MissingReferenceException() { }
        public MissingReferenceException(string message) : base(message) { }
        public MissingReferenceException(string message, Exception innerException) : base(message, innerException) { }
    }
}
