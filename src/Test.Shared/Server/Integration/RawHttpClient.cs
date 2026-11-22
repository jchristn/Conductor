namespace Test.Shared.Server.Integration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Sends an HTTP/1.1 request over a raw TCP connection to 127.0.0.1 with the request target exactly as given, so tests
    /// can send paths that HttpClient would normalize (dot segments, encoded separators). All members are static and thread
    /// safe.
    /// </summary>
    public static class RawHttpClient
    {
        /// <summary>
        /// Send one request and read the whole response (the connection is closed by the request).
        /// </summary>
        /// <param name="port">Port on 127.0.0.1.</param>
        /// <param name="method">HTTP method, for example GET.</param>
        /// <param name="rawTarget">Request target sent verbatim, for example /v1.0/api/x/v1/../files.</param>
        /// <param name="headers">Extra headers. Null allowed.</param>
        /// <param name="body">Body. Null sends none.</param>
        /// <returns>The parsed response. Never null.</returns>
        public static async Task<RawHttpResponse> SendAsync(int port, string method, string rawTarget, Dictionary<string, string> headers = null, string body = null)
        {
            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync("127.0.0.1", port).ConfigureAwait(false);
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] bodyBytes = body != null ? Encoding.UTF8.GetBytes(body) : Array.Empty<byte>();
                    StringBuilder request = new StringBuilder();
                    request.Append(method + " " + rawTarget + " HTTP/1.1\r\n");
                    request.Append("Host: 127.0.0.1:" + port + "\r\n");
                    request.Append("Connection: close\r\n");
                    if (headers != null)
                    {
                        foreach (KeyValuePair<string, string> header in headers) request.Append(header.Key + ": " + header.Value + "\r\n");
                    }

                    if (body != null)
                    {
                        request.Append("Content-Type: application/json\r\n");
                        request.Append("Content-Length: " + bodyBytes.Length + "\r\n");
                    }

                    request.Append("\r\n");
                    byte[] head = Encoding.ASCII.GetBytes(request.ToString());
                    await stream.WriteAsync(head, 0, head.Length).ConfigureAwait(false);
                    if (bodyBytes.Length > 0) await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    using (MemoryStream buffer = new MemoryStream())
                    {
                        stream.ReadTimeout = 15000;
                        byte[] chunk = new byte[8192];
                        while (true)
                        {
                            int read;
                            try
                            {
                                read = await stream.ReadAsync(chunk, 0, chunk.Length).ConfigureAwait(false);
                            }
                            catch (IOException)
                            {
                                break;
                            }

                            if (read <= 0) break;
                            buffer.Write(chunk, 0, read);
                            if (LooksComplete(buffer.ToArray())) break;
                        }

                        return Parse(Encoding.UTF8.GetString(buffer.ToArray()));
                    }
                }
            }
        }

        private static bool LooksComplete(byte[] data)
        {
            string text = Encoding.UTF8.GetString(data);
            int split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (split < 0) return false;
            RawHttpResponse parsed = Parse(text);
            if (parsed.Headers.TryGetValue("Content-Length", out string length) && Int32.TryParse(length, out int expected))
            {
                return Encoding.UTF8.GetByteCount(text.Substring(split + 4)) >= expected;
            }

            if (parsed.Headers.TryGetValue("Transfer-Encoding", out string encoding) && encoding.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return text.EndsWith("0\r\n\r\n", StringComparison.Ordinal);
            }

            return false;
        }

        private static RawHttpResponse Parse(string text)
        {
            RawHttpResponse response = new RawHttpResponse();
            int split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            string head = split >= 0 ? text.Substring(0, split) : text;
            response.Body = split >= 0 ? text.Substring(split + 4) : String.Empty;

            string[] lines = head.Split("\r\n");
            if (lines.Length > 0)
            {
                string[] status = lines[0].Split(' ');
                if (status.Length > 1 && Int32.TryParse(status[1], out int code)) response.StatusCode = code;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon > 0) response.Headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
            }

            return response;
        }
    }
}
