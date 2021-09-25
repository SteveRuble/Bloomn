using System;
using System.Runtime.InteropServices;

namespace Bloomn
{
    public readonly ref struct BloomFilterKey
    {
        public readonly ReadOnlySpan<byte> Bytes;

        public BloomFilterKey(string key)
        {
            Bytes = MemoryMarshal.AsBytes(key.AsSpan());
        }

        public static implicit operator BloomFilterKey(string s) => new BloomFilterKey(s);
    }

    public enum BloomFilterCheckBehavior
    {
        CheckOnly,
        PrepareForAdd,
        AddImmediately
    }
    
    public readonly ref struct BloomFilterCheckRequest<T>
    {
        public readonly T Key;
        public readonly BloomFilterCheckBehavior Behavior;

        public static BloomFilterCheckRequest<T> CheckOnly(T key) => new BloomFilterCheckRequest<T>(key, BloomFilterCheckBehavior.CheckOnly);
        public static BloomFilterCheckRequest<T> PrepareForAdd(T key) => new BloomFilterCheckRequest<T>(key, BloomFilterCheckBehavior.PrepareForAdd);
        public static BloomFilterCheckRequest<T> AddImmediately(T key) => new BloomFilterCheckRequest<T>(key, BloomFilterCheckBehavior.AddImmediately);
        
        public BloomFilterCheckRequest(T key, BloomFilterCheckBehavior behavior)
        {
            Key = key;
            Behavior = behavior;
        }
    }
}