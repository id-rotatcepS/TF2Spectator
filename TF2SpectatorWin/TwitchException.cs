using System;
using System.Runtime.Serialization;

namespace TF2SpectatorWin
{
    [Serializable]
    internal class TwitchException : ApplicationException
    {
        public TwitchException()
        {
        }

        public TwitchException(string message) : base(message)
        {
        }

        public TwitchException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TwitchException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    internal class TwitchTimeoutException : TwitchException
    {
        public TwitchTimeoutException()
        {
        }

        public TwitchTimeoutException(string message) : base(message)
        {
        }

        public TwitchTimeoutException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TwitchTimeoutException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

}