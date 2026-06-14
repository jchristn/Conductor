namespace Test.Shared.Core.Models
{
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Serialization;
    using FluentAssertions;

    /// <summary>
    /// Unit tests for model access evaluation contracts.
    /// </summary>
    public class ModelAccessEvaluationTests
    {
        #region Context-Tests

        public void EvaluationContext_LabelLists_DefaultToEmpty()
        {
            ModelAccessEvaluationContext context = new ModelAccessEvaluationContext();

            context.UserLabels.Should().NotBeNull();
            context.UserLabels.Should().BeEmpty();
            context.CredentialLabels.Should().NotBeNull();
            context.CredentialLabels.Should().BeEmpty();
            context.ModelLabels.Should().NotBeNull();
            context.ModelLabels.Should().BeEmpty();
        }

        public void EvaluationContext_LabelLists_WhenSetToNull_BecomeEmpty()
        {
            ModelAccessEvaluationContext context = new ModelAccessEvaluationContext
            {
                UserLabels = null,
                CredentialLabels = null,
                ModelLabels = null
            };

            context.UserLabels.Should().NotBeNull();
            context.UserLabels.Should().BeEmpty();
            context.CredentialLabels.Should().NotBeNull();
            context.CredentialLabels.Should().BeEmpty();
            context.ModelLabels.Should().NotBeNull();
            context.ModelLabels.Should().BeEmpty();
        }

        public void EvaluationContext_HasExpectedDefaults()
        {
            ModelAccessEvaluationContext context = new ModelAccessEvaluationContext();

            context.Action.Should().Be(ModelAccessActionEnum.Completions);
            context.RequestType.Should().Be(RequestTypeEnum.Unknown);
            context.ApiType.Should().Be(ApiTypeEnum.Ollama);
        }

        #endregion

        #region Result-Tests

        public void EvaluationResult_DefaultsToPermitAndDisabled()
        {
            ModelAccessEvaluationResult result = new ModelAccessEvaluationResult();

            result.Decision.Should().Be(ModelAccessDefaultDecisionEnum.Permit);
            result.Mode.Should().Be(ModelAccessEnforcementModeEnum.Disabled);
            result.Allowed.Should().BeTrue();
        }

        public void EvaluationResult_DenyInEnforceMode_IsNotAllowed()
        {
            ModelAccessEvaluationResult result = new ModelAccessEvaluationResult
            {
                Decision = ModelAccessDefaultDecisionEnum.Deny,
                Mode = ModelAccessEnforcementModeEnum.Enforce
            };

            result.Allowed.Should().BeFalse();
        }

        public void EvaluationResult_DenyInMonitorMode_IsAllowedAndCanWouldDeny()
        {
            ModelAccessEvaluationResult result = new ModelAccessEvaluationResult
            {
                Decision = ModelAccessDefaultDecisionEnum.Deny,
                Mode = ModelAccessEnforcementModeEnum.Monitor,
                WouldDeny = true
            };

            result.Allowed.Should().BeTrue();
            result.WouldDeny.Should().BeTrue();
        }

        public void EvaluationResult_DenyInDisabledMode_IsAllowed()
        {
            ModelAccessEvaluationResult result = new ModelAccessEvaluationResult
            {
                Decision = ModelAccessDefaultDecisionEnum.Deny,
                Mode = ModelAccessEnforcementModeEnum.Disabled
            };

            result.Allowed.Should().BeTrue();
        }

        public void EvaluationResult_SerializesAllowedComputedProperty()
        {
            ModelAccessEvaluationResult result = new ModelAccessEvaluationResult
            {
                Decision = ModelAccessDefaultDecisionEnum.Deny,
                Mode = ModelAccessEnforcementModeEnum.Enforce
            };

            string json = new Serializer().SerializeJson(result, false);

            json.Should().Contain("\"Allowed\":false");
        }

        #endregion
    }
}
