using System;
using System.Threading;

namespace Bloomn
{
    public class Callbacks
    {
        public Action<string, int>? OnCapacityChanged { get; set; }
        public Action<string, long>? OnCountChanged { get; set; }
        public Action<string, int>? OnBitCountChanged { get; set; }
        public Action<string, BloomFilterParameters>? OnScaled { get; set; }
        public Action<string>? OnHit { get; set; }
        public Action<string>? OnMiss { get; set; }
        public Action<string>? OnFalsePositive { get; set; }
    }

    internal class StateMetrics: IBloomFilterDimensions
    {
        private long _count;
        private int _setBitCount;
        
        private readonly Callbacks _callbacks;
        public string Id { get; }
        public double ErrorRate { get; }
        public int Capacity { get; private set; }
        
        public long Count => _count;
        public int BitCount { get; private set; }
        public int HashCount { get; private set; }

        public int SetBitCount => _setBitCount;

        public StateMetrics(BloomFilterParameters parameters, Callbacks callbacks)
        {
            _callbacks = callbacks;
            Id = parameters.Id;
            ErrorRate = parameters.Dimensions.ErrorRate;
            Capacity = parameters.Dimensions.Capacity;
            BitCount = parameters.Dimensions.BitCount;
            HashCount = parameters.Dimensions.HashCount;
        }
        
        public void OnCapacityChanged(int value)
        {
            Capacity = value;
            _callbacks.OnCapacityChanged?.Invoke(Id, value);
        }

        public void IncrementCount(int amount) 
        {
            var value = Interlocked.Add(ref _count, amount);
            _callbacks.OnCountChanged?.Invoke(Id, value);
        }

        public void IncrementSetBitCount(int amount)
        {
            Interlocked.Add(ref _setBitCount, amount);
        }
        
        public void OnCountChanged(long value)
        {
            _count = value;
            _callbacks.OnCountChanged?.Invoke(Id, _count);
        }

        public void OnBitCountChanged(int value)
        {
            BitCount = value;
            _callbacks.OnBitCountChanged?.Invoke(Id, value);
        }

        public void OnScaled(BloomFilterParameters parameters) => _callbacks.OnScaled?.Invoke(Id, parameters);
        public void OnHit() => _callbacks.OnHit?.Invoke(Id);
        public void OnMiss() => _callbacks.OnMiss?.Invoke(Id);
        public void OnFalsePositive() => _callbacks.OnFalsePositive?.Invoke(Id);
    }
}