namespace HealthCheckPlus.Options
{
    /// <summary>
    /// Usage for publishers registered 
    /// </summary>
    public class PublishingOptions
    {
        private readonly bool _enabled;

        /// <summary>
        /// Create Default instance
        /// </summary>
        public PublishingOptions()
        {
            _enabled = true;                
        }

        internal PublishingOptions(bool value)
        {
            _enabled = value;
        }


        /// <summary>
        /// Gets whether publishing is enabled. There is no setter - the only way to change this is
        /// to construct a whole new instance (the parameterless constructor defaults to
        /// <c>true</c>). Whatever this evaluates to, it is also forced to <c>false</c> whenever
        /// <see cref="AfterIdleCount"/> is less than 1.
        /// </summary>
        /// <remarks>
        /// The instance actually assigned to <see cref="HealthCheckPlusBackGroundOptions.Publishing"/>
        /// by default is constructed via the internal constructor with publishing disabled, not via
        /// this class's own public parameterless constructor - so <c>true</c> is only this class's
        /// own standalone default, not what a consumer sees from <c>AddBackgroundPolicy</c> unless
        /// they explicitly replace the whole <see cref="HealthCheckPlusBackGroundOptions.Publishing"/>
        /// instance with <c>new PublishingOptions()</c> (or one built via its object-initializer form).
        /// </remarks>
        public bool Enabled => AfterIdleCount >= 1 && _enabled;

        /// <summary>
        /// Gets or sets the Number of counts idle to publish.The default value is 1.
        /// </summary>
        /// <remarks>
        /// The <see cref="AfterIdleCount"/> less than 1 the <see cref="Enabled"/> is false.
        /// </remarks>
        public int AfterIdleCount { get; set; } = 1;

        /// <summary>
        /// Gets or sets publish only when the report has a status change in one of its entries
        /// The default value is True.
        /// </summary>
        public bool WhenReportChange { get; set; } = true;
    }
}
