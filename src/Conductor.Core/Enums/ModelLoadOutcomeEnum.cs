namespace Conductor.Core.Enums
{
    /// <summary>
    /// Stable outcome code for a model load or verification attempt.
    /// </summary>
    public enum ModelLoadOutcomeEnum
    {
        /// <summary>
        /// The model was loaded or warmed successfully.
        /// </summary>
        Loaded = 0,

        /// <summary>
        /// The model was already reported as available or resident.
        /// </summary>
        AlreadyAvailable = 1,

        /// <summary>
        /// The model was verified through a provider API.
        /// </summary>
        Verified = 2,

        /// <summary>
        /// A hosted provider verified the request, but no host-local load action exists.
        /// </summary>
        VerifiedRemote = 3,

        /// <summary>
        /// The provider does not expose an explicit load operation for this model.
        /// </summary>
        NoExplicitLoadSupported = 4,

        /// <summary>
        /// The request was planned but no upstream request was sent.
        /// </summary>
        DryRun = 5,

        /// <summary>
        /// The endpoint was skipped.
        /// </summary>
        Skipped = 6,

        /// <summary>
        /// The load or verification attempt failed.
        /// </summary>
        Failed = 7,

        /// <summary>
        /// The load or verification attempt exceeded its timeout.
        /// </summary>
        TimedOut = 8,

        /// <summary>
        /// The upstream provider rejected the configured credential.
        /// </summary>
        UnauthorizedUpstream = 9,

        /// <summary>
        /// A model name was required but not supplied or resolvable.
        /// </summary>
        ModelRequired = 10,

        /// <summary>
        /// The requested model is not attached to the virtual model runner.
        /// </summary>
        ModelNotAttached = 11,

        /// <summary>
        /// No endpoints were eligible for the requested target mode.
        /// </summary>
        NoEligibleEndpoints = 12,

        /// <summary>
        /// The endpoint API type is not supported by model loading.
        /// </summary>
        UnsupportedApiType = 13
    }
}
