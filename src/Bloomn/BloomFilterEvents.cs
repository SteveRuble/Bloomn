using System;

namespace Bloomn
{
    public record BloomFilterEvents
    {
        public Action<string, int>? OnCapacityChanged { get; init; }
        public Action<string, long>? OnCountChanged { get; init; }
        public Action<string, int>? OnBitCountChanged { get; init; }
        public Action<string, BloomFilterParameters>? OnScaled { get; init; }
        public Action<string>? OnHit { get; init; }
        public Action<string>? OnMiss { get; init; }
        public Action<string>? OnFalsePositive { get; init; }
    }
}