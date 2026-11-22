namespace Conductor.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;

    /// <summary>
    /// The API type rules every virtual model runner write must satisfy, shared by the REST validation service, backup
    /// restore and the MCP server: VmrApiTypeNotClientFacing, EndpointApiTypeMismatch (compatibility table) and
    /// DecisionsDisabledOnDecisionRoute. All members are static; they mutate only the result passed in.
    /// </summary>
    public static class VirtualModelRunnerTypeRules
    {
        /// <summary>
        /// Add errors for API type rule violations to a validation result.
        /// </summary>
        /// <param name="vmr">Virtual model runner draft. Must not be null.</param>
        /// <param name="endpoints">Resolved attached endpoints. Null is treated as empty.</param>
        /// <param name="result">Validation result to append errors to. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when vmr or result is null.</exception>
        public static void Validate(VirtualModelRunner vmr, IEnumerable<ModelRunnerEndpoint> endpoints, ResourceValidationResult result)
        {
            ArgumentNullException.ThrowIfNull(vmr);
            ArgumentNullException.ThrowIfNull(result);

            if (!ApiTypeCompatibility.IsClientFacing(vmr.ApiType))
            {
                AddError(result, "VmrApiTypeNotClientFacing", "ApiType", "API type '" + vmr.ApiType + "' is an endpoint-only type and cannot be used for a virtual model runner. Use one of: "
                    + String.Join(", ", ApiTypeCompatibility.GetClientFacingTypes()) + ".");
            }

            if (vmr.ApiType == ApiTypeEnum.TypeSafe && !vmr.AllowDecisions)
            {
                AddError(result, "DecisionsDisabledOnDecisionRoute", "AllowDecisions", "A TypeSafe virtual model runner serves decision requests, so AllowDecisions must be true.");
            }

            // A runner whose own type is not client-facing is already rejected above; reporting every attached endpoint as a
            // mismatch as well would only repeat that error.
            if (endpoints == null || !ApiTypeCompatibility.IsClientFacing(vmr.ApiType)) return;

            foreach (ModelRunnerEndpoint endpoint in endpoints)
            {
                if (endpoint == null) continue;
                if (ApiTypeCompatibility.AcceptsEndpoint(vmr.ApiType, endpoint.ApiType)) continue;

                AddError(result, "EndpointApiTypeMismatch", "ModelRunnerEndpointIds", BuildMismatchMessage(vmr.ApiType, endpoint.Name, endpoint.ApiType));
            }
        }

        /// <summary>
        /// Build the EndpointApiTypeMismatch message for an endpoint a virtual model runner type does not accept.
        /// </summary>
        /// <param name="vmrApiType">Virtual model runner API type.</param>
        /// <param name="endpointName">Endpoint name. Null allowed.</param>
        /// <param name="endpointApiType">Endpoint API type.</param>
        /// <returns>The message, listing the accepted endpoint types. Never null.</returns>
        public static string BuildMismatchMessage(ApiTypeEnum vmrApiType, string endpointName, ApiTypeEnum endpointApiType)
        {
            List<ApiTypeEnum> accepted = ApiTypeCompatibility.GetAcceptedEndpointTypes(vmrApiType);
            return "Endpoint '" + (endpointName ?? String.Empty) + "' has API type " + endpointApiType + ", but virtual model runners of type " + vmrApiType
                + (accepted.Count > 0
                    ? " accept only endpoints of type " + String.Join(", ", accepted) + "."
                    : " accept no endpoints.");
        }

        private static void AddError(ResourceValidationResult result, string code, string field, string message)
        {
            result.Errors.Add(new ResourceValidationIssue
            {
                Code = code,
                Field = field,
                Message = message
            });
            result.IsValid = false;
        }
    }
}
