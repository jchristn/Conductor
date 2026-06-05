namespace Conductor.Server.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server;
    using WatsonWebserver.Core.OpenApi;
    using Controllers = Conductor.Server.Controllers;
    using Services = Conductor.Server.Services;

    internal sealed class BackupRouteModule : ConductorRouteModule
    {
        internal BackupRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {

            _App.Get("/v1.0/backup", async (req) =>
            {
                // Support both admin auth and user auth (for users with IsAdmin=true)
                string createdBy = "unknown";
                if (req.Http.Metadata is Services.AdminAuthenticationResult adminAuth)
                {
                    createdBy = adminAuth.Administrator?.Email ?? "admin";
                }
                else if (req.Http.Metadata is Services.AuthenticationResult userAuth)
                {
                    createdBy = userAuth.User?.Email ?? "user";
                }
                return await backupController.CreateBackup(createdBy);
            },
            api => api
                .WithTag("Backup")
                .WithSummary("Create backup")
                .WithDescription("Create a complete backup of all Conductor configuration data")
                .WithSecurity("Bearer")
                .WithResponse(200, Api.JsonResponse<BackupPackage>("Complete backup package"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<RestoreRequest>("/v1.0/backup/restore", async (req) =>
                await backupController.RestoreBackup(req.Data as RestoreRequest),
            api => api
                .WithTag("Backup")
                .WithSummary("Restore from backup")
                .WithDescription("Restore configuration from a backup package")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<RestoreRequest>("Restore request with backup package and options", true))
                .WithResponse(200, Api.JsonResponse<RestoreResult>("Restore operation result"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

            _App.Post<BackupPackage>("/v1.0/backup/validate", async (req) =>
                await backupController.ValidateBackup(req.Data as BackupPackage),
            api => api
                .WithTag("Backup")
                .WithSummary("Validate backup")
                .WithDescription("Validate a backup package without applying changes")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<BackupPackage>("Backup package to validate", true))
                .WithResponse(200, Api.JsonResponse<ValidationResult>("Validation result with conflicts"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized()),
            auth: true);

        }
    }
}
