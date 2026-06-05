namespace Conductor.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using Conductor.Core.Enums;

    /// <summary>
    /// Request to load or verify a model on a model runner endpoint or virtual model runner.
    /// </summary>
    public class ModelLoadRequest
    {
        /// <summary>
        /// Model name or provider tag to load.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Optional model definition identifier used when loading through a virtual model runner.
        /// </summary>
        public string ModelDefinitionId { get; set; } = null;

        /// <summary>
        /// Provider probe mechanism. Default is <see cref="ModelLoadProbeKindEnum.Auto"/>.
        /// </summary>
        public ModelLoadProbeKindEnum ProbeKind { get; set; } = ModelLoadProbeKindEnum.Auto;

        /// <summary>
        /// Virtual model runner target mode. Default is <see cref="ModelLoadTargetModeEnum.SelectedEndpoint"/>.
        /// </summary>
        public ModelLoadTargetModeEnum TargetMode { get; set; } = ModelLoadTargetModeEnum.SelectedEndpoint;

        /// <summary>
        /// Specific endpoint identifiers used when <see cref="TargetMode"/> is <see cref="ModelLoadTargetModeEnum.SpecificEndpointIds"/>.
        /// </summary>
        public List<string> EndpointIds
        {
            get
            {
                return _EndpointIds;
            }
            set
            {
                _EndpointIds = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Tiny input text used by generation or embedding probes. Default is "conductor warmup".
        /// </summary>
        public string InputText
        {
            get
            {
                return _InputText;
            }
            set
            {
                _InputText = String.IsNullOrWhiteSpace(value) ? "conductor warmup" : value;
            }
        }

        /// <summary>
        /// Provider-specific keep-alive hint. Used by Ollama and ignored by providers that do not support it.
        /// </summary>
        public string KeepAlive { get; set; } = "30m";

        /// <summary>
        /// Per-upstream attempt timeout in milliseconds. Minimum is 1000, maximum is 1800000, default is 300000.
        /// </summary>
        public int TimeoutMs
        {
            get
            {
                return _TimeoutMs;
            }
            set
            {
                if (value < 1000)
                {
                    _TimeoutMs = 300000;
                }
                else if (value > 1800000)
                {
                    _TimeoutMs = 1800000;
                }
                else
                {
                    _TimeoutMs = value;
                }
            }
        }

        /// <summary>
        /// Number of retry attempts after the initial upstream attempt. Minimum is 0, maximum is 3, default is 0.
        /// </summary>
        public int MaxRetries
        {
            get
            {
                return _MaxRetries;
            }
            set
            {
                if (value < 0)
                {
                    _MaxRetries = 0;
                }
                else if (value > 3)
                {
                    _MaxRetries = 3;
                }
                else
                {
                    _MaxRetries = value;
                }
            }
        }

        /// <summary>
        /// Whether provider-specific post-checks should verify the model is available after the probe. Default is true.
        /// </summary>
        public bool VerifyLoaded { get; set; } = true;

        /// <summary>
        /// Whether virtual model runner multi-endpoint modes may include inactive endpoints. Default is false.
        /// </summary>
        public bool IncludeInactive { get; set; } = false;

        /// <summary>
        /// Whether to return the planned provider request without sending upstream traffic. Default is false.
        /// </summary>
        public bool DryRun { get; set; } = false;

        private List<string> _EndpointIds = new List<string>();
        private string _InputText = "conductor warmup";
        private int _TimeoutMs = 300000;
        private int _MaxRetries = 0;
    }
}
