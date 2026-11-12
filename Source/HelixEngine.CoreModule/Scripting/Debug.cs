using System;
using System.Globalization;

namespace HelixEngine
{
    // The C# overloads of Debug: any object as the message (its ToString, "Null" for none), the format variants, and
    // exceptions; they call the engine's text functions (internal here, the C++ script API's Debug).
    public partial class Debug
    {
        static string Text(object message) => message == null ? "Null" : message is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : message.ToString();

        public static void Log(object message) => Log(Text(message));
        public static void Log(object message, Object context) => Log(Text(message), context);
        public static void LogFormat(string format, params object[] args) => Log(string.Format(format, args));
        public static void LogFormat(Object context, string format, params object[] args) => Log(string.Format(format, args), context);
        public static void LogFormat(LogType logType, LogOption logOptions, Object context, string format, params object[] args)
        {
            string text = string.Format(format, args);
            switch (logType)
            {
                case LogType.Error:
                case LogType.Exception:
                    LogError(text, context);
                    break;
                case LogType.Assert:
                    LogAssertion(text, context);
                    break;
                case LogType.Warning:
                    LogWarning(text, context);
                    break;
                default:
                    Log(text, context);
                    break;
            }
        }

        public static void LogWarning(object message) => LogWarning(Text(message));
        public static void LogWarning(object message, Object context) => LogWarning(Text(message), context);
        public static void LogWarningFormat(string format, params object[] args) => LogWarning(string.Format(format, args));
        public static void LogWarningFormat(Object context, string format, params object[] args) => LogWarning(string.Format(format, args), context);

        public static void LogError(object message) => LogError(Text(message));
        public static void LogError(object message, Object context) => LogError(Text(message), context);
        public static void LogErrorFormat(string format, params object[] args) => LogError(string.Format(format, args));
        public static void LogErrorFormat(Object context, string format, params object[] args) => LogError(string.Format(format, args), context);

        public static void LogAssertion(object message) => LogAssertion(Text(message));
        public static void LogAssertion(object message, Object context) => LogAssertion(Text(message), context);
        public static void LogAssertionFormat(string format, params object[] args) => LogAssertion(string.Format(format, args));
        public static void LogAssertionFormat(Object context, string format, params object[] args) => LogAssertion(string.Format(format, args), context);

        public static void LogException(Exception exception) => LogExceptionText(exception == null ? "Null" : exception.ToString(), null);
        public static void LogException(Exception exception, Object context) => LogExceptionText(exception == null ? "Null" : exception.ToString(), context);

        public static void Assert(bool condition, object message) => Assert(condition, Text(message));
        public static void Assert(bool condition, object message, Object context) => Assert(condition, Text(message), context);
        public static void AssertFormat(bool condition, string format, params object[] args)
        {
            if (!condition)
                Assert(false, string.Format(format, args));
        }
        public static void AssertFormat(bool condition, Object context, string format, params object[] args)
        {
            if (!condition)
                Assert(false, string.Format(format, args), context);
        }
    }

    // How a log message is written (the stack trace is the platform's).
    public enum LogOption
    {
        None = 0,
        NoStacktrace = 1,
    }

    public partial class Application
    {
        public delegate void LogCallback(string condition, string stackTrace, LogType type);
        public delegate void LowMemoryCallback();
    }
}
