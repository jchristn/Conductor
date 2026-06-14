namespace Conductor.Server.Controllers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Conductor.Core.Database;
    using Conductor.Core.Models;

    internal sealed class BackupEntityRestorer
    {
        private readonly DatabaseDriverBase _Database;

        internal BackupEntityRestorer(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        internal async Task RestoreAdministratorAsync(Administrator admin, ConflictResolutionMode conflictResolution, EntityRestoreCount counter, CancellationToken token)
        {
            bool exists = await _Database.Administrator.ExistsAsync(admin.Id, token).ConfigureAwait(false);

            if (exists)
            {
                switch (conflictResolution)
                {
                    case ConflictResolutionMode.Skip:
                        counter.Skipped++;
                        break;
                    case ConflictResolutionMode.Overwrite:
                        await _Database.Administrator.UpdateAsync(admin, token).ConfigureAwait(false);
                        counter.Updated++;
                        break;
                    case ConflictResolutionMode.Fail:
                        throw new InvalidOperationException("Administrator with ID '" + admin.Id + "' already exists.");
                }
            }
            else
            {
                await _Database.Administrator.CreateAsync(admin, token).ConfigureAwait(false);
                counter.Created++;
            }
        }

        internal async Task RestoreTenantAsync(TenantMetadata tenant, ConflictResolutionMode conflictResolution, EntityRestoreCount counter, CancellationToken token)
        {
            bool exists = await _Database.Tenant.ExistsAsync(tenant.Id, token).ConfigureAwait(false);

            if (exists)
            {
                switch (conflictResolution)
                {
                    case ConflictResolutionMode.Skip:
                        counter.Skipped++;
                        break;
                    case ConflictResolutionMode.Overwrite:
                        await _Database.Tenant.UpdateAsync(tenant, token).ConfigureAwait(false);
                        counter.Updated++;
                        break;
                    case ConflictResolutionMode.Fail:
                        throw new InvalidOperationException("Tenant with ID '" + tenant.Id + "' already exists.");
                }
            }
            else
            {
                await _Database.Tenant.CreateAsync(tenant, token).ConfigureAwait(false);
                counter.Created++;
            }
        }

        internal async Task RestoreUserAsync(UserMaster user, ConflictResolutionMode conflictResolution, EntityRestoreCount counter, CancellationToken token)
        {
            bool exists = await _Database.User.ExistsAsync(user.TenantId, user.Id, token).ConfigureAwait(false);

            if (exists)
            {
                switch (conflictResolution)
                {
                    case ConflictResolutionMode.Skip:
                        counter.Skipped++;
                        break;
                    case ConflictResolutionMode.Overwrite:
                        await _Database.User.UpdateAsync(user, token).ConfigureAwait(false);
                        counter.Updated++;
                        break;
                    case ConflictResolutionMode.Fail:
                        throw new InvalidOperationException("User with ID '" + user.Id + "' already exists.");
                }
            }
            else
            {
                await _Database.User.CreateAsync(user, token).ConfigureAwait(false);
                counter.Created++;
            }
        }

        internal async Task RestoreCredentialAsync(Credential credential, ConflictResolutionMode conflictResolution, EntityRestoreCount counter, CancellationToken token)
        {
            bool exists = await _Database.Credential.ExistsAsync(credential.TenantId, credential.Id, token).ConfigureAwait(false);

            if (exists)
            {
                switch (conflictResolution)
                {
                    case ConflictResolutionMode.Skip:
                        counter.Skipped++;
                        break;
                    case ConflictResolutionMode.Overwrite:
                        await _Database.Credential.UpdateAsync(credential, token).ConfigureAwait(false);
                        counter.Updated++;
                        break;
                    case ConflictResolutionMode.Fail:
                        throw new InvalidOperationException("Credential with ID '" + credential.Id + "' already exists.");
                }
            }
            else
            {
                await _Database.Credential.CreateAsync(credential, token).ConfigureAwait(false);
                counter.Created++;
            }
        }

        internal async Task RestoreModelRunnerEndpointAsync(ModelRunnerEndpoint endpoint, ConflictResolutionMode conflictResolution, EntityRestoreCount counter, CancellationToken token)
        {
            bool exists = await _Database.ModelRunnerEndpoint.ExistsAsync(endpoint.TenantId, endpoint.Id, token).ConfigureAwait(false);

            if (exists)
            {
                switch (conflictResolution)
                {
                    case ConflictResolutionMode.Skip:
                        counter.Skipped++;
                        break;
                    case ConflictResolutionMode.Overwrite:
                        await _Database.ModelRunnerEndpoint.UpdateAsync(endpoint, token).ConfigureAwait(false);
                        counter.Updated++;
                        break;
                    case ConflictResolutionMode.Fail:
                        throw new InvalidOperationException("Model Runner Endpoint with ID '" + endpoint.Id + "' already exists.");
                }
            }
            else
            {
                await _Database.ModelRunnerEndpoint.CreateAsync(endpoint, token).ConfigureAwait(false);
                counter.Created++;
            }
        }

        internal async Task RestoreModelDefinitionAsync(ModelDefinition modelDef, ConflictResolutionMode conflictResolution, EntityRestoreCount counter, CancellationToken token)
        {
            bool exists = await _Database.ModelDefinition.ExistsAsync(modelDef.TenantId, modelDef.Id, token).ConfigureAwait(false);

            if (exists)
            {
                switch (conflictResolution)
                {
                    case ConflictResolutionMode.Skip:
                        counter.Skipped++;
                        break;
                    case ConflictResolutionMode.Overwrite:
                        await _Database.ModelDefinition.UpdateAsync(modelDef, token).ConfigureAwait(false);
                        counter.Updated++;
                        break;
                    case ConflictResolutionMode.Fail:
                        throw new InvalidOperationException("Model Definition with ID '" + modelDef.Id + "' already exists.");
                }
            }
            else
            {
                await _Database.ModelDefinition.CreateAsync(modelDef, token).ConfigureAwait(false);
                counter.Created++;
            }
        }

        internal async Task RestoreModelConfigurationAsync(ModelConfiguration modelConfig, ConflictResolutionMode conflictResolution, EntityRestoreCount counter, CancellationToken token)
        {
            bool exists = await _Database.ModelConfiguration.ExistsAsync(modelConfig.TenantId, modelConfig.Id, token).ConfigureAwait(false);

            if (exists)
            {
                switch (conflictResolution)
                {
                    case ConflictResolutionMode.Skip:
                        counter.Skipped++;
                        break;
                    case ConflictResolutionMode.Overwrite:
                        await _Database.ModelConfiguration.UpdateAsync(modelConfig, token).ConfigureAwait(false);
                        counter.Updated++;
                        break;
                    case ConflictResolutionMode.Fail:
                        throw new InvalidOperationException("Model Configuration with ID '" + modelConfig.Id + "' already exists.");
                }
            }
            else
            {
                await _Database.ModelConfiguration.CreateAsync(modelConfig, token).ConfigureAwait(false);
                counter.Created++;
            }
        }

        internal async Task RestoreVirtualModelRunnerAsync(VirtualModelRunner vmr, ConflictResolutionMode conflictResolution, EntityRestoreCount counter, CancellationToken token)
        {
            bool exists = await _Database.VirtualModelRunner.ExistsAsync(vmr.TenantId, vmr.Id, token).ConfigureAwait(false);

            if (exists)
            {
                switch (conflictResolution)
                {
                    case ConflictResolutionMode.Skip:
                        counter.Skipped++;
                        break;
                    case ConflictResolutionMode.Overwrite:
                        await _Database.VirtualModelRunner.UpdateAsync(vmr, token).ConfigureAwait(false);
                        counter.Updated++;
                        break;
                    case ConflictResolutionMode.Fail:
                        throw new InvalidOperationException("Virtual Model Runner with ID '" + vmr.Id + "' already exists.");
                }
            }
            else
            {
                await _Database.VirtualModelRunner.CreateAsync(vmr, token).ConfigureAwait(false);
                counter.Created++;
            }
        }

        internal async Task RestoreLoadBalancingPolicyAsync(LoadBalancingPolicy policy, ConflictResolutionMode conflictResolution, EntityRestoreCount counter, CancellationToken token)
        {
            bool exists = await _Database.LoadBalancingPolicy.ExistsAsync(policy.TenantId, policy.Id, token).ConfigureAwait(false);

            if (exists)
            {
                switch (conflictResolution)
                {
                    case ConflictResolutionMode.Skip:
                        counter.Skipped++;
                        break;
                    case ConflictResolutionMode.Overwrite:
                        await _Database.LoadBalancingPolicy.UpdateAsync(policy, token).ConfigureAwait(false);
                        counter.Updated++;
                        break;
                    case ConflictResolutionMode.Fail:
                        throw new InvalidOperationException("Load Balancing Policy with ID '" + policy.Id + "' already exists.");
                }
            }
            else
            {
                await _Database.LoadBalancingPolicy.CreateAsync(policy, token).ConfigureAwait(false);
                counter.Created++;
            }
        }

        internal async Task RestoreModelAccessPolicyAsync(ModelAccessPolicy policy, ConflictResolutionMode conflictResolution, EntityRestoreCount counter, CancellationToken token)
        {
            bool exists = await _Database.ModelAccessPolicy.ExistsAsync(policy.TenantId, policy.Id, token).ConfigureAwait(false);

            if (exists)
            {
                switch (conflictResolution)
                {
                    case ConflictResolutionMode.Skip:
                        counter.Skipped++;
                        break;
                    case ConflictResolutionMode.Overwrite:
                        await _Database.ModelAccessPolicy.UpdateAsync(policy, token).ConfigureAwait(false);
                        counter.Updated++;
                        break;
                    case ConflictResolutionMode.Fail:
                        throw new InvalidOperationException("Model Access Policy with ID '" + policy.Id + "' already exists.");
                }
            }
            else
            {
                await _Database.ModelAccessPolicy.CreateAsync(policy, token).ConfigureAwait(false);
                counter.Created++;
            }
        }

        internal async Task RestoreModelAccessRuleAsync(ModelAccessRule rule, ConflictResolutionMode conflictResolution, EntityRestoreCount counter, CancellationToken token)
        {
            bool exists = await _Database.ModelAccessPolicy.ExistsRuleAsync(rule.TenantId, rule.PolicyId, rule.Id, token).ConfigureAwait(false);

            if (exists)
            {
                switch (conflictResolution)
                {
                    case ConflictResolutionMode.Skip:
                        counter.Skipped++;
                        break;
                    case ConflictResolutionMode.Overwrite:
                        await _Database.ModelAccessPolicy.UpdateRuleAsync(rule, token).ConfigureAwait(false);
                        counter.Updated++;
                        break;
                    case ConflictResolutionMode.Fail:
                        throw new InvalidOperationException("Model Access Rule with ID '" + rule.Id + "' already exists.");
                }
            }
            else
            {
                await _Database.ModelAccessPolicy.CreateRuleAsync(rule, token).ConfigureAwait(false);
                counter.Created++;
            }
        }
    }
}
