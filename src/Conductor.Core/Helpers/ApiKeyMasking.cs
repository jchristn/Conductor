namespace Conductor.Core.Helpers
{
    using System;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;

    /// <summary>
    /// Masks upstream endpoint API keys in management responses and resolves submitted keys on update. A masked key is
    /// four asterisks followed by the last four characters of the key; keys of four characters or fewer mask to four
    /// asterisks only so no part of a short key is revealed. All members are static and thread safe.
    /// </summary>
    public static class ApiKeyMasking
    {
        /// <summary>
        /// The prefix of every masked key.
        /// </summary>
        public const string MaskPrefix = "****";

        /// <summary>
        /// Number of trailing characters revealed by the mask.
        /// </summary>
        public const int VisibleCharacters = 4;

        private static readonly Serializer _Serializer = new Serializer();

        /// <summary>
        /// Mask an API key.
        /// </summary>
        /// <param name="apiKey">Key to mask. Null or empty returns null.</param>
        /// <returns>The masked key, or null when no key is set.</returns>
        public static string Mask(string apiKey)
        {
            if (String.IsNullOrEmpty(apiKey)) return null;
            if (apiKey.Length <= VisibleCharacters) return MaskPrefix;
            return MaskPrefix + apiKey.Substring(apiKey.Length - VisibleCharacters);
        }

        /// <summary>
        /// Resolve the key to store when a client submits an endpoint update.
        /// Null (omitted) keeps the stored key; a value equal to the masked stored key keeps the stored key; an empty
        /// string clears the key; any other value replaces it.
        /// </summary>
        /// <param name="submittedKey">Key value from the update request. Null when omitted.</param>
        /// <param name="storedKey">Key currently stored. Null when unset.</param>
        /// <returns>The key to store; null when cleared or unset.</returns>
        public static string ResolveUpdatedKey(string submittedKey, string storedKey)
        {
            if (submittedKey == null) return storedKey;
            if (submittedKey.Length == 0) return null;
            if (!String.IsNullOrEmpty(storedKey) && String.Equals(submittedKey, Mask(storedKey), StringComparison.Ordinal)) return storedKey;
            return submittedKey;
        }

        /// <summary>
        /// Create a copy of an endpoint whose ApiKey is masked, for returning in management responses.
        /// The original endpoint is not modified.
        /// </summary>
        /// <param name="endpoint">Endpoint to copy. Null returns null.</param>
        /// <returns>A masked copy, or null.</returns>
        public static ModelRunnerEndpoint CloneMasked(ModelRunnerEndpoint endpoint)
        {
            if (endpoint == null) return null;
            ModelRunnerEndpoint copy = _Serializer.DeserializeJson<ModelRunnerEndpoint>(_Serializer.SerializeJson(endpoint, false));
            copy.ApiKey = Mask(endpoint.ApiKey);
            copy.ClearExplicitlySetProperties();
            return copy;
        }
    }
}
