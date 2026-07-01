// Assets/Core/Runtime/Architecture/Logging/UnityLog.cs
using UnityEngine;

namespace hp55games.Mobile.Core.Architecture
{
    public sealed class CustomUnityLog : ILog
    {
        public void Info(string msg)  => Debug.Log(msg);
        public void Warn(string msg)  => Debug.LogWarning(msg);
        public void Error(string msg) => Debug.LogError(msg);
    }
}