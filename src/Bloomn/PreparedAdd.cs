using System;

namespace Bloomn
{
    public readonly struct PreparedAdd : IDisposable
    {
        private readonly Func<PreparedAdd, bool>? _add;
        public readonly string FilterId;
        internal readonly int[]? Indexes;
        internal readonly Action<PreparedAdd>? Release;
        public readonly bool CanAdd;

        public PreparedAdd(string filterId, int[]? indexes, Func<PreparedAdd, bool>? add, Action<PreparedAdd>? release)
        {
            _add = add;
            FilterId = filterId;
            Release = release;
            Indexes = indexes;
            CanAdd = add != null && release != null && indexes != null;
        }

        public static readonly PreparedAdd AlreadyAdded = new("AlreadyAdded", null, null, null);

        public bool Add()
        {
            if (_add != null)
            {
                return _add(this);
            }

            return false;
        }

        public void Dispose()
        {
            if (Release != null)
            {
                Release(this);
            }
        }
    }
}