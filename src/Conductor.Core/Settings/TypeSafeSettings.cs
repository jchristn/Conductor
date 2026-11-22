namespace Conductor.Core.Settings
{
    using System;

    /// <summary>
    /// Settings for TypeSafe System One (hosted TypeSafe and Ollaya) endpoints and virtual model runners.
    /// Instances are not thread safe for concurrent mutation; treat a loaded instance as read-only.
    /// </summary>
    public class TypeSafeSettings
    {
        /// <summary>
        /// Minimum value for MaxQuestionsPerRequest.
        /// </summary>
        public const int MinimumQuestionsPerRequest = 1;

        /// <summary>
        /// Maximum value for MaxQuestionsPerRequest.
        /// </summary>
        public const int MaximumQuestionsPerRequest = 1024;

        /// <summary>
        /// The model used by health and model load decision probes when none is supplied.
        /// Null or empty values reset to the default. Default: jev-latest.
        /// </summary>
        public string DefaultModel
        {
            get => _DefaultModel;
            set => _DefaultModel = (String.IsNullOrWhiteSpace(value) ? _DefaultModelValue : value.Trim());
        }

        /// <summary>
        /// The maximum number of questions accepted in one decision request. Requests with more questions, or with an
        /// empty questions object, are rejected with HTTP 400. Minimum 1, maximum 1024; out-of-range values are clamped.
        /// Default: 64.
        /// </summary>
        public int MaxQuestionsPerRequest
        {
            get => _MaxQuestionsPerRequest;
            set => _MaxQuestionsPerRequest = (value < MinimumQuestionsPerRequest
                ? MinimumQuestionsPerRequest
                : (value > MaximumQuestionsPerRequest ? MaximumQuestionsPerRequest : value));
        }

        private const string _DefaultModelValue = "jev-latest";
        private string _DefaultModel = _DefaultModelValue;
        private int _MaxQuestionsPerRequest = 64;

        /// <summary>
        /// Instantiate TypeSafe settings with defaults.
        /// </summary>
        public TypeSafeSettings()
        {
        }
    }
}
