namespace Conductor.Server.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using Conductor.Server.Controllers;
    using Conductor.Server.Services;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Routing;
    using HttpMethod = WatsonWebserver.Core.HttpMethod;

    internal abstract class ConductorRouteModule
    {
        protected readonly string _Header;
        protected readonly Webserver _App;
        protected readonly ServerSettings _Settings;
        protected readonly SyslogLogging.LoggingModule _Logging;
        protected readonly AuthenticationService _AuthService;
        protected readonly OperationalMetricsService _OperationalMetricsService;

        protected readonly TenantController tenantController;
        protected readonly UserController userController;
        protected readonly CredentialController credentialController;
        protected readonly ModelRunnerEndpointController mreController;
        protected readonly EndpointGroupController endpointGroupController;
        protected readonly ModelDefinitionController mdController;
        protected readonly ModelConfigurationController mcController;
        protected readonly LoadBalancingPolicyController lbpController;
        protected readonly ModelAccessPolicyController mapController;
        protected readonly VirtualModelRunnerController vmrController;
        protected readonly VirtualModelRunnerReservationController vmrReservationController;
        protected readonly AuthController authController;
        protected readonly AdministratorController adminController;
        protected readonly BackupController backupController;
        protected readonly RequestHistoryController requestHistoryController;

        protected ConductorRouteModule(ConductorRouteContext context)
        {
            _Header = context.Header;
            _App = context.App;
            _Settings = context.Settings;
            _Logging = context.Logging;
            _AuthService = context.AuthService;
            _OperationalMetricsService = context.OperationalMetricsService;
            tenantController = context.TenantController;
            userController = context.UserController;
            credentialController = context.CredentialController;
            mreController = context.ModelRunnerEndpointController;
            endpointGroupController = context.EndpointGroupController;
            mdController = context.ModelDefinitionController;
            mcController = context.ModelConfigurationController;
            lbpController = context.LoadBalancingPolicyController;
            mapController = context.ModelAccessPolicyController;
            vmrController = context.VirtualModelRunnerController;
            vmrReservationController = context.VirtualModelRunnerReservationController;
            authController = context.AuthController;
            adminController = context.AdministratorController;
            backupController = context.BackupController;
            requestHistoryController = context.RequestHistoryController;
        }

        internal abstract void Register();

        protected string GetTenantIdFromAuth(object metadata, string requestBodyTenantId)
        {
            if (metadata is AuthenticationResult userAuth)
            {
                if (userAuth.IsAdmin)
                {
                    return !String.IsNullOrEmpty(requestBodyTenantId) ? requestBodyTenantId : null;
                }

                return userAuth.Tenant?.Id;
            }

            if (metadata is AdminAuthenticationResult)
            {
                return requestBodyTenantId;
            }

            return null;
        }

        protected string GetUserIdFromAuth(object metadata, string requestBodyUserId)
        {
            if (metadata is AuthenticationResult userAuth)
            {
                return userAuth.User?.Id;
            }

            if (metadata is AdminAuthenticationResult)
            {
                return requestBodyUserId;
            }

            return null;
        }

        protected RequestHistorySearchFilter BuildRequestHistorySearchFilter(HttpRequestBase request, string tenantId)
        {
            RequestHistorySearchFilter filter = new RequestHistorySearchFilter
            {
                TenantGuid = tenantId,
                VirtualModelRunnerGuid = request.Query.Elements.Get("vmrGuid"),
                ModelEndpointGuid = request.Query.Elements.Get("endpointGuid"),
                RequestorUserGuid = request.Query.Elements.Get("requestorUserGuid"),
                CredentialGuid = request.Query.Elements.Get("credentialGuid"),
                LoadBalancingPolicyGuid = request.Query.Elements.Get("loadBalancingPolicyGuid"),
                ModelAccessPolicyGuid = request.Query.Elements.Get("modelAccessPolicyGuid"),
                ModelAccessRuleGuid = request.Query.Elements.Get("modelAccessRuleGuid"),
                ModelAccessDecision = request.Query.Elements.Get("modelAccessDecision"),
                ModelAccessWouldDeny = ParseNullableBool(request.Query.Elements.Get("modelAccessWouldDeny")),
                ModelName = request.Query.Elements.Get("modelName"),
                MutationSummary = request.Query.Elements.Get("mutationSummary"),
                SelectionStrategy = request.Query.Elements.Get("selectionStrategy"),
                EndpointGroupGuid = request.Query.Elements.Get("endpointGroupGuid"),
                BackoffReason = request.Query.Elements.Get("backoffReason"),
                AdaptiveSelection = ParseNullableBool(request.Query.Elements.Get("adaptiveSelection")),
                PolicyFallbackUsed = ParseNullableBool(request.Query.Elements.Get("policyFallbackUsed")),
                DenialReasonCode = request.Query.Elements.Get("denialReasonCode"),
                ReservationGuid = request.Query.Elements.Get("reservationGuid"),
                ReservationDecision = request.Query.Elements.Get("reservationDecision"),
                ReservationReasonCode = request.Query.Elements.Get("reservationReasonCode"),
                SessionAffinityOutcome = request.Query.Elements.Get("sessionAffinityOutcome"),
                StatusClass = request.Query.Elements.Get("statusClass"),
                CreatedAfterUtc = ParseUtcQueryValue(request.Query.Elements.Get("createdAfterUtc")),
                CreatedBeforeUtc = ParseUtcQueryValue(request.Query.Elements.Get("createdBeforeUtc")),
                RequestorSourceIp = request.Query.Elements.Get("sourceIp"),
                HttpStatus = ParseNullableInt(request.Query.Elements.Get("httpStatus")),
                Page = ParsePositiveInt(request.Query.Elements.Get("page"), 1),
                PageSize = ParsePositiveInt(request.Query.Elements.Get("pageSize"), 10)
            };

            return filter;
        }

        protected RequestHistorySummaryFilter BuildRequestHistorySummaryFilter(HttpRequestBase request, string tenantId)
        {
            DateTime? startUtc = ParseUtcQueryValue(request.Query.Elements.Get("startUtc"));
            DateTime? endUtc = ParseUtcQueryValue(request.Query.Elements.Get("endUtc"));

            RequestHistorySummaryFilter filter = new RequestHistorySummaryFilter
            {
                TenantGuid = tenantId,
                VirtualModelRunnerGuid = request.Query.Elements.Get("vmrGuid"),
                ModelEndpointGuid = request.Query.Elements.Get("endpointGuid"),
                RequestorUserGuid = request.Query.Elements.Get("requestorUserGuid"),
                CredentialGuid = request.Query.Elements.Get("credentialGuid"),
                LoadBalancingPolicyGuid = request.Query.Elements.Get("loadBalancingPolicyGuid"),
                ModelAccessPolicyGuid = request.Query.Elements.Get("modelAccessPolicyGuid"),
                ModelAccessRuleGuid = request.Query.Elements.Get("modelAccessRuleGuid"),
                ModelAccessDecision = request.Query.Elements.Get("modelAccessDecision"),
                ModelAccessWouldDeny = ParseNullableBool(request.Query.Elements.Get("modelAccessWouldDeny")),
                ModelName = request.Query.Elements.Get("modelName"),
                MutationSummary = request.Query.Elements.Get("mutationSummary"),
                SelectionStrategy = request.Query.Elements.Get("selectionStrategy"),
                EndpointGroupGuid = request.Query.Elements.Get("endpointGroupGuid"),
                BackoffReason = request.Query.Elements.Get("backoffReason"),
                AdaptiveSelection = ParseNullableBool(request.Query.Elements.Get("adaptiveSelection")),
                PolicyFallbackUsed = ParseNullableBool(request.Query.Elements.Get("policyFallbackUsed")),
                DenialReasonCode = request.Query.Elements.Get("denialReasonCode"),
                ReservationGuid = request.Query.Elements.Get("reservationGuid"),
                ReservationDecision = request.Query.Elements.Get("reservationDecision"),
                ReservationReasonCode = request.Query.Elements.Get("reservationReasonCode"),
                SessionAffinityOutcome = request.Query.Elements.Get("sessionAffinityOutcome"),
                StatusClass = request.Query.Elements.Get("statusClass"),
                RequestorSourceIp = request.Query.Elements.Get("sourceIp"),
                HttpStatus = ParseNullableInt(request.Query.Elements.Get("httpStatus")),
                StartUtc = startUtc ?? DateTime.UtcNow.AddHours(-1),
                EndUtc = endUtc ?? DateTime.UtcNow,
                Interval = request.Query.Elements.Get("interval") ?? "hour"
            };

            return filter;
        }

        protected RequestAnalyticsFilter BuildRequestAnalyticsFilter(HttpRequestBase request, string tenantId)
        {
            DateTime? startUtc = ParseUtcQueryValue(request.Query.Elements.Get("startUtc"));
            DateTime? endUtc = ParseUtcQueryValue(request.Query.Elements.Get("endUtc"));
            int? bucketSeconds = ParseNullableInt(request.Query.Elements.Get("bucketSeconds"));
            int limit = ParsePositiveInt(request.Query.Elements.Get("limit"), 10000);

            RequestAnalyticsFilter filter = new RequestAnalyticsFilter
            {
                TenantGuid = tenantId,
                VirtualModelRunnerGuid = request.Query.Elements.Get("vmrGuid"),
                ModelEndpointGuid = request.Query.Elements.Get("endpointGuid"),
                ProviderName = request.Query.Elements.Get("providerName"),
                ModelName = request.Query.Elements.Get("modelName"),
                ModelAccessPolicyGuid = request.Query.Elements.Get("modelAccessPolicyGuid"),
                ModelAccessRuleGuid = request.Query.Elements.Get("modelAccessRuleGuid"),
                ModelAccessDecision = request.Query.Elements.Get("modelAccessDecision"),
                ModelAccessWouldDeny = ParseNullableBool(request.Query.Elements.Get("modelAccessWouldDeny")),
                ReservationGuid = request.Query.Elements.Get("reservationGuid"),
                ReservationDecision = request.Query.Elements.Get("reservationDecision"),
                ReservationReasonCode = request.Query.Elements.Get("reservationReasonCode"),
                StageKind = request.Query.Elements.Get("stageKind"),
                StatusClass = request.Query.Elements.Get("statusClass"),
                Range = request.Query.Elements.Get("range") ?? "lastDay",
                StartUtc = startUtc,
                EndUtc = endUtc,
                BucketSeconds = bucketSeconds,
                Limit = limit
            };

            return filter;
        }

        protected async Task<AuthResult> AuthenticationRoute(HttpContextBase ctx)
        {
            string method = ctx.Request.Method.ToString();
            string path = ctx.Request.Url.RawWithoutQuery;
            RequestTypeEnum requestType = RequestTypeResolver.Resolve(method, path);

            if (Core.Authorization.AuthorizationConfig.IsPublic(requestType))
            {
                return new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.Success,
                    AuthorizationResult = AuthorizationResultEnum.Permitted
                };
            }

            bool requiresAdminAuth = Core.Authorization.AuthorizationConfig.RequiresAdmin(requestType);

            AdminAuthenticationResult adminAuth = await _AuthService.AuthenticateAdminAsync(ctx).ConfigureAwait(false);
            if (adminAuth.IsAuthenticated)
            {
                adminAuth.RequestType = requestType;
                ctx.Metadata = adminAuth;
                return new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.Success,
                    AuthorizationResult = AuthorizationResultEnum.Permitted
                };
            }

            if (requiresAdminAuth)
            {
                return new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.NotFound,
                    AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
                };
            }

            AuthenticationResult userAuth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            if (userAuth.IsAuthenticated)
            {
                userAuth.RequestType = requestType;
                AuthorizationResultEnum authzResult = CheckUserAuthorization(userAuth, requestType);
                if (authzResult != AuthorizationResultEnum.Permitted)
                {
                    return new AuthResult
                    {
                        AuthenticationResult = AuthenticationResultEnum.Success,
                        AuthorizationResult = authzResult
                    };
                }

                ctx.Metadata = userAuth;
                return new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.Success,
                    AuthorizationResult = AuthorizationResultEnum.Permitted
                };
            }

            return new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.NotFound,
                AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
            };
        }

        protected void ApplyCorsHeaders(HttpResponseBase response, HttpRequestBase request)
        {
            if (_Settings.Webserver.Cors == null || !_Settings.Webserver.Cors.Enabled) return;

            CorsSettings cors = _Settings.Webserver.Cors;
            string origin = request.Headers.Get("Origin");
            if (String.IsNullOrEmpty(origin)) return;

            bool originAllowed = false;
            string allowedOriginValue = null;

            if (cors.AllowedOrigins.Contains("*"))
            {
                originAllowed = true;
                allowedOriginValue = cors.AllowCredentials ? origin : "*";
            }
            else
            {
                foreach (string allowedOrigin in cors.AllowedOrigins)
                {
                    if (String.Equals(allowedOrigin, origin, StringComparison.OrdinalIgnoreCase))
                    {
                        originAllowed = true;
                        allowedOriginValue = origin;
                        break;
                    }
                }
            }

            if (!originAllowed) return;

            response.Headers.Add("Access-Control-Allow-Origin", allowedOriginValue);

            if (cors.AllowedMethods != null && cors.AllowedMethods.Count > 0)
            {
                response.Headers.Add("Access-Control-Allow-Methods", String.Join(", ", cors.AllowedMethods));
            }

            if (cors.AllowedHeaders != null && cors.AllowedHeaders.Count > 0)
            {
                response.Headers.Add("Access-Control-Allow-Headers", String.Join(", ", cors.AllowedHeaders));
            }

            if (cors.ExposedHeaders != null && cors.ExposedHeaders.Count > 0)
            {
                response.Headers.Add("Access-Control-Expose-Headers", String.Join(", ", cors.ExposedHeaders));
            }

            if (cors.AllowCredentials)
            {
                response.Headers.Add("Access-Control-Allow-Credentials", "true");
            }

            if (request.Method == HttpMethod.OPTIONS && cors.MaxAgeSeconds > 0)
            {
                response.Headers.Add("Access-Control-Max-Age", cors.MaxAgeSeconds.ToString());
            }
        }

        private static AuthorizationResultEnum CheckUserAuthorization(AuthenticationResult userAuth, RequestTypeEnum requestType)
        {
            if (Core.Authorization.AuthorizationConfig.RequiresGlobalAdmin(requestType) && !userAuth.IsAdmin)
            {
                return AuthorizationResultEnum.DeniedImplicit;
            }

            if (Core.Authorization.AuthorizationConfig.RequiresAnalyticsRead(requestType)
                && !Core.Authorization.AuthorizationConfig.UserHasAnalyticsReadAccess(userAuth.User))
            {
                return AuthorizationResultEnum.DeniedImplicit;
            }

            if (Core.Authorization.AuthorizationConfig.RequiresTenantAdmin(requestType) && !userAuth.IsAdmin && !userAuth.IsTenantAdmin)
            {
                return AuthorizationResultEnum.DeniedImplicit;
            }

            return AuthorizationResultEnum.Permitted;
        }

        private static int? ParseNullableInt(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Int32.TryParse(value, out int parsed))
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
