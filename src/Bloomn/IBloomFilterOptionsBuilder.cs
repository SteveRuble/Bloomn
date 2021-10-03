namespace Bloomn
{
    public interface IBloomFilterOptionsBuilder<TKey>
    {
        /// <summary>
        /// Computes the dimensions for the common case where you know the capacity and false positive probability
        /// you want to support.
        /// </summary>
        /// <param name="capacity"></param>
        /// <param name="falsePositiveProbability"></param>
        /// <returns></returns>
        IBloomFilterOptionsBuilder<TKey> WithCapacityAndFalsePositiveProbability(int capacity, double falsePositiveProbability);
        
        /// <summary>
        /// Specify the dimensions directly. If you use this the dimensions will not be sanity checked.
        /// You can use <see cref="BloomFilterDimensionsBuilder"/> to build consistent dimensions.
        /// </summary>
        /// <param name="dimensions"></param>
        /// <returns></returns>
        IBloomFilterOptionsBuilder<TKey> WithDimensions(BloomFilterDimensions dimensions);
        
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
        /// <param name="capacityScaling">
        /// The rate at which the size of the filter is increased when scaling is required.
        /// Good values for this are 2 for slow growth, 4 for fast growth.
        /// Values below 2 will be inefficient.
        /// </param>
        /// <param name="falsePositiveProbabilityScaling">
        /// The rate at which the false positive probability is scaled. Good values are around 0.8-0.9. Must
        /// be between 0 and 1.
        /// </param>
        /// <returns></returns>
        IBloomFilterOptionsBuilder<TKey> WithScaling(double capacityScaling = 2, double falsePositiveProbabilityScaling = 0.8);
        
        /// <summary>
        /// Provide a custom hasher factory.
        /// </summary>
        /// <param name="hasherFactory"></param>
        /// <returns></returns>
        IBloomFilterOptionsBuilder<TKey> WithHasherFactory(IKeyHasherFactory<TKey> hasherFactory);

        /// <summary>
        /// Provide event handling callbacks for logging or metrics.
        /// </summary>
        /// <param name="events"></param>
        /// <returns></returns>
        IBloomFilterOptionsBuilder<TKey> WithEventHandlers(BloomFilterEvents events);
        
        /// <summary>
        /// Disable the capacity guard, allowing you to continue adding items the bloom filter 
        /// even when it can no longer maintain the configured false positive probability.
        /// </summary>
        /// <returns></returns>
        IBloomFilterOptionsBuilder<TKey> IgnoreCapacityLimits();
        
        /// <summary>
        /// If the imported state is inconsistent with the builder configuration,
        /// the builder configuration is ignored.
        /// </summary>
        /// <returns></returns>
        IBloomFilterOptionsBuilder<TKey> PreferStateConfiguration();
        
        /// <summary>
        /// If the imported state is inconsistent with the builder configuration,
        /// the imported state is ignored.
        /// </summary>
        /// <returns></returns>
        IBloomFilterOptionsBuilder<TKey> DiscardInconsistentState();
    }
}