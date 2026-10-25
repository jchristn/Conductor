namespace Conductor.Core.Database
{
    using System;

    /// <summary>
    /// Thrown when a database command runs longer than the configured command timeout
    /// (see <see cref="Conductor.Core.Settings.DatabaseSettings.CommandTimeoutSeconds"/>).
    /// The provider-specific exception is preserved as the inner exception.
    /// </summary>
    public class DatabaseTimeoutException : Exception
    {
        /// <summary>
        /// Command timeout, in seconds, that was in effect when the command was stopped.
        /// Zero means no client-side timeout was configured.
        /// </summary>
        public int TimeoutSeconds { get; }

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="timeoutSeconds">Command timeout in seconds that was in effect.</param>
        /// <param name="innerException">Provider-specific exception.</param>
        public DatabaseTimeoutException(int timeoutSeconds, Exception innerException)
            : base(BuildMessage(timeoutSeconds), innerException)
        {
            TimeoutSeconds = timeoutSeconds;
        }

        private static string BuildMessage(int timeoutSeconds)
        {
            if (timeoutSeconds > 0)
            {
                return "The database query timed out after " + timeoutSeconds + " seconds.";
            }

            return "The database query timed out.";
        }
    }
}
