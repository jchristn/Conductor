namespace Conductor.Server.Routing
{
    using System;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Server;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using Services = Conductor.Server.Services;

    internal sealed class VirtualModelRunnerReservationRouteModule : ConductorRouteModule
    {
        internal VirtualModelRunnerReservationRouteModule(ConductorRouteContext context)
            : base(context)
        {
        }

        internal override void Register()
        {
            _App.Post<VirtualModelRunnerReservation>("/v1.0/vmrreservations", async (req) =>
            {
                VirtualModelRunnerReservation reservation = req.Data as VirtualModelRunnerReservation;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, reservation?.TenantId);
                req.Http.Response.StatusCode = 201;
                return await vmrReservationController.Create(tenantId, reservation, req.Http.Metadata as Services.AuthenticationResult);
            },
            api => api
                .WithTag("VMR Reservations")
                .WithSummary("Create VMR reservation")
                .WithDescription("Create a time-bound virtual model runner reservation with explicit user and credential participants")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<VirtualModelRunnerReservation>("Reservation to create", true))
                .WithResponse(201, Api.JsonResponse<VirtualModelRunnerReservation>("Created reservation"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
            auth: true);

            _App.Post<VirtualModelRunnerReservation>("/v1.0/vmrreservations/validate", async (req) =>
            {
                VirtualModelRunnerReservation reservation = req.Data as VirtualModelRunnerReservation;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, reservation?.TenantId);
                return await vmrReservationController.Validate(tenantId, reservation, reservation?.Id);
            },
            api => api
                .WithTag("VMR Reservations")
                .WithSummary("Validate VMR reservation")
                .WithDescription("Validate a virtual model runner reservation draft without persisting it")
                .WithSecurity("Bearer")
                .WithRequestBody(Api.JsonRequestBody<VirtualModelRunnerReservation>("Reservation draft", true))
                .WithResponse(200, Api.JsonResponse<ResourceValidationResult>("Validation result"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
            auth: true);

            _App.Get("/v1.0/vmrreservations", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await vmrReservationController.Enumerate(BuildFilter(req.Http.Request, tenantId));
            },
            api => api
                .WithTag("VMR Reservations")
                .WithSummary("List VMR reservations")
                .WithDescription("List virtual model runner reservations by tenant, VMR, state, time range, and participant")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Query("tenantId", "Tenant ID for admin or cross-tenant callers", false))
                .WithParameter(OpenApiParameterMetadata.Query("vmrId", "Virtual model runner ID", false))
                .WithParameter(OpenApiParameterMetadata.Query("state", "State filter: active, upcoming, past, inactive", false))
                .WithParameter(OpenApiParameterMetadata.Query("subjectType", "Subject type: User or Credential", false))
                .WithParameter(OpenApiParameterMetadata.Query("subjectId", "Subject ID", false))
                .WithParameter(OpenApiParameterMetadata.Query("nameFilter", "Name filter", false))
                .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum results", false, OpenApiSchemaMetadata.Integer()))
                .WithResponse(200, Api.JsonResponse<object>("Reservation list"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
            auth: true);

            _App.Get("/v1.0/vmrreservations/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await vmrReservationController.Read(tenantId, req.Parameters["id"]);
            },
            api => api
                .WithTag("VMR Reservations")
                .WithSummary("Get VMR reservation")
                .WithDescription("Read a virtual model runner reservation and its participants")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Reservation ID"))
                .WithParameter(OpenApiParameterMetadata.Query("tenantId", "Tenant ID for admin or cross-tenant callers", false))
                .WithResponse(200, Api.JsonResponse<VirtualModelRunnerReservation>("Reservation"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Put<VirtualModelRunnerReservation>("/v1.0/vmrreservations/{id}", async (req) =>
            {
                VirtualModelRunnerReservation reservation = req.Data as VirtualModelRunnerReservation;
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, reservation?.TenantId);
                return await vmrReservationController.Update(tenantId, req.Parameters["id"], reservation);
            },
            api => api
                .WithTag("VMR Reservations")
                .WithSummary("Update VMR reservation")
                .WithDescription("Update a reservation and replace its participant list")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Reservation ID"))
                .WithRequestBody(Api.JsonRequestBody<VirtualModelRunnerReservation>("Reservation update", true))
                .WithResponse(200, Api.JsonResponse<VirtualModelRunnerReservation>("Updated reservation"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Delete("/v1.0/vmrreservations/{id}", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                req.Http.Response.StatusCode = 204;
                await vmrReservationController.Delete(tenantId, req.Parameters["id"]);
                return null;
            },
            api => api
                .WithTag("VMR Reservations")
                .WithSummary("Deactivate VMR reservation")
                .WithDescription("Deactivate a virtual model runner reservation while retaining audit history")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Reservation ID"))
                .WithParameter(OpenApiParameterMetadata.Query("tenantId", "Tenant ID for admin or cross-tenant callers", false))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden())
                .WithResponse(404, OpenApiResponseMetadata.NotFound()),
            auth: true);

            _App.Get("/v1.0/virtualmodelrunners/{id}/reservations", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                VirtualModelRunnerReservationFilter filter = BuildFilter(req.Http.Request, tenantId);
                filter.VirtualModelRunnerId = req.Parameters["id"];
                return await vmrReservationController.Enumerate(filter);
            },
            api => api
                .WithTag("VMR Reservations")
                .WithSummary("List reservations for VMR")
                .WithDescription("List reservations scoped to a virtual model runner")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Virtual model runner ID"))
                .WithParameter(OpenApiParameterMetadata.Query("tenantId", "Tenant ID for admin or cross-tenant callers", false))
                .WithResponse(200, Api.JsonResponse<object>("Reservation list"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
            auth: true);

            _App.Get("/v1.0/virtualmodelrunners/{id}/reservation-effective", async (req) =>
            {
                string tenantId = GetTenantIdFromAuth(req.Http.Metadata, req.Http.Request.Query.Elements.Get("tenantId"));
                return await vmrReservationController.EvaluateEffectiveAsync(
                    tenantId,
                    req.Parameters["id"],
                    req.Http.Request.Query.Elements.Get("userId"),
                    req.Http.Request.Query.Elements.Get("credentialId"),
                    ParseUtcQueryValue(req.Http.Request.Query.Elements.Get("atUtc")));
            },
            api => api
                .WithTag("VMR Reservations")
                .WithSummary("Evaluate effective reservation access")
                .WithDescription("Explain whether a user or credential would be admitted through the reservation gate at a specific time")
                .WithSecurity("Bearer")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Virtual model runner ID"))
                .WithParameter(OpenApiParameterMetadata.Query("tenantId", "Tenant ID for admin or cross-tenant callers", false))
                .WithParameter(OpenApiParameterMetadata.Query("userId", "Candidate user ID", false))
                .WithParameter(OpenApiParameterMetadata.Query("credentialId", "Candidate credential ID", false))
                .WithParameter(OpenApiParameterMetadata.Query("atUtc", "Evaluation time in UTC", false))
                .WithResponse(200, Api.JsonResponse<ReservationEvaluationResult>("Reservation evaluation"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                .WithResponse(403, OpenApiResponseMetadata.Forbidden()),
            auth: true);
        }

        private static VirtualModelRunnerReservationFilter BuildFilter(HttpRequestBase request, string tenantId)
        {
            return new VirtualModelRunnerReservationFilter
            {
                TenantId = tenantId,
                VirtualModelRunnerId = request.Query.Elements.Get("vmrId"),
                State = request.Query.Elements.Get("state"),
                SubjectType = ParseSubjectType(request.Query.Elements.Get("subjectType")),
                SubjectId = request.Query.Elements.Get("subjectId"),
                StartsBeforeUtc = ParseUtcQueryValue(request.Query.Elements.Get("startsBeforeUtc")),
                EndsAfterUtc = ParseUtcQueryValue(request.Query.Elements.Get("endsAfterUtc")),
                NameFilter = request.Query.Elements.Get("nameFilter"),
                ActiveFilter = ParseNullableBool(request.Query.Elements.Get("activeFilter")),
                MaxResults = ParsePositiveInt(request.Query.Elements.Get("maxResults"), 100),
                ContinuationToken = request.Query.Elements.Get("continuationToken")
            };
        }

        private static ReservationSubjectTypeEnum? ParseSubjectType(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Enum.TryParse(value, true, out ReservationSubjectTypeEnum parsed))
            {
                return parsed;
            }

            return null;
        }

        private static bool? ParseNullableBool(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Boolean.TryParse(value, out bool parsed))
            {
                return parsed;
            }

            return null;
        }

        private static int ParsePositiveInt(string value, int defaultValue)
        {
            if (Int32.TryParse(value, out int parsed) && parsed > 0)
            {
                return parsed;
            }

            return defaultValue;
        }

        private static DateTime? ParseUtcQueryValue(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                return parsed.ToUniversalTime();
            }

            return null;
        }
    }
}
