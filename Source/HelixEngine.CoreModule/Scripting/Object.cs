namespace HelixEngine
{
    // The parts of Object written in C#: its text and the generic Instantiate (the clone as the original's type).
    public partial class Object
    {
        public override string ToString() => this ? name : "null";

        public static T Instantiate<T>(T original) where T : Object => (T)Instantiate((Object)original);
        public static T Instantiate<T>(T original, Transform parent) where T : Object => (T)Instantiate((Object)original, parent);
        public static T Instantiate<T>(T original, Transform parent, bool worldPositionStays) where T : Object =>
            (T)Instantiate((Object)original, parent, worldPositionStays);
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object =>
            (T)Instantiate((Object)original, position, rotation);
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent) where T : Object =>
            (T)Instantiate((Object)original, position, rotation, parent);
    }
}
