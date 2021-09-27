using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Bloomn
{
    public class DefaultHasherFactoryV1 :
        IKeyHasherFactory<string>,
        IKeyHasherFactory<byte[]>,
        IKeyHasherFactory<int>,
        IKeyHasherFactory<long>,
        IKeyHasherFactory<float>,
        IKeyHasherFactory<double>,
        IKeyHasherFactory<decimal>,
        IKeyHasherFactory<Guid>
    {
        Func<int, uint> IKeyHasherFactory<int>.CreateHasher(int seed, int modulus) => key => CreateDeterministicHash(key, seed, modulus);

        Func<long, uint> IKeyHasherFactory<long>.CreateHasher(int seed, int modulus) => key => CreateDeterministicHash(key, seed, modulus);

        Func<decimal, uint> IKeyHasherFactory<decimal>.CreateHasher(int seed, int modulus) => key => CreateDeterministicHash(key, seed, modulus);

        Func<float, uint> IKeyHasherFactory<float>.CreateHasher(int seed, int modulus) => key => CreateDeterministicHash(key, seed, modulus);

        Func<double, uint> IKeyHasherFactory<double>.CreateHasher(int seed, int modulus) => key => CreateDeterministicHash(key, seed, modulus);

        Func<Guid, uint> IKeyHasherFactory<Guid>.CreateHasher(int seed, int modulus)
        {
            var useed = (uint) seed;

            return key =>
            {
                Span<byte> g =  stackalloc byte[16];
                key.TryWriteBytes(g);
                var h = Compute(g, (uint) g.Length, useed);
                return (uint) Math.Abs(h % modulus);
            };            
        }

        Func<byte[], uint> IKeyHasherFactory<byte[]>.CreateHasher(int seed, int modulus)
        {
            var useed = (uint) seed;

            return key =>
            {
                var h = Compute(key, (uint) key.Length, useed);
                return (uint) Math.Abs(h % modulus);
            };
        }

        public string Algorithm => "default";

        private static uint CreateDeterministicHash<T>(T source, int seed, int modulus)
        {
            return (uint) Math.Abs(DeterministicHashCode.Combine(seed, source) % modulus);
        }

        public Func<string, uint> CreateHasher(int seed, int modulus)
        {
            var useed = (uint) seed;

            return key =>
            {
                var bytes = MemoryMarshal.AsBytes(key.AsSpan());
                var h = Compute(bytes, (uint) bytes.Length, useed);
                return (uint) Math.Abs(h % modulus);
            };
        }

        public static uint Compute(ReadOnlySpan<byte> data, uint length, uint seed)
        {
            var nblocks = length >> 2;

            var h1 = seed;

            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;

            //----------
            // body

            var i = 0;

            for (var j = nblocks; j > 0; --j)
            {
                var k1l = BitConverter.ToUInt32(data[i..]);

                k1l *= c1;
                k1l = Rotl32(k1l, 15);
                k1l *= c2;

                h1 ^= k1l;
                h1 = Rotl32(h1, 13);
                h1 = h1 * 5 + 0xe6546b64;

                i += 4;
            }

            //----------
            // tail

            nblocks <<= 2;

            uint k1 = 0;

            var tailLength = length & 3;

            if (tailLength == 3)
            {
                k1 ^= (uint) data[2 + (int) nblocks] << 16;
            }

            if (tailLength >= 2)
            {
                k1 ^= (uint) data[1 + (int) nblocks] << 8;
            }

            if (tailLength >= 1)
            {
                k1 ^= data[(int) nblocks];
                k1 *= c1;
                k1 = Rotl32(k1, 15);
                k1 *= c2;
                h1 ^= k1;
            }

            //----------
            // finalization

            h1 ^= length;

            h1 = Fmix32(h1);

            return h1;
        }

        private static uint Fmix32(uint h)
        {
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;

            return h;
        }

        private static uint Rotl32(uint x, byte r)
        {
            return (x << r) | (x >> (32 - r));
        }
    }

    /// <summary>
    /// This is copied from the framework HashCode type,
    /// with the seed set to a constant value rather than being
    /// randomly set at startup.
    /// </summary>
    internal struct DeterministicHashCode
    {
        private const uint Seed = 1234567;
        
        private const uint Prime1 = 2654435761U;
        private const uint Prime2 = 2246822519U;
        private const uint Prime3 = 3266489917U;
        private const uint Prime4 = 668265263U;
        private const uint Prime5 = 374761393U;

        private uint _v1, _v2, _v3, _v4;
        private uint _queue1, _queue2, _queue3;
        private uint _length;

        public static int Combine<T1>(T1 value1)
        {
            // Provide a way of diffusing bits from something with a limited
            // input hash space. For example, many enums only have a few
            // possible hashes, only using the bottom few bits of the code. Some
            // collections are built on the assumption that hashes are spread
            // over a larger space, so diffusing the bits may help the
            // collection work more efficiently.

            uint hc1 = (uint) (value1?.GetHashCode() ?? 0);

            uint hash = MixEmptyState();
            hash += 4;

            hash = QueueRound(hash, hc1);

            hash = MixFinal(hash);
            return (int) hash;
        }

        public static int Combine<T1, T2>(T1 value1, T2 value2)
        {
            uint hc1 = (uint) (value1?.GetHashCode() ?? 0);
            uint hc2 = (uint) (value2?.GetHashCode() ?? 0);

            uint hash = MixEmptyState();
            hash += 8;

            hash = QueueRound(hash, hc1);
            hash = QueueRound(hash, hc2);

            hash = MixFinal(hash);
            return (int) hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Initialize(out uint v1, out uint v2, out uint v3, out uint v4)
        {
            v1 = unchecked(Seed + Prime1 + Prime2);
            v2 = Seed + Prime2;
            v3 = Seed;
            v4 = unchecked(Seed - Prime1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Round(uint hash, uint input)
        {
            return BitOperations.RotateLeft(hash + input * Prime2, 13) * Prime1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint QueueRound(uint hash, uint queuedValue)
        {
            return BitOperations.RotateLeft(hash + queuedValue * Prime3, 17) * Prime4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixState(uint v1, uint v2, uint v3, uint v4)
        {
            return BitOperations.RotateLeft(v1, 1) + BitOperations.RotateLeft(v2, 7) + BitOperations.RotateLeft(v3, 12) + BitOperations.RotateLeft(v4, 18);
        }

        private static uint MixEmptyState()
        {
            return Seed + Prime5;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixFinal(uint hash)
        {
            hash ^= hash >> 15;
            hash *= Prime2;
            hash ^= hash >> 13;
            hash *= Prime3;
            hash ^= hash >> 16;
            return hash;
        }

        public void Add<T>(T value)
        {
            Add(value?.GetHashCode() ?? 0);
        }

        public void Add<T>(T value, IEqualityComparer<T>? comparer)
        {
            Add(value is null ? 0 : (comparer?.GetHashCode(value) ?? value.GetHashCode()));
        }

        private void Add(int value)
        {
            // The original xxHash works as follows:
            // 0. Initialize immediately. We can't do this in a struct (no
            //    default ctor).
            // 1. Accumulate blocks of length 16 (4 uints) into 4 accumulators.
            // 2. Accumulate remaining blocks of length 4 (1 uint) into the
            //    hash.
            // 3. Accumulate remaining blocks of length 1 into the hash.

            // There is no need for #3 as this type only accepts ints. _queue1,
            // _queue2 and _queue3 are basically a buffer so that when
            // ToHashCode is called we can execute #2 correctly.

            // We need to initialize the xxHash32 state (_v1 to _v4) lazily (see
            // #0) nd the last place that can be done if you look at the
            // original code is just before the first block of 16 bytes is mixed
            // in. The xxHash32 state is never used for streams containing fewer
            // than 16 bytes.

            // To see what's really going on here, have a look at the Combine
            // methods.

            uint val = (uint) value;

            // Storing the value of _length locally shaves of quite a few bytes
            // in the resulting machine code.
            uint previousLength = _length++;
            uint position = previousLength % 4;

            // Switch can't be inlined.

            if (position == 0)
                _queue1 = val;
            else if (position == 1)
                _queue2 = val;
            else if (position == 2)
                _queue3 = val;
            else // position == 3
            {
                if (previousLength == 3)
                    Initialize(out _v1, out _v2, out _v3, out _v4);

                _v1 = Round(_v1, _queue1);
                _v2 = Round(_v2, _queue2);
                _v3 = Round(_v3, _queue3);
                _v4 = Round(_v4, val);
            }
        }

        public int ToHashCode()
        {
            // Storing the value of _length locally shaves of quite a few bytes
            // in the resulting machine code.
            uint length = _length;

            // position refers to the *next* queue position in this method, so
            // position == 1 means that _queue1 is populated; _queue2 would have
            // been populated on the next call to Add.
            uint position = length % 4;

            // If the length is less than 4, _v1 to _v4 don't contain anything
            // yet. xxHash32 treats this differently.

            uint hash = length < 4 ? MixEmptyState() : MixState(_v1, _v2, _v3, _v4);

            // _length is incremented once per Add(Int32) and is therefore 4
            // times too small (xxHash length is in bytes, not ints).

            hash += length * 4;

            // Mix what remains in the queue

            // Switch can't be inlined right now, so use as few branches as
            // possible by manually excluding impossible scenarios (position > 1
            // is always false if position is not > 0).
            if (position > 0)
            {
                hash = QueueRound(hash, _queue1);
                if (position > 1)
                {
                    hash = QueueRound(hash, _queue2);
                    if (position > 2)
                        hash = QueueRound(hash, _queue3);
                }
            }

            hash = MixFinal(hash);
            return (int) hash;
        }
    }
}