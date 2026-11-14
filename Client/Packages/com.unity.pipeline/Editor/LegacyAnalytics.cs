#if !UNITY_2023_2_OR_NEWER
using System;

namespace Unity.Pipeline.Editor
{
    // Package-local payload contracts for recording diagnostics on Editors without IAnalytic.
    internal interface IAnalytic
    {
        interface IData { }
        bool TryGatherData(out IData data, out Exception error);
    }

    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class AnalyticInfoAttribute : Attribute
    {
        public AnalyticInfoAttribute(string eventName, string vendorKey, int maxEventsPerHour, int maxNumberOfElements) { }
    }
}
#endif
