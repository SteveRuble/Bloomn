using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Bloomn
{
    public readonly struct PreparedAdd : IDisposable
    {
        private readonly bool _canAdd;
        public readonly IPreparedAddTarget? AddTarget;
        public readonly string FilterId;
        internal readonly int[]? Indexes;
        [MemberNotNullWhen(true, nameof(Indexes))]
        [MemberNotNullWhen(true, nameof(AddTarget))]
        public bool CanAdd => _canAdd;
        public PreparedAdd(string filterId, int[]? indexes, IPreparedAddTarget? addTarget)
        {
            AddTarget = addTarget;
            FilterId = filterId;
            Indexes = indexes;
            _canAdd = AddTarget != null && indexes != null;
        }

        public static readonly PreparedAdd AlreadyAdded = new("AlreadyAdded", null, null);

        public bool Add()
        {
            if (AddTarget != null && Indexes != null)
            {
                return AddTarget.ApplyPreparedAdd(FilterId, Indexes);
            }

            return false;
        }

        public void Dispose()
        {
            if (AddTarget != null && Indexes != null)
            {
                AddTarget.Release(FilterId, Indexes);
            }
        }
    }
}