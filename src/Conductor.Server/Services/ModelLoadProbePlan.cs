namespace Conductor.Server.Services
{
    using System.Collections.Generic;
    using Conductor.Core.Enums;

    /// <summary>
    /// Provider request plan for a model load or verification attempt.
    /// </summary>
    public class ModelLoadProbePlan
    {
        /// <summary>
        /// HTTP method to send upstream.
        /// </summary>
        public string Method { get; set; } = "GET";

        /// <summary>
        /// Provider-relative request path.
        /// </summary>
        public string Path { get; set; } = "/";

        /// <summary>
        /// Request body JSON. Null when the plan has no request body.
        /// </summary>
        public string BodyJson { get; set; } = null;

        /// <summary>
        /// Mechanism identifier shown to operators.
        /// </summary>
        public string Mechanism { get; set; } = null;

        /// <summary>
        /// Effective probe kind after resolving Auto.
        /// </summary>
        public ModelLoadProbeKindEnum EffectiveProbeKind { get; set; } = ModelLoadProbeKindEnum.Auto;

        /// <summary>
        /// Whether the plan attempts an explicit load or warmup rather than metadata verification only.
        /// </summary>
        public bool ExplicitLoad { get; set; } = false;

        /// <summary>
        /// Whether the plan only verifies model metadata.
        /// </summary>
        public bool MetadataOnly { get; set; } = false;

        /// <summary>
        /// Whether the upstream provider has a host-local load primitive for this plan.
        /// </summary>
        public bool HostLocalLoadSupported { get; set; } = false;

        /// <summary>
        /// Request fields ignored because the provider does not support them.
        /// </summary>
        public List<string> IgnoredFields { get; set; } = new List<string>();
    }
}
