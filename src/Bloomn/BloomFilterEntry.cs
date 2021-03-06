using System;

namespace Bloomn
{
    public readonly struct BloomFilterEntry : IDisposable
    {
        public static readonly BloomFilterEntry MaybePresent = new(false, PreparedAdd.AlreadyAdded);
        public static readonly BloomFilterEntry NotPresent = new(true, PreparedAdd.AlreadyAdded);

        public static BloomFilterEntry Addable(PreparedAdd preparedAdd)
        {
            return new(true, preparedAdd);
        }

        public readonly bool IsNotPresent;
        public readonly PreparedAdd PreparedAdd;

        private BloomFilterEntry(bool isNotPresent, PreparedAdd preparedAdd)
        {
            IsNotPresent = isNotPresent;
            PreparedAdd = preparedAdd;
        }

        public bool Add()
        {
            if (PreparedAdd.CanAdd)
            {
                return PreparedAdd.Add();
            }

            return false;
        }

        public void Dispose()
        {
            PreparedAdd.Dispose();
        }
    }
}