namespace Conductor.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a complete backup package containing all Conductor configuration data.
    /// </summary>
    public class BackupPackage
    {
        /// <summary>
        /// Schema version for forward/backward compatibility.
        /// </summary>
        public string SchemaVersion { get; set; } = "1.0";

        /// <summary>
        /// UTC timestamp when the backup was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Hostname/identifier of the source Conductor instance.
        /// </summary>
        public string SourceInstance { get; set; } = Environment.MachineName;

        /// <summary>
        /// Administrator who created the backup (email).
        /// </summary>
        public string CreatedBy { get; set; } = null;

        /// <summary>
        /// All tenant records.
        /// </summary>
        public List<TenantMetadata> Tenants
        {
            get => _Tenants;
            set => _Tenants = (value != null ? value : new List<TenantMetadata>());
        }

        /// <summary>
        /// All user records (passwords are hashed, not plaintext).
        /// </summary>
        public List<UserMaster> Users
        {
            get => _Users;
            set => _Users = (value != null ? value : new List<UserMaster>());
        }

        /// <summary>
        /// All credential records (bearer tokens included).
        /// </summary>
        public List<Credential> Credentials
        {
            get => _Credentials;
            set => _Credentials = (value != null ? value : new List<Credential>());
        }

        /// <summary>
        /// All model definition records.
        /// </summary>
        public List<ModelDefinition> ModelDefinitions
        {
            get => _ModelDefinitions;
            set => _ModelDefinitions = (value != null ? value : new List<ModelDefinition>());
        }

        /// <summary>
        /// All model configuration records.
        /// </summary>
        public List<ModelConfiguration> ModelConfigurations
        {
            get => _ModelConfigurations;
            set => _ModelConfigurations = (value != null ? value : new List<ModelConfiguration>());
        }

        /// <summary>
        /// All model runner endpoint records.
        /// </summary>
        public List<ModelRunnerEndpoint> ModelRunnerEndpoints
        {
            get => _ModelRunnerEndpoints;
            set => _ModelRunnerEndpoints = (value != null ? value : new List<ModelRunnerEndpoint>());
        }

        /// <summary>
        /// All virtual model runner records.
        /// </summary>
        public List<VirtualModelRunner> VirtualModelRunners
        {
            get => _VirtualModelRunners;
            set => _VirtualModelRunners = (value != null ? value : new List<VirtualModelRunner>());
        }

        /// <summary>
        /// All administrator records (passwords are hashed, not plaintext).
        /// Note: Only included when backup is created by an administrator.
        /// </summary>
        public List<Administrator> Administrators
        {
            get => _Administrators;
            set => _Administrators = (value != null ? value : new List<Administrator>());
        }

        private List<TenantMetadata> _Tenants = new List<TenantMetadata>();
        private List<UserMaster> _Users = new List<UserMaster>();
        private List<Credential> _Credentials = new List<Credential>();
        private List<ModelDefinition> _ModelDefinitions = new List<ModelDefinition>();
        private List<ModelConfiguration> _ModelConfigurations = new List<ModelConfiguration>();
        private List<ModelRunnerEndpoint> _ModelRunnerEndpoints = new List<ModelRunnerEndpoint>();
        private List<VirtualModelRunner> _VirtualModelRunners = new List<VirtualModelRunner>();
        private List<Administrator> _Administrators = new List<Administrator>();

        /// <summary>
        /// Instantiate the backup package.
        /// </summary>
        public BackupPackage()
        {
        }
    }
}
