namespace Bloomn.Behaviors
{
    public enum MaxCapacityBehavior
    {
        /// <summary>
        ///     The bloom filter will throw an exception when it hits capacity.
        /// </summary>
        Throw,

        /// <summary>
        ///     The bloom filter will scale up when it reaches capacity, using the algorithm from "Scalable Bloom Filters".
        ///     Enabling scaling allows you to avoid over-allocating storage when you don't know how many items you'll
        ///     need to add to the filter. However, if you do know how many items you need to add you will get better performance
        ///     and storage efficiency by specifying the capacity initially.
        ///     <para>
        ///         https://doi.org/10.1016/j.ipl.2006.10.007
        ///     </para>
        ///     <para>
        ///         https://haslab.uminho.pt/cbm/files/dbloom.pdf
        ///     </para>
        ///     <para>
        ///         Almeida, P. S. et al. “Scalable Bloom Filters.” Inf. Process. Lett. 101 (2007): 255-261.
        ///     </para>
        /// </summary>
        Scale,

        /// <summary>
        ///     The bloom filter will continue to add items even if it can no longer fulfil the requested error rate.
        /// </summary>
        Ignore
    }
}