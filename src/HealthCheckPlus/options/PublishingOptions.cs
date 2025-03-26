namespace HealthCheckPlus.options
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
        /// Gets or sets the usage is enabled.
        /// Default value is true
        /// </summary>
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
