namespace Conductor.Server.Services
{
    /// <summary>
    /// Upstream transport response from a model load request.
    /// </summary>
    public class ModelLoadTransportResponse
    {
        /// <summary>
        /// Provider HTTP status code.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Provider response body text.
        /// </summary>
        public string Body { get; set; } = null;

        /// <summary>
        /// Whether the status code is in the 2xx range.
        /// </summary>
        public bool IsSuccessStatusCode
        {
            get
            {
                return StatusCode >= 200 && StatusCode <= 299;
            }
        }
    }
}
