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
    
    public readonly ref struct BloomFilterCheckRequest
    {
        public readonly BloomFilterKey Key;
        public readonly BloomFilterCheckBehavior Behavior;

        public static BloomFilterCheckRequest CheckOnly(BloomFilterKey key) => new BloomFilterCheckRequest(key, BloomFilterCheckBehavior.CheckOnly);
        public static BloomFilterCheckRequest PrepareForAdd(BloomFilterKey key) => new BloomFilterCheckRequest(key, BloomFilterCheckBehavior.PrepareForAdd);
        public static BloomFilterCheckRequest AddImmediately(BloomFilterKey key) => new BloomFilterCheckRequest(key, BloomFilterCheckBehavior.AddImmediately);
        
        public BloomFilterCheckRequest(BloomFilterKey key, BloomFilterCheckBehavior behavior)
        {
            Key = key;
            Behavior = behavior;
        }
    }
}