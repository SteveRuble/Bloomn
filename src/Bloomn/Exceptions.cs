using System;
using System.Runtime.Serialization;

namespace Bloomn
{
    public enum BloomFilterExceptionCode
    {
        Unknown = 0,
        MaxCapacityExceeded,
        InvalidParameters,
        InvalidOptions,
        ParameterMismatch,
        InvalidSerializedState
    }

    [Serializable]
    public sealed class BloomFilterException : Exception
    {
        public BloomFilterException(BloomFilterExceptionCode code, string message, Exception? innerException = null)
            : base($"{code}: {message}", innerException)
        {
            Code = code;
        }

        private BloomFilterException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public BloomFilterExceptionCode Code
        {
            get => Enum.Parse<BloomFilterExceptionCode>(Data["Bloomn.Code"]?.ToString() ?? BloomFilterExceptionCode.Unknown.ToString());
            set => Data["Bloomn.Code"] = value.ToString();
        }
    }
}