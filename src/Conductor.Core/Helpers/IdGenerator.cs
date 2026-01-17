namespace Conductor.Core.Helpers
{
    using System;
    using PrettyId;

    /// <summary>
    /// ID generator helper using PrettyId for K-sortable identifiers.
    /// </summary>
    public static class IdGenerator
    {
        /// <summary>
        /// Tenant ID prefix.
        /// </summary>
        public const string TenantPrefix = "ten_";

        /// <summary>
        /// User ID prefix.
        /// </summary>
        public const string UserPrefix = "usr_";

        /// <summary>
        /// Credential ID prefix.
        /// </summary>
        public const string CredentialPrefix = "cred_";

        /// <summary>
        /// Model runner endpoint ID prefix.
        /// </summary>
        public const string ModelRunnerEndpointPrefix = "mre_";

        /// <summary>
        /// Model definition ID prefix.
        /// </summary>
        public const string ModelDefinitionPrefix = "md_";

        /// <summary>
        /// Model configuration ID prefix.
        /// </summary>
        public const string ModelConfigurationPrefix = "mc_";

        /// <summary>
        /// Virtual model runner ID prefix.
        /// </summary>
        public const string VirtualModelRunnerPrefix = "vmr_";

        /// <summary>
        /// Administrator ID prefix.
        /// </summary>
        public const string AdministratorPrefix = "admin_";

        /// <summary>
        /// Default ID length including prefix.
        /// </summary>
        public const int DefaultIdLength = 48;

        private static readonly PrettyId.IdGenerator _Generator = new PrettyId.IdGenerator();

        /// <summary>
        /// Generate a new tenant ID.
        /// </summary>
        /// <returns>K-sortable tenant ID.</returns>
        public static string NewTenantId()
        {
            return _Generator.GenerateKSortable(TenantPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new user ID.
        /// </summary>
        /// <returns>K-sortable user ID.</returns>
        public static string NewUserId()
        {
            return _Generator.GenerateKSortable(UserPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new credential ID.
        /// </summary>
        /// <returns>K-sortable credential ID.</returns>
        public static string NewCredentialId()
        {
            return _Generator.GenerateKSortable(CredentialPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new model runner endpoint ID.
        /// </summary>
        /// <returns>K-sortable model runner endpoint ID.</returns>
        public static string NewModelRunnerEndpointId()
        {
            return _Generator.GenerateKSortable(ModelRunnerEndpointPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new model definition ID.
        /// </summary>
        /// <returns>K-sortable model definition ID.</returns>
        public static string NewModelDefinitionId()
        {
            return _Generator.GenerateKSortable(ModelDefinitionPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new model configuration ID.
        /// </summary>
        /// <returns>K-sortable model configuration ID.</returns>
        public static string NewModelConfigurationId()
        {
            return _Generator.GenerateKSortable(ModelConfigurationPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new virtual model runner ID.
        /// </summary>
        /// <returns>K-sortable virtual model runner ID.</returns>
        public static string NewVirtualModelRunnerId()
        {
            return _Generator.GenerateKSortable(VirtualModelRunnerPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a new administrator ID.
        /// </summary>
        /// <returns>K-sortable administrator ID.</returns>
        public static string NewAdministratorId()
        {
            return _Generator.GenerateKSortable(AdministratorPrefix, DefaultIdLength);
        }

        /// <summary>
        /// Generate a bearer token for credentials.
        /// </summary>
        /// <returns>64-character bearer token.</returns>
        public static string NewBearerToken()
        {
            return _Generator.Generate(64);
        }

        /// <summary>
        /// Generate a random string of specified length.
        /// </summary>
        /// <param name="length">Length of the string to generate.</param>
        /// <returns>Random string.</returns>
        public static string NewRandom(int length = 32)
        {
            if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
            return _Generator.Generate(length);
        }
    }
}
