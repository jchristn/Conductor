namespace Conductor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using Conductor.Server.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;


    /// <summary>
    /// Backup and restore API controller.
    /// </summary>
    public class BackupController : BaseController
    {
        private string _Header = "[BackupController] ";
        private readonly ConfigurationValidationService _ValidationService;
        private readonly BackupEntityRestorer _EntityRestorer;

        /// <summary>
        /// Instantiate the backup controller.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="validationService">Shared configuration validation service.</param>
        public BackupController(DatabaseDriverBase database, AuthenticationService authService, Serializer serializer, LoggingModule logging, ConfigurationValidationService validationService = null)
            : base(database, authService, serializer, logging)
        {
            _ValidationService = validationService;
            _EntityRestorer = new BackupEntityRestorer(database);
        }

        /// <summary>
        /// Create a complete backup of all Conductor configuration data.
        /// </summary>
        /// <param name="createdBy">Email of the administrator creating the backup.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Complete backup package.</returns>
        /// <exception cref="InvalidOperationException">Thrown when backup operation fails.</exception>
        public async Task<BackupPackage> CreateBackup(string createdBy, CancellationToken token = default)
        {
            Logging.Info(_Header + "starting backup creation by " + createdBy);

            BackupPackage package = new BackupPackage
            {
                CreatedUtc = DateTime.UtcNow,
                SourceInstance = Environment.MachineName,
                CreatedBy = createdBy
            };

            EnumerationRequest request = new EnumerationRequest { MaxResults = 10000 };

            // Enumerate all tenants (not tenant-scoped)
            EnumerationResult<TenantMetadata> tenants = await Database.Tenant
                .EnumerateAsync(request, token).ConfigureAwait(false);
            package.Tenants = tenants.Data.ToList();
            Logging.Debug(_Header + "backup includes " + package.Tenants.Count + " tenants");

            // For each tenant, enumerate tenant-scoped entities
            foreach (TenantMetadata tenant in package.Tenants)
            {
                token.ThrowIfCancellationRequested();

                // Users
                EnumerationResult<UserMaster> users = await Database.User
                    .EnumerateAsync(tenant.Id, request, token).ConfigureAwait(false);
                package.Users.AddRange(users.Data);

                // Credentials
                EnumerationResult<Credential> credentials = await Database.Credential
                    .EnumerateAsync(tenant.Id, request, token).ConfigureAwait(false);
                package.Credentials.AddRange(credentials.Data);

                // Model Definitions
                EnumerationResult<ModelDefinition> modelDefs = await Database.ModelDefinition
                    .EnumerateAsync(tenant.Id, request, token).ConfigureAwait(false);
                package.ModelDefinitions.AddRange(modelDefs.Data);

                // Model Configurations
                EnumerationResult<ModelConfiguration> modelConfigs = await Database.ModelConfiguration
                    .EnumerateAsync(tenant.Id, request, token).ConfigureAwait(false);
                package.ModelConfigurations.AddRange(modelConfigs.Data);

                // Model Runner Endpoints
                EnumerationResult<ModelRunnerEndpoint> endpoints = await Database.ModelRunnerEndpoint
                    .EnumerateAsync(tenant.Id, request, token).ConfigureAwait(false);
                package.ModelRunnerEndpoints.AddRange(endpoints.Data);

                // Virtual Model Runners
                EnumerationResult<VirtualModelRunner> vmrs = await Database.VirtualModelRunner
                    .EnumerateAsync(tenant.Id, request, token).ConfigureAwait(false);
                package.VirtualModelRunners.AddRange(vmrs.Data);

                // Load Balancing Policies
                EnumerationResult<LoadBalancingPolicy> policies = await Database.LoadBalancingPolicy
                    .EnumerateAsync(tenant.Id, request, token).ConfigureAwait(false);
                package.LoadBalancingPolicies.AddRange(policies.Data);
            }

            // Administrators (global, not tenant-scoped)
            EnumerationResult<Administrator> admins = await Database.Administrator
                .EnumerateAsync(request, token).ConfigureAwait(false);
            package.Administrators = admins.Data.ToList();

            Logging.Info(_Header + "backup completed: " +
                package.Tenants.Count + " tenants, " +
                package.Users.Count + " users, " +
                package.Credentials.Count + " credentials, " +
                package.ModelDefinitions.Count + " model definitions, " +
                package.ModelConfigurations.Count + " model configurations, " +
                package.ModelRunnerEndpoints.Count + " model runner endpoints, " +
                package.VirtualModelRunners.Count + " virtual model runners, " +
                package.LoadBalancingPolicies.Count + " load-balancing policies, " +
                package.Administrators.Count + " administrators");

            return package;
        }

        /// <summary>
        /// Restore configuration from a backup package.
        /// </summary>
        /// <param name="restoreRequest">The restore request containing package and options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result of the restore operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when restore request is null.</exception>
        public async Task<RestoreResult> RestoreBackup(RestoreRequest restoreRequest, CancellationToken token = default)
        {
            if (restoreRequest == null)
                throw new ArgumentNullException(nameof(restoreRequest));

            if (restoreRequest.Package == null)
                throw new WebserverException(ApiResultEnum.BadRequest, "Backup package is required");

            BackupPackage package = restoreRequest.Package;
            RestoreOptions options = restoreRequest.Options ?? new RestoreOptions();
            NormalizeBackupPackage(package);

            Logging.Info(_Header + "starting restore operation with conflict resolution: " + options.ConflictResolution);

            RestoreResult result = new RestoreResult { Success = true };

            try
            {
                // Determine which tenant IDs to restore
                List<string> tenantIdsToRestore = options.TenantFilter.Count > 0
                    ? options.TenantFilter
                    : package.Tenants.Select(t => t.Id).ToList();

                // Restore in dependency order

                // 1. Administrators (if enabled)
                if (options.RestoreAdministrators && package.Administrators != null)
                {
                    foreach (Administrator admin in package.Administrators)
                    {
                        token.ThrowIfCancellationRequested();
                        await _EntityRestorer.RestoreAdministratorAsync(admin, options.ConflictResolution, result.Summary.Administrators, token).ConfigureAwait(false);
                    }
                    Logging.Debug(_Header + "restored administrators: " + result.Summary.Administrators.Created + " created, " + result.Summary.Administrators.Updated + " updated, " + result.Summary.Administrators.Skipped + " skipped");
                }

                // 2. Tenants
                foreach (TenantMetadata tenant in package.Tenants.Where(t => tenantIdsToRestore.Contains(t.Id)))
                {
                    token.ThrowIfCancellationRequested();
                    await _EntityRestorer.RestoreTenantAsync(tenant, options.ConflictResolution, result.Summary.Tenants, token).ConfigureAwait(false);
                }
                Logging.Debug(_Header + "restored tenants: " + result.Summary.Tenants.Created + " created, " + result.Summary.Tenants.Updated + " updated, " + result.Summary.Tenants.Skipped + " skipped");

                // 3. Users (depends on Tenant)
                foreach (UserMaster user in package.Users.Where(u => tenantIdsToRestore.Contains(u.TenantId)))
                {
                    token.ThrowIfCancellationRequested();
                    await _EntityRestorer.RestoreUserAsync(user, options.ConflictResolution, result.Summary.Users, token).ConfigureAwait(false);
                }
                Logging.Debug(_Header + "restored users: " + result.Summary.Users.Created + " created, " + result.Summary.Users.Updated + " updated, " + result.Summary.Users.Skipped + " skipped");

                // 4. Credentials (depends on Tenant + User, if enabled)
                if (options.RestoreCredentials)
                {
                    foreach (Credential credential in package.Credentials.Where(c => tenantIdsToRestore.Contains(c.TenantId)))
                    {
                        token.ThrowIfCancellationRequested();
                        await _EntityRestorer.RestoreCredentialAsync(credential, options.ConflictResolution, result.Summary.Credentials, token).ConfigureAwait(false);
                    }
                    Logging.Debug(_Header + "restored credentials: " + result.Summary.Credentials.Created + " created, " + result.Summary.Credentials.Updated + " updated, " + result.Summary.Credentials.Skipped + " skipped");
                }

                // 5. Model Runner Endpoints (depends on Tenant)
                foreach (ModelRunnerEndpoint endpoint in package.ModelRunnerEndpoints.Where(e => tenantIdsToRestore.Contains(e.TenantId)))
                {
                    token.ThrowIfCancellationRequested();
                    await ValidateBackupEndpointAsync(endpoint, token).ConfigureAwait(false);
                    await _EntityRestorer.RestoreModelRunnerEndpointAsync(endpoint, options.ConflictResolution, result.Summary.ModelRunnerEndpoints, token).ConfigureAwait(false);
                }
                Logging.Debug(_Header + "restored model runner endpoints: " + result.Summary.ModelRunnerEndpoints.Created + " created, " + result.Summary.ModelRunnerEndpoints.Updated + " updated, " + result.Summary.ModelRunnerEndpoints.Skipped + " skipped");

                // 6. Model Definitions (depends on Tenant)
                foreach (ModelDefinition modelDef in package.ModelDefinitions.Where(m => tenantIdsToRestore.Contains(m.TenantId)))
                {
                    token.ThrowIfCancellationRequested();
                    await ValidateBackupModelDefinitionAsync(modelDef, token).ConfigureAwait(false);
                    await _EntityRestorer.RestoreModelDefinitionAsync(modelDef, options.ConflictResolution, result.Summary.ModelDefinitions, token).ConfigureAwait(false);
                }
                Logging.Debug(_Header + "restored model definitions: " + result.Summary.ModelDefinitions.Created + " created, " + result.Summary.ModelDefinitions.Updated + " updated, " + result.Summary.ModelDefinitions.Skipped + " skipped");

                // 7. Model Configurations (depends on Tenant)
                foreach (ModelConfiguration modelConfig in package.ModelConfigurations.Where(m => tenantIdsToRestore.Contains(m.TenantId)))
                {
                    token.ThrowIfCancellationRequested();
                    await ValidateBackupModelConfigurationAsync(modelConfig, token).ConfigureAwait(false);
                    await _EntityRestorer.RestoreModelConfigurationAsync(modelConfig, options.ConflictResolution, result.Summary.ModelConfigurations, token).ConfigureAwait(false);
                }
                Logging.Debug(_Header + "restored model configurations: " + result.Summary.ModelConfigurations.Created + " created, " + result.Summary.ModelConfigurations.Updated + " updated, " + result.Summary.ModelConfigurations.Skipped + " skipped");

                // 8. Load Balancing Policies (depends on Tenant)
                foreach (LoadBalancingPolicy policy in package.LoadBalancingPolicies.Where(p => tenantIdsToRestore.Contains(p.TenantId)))
                {
                    token.ThrowIfCancellationRequested();
                    await ValidateBackupPolicyAsync(policy, token).ConfigureAwait(false);
                    await _EntityRestorer.RestoreLoadBalancingPolicyAsync(policy, options.ConflictResolution, result.Summary.LoadBalancingPolicies, token).ConfigureAwait(false);
                }
                Logging.Debug(_Header + "restored load-balancing policies: " + result.Summary.LoadBalancingPolicies.Created + " created, " + result.Summary.LoadBalancingPolicies.Updated + " updated, " + result.Summary.LoadBalancingPolicies.Skipped + " skipped");

                // 9. Virtual Model Runners (depends on Tenant + referenced IDs)
                foreach (VirtualModelRunner vmr in package.VirtualModelRunners.Where(v => tenantIdsToRestore.Contains(v.TenantId)))
                {
                    token.ThrowIfCancellationRequested();
                    await ValidateBackupVirtualModelRunnerAsync(vmr, token).ConfigureAwait(false);
                    await _EntityRestorer.RestoreVirtualModelRunnerAsync(vmr, options.ConflictResolution, result.Summary.VirtualModelRunners, token).ConfigureAwait(false);
                }
                Logging.Debug(_Header + "restored virtual model runners: " + result.Summary.VirtualModelRunners.Created + " created, " + result.Summary.VirtualModelRunners.Updated + " updated, " + result.Summary.VirtualModelRunners.Skipped + " skipped");

                Logging.Info(_Header + "restore operation completed successfully");
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "Restore operation was cancelled.";
                Logging.Warn(_Header + "restore operation cancelled");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Logging.Warn(_Header + "restore operation failed: " + ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Validate a backup package without applying changes.
        /// </summary>
        /// <param name="package">The backup package to validate.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Validation result including potential conflicts.</returns>
        /// <exception cref="ArgumentNullException">Thrown when package is null.</exception>
        public async Task<ValidationResult> ValidateBackup(BackupPackage package, CancellationToken token = default)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));

            Logging.Info(_Header + "validating backup package");
            NormalizeBackupPackage(package);

            ValidationResult result = new ValidationResult { IsValid = true };

            // Populate summary
            result.Summary = new BackupSummary
            {
                TenantCount = package.Tenants?.Count ?? 0,
                UserCount = package.Users?.Count ?? 0,
                CredentialCount = package.Credentials?.Count ?? 0,
                ModelDefinitionCount = package.ModelDefinitions?.Count ?? 0,
                ModelConfigurationCount = package.ModelConfigurations?.Count ?? 0,
                ModelRunnerEndpointCount = package.ModelRunnerEndpoints?.Count ?? 0,
                VirtualModelRunnerCount = package.VirtualModelRunners?.Count ?? 0,
                AdministratorCount = package.Administrators?.Count ?? 0,
                LoadBalancingPolicyCount = package.LoadBalancingPolicies?.Count ?? 0
            };

            // Validate schema version
            if (String.IsNullOrEmpty(package.SchemaVersion))
            {
                result.IsValid = false;
                result.Errors.Add("Missing schema version.");
            }

            // Check for tenant ID conflicts
            if (package.Tenants != null)
            {
                foreach (TenantMetadata tenant in package.Tenants)
                {
                    token.ThrowIfCancellationRequested();
                    if (await Database.Tenant.ExistsAsync(tenant.Id, token).ConfigureAwait(false))
                    {
                        result.Conflicts.Add("Tenant '" + tenant.Name + "' (ID: " + tenant.Id + ") already exists.");
                    }
                }
            }

            // Check for administrator ID conflicts
            if (package.Administrators != null)
            {
                foreach (Administrator admin in package.Administrators)
                {
                    token.ThrowIfCancellationRequested();
                    if (await Database.Administrator.ExistsAsync(admin.Id, token).ConfigureAwait(false))
                    {
                        result.Conflicts.Add("Administrator '" + admin.Email + "' (ID: " + admin.Id + ") already exists.");
                    }
                }
            }

            // Check for user ID conflicts
            if (package.Users != null)
            {
                foreach (UserMaster user in package.Users)
                {
                    token.ThrowIfCancellationRequested();
                    if (await Database.User.ExistsAsync(user.TenantId, user.Id, token).ConfigureAwait(false))
                    {
                        result.Conflicts.Add("User '" + user.Email + "' (ID: " + user.Id + ") already exists.");
                    }
                }
            }

            // Check for credential ID conflicts
            if (package.Credentials != null)
            {
                foreach (Credential credential in package.Credentials)
                {
                    token.ThrowIfCancellationRequested();
                    if (await Database.Credential.ExistsAsync(credential.TenantId, credential.Id, token).ConfigureAwait(false))
                    {
                        result.Conflicts.Add("Credential '" + credential.Name + "' (ID: " + credential.Id + ") already exists.");
                    }
                }
            }

            // Check for model runner endpoint ID conflicts
            if (package.ModelRunnerEndpoints != null)
            {
                foreach (ModelRunnerEndpoint endpoint in package.ModelRunnerEndpoints)
                {
                    token.ThrowIfCancellationRequested();
                    await AppendBackupValidationErrorsAsync(
                        result,
                        "endpoint",
                        endpoint.Name ?? endpoint.Id,
                        endpoint.Id,
                        _ValidationService != null
                            ? await _ValidationService.ValidateModelRunnerEndpointAsync(endpoint.TenantId, endpoint, endpoint.Id, token).ConfigureAwait(false)
                            : null).ConfigureAwait(false);

                    if (await Database.ModelRunnerEndpoint.ExistsAsync(endpoint.TenantId, endpoint.Id, token).ConfigureAwait(false))
                    {
                        result.Conflicts.Add("Model Runner Endpoint '" + endpoint.Name + "' (ID: " + endpoint.Id + ") already exists.");
                    }
                }
            }

            // Check for model definition ID conflicts
            if (package.ModelDefinitions != null)
            {
                foreach (ModelDefinition modelDef in package.ModelDefinitions)
                {
                    token.ThrowIfCancellationRequested();
                    await AppendBackupValidationErrorsAsync(
                        result,
                        "model definition",
                        modelDef.Name ?? modelDef.Id,
                        modelDef.Id,
                        _ValidationService != null
                            ? await _ValidationService.ValidateModelDefinitionAsync(modelDef.TenantId, modelDef, modelDef.Id, token).ConfigureAwait(false)
                            : null).ConfigureAwait(false);

                    if (await Database.ModelDefinition.ExistsAsync(modelDef.TenantId, modelDef.Id, token).ConfigureAwait(false))
                    {
                        result.Conflicts.Add("Model Definition '" + modelDef.Name + "' (ID: " + modelDef.Id + ") already exists.");
                    }
                }
            }

            // Check for model configuration ID conflicts
            if (package.ModelConfigurations != null)
            {
                foreach (ModelConfiguration modelConfig in package.ModelConfigurations)
                {
                    token.ThrowIfCancellationRequested();
                    await AppendBackupValidationErrorsAsync(
                        result,
                        "model configuration",
                        modelConfig.Name ?? modelConfig.Id,
                        modelConfig.Id,
                        _ValidationService != null
                            ? await _ValidationService.ValidateModelConfigurationAsync(modelConfig.TenantId, modelConfig, modelConfig.Id, token).ConfigureAwait(false)
                            : null).ConfigureAwait(false);

                    if (await Database.ModelConfiguration.ExistsAsync(modelConfig.TenantId, modelConfig.Id, token).ConfigureAwait(false))
                    {
                        result.Conflicts.Add("Model Configuration '" + modelConfig.Name + "' (ID: " + modelConfig.Id + ") already exists.");
                    }
                }
            }

            // Check for virtual model runner ID conflicts
            if (package.VirtualModelRunners != null)
            {
                foreach (VirtualModelRunner vmr in package.VirtualModelRunners)
                {
                    token.ThrowIfCancellationRequested();
                    await AppendBackupValidationErrorsAsync(
                        result,
                        "virtual model runner",
                        vmr.Name ?? vmr.Id,
                        vmr.Id,
                        _ValidationService != null
                            ? await _ValidationService.ValidateVirtualModelRunnerAsync(vmr.TenantId, vmr, vmr.Id, token).ConfigureAwait(false)
                            : null).ConfigureAwait(false);

                    if (await Database.VirtualModelRunner.ExistsAsync(vmr.TenantId, vmr.Id, token).ConfigureAwait(false))
                    {
                        result.Conflicts.Add("Virtual Model Runner '" + vmr.Name + "' (ID: " + vmr.Id + ") already exists.");
                    }
                }
            }

            // Check for load-balancing policy ID conflicts
            if (package.LoadBalancingPolicies != null)
            {
                foreach (LoadBalancingPolicy policy in package.LoadBalancingPolicies)
                {
                    token.ThrowIfCancellationRequested();
                    await AppendBackupValidationErrorsAsync(
                        result,
                        "load-balancing policy",
                        policy.Name ?? policy.Id,
                        policy.Id,
                        _ValidationService != null
                            ? await _ValidationService.ValidateLoadBalancingPolicyAsync(policy.TenantId, policy, policy.Id, token).ConfigureAwait(false)
                            : null).ConfigureAwait(false);

                    if (await Database.LoadBalancingPolicy.ExistsAsync(policy.TenantId, policy.Id, token).ConfigureAwait(false))
                    {
                        result.Conflicts.Add("Load Balancing Policy '" + policy.Name + "' (ID: " + policy.Id + ") already exists.");
                    }
                }
            }

            result.IsValid = result.Errors.Count < 1;

            Logging.Info(_Header + "validation completed: " + (result.IsValid ? "valid" : "invalid") + " with " + result.Conflicts.Count + " conflicts");

            return result;
        }

        private void NormalizeBackupPackage(BackupPackage package)
        {
            if (package == null) return;

            package.SchemaVersion = String.IsNullOrWhiteSpace(package.SchemaVersion) ? "1.0" : package.SchemaVersion;
            package.Tenants = package.Tenants;
            package.Users = package.Users;
            package.Credentials = package.Credentials;
            package.ModelDefinitions = package.ModelDefinitions;
            package.ModelConfigurations = package.ModelConfigurations;
            package.ModelRunnerEndpoints = package.ModelRunnerEndpoints;
            package.VirtualModelRunners = package.VirtualModelRunners;
            package.LoadBalancingPolicies = package.LoadBalancingPolicies;
            package.Administrators = package.Administrators;

            foreach (ModelRunnerEndpoint endpoint in package.ModelRunnerEndpoints)
            {
                endpoint.ServiceState = endpoint.ServiceState;
                endpoint.RigMonitor = endpoint.RigMonitor;
                endpoint.Labels = endpoint.Labels;
                endpoint.Tags = endpoint.Tags;
            }

            foreach (VirtualModelRunner vmr in package.VirtualModelRunners)
            {
                vmr.ModelRunnerEndpointIds = vmr.ModelRunnerEndpointIds;
                vmr.ModelConfigurationIds = vmr.ModelConfigurationIds;
                vmr.ModelDefinitionIds = vmr.ModelDefinitionIds;
                vmr.ModelConfigurationMappings = vmr.ModelConfigurationMappings;
                vmr.Labels = vmr.Labels;
                vmr.Tags = vmr.Tags;
            }
        }

        private async Task ValidateBackupEndpointAsync(ModelRunnerEndpoint endpoint, CancellationToken token)
        {
            if (_ValidationService == null || endpoint == null) return;

            ResourceValidationResult validation = await _ValidationService.ValidateModelRunnerEndpointAsync(endpoint.TenantId, endpoint, endpoint.Id, token).ConfigureAwait(false);
            ThrowIfInvalid(validation, "endpoint");
        }

        private async Task ValidateBackupModelDefinitionAsync(ModelDefinition definition, CancellationToken token)
        {
            if (_ValidationService == null || definition == null) return;

            ResourceValidationResult validation = await _ValidationService.ValidateModelDefinitionAsync(definition.TenantId, definition, definition.Id, token).ConfigureAwait(false);
            ThrowIfInvalid(validation, "model definition");
        }

        private async Task ValidateBackupModelConfigurationAsync(ModelConfiguration configuration, CancellationToken token)
        {
            if (_ValidationService == null || configuration == null) return;

            ResourceValidationResult validation = await _ValidationService.ValidateModelConfigurationAsync(configuration.TenantId, configuration, configuration.Id, token).ConfigureAwait(false);
            ThrowIfInvalid(validation, "model configuration");
        }

        private async Task ValidateBackupPolicyAsync(LoadBalancingPolicy policy, CancellationToken token)
        {
            if (_ValidationService == null || policy == null) return;

            ResourceValidationResult validation = await _ValidationService.ValidateLoadBalancingPolicyAsync(policy.TenantId, policy, policy.Id, token).ConfigureAwait(false);
            ThrowIfInvalid(validation, "load-balancing policy");
        }

        private async Task ValidateBackupVirtualModelRunnerAsync(VirtualModelRunner vmr, CancellationToken token)
        {
            if (_ValidationService == null || vmr == null) return;

            ResourceValidationResult validation = await _ValidationService.ValidateVirtualModelRunnerAsync(vmr.TenantId, vmr, vmr.Id, token).ConfigureAwait(false);
            ThrowIfInvalid(validation, "virtual model runner");
        }

        private static void ThrowIfInvalid(ResourceValidationResult validation, string resourceType)
        {
            if (validation == null || validation.IsValid)
            {
                return;
            }

            throw new WebserverException(ApiResultEnum.BadRequest, "Backup " + resourceType + " failed validation: " + String.Join(" ", validation.Errors.Select(item => item.Message)));
        }

        private static Task AppendBackupValidationErrorsAsync(
            ValidationResult result,
            string resourceType,
            string resourceName,
            string resourceId,
            ResourceValidationResult validation)
        {
            if (result == null || validation == null || validation.IsValid)
            {
                return Task.CompletedTask;
            }

            string displayName = !String.IsNullOrWhiteSpace(resourceName) ? resourceName : resourceId;
            foreach (ResourceValidationIssue issue in validation.Errors)
            {
                result.Errors.Add("Backup " + resourceType + " '" + displayName + "' (ID: " + resourceId + ") failed validation: " + issue.Message);
            }

            result.IsValid = false;
            return Task.CompletedTask;
        }

    }
}
