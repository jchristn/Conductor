namespace Conductor.Core.Enums
{
    /// <summary>
    /// The request attribute a QoS classifier rule keys on when mapping a request to a traffic class.
    /// </summary>
    public enum QosClassifierSourceEnum
    {
        /// <summary>
        /// A request HTTP header, named by the rule key (for example a custom "X-Conductor-Class" header).
        /// </summary>
        Header = 0,

        /// <summary>
        /// A JSON path into the parsed request body, named by the rule key.
        /// </summary>
        BodyJsonPath = 1,

        /// <summary>
        /// A query-string parameter, named by the rule key.
        /// </summary>
        QueryParam = 2,

        /// <summary>
        /// The requested model name.
        /// </summary>
        Model = 3,

        /// <summary>
        /// The coarse API family (OpenAI, Ollama, Gemini, Management).
        /// </summary>
        ApiFamily = 4,

        /// <summary>
        /// The resolved request type.
        /// </summary>
        RequestType = 5,

        /// <summary>
        /// The tenant identifier.
        /// </summary>
        Tenant = 6,

        /// <summary>
        /// The authenticated credential identifier or name.
        /// </summary>
        Credential = 7,

        /// <summary>
        /// The authenticated user identifier or email.
        /// </summary>
        User = 8,

        /// <summary>
        /// The client IP address.
        /// </summary>
        ClientIp = 9,

        /// <summary>
        /// The virtual model runner identifier or name.
        /// </summary>
        Vmr = 10
    }
}
