namespace Test.Shared.Server.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using Conductor.Core.Settings;
    using FluentAssertions;
    using Test.Shared.Server.Fakes;

    /// <summary>
    /// End-to-end tests through the real proxy against fake TypeSafe and Ollaya upstreams on 127.0.0.1.
    /// </summary>
    public class TypeSafeProxyEndToEndTests : IDisposable
    {
        private readonly ProxyTestHost _Host = new ProxyTestHost();
        private readonly FakeTypeSafeServer _TypeSafe = new FakeTypeSafeServer();

        /// <summary>
        /// Start the proxy with a two-question limit.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await _Host.InitializeAsync(null, null, new TypeSafeSettings { MaxQuestionsPerRequest = 2 }).ConfigureAwait(false);
        }

        /// <summary>
        /// Stop the proxy and the fake.
        /// </summary>
        public void Dispose()
        {
            _Host.Dispose();
            _TypeSafe.Dispose();
        }

        /// <summary>
        /// A decision round trip: the upstream receives the endpoint key as a Bearer token, the answers come back
        /// unchanged, and usage plus the question count are recorded.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Decide_RoundTrip_ForwardsBearerAndRecordsUsageAndQuestionCount()
        {
            ModelRunnerEndpoint endpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.TypeSafe, _TypeSafe.Server, _TypeSafe.ApiKey).ConfigureAwait(false);
            VirtualModelRunner vmr = await _Host.CreateVmrAsync(ApiTypeEnum.TypeSafe, new[] { endpoint }).ConfigureAwait(false);

            HttpResponseMessage response = await PostDecisionAsync(vmr, 2, new Dictionary<string, string> { { "Authorization", "Bearer " + _Host.BearerToken } }).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            body.Should().Contain("\"answers\"").And.Contain("\"q0\"").And.Contain("\"q1\"");

            FakeUpstreamRequest upstream = _TypeSafe.Server.GetRequests().Single();
            upstream.Path.Should().Be("/v1/systemone");
            upstream.GetHeader("Authorization").Should().Be("Bearer " + _TypeSafe.ApiKey);

            RequestHistoryEntry entry = await ProxyTestHelpers.WaitForHistoryAsync(_Host.Database, _Host.TenantId, vmr.Id).ConfigureAwait(false);
            entry.Should().NotBeNull();
            entry.ProviderName.Should().Be("TypeSafe");
            entry.RequestType.Should().Be(RequestTypeEnum.TypeSafeDecide.ToString());
            entry.DecisionQuestionCount.Should().Be(2);
            entry.PromptTokens.Should().Be(32);
            entry.CompletionTokens.Should().Be(2);
            entry.TotalTokens.Should().Be(34);
        }

        /// <summary>
        /// More than MaxQuestionsPerRequest questions, or an empty questions object, get a 400 without forwarding.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Decide_QuestionLimitAndEmptyQuestions_Return400WithoutForwarding()
        {
            ModelRunnerEndpoint endpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.TypeSafe, _TypeSafe.Server, _TypeSafe.ApiKey).ConfigureAwait(false);
            VirtualModelRunner vmr = await _Host.CreateVmrAsync(ApiTypeEnum.TypeSafe, new[] { endpoint }).ConfigureAwait(false);

            HttpResponseMessage tooMany = await PostDecisionAsync(vmr, 3, Bearer()).ConfigureAwait(false);
            HttpResponseMessage empty = await PostDecisionAsync(vmr, 0, Bearer()).ConfigureAwait(false);

            tooMany.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await tooMany.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Contain("maximum is 2");
            empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            _TypeSafe.Server.GetRequests().Should().BeEmpty();
        }

        /// <summary>
        /// A decision request on a runner with AllowDecisions false gets a 403.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Decide_WithAllowDecisionsFalse_Returns403()
        {
            ModelRunnerEndpoint endpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.TypeSafe, _TypeSafe.Server, _TypeSafe.ApiKey).ConfigureAwait(false);
            VirtualModelRunner vmr = await _Host.CreateVmrAsync(ApiTypeEnum.TypeSafe, new[] { endpoint }, item => item.AllowDecisions = false).ConfigureAwait(false);

            HttpResponseMessage response = await PostDecisionAsync(vmr, 1, Bearer()).ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            _TypeSafe.Server.GetRequests().Should().BeEmpty();
        }

        /// <summary>
        /// An Ollaya endpoint without a key serves a TypeSafe runner and no Authorization header is sent.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Decide_OnOllayaEndpoint_SendsNoAuthorizationAndListsModels()
        {
            using (FakeTypeSafeServer ollaya = new FakeTypeSafeServer(null))
            {
                ModelRunnerEndpoint endpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.Ollaya, ollaya.Server, null).ConfigureAwait(false);
                VirtualModelRunner vmr = await _Host.CreateVmrAsync(ApiTypeEnum.TypeSafe, new[] { endpoint }).ConfigureAwait(false);

                HttpResponseMessage decide = await PostDecisionAsync(vmr, 1, new Dictionary<string, string> { { "Authorization", "Bearer " + _Host.BearerToken } }).ConfigureAwait(false);
                HttpResponseMessage models = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Get, vmr.BasePath + "v1/models", null, Bearer())).ConfigureAwait(false);

                decide.StatusCode.Should().Be(HttpStatusCode.OK);
                models.StatusCode.Should().Be(HttpStatusCode.OK);
                (await models.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Contain("jev-mini");
                ollaya.Server.GetRequests().Should().OnlyContain(item => item.GetHeader("Authorization") == null);
            }
        }

        /// <summary>
        /// Paths outside the /v1/ namespace of a TypeSafe runner return 404 without forwarding.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task OutsideNamespace_Returns404()
        {
            ModelRunnerEndpoint endpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.TypeSafe, _TypeSafe.Server, _TypeSafe.ApiKey).ConfigureAwait(false);
            VirtualModelRunner vmr = await _Host.CreateVmrAsync(ApiTypeEnum.TypeSafe, new[] { endpoint }).ConfigureAwait(false);

            HttpResponseMessage response = await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, vmr.BasePath + "api/chat", "{}", Bearer())).ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            _TypeSafe.Server.GetRequests().Should().BeEmpty();
        }

        /// <summary>
        /// An unauthenticated request to a TypeSafe runner is rejected at the route layer, and x-api-key is not accepted.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Decide_WithoutCredentialOrWithXApiKey_Returns401()
        {
            ModelRunnerEndpoint endpoint = await _Host.CreateEndpointAsync(ApiTypeEnum.TypeSafe, _TypeSafe.Server, _TypeSafe.ApiKey).ConfigureAwait(false);
            VirtualModelRunner vmr = await _Host.CreateVmrAsync(ApiTypeEnum.TypeSafe, new[] { endpoint }).ConfigureAwait(false);

            HttpResponseMessage none = await PostDecisionAsync(vmr, 1, null).ConfigureAwait(false);
            HttpResponseMessage xApiKey = await PostDecisionAsync(vmr, 1, new Dictionary<string, string> { { "x-api-key", _Host.BearerToken } }).ConfigureAwait(false);

            none.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            xApiKey.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            _TypeSafe.Server.GetRequests().Should().BeEmpty();
        }

        private Dictionary<string, string> Bearer()
        {
            return new Dictionary<string, string> { { "Authorization", "Bearer " + _Host.BearerToken } };
        }

        private async Task<HttpResponseMessage> PostDecisionAsync(VirtualModelRunner vmr, int questionCount, Dictionary<string, string> headers)
        {
            StringBuilder questions = new StringBuilder();
            for (int i = 0; i < questionCount; i++)
            {
                if (i > 0) questions.Append(',');
                questions.Append("\"q" + i + "\":{\"type\":\"noul\",\"instructions\":\"Is item " + i + " urgent?\"}");
            }

            string json = "{\"model\":\"jev-latest\",\"state\":\"A customer email.\",\"questions\":{" + questions + "}}";
            return await _Host.Client.SendAsync(ProxyTestHelpers.Build(HttpMethod.Post, vmr.BasePath + "v1/systemone", json, headers)).ConfigureAwait(false);
        }
    }
}
