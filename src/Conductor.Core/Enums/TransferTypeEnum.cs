namespace Conductor.Core.Enums
{
    using System;

    /// <summary>
    /// Transfer type enumeration indicating how request or response data was transferred.
    /// </summary>
    public enum TransferTypeEnum
    {
        /// <summary>
        /// Standard non-chunked transfer.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Chunked transfer encoding.
        /// </summary>
        Chunked = 1,

        /// <summary>
        /// Server-Sent Events (SSE) streaming.
        /// </summary>
        ServerSentEvents = 2
    }
}
