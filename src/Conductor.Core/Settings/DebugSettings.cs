namespace Conductor.Core.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Debug settings.
    /// </summary>
    public class DebugSettings
    {
        /// <summary>
        /// Enable or disable request body logging.
        /// Default is false.
        /// </summary>
        public bool RequestBody { get; set; } = false;

        /// <summary>
        /// Instantiate the debug settings.
        /// </summary>
        public DebugSettings()
        {
        }
    }
}
