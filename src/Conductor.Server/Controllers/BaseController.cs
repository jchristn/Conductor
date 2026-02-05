namespace Conductor.Server.Controllers
{
    using System;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// Base controller with common functionality.
    /// </summary>
    public abstract class BaseController
    {
        private string _Header = "[BaseController] ";

        /// <summary>
        /// Database driver.
        /// </summary>
        protected readonly DatabaseDriverBase Database;

        /// <summary>
        /// Authentication service.
        /// </summary>
        protected readonly AuthenticationService AuthService;

        /// <summary>
        /// Serializer.
        /// </summary>
        protected readonly Serializer Serializer;

        /// <summary>
        /// Logging module.
        /// </summary>
        protected readonly LoggingModule Logging;

        /// <summary>
        /// Instantiate the base controller.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="logging">Logging module.</param>
        protected BaseController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging)
        {
            Database = database ?? throw new ArgumentNullException(nameof(database));
            AuthService = authService ?? throw new ArgumentNullException(nameof(authService));
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Read request body as string.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Request body string.</returns>
        protected Task<string> ReadRequestBodyAsync(HttpContextBase ctx)
        {
            return Task.FromResult(ctx.Request.DataAsString);
        }

        /// <summary>
        /// Deserialize request body.
        /// </summary>
        /// <typeparam name="T">Type to deserialize to.</typeparam>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Deserialized object or null.</returns>
        protected async Task<T> DeserializeRequestAsync<T>(HttpContextBase ctx) where T : class
        {
            string body = await ReadRequestBodyAsync(ctx);
            if (String.IsNullOrEmpty(body)) return null;

            try
            {
                return Serializer.DeserializeJson<T>(body);
            }
            catch (Exception ex)
            {
                Logging.Warn(_Header + "failed to deserialize request body: " + ex.Message);
                return null;
            }
        }

    }
}
