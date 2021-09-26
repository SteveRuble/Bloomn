using System;

namespace Bloomn
{
    internal static class MathHelpers
    {
        public static int GetNextPrimeNumber(int n)
        {
            if (n % 2 == 0)
            {
                n++;
            }
            
            // The maximum prime gap at 1,346,294,310,749 is 582 so we should never hit it
            var safety = n + 582;
            int i, j;
            for (i = n; i < safety; i += 2)
            {
                var limit = Math.Sqrt(i);
                for (j = 3; j <= limit; j += 2)
                    if (i % j == 0)
                    {
                        break;
                    }

                if (j > limit)
                {
                    return i;
                }
            }
            
            throw new BloomFilterException(  BloomFilterExceptionCode.InvalidParameters,$"Prime above {n} not found in a reasonable time (your filter must be unreasonably large).");

        }
    }
}