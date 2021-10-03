namespace Bloomn.Behaviors
{
    public enum StateValidationBehavior
    {
        /// <summary>
        /// Default behavior: throws if you attempt to import state which
        /// has different parameters than the those used to configure the builder.
        /// </summary>
        ThrowIfInconsistent,
        
        /// <summary>
        /// If an imported state is inconsistent with the builder configuration,
        /// the builder configuration is ignored.
        /// </summary>
        PreferStateConfiguration,
        
        /// <summary>
        /// If the imported state is inconsistent with the builder configuration,
        /// the imported state is ignored.
        /// </summary>
        DiscardInconsistentState,
    }
}