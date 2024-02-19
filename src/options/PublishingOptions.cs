using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HealthCheckPlus.options
{
    /// <summary>
    /// Usage for publishers registered 
    /// </summary>
    public class PublishingOptions
    {
        private int _afterIdleCount = 1;
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
        public bool Enabled => _enabled;

        /// <summary>
        /// Gets or sets the Number of counts idle to publish.
        /// The default value is 1.
        /// </summary>
        /// <remarks>
        /// The <see cref="AfterIdleCount"/> cannot be set to a value lower than 1.
        /// </remarks>
        public int AfterIdleCount
        {
            get => _afterIdleCount;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException($"The {nameof(AfterIdleCount)} must be greater than or equal to 1.", nameof(value));
                }
                if (value > int.MaxValue-1)
                {
                    throw new ArgumentException($"The {nameof(AfterIdleCount)} must be less than or equal to {int.MaxValue-1}.", nameof(value));
                }
                _afterIdleCount = value;
            }
        }

        /// <summary>
        /// Gets or sets publish only when the report has a status change in one of its entries
        /// The default value is True.
        /// </summary>
        public bool WhenReportChange { get; set; } = true;
    }
}
