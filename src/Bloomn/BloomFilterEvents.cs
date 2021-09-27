using System;

namespace Bloomn
{
    public class BloomFilterEvents
    {
        public Action<string, int>? OnCapacityChanged { get; set; }
        public Action<string, long>? OnCountChanged { get; set; }
        public Action<string, int>? OnBitCountChanged { get; set; }
        public Action<string, BloomFilterParameters>? OnScaled { get; set; }
        public Action<string>? OnHit { get; set; }
        public Action<string>? OnMiss { get; set; }
        public Action<string>? OnFalsePositive { get; set; }
    }
}