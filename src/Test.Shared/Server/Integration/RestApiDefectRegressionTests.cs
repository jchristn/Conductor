namespace Test.Shared.Server.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Conductor.Core.Enums;
    using Conductor.Core.Models;
    using FluentAssertions;
    using Test.Shared.Server.Fakes;

    /// <summary>
    /// Regression tests for the REST API defects found in the v1.1.0 simulated user testing session (UT-002, UT-004,
    /// UT-006, UT-009, UT-011, UT-012, UT-015, UT-017, UT-020, UT-023, UT-026, UT-027, UT-028, UT-029), through the full route registry and
    /// the real authentication route on 127.0.0.1.
    /// </summary>
    public class RestApiDefectRegressionTests : IDisposable
    {
        private readonly RestTestHost _Host = new RestTestHost();

        /// <summary>
        /// Start the REST host.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task InitializeAsync()
        {
            await _Host.InitializeAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Stop the REST host.
        /// </summary>
        public void Dispose()
        {
            _Host.Dispose();
        }

        /// <summary>
        /// UT-006: a global admin user's create, body-less update and delete without TenantId act on the user's own tenant
        /// (no 500); a system administrator write without TenantId gets 400 naming TenantId.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task WritesWithoutTenantId_DefaultForGlobalAdminAndRequireTenantForSystemAdmin()
        {
            HttpResponseMessage created = await _Host.SendAsync(HttpMethod.Post, "/v1.0/modelrunnerendpoints", "{\"Name\":\"Anthropic\",\"ApiType\":\"Anthropic\",\"ApiKey\":\"sk-ant-test\"}", _Host.GlobalAdminToken).ConfigureAwait(false);
            string createdBody = await created.Content.ReadAsStringAsync().ConfigureAwait(false);
            created.StatusCode.Should().Be(HttpStatusCode.Created, createdBody);
            string endpointId;
            using (JsonDocument document = JsonDocument.Parse(createdBody))
            {
                document.RootElement.GetProperty("TenantId").GetString().Should().Be(_Host.TenantId);
                endpointId = document.RootElement.GetProperty("Id").GetString();
            }

            HttpResponseMessage vmr = await _Host.SendAsync(HttpMethod.Post, "/v1.0/virtualmodelrunners", "{\"Name\":\"claude\",\"BasePath\":\"/v1.0/api/claude/\",\"ApiType\":\"Anthropic\",\"ModelRunnerEndpointIds\":[\"" + endpointId + "\"]}", _Host.GlobalAdminToken).ConfigureAwait(false);
            vmr.StatusCode.Should().Be(HttpStatusCode.Created, await vmr.Content.ReadAsStringAsync().ConfigureAwait(false));

            HttpResponseMessage noBody = await _Host.SendAsync(HttpMethod.Put, "/v1.0/modelrunnerendpoints/" + endpointId, null, _Host.GlobalAdminToken).ConfigureAwait(false);
            noBody.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            HttpResponseMessage adminCreate = await _Host.SendAsAdminAsync(HttpMethod.Post, "/v1.0/modelrunnerendpoints", "{\"Name\":\"Anthropic 2\",\"ApiType\":\"Anthropic\",\"ApiKey\":\"sk-ant-test\"}").ConfigureAwait(false);
            adminCreate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await adminCreate.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Contain("TenantId is required");

            HttpResponseMessage adminCreateWithTenant = await _Host.SendAsAdminAsync(HttpMethod.Post, "/v1.0/modelrunnerendpoints", "{\"TenantId\":\"" + _Host.TenantId + "\",\"Name\":\"Anthropic 3\",\"ApiType\":\"Anthropic\",\"ApiKey\":\"sk-ant-test\"}").ConfigureAwait(false);
            adminCreateWithTenant.StatusCode.Should().Be(HttpStatusCode.Created);

            HttpResponseMessage delete = await _Host.SendAsync(HttpMethod.Delete, "/v1.0/modelrunnerendpoints/" + endpointId, null, _Host.GlobalAdminToken).ConfigureAwait(false);
            delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// UT-002: a global admin (or tenant admin) creating a credential with UserId binds it to that user, so the key
        /// carries the user's permissions; a UserId outside the target tenant is rejected.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task CreateCredential_HonorsUserIdValidatedInTargetTenant()
        {
            HttpResponseMessage response = await _Host.SendAsync(HttpMethod.Post, "/v1.0/credentials",
                "{\"TenantId\":\"" + _Host.TenantId + "\",\"UserId\":\"" + _Host.StandardUser.Id + "\",\"Name\":\"sam key\",\"BearerToken\":\"rest-test-sam-key-not-for-production\"}",
                _Host.GlobalAdminToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            response.StatusCode.Should().Be(HttpStatusCode.Created, body);
            using (JsonDocument document = JsonDocument.Parse(body))
            {
                document.RootElement.GetProperty("UserId").GetString().Should().Be(_Host.StandardUser.Id);
            }

            HttpResponseMessage login = await _Host.SendAsync(HttpMethod.Post, "/v1.0/auth/login/apikey", "{\"ApiKey\":\"rest-test-sam-key-not-for-production\"}", null).ConfigureAwait(false);
            string loginBody = await login.Content.ReadAsStringAsync().ConfigureAwait(false);
            login.StatusCode.Should().Be(HttpStatusCode.OK, loginBody);
            using (JsonDocument document = JsonDocument.Parse(loginBody))
            {
                JsonElement user = document.RootElement.GetProperty("User");
                user.GetProperty("Id").GetString().Should().Be(_Host.StandardUser.Id);
                user.GetProperty("IsAdmin").GetBoolean().Should().BeFalse();
            }

            HttpResponseMessage tenantAdmin = await _Host.SendAsync(HttpMethod.Post, "/v1.0/credentials",
                "{\"UserId\":\"" + _Host.StandardUser.Id + "\",\"Name\":\"by tenant admin\"}", _Host.TenantAdminToken).ConfigureAwait(false);
            tenantAdmin.StatusCode.Should().Be(HttpStatusCode.Created);

            HttpResponseMessage foreignUser = await _Host.SendAsync(HttpMethod.Post, "/v1.0/credentials",
                "{\"UserId\":\"" + _Host.OtherTenantAdmin.Id + "\",\"Name\":\"foreign\"}", _Host.TenantAdminToken).ConfigureAwait(false);
            foreignUser.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await foreignUser.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Contain("was not found in tenant");

            HttpResponseMessage adminNoUser = await _Host.SendAsAdminAsync(HttpMethod.Post, "/v1.0/credentials", "{\"TenantId\":\"" + _Host.TenantId + "\",\"Name\":\"no user\"}").ConfigureAwait(false);
            adminNoUser.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await adminNoUser.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Contain("UserId is required");
        }

        /// <summary>
        /// UT-009: user create, read, list and update responses never contain the password, and an update that omits
        /// Password keeps the stored password.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task UserResponses_NeverContainPassword_AndUpdateKeepsStoredPassword()
        {
            string email = "pat-" + Guid.NewGuid().ToString("N").Substring(0, 6) + "@example.test";
            HttpResponseMessage created = await _Host.SendAsync(HttpMethod.Post, "/v1.0/users",
                "{\"FirstName\":\"Pat\",\"LastName\":\"User\",\"Email\":\"" + email + "\",\"Password\":\"usertest-password\"}", _Host.TenantAdminToken).ConfigureAwait(false);
            string createdBody = await created.Content.ReadAsStringAsync().ConfigureAwait(false);
            created.StatusCode.Should().Be(HttpStatusCode.Created, createdBody);
            createdBody.Should().NotContain("usertest-password").And.NotContain("\"Password\"");
            string userId;
            using (JsonDocument document = JsonDocument.Parse(createdBody)) userId = document.RootElement.GetProperty("Id").GetString();

            string readBody = await (await _Host.SendAsync(HttpMethod.Get, "/v1.0/users/" + userId, null, _Host.TenantAdminToken).ConfigureAwait(false)).Content.ReadAsStringAsync().ConfigureAwait(false);
            readBody.Should().NotContain("\"Password\"");
            string listBody = await (await _Host.SendAsync(HttpMethod.Get, "/v1.0/users", null, _Host.TenantAdminToken).ConfigureAwait(false)).Content.ReadAsStringAsync().ConfigureAwait(false);
            listBody.Should().NotContain("\"Password\"").And.NotContain(RestTestHost.UserPassword);

            HttpResponseMessage updated = await _Host.SendAsync(HttpMethod.Put, "/v1.0/users/" + userId,
                "{\"FirstName\":\"Patricia\",\"LastName\":\"User\",\"Email\":\"" + email + "\"}", _Host.TenantAdminToken).ConfigureAwait(false);
            string updatedBody = await updated.Content.ReadAsStringAsync().ConfigureAwait(false);
            updated.StatusCode.Should().Be(HttpStatusCode.OK, updatedBody);
            updatedBody.Should().NotContain("\"Password\"");

            HttpResponseMessage login = await _Host.SendAsync(HttpMethod.Post, "/v1.0/auth/login/credential",
                "{\"TenantId\":\"" + _Host.TenantId + "\",\"Email\":\"" + email + "\",\"Password\":\"usertest-password\"}", null).ConfigureAwait(false);
            login.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        /// <summary>
        /// UT-017: revealing an endpoint key requires a tenant admin, global admin or system administrator; a standard user
        /// is denied.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task RevealApiKey_RequiresTenantAdmin()
        {
            ModelRunnerEndpoint endpoint = await _Host.Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = _Host.TenantId,
                Name = "keyed",
                Hostname = "127.0.0.1",
                Port = 9,
                ApiType = ApiTypeEnum.Anthropic,
                ApiKey = "sk-ant-secret-value"
            }).ConfigureAwait(false);

            HttpResponseMessage standard = await _Host.SendAsync(HttpMethod.Get, "/v1.0/modelrunnerendpoints/" + endpoint.Id + "/apikey", null, _Host.StandardUserToken).ConfigureAwait(false);
            ((int)standard.StatusCode).Should().BeOneOf(401, 403);
            (await standard.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().NotContain("sk-ant-secret-value");

            HttpResponseMessage tenantAdmin = await _Host.SendAsync(HttpMethod.Get, "/v1.0/modelrunnerendpoints/" + endpoint.Id + "/apikey", null, _Host.TenantAdminToken).ConfigureAwait(false);
            tenantAdmin.StatusCode.Should().Be(HttpStatusCode.OK);
            (await tenantAdmin.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Contain("sk-ant-secret-value");

            HttpResponseMessage admin = await _Host.SendAsAdminAsync(HttpMethod.Get, "/v1.0/modelrunnerendpoints/" + endpoint.Id + "/apikey?tenantId=" + _Host.TenantId, null).ConfigureAwait(false);
            admin.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        /// <summary>
        /// UT-004: changing an attached endpoint's ApiType to one its runner does not accept is rejected with
        /// EndpointApiTypeMismatch naming the runner; a compatible change is still allowed.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task EndpointApiTypeChange_IsValidatedAgainstAttachedRunners()
        {
            ModelRunnerEndpoint endpoint = await _Host.Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = _Host.TenantId,
                Name = "Anthropic fake",
                Hostname = "127.0.0.1",
                Port = 9,
                ApiType = ApiTypeEnum.Anthropic,
                ApiKey = "sk-ant-test"
            }).ConfigureAwait(false);
            await _Host.Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = _Host.TenantId,
                Name = "claude-runner",
                BasePath = "/v1.0/api/claude-runner/",
                ApiType = ApiTypeEnum.Anthropic,
                ModelRunnerEndpointIds = new List<string> { endpoint.Id }
            }).ConfigureAwait(false);

            string openAiBody = "{\"Name\":\"Anthropic fake\",\"Hostname\":\"127.0.0.1\",\"Port\":9,\"ApiType\":\"OpenAI\",\"ApiKey\":\"sk-ant-test\"}";
            HttpResponseMessage update = await _Host.SendAsync(HttpMethod.Put, "/v1.0/modelrunnerendpoints/" + endpoint.Id, openAiBody, _Host.TenantAdminToken).ConfigureAwait(false);
            string updateBody = await update.Content.ReadAsStringAsync().ConfigureAwait(false);
            update.StatusCode.Should().Be(HttpStatusCode.BadRequest, updateBody);
            updateBody.Should().Contain("claude-runner").And.Contain("cannot change its API type");

            HttpResponseMessage validate = await _Host.SendAsync(HttpMethod.Post, "/v1.0/modelrunnerendpoints/validate?existingId=" + endpoint.Id, openAiBody, _Host.TenantAdminToken).ConfigureAwait(false);
            (await validate.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Contain("EndpointApiTypeMismatch");

            ModelRunnerEndpoint stored = await _Host.Database.ModelRunnerEndpoint.ReadAsync(_Host.TenantId, endpoint.Id).ConfigureAwait(false);
            stored.ApiType.Should().Be(ApiTypeEnum.Anthropic);
        }

        /// <summary>
        /// UT-011: the validate route applies type defaults to a new draft the same way create does, so the README's
        /// Anthropic body validates without the AnthropicHealthCheckUnauthenticated warning.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ValidateRoute_AppliesTypeDefaultsToNewDrafts()
        {
            HttpResponseMessage validate = await _Host.SendAsync(HttpMethod.Post, "/v1.0/modelrunnerendpoints/validate",
                "{\"Name\":\"Anthropic\",\"ApiType\":\"Anthropic\",\"ApiKey\":\"sk-ant-test\"}", _Host.TenantAdminToken).ConfigureAwait(false);
            string body = await validate.Content.ReadAsStringAsync().ConfigureAwait(false);
            validate.StatusCode.Should().Be(HttpStatusCode.OK, body);
            body.Should().NotContain("AnthropicHealthCheckUnauthenticated");
            using (JsonDocument document = JsonDocument.Parse(body))
            {
                document.RootElement.GetProperty("IsValid").GetBoolean().Should().BeTrue(body);
            }
        }

        /// <summary>
        /// UT-012: Ollaya and OpenAICompatible endpoints have no default port, so create and validate without Port fail with
        /// PortRequired; an explicit port works and an update that omits Port keeps it; Ollama keeps its existing default.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task OllayaAndOpenAICompatible_RequireExplicitPort()
        {
            foreach (string type in new[] { "Ollaya", "OpenAICompatible" })
            {
                HttpResponseMessage noPort = await _Host.SendAsync(HttpMethod.Post, "/v1.0/modelrunnerendpoints", "{\"Name\":\"" + type + " noport\",\"ApiType\":\"" + type + "\"}", _Host.TenantAdminToken).ConfigureAwait(false);
                noPort.StatusCode.Should().Be(HttpStatusCode.BadRequest, type);
                (await noPort.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Contain("no default port", type);

                HttpResponseMessage validate = await _Host.SendAsync(HttpMethod.Post, "/v1.0/modelrunnerendpoints/validate", "{\"Name\":\"" + type + " noport\",\"ApiType\":\"" + type + "\"}", _Host.TenantAdminToken).ConfigureAwait(false);
                (await validate.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Contain("PortRequired", type);

                HttpResponseMessage withPort = await _Host.SendAsync(HttpMethod.Post, "/v1.0/modelrunnerendpoints", "{\"Name\":\"" + type + " port\",\"ApiType\":\"" + type + "\",\"Port\":8123}", _Host.TenantAdminToken).ConfigureAwait(false);
                string withPortBody = await withPort.Content.ReadAsStringAsync().ConfigureAwait(false);
                withPort.StatusCode.Should().Be(HttpStatusCode.Created, withPortBody);
                string id;
                using (JsonDocument document = JsonDocument.Parse(withPortBody))
                {
                    document.RootElement.GetProperty("Port").GetInt32().Should().Be(8123);
                    id = document.RootElement.GetProperty("Id").GetString();
                }

                HttpResponseMessage update = await _Host.SendAsync(HttpMethod.Put, "/v1.0/modelrunnerendpoints/" + id, "{\"Name\":\"" + type + " renamed\",\"Hostname\":\"127.0.0.1\",\"ApiType\":\"" + type + "\"}", _Host.TenantAdminToken).ConfigureAwait(false);
                string updateBody = await update.Content.ReadAsStringAsync().ConfigureAwait(false);
                update.StatusCode.Should().Be(HttpStatusCode.OK, updateBody);
                using (JsonDocument document = JsonDocument.Parse(updateBody)) document.RootElement.GetProperty("Port").GetInt32().Should().Be(8123);
            }

            HttpResponseMessage ollama = await _Host.SendAsync(HttpMethod.Post, "/v1.0/modelrunnerendpoints", "{\"Name\":\"ollama\",\"Hostname\":\"127.0.0.1\",\"ApiType\":\"Ollama\"}", _Host.TenantAdminToken).ConfigureAwait(false);
            string ollamaBody = await ollama.Content.ReadAsStringAsync().ConfigureAwait(false);
            ollama.StatusCode.Should().Be(HttpStatusCode.Created, ollamaBody);
            using (JsonDocument document = JsonDocument.Parse(ollamaBody)) document.RootElement.GetProperty("Port").GetInt32().Should().Be(11434);
        }

        /// <summary>
        /// UT-015: an empty endpoint id in ModelRunnerEndpointIds is a 400 EndpointMissing, not a 500.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task CreateVmr_WithEmptyEndpointId_Returns400()
        {
            HttpResponseMessage response = await _Host.SendAsync(HttpMethod.Post, "/v1.0/virtualmodelrunners",
                "{\"Name\":\"empty-id\",\"BasePath\":\"/v1.0/api/empty-id/\",\"ApiType\":\"Anthropic\",\"ModelRunnerEndpointIds\":[\"\"]}", _Host.TenantAdminToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
            body.Should().Contain("empty endpoint ID");
        }

        /// <summary>
        /// UT-020: validation messages lead with the real problem: unknown enum values list the valid values without .NET
        /// type names, mismatches name the runner type plainly, an endpoint-only runner type is reported once, and a
        /// keyless TypeSafe endpoint reports only TypeSafeApiKeyMissing.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task ValidationMessages_AreClearAndNotDuplicated()
        {
            HttpResponseMessage badEnum = await _Host.SendAsync(HttpMethod.Post, "/v1.0/virtualmodelrunners", "{\"Name\":\"bad\",\"ApiType\":\"Claude\"}", _Host.TenantAdminToken).ConfigureAwait(false);
            string badEnumBody = await badEnum.Content.ReadAsStringAsync().ConfigureAwait(false);
            badEnum.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            badEnumBody.Should().Contain("ApiType").And.Contain("Valid values").And.Contain("Anthropic").And.NotContain("Conductor.Core.Enums");

            ModelRunnerEndpoint anthropic = await CreateEndpointAsync(ApiTypeEnum.Anthropic, "sk-ant").ConfigureAwait(false);
            ModelRunnerEndpoint ollaya = await CreateEndpointAsync(ApiTypeEnum.Ollaya, null).ConfigureAwait(false);

            HttpResponseMessage mismatch = await _Host.SendAsync(HttpMethod.Post, "/v1.0/virtualmodelrunners/validate",
                "{\"Name\":\"m\",\"BasePath\":\"/v1.0/api/m/\",\"ApiType\":\"OpenAI\",\"ModelRunnerEndpointIds\":[\"" + anthropic.Id + "\"]}", _Host.TenantAdminToken).ConfigureAwait(false);
            string mismatchBody = await mismatch.Content.ReadAsStringAsync().ConfigureAwait(false);
            mismatchBody.Should().Contain("virtual model runners of type OpenAI accept only endpoints of type OpenAI, OpenAICompatible, vLLM").And.NotContain("route accepts");

            HttpResponseMessage endpointOnly = await _Host.SendAsync(HttpMethod.Post, "/v1.0/virtualmodelrunners/validate",
                "{\"Name\":\"o\",\"BasePath\":\"/v1.0/api/o/\",\"ApiType\":\"Ollaya\",\"ModelRunnerEndpointIds\":[\"" + ollaya.Id + "\"]}", _Host.TenantAdminToken).ConfigureAwait(false);
            string endpointOnlyBody = await endpointOnly.Content.ReadAsStringAsync().ConfigureAwait(false);
            endpointOnlyBody.Should().Contain("VmrApiTypeNotClientFacing").And.NotContain("EndpointApiTypeMismatch");

            HttpResponseMessage keyless = await _Host.SendAsync(HttpMethod.Post, "/v1.0/modelrunnerendpoints/validate", "{\"Name\":\"ts\",\"ApiType\":\"TypeSafe\"}", _Host.TenantAdminToken).ConfigureAwait(false);
            string keylessBody = await keyless.Content.ReadAsStringAsync().ConfigureAwait(false);
            keylessBody.Should().Contain("TypeSafeApiKeyMissing").And.NotContain("HealthCheckAuthRequiresApiKey");
        }

        /// <summary>
        /// UT-023: responses do not echo request-only headers (Host, Accept, Accept-Language, Accept-Charset).
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Responses_DoNotEchoRequestHeaders()
        {
            HttpResponseMessage response = await _Host.SendAsync(HttpMethod.Get, "/v1.0/modelrunnerendpoints", null, _Host.TenantAdminToken).ConfigureAwait(false);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            foreach (string name in new[] { "Host", "Accept", "Accept-Language", "Accept-Charset" })
            {
                ProxyTestHelpers.GetHeader(response, name).Should().BeNull(name);
            }

            RawHttpResponse raw = await RawHttpClient.SendAsync(_Host.Port, "GET", "/health").ConfigureAwait(false);
            raw.StatusCode.Should().Be(200);
            raw.Headers.Should().NotContainKey("Host").And.NotContainKey("Accept").And.NotContainKey("Accept-Charset");
        }

        /// <summary>
        /// UT-026: every tenant route requires authentication; create, update and delete require a system administrator;
        /// listing is scoped to the caller's own tenant for non-admin users.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task TenantRoutes_RequireAuthenticationAndAdminRights()
        {
            HttpResponseMessage anonymousCreate = await _Host.SendAsync(HttpMethod.Post, "/v1.0/tenants", "{\"Name\":\"Anonymous Tenant\"}", null).ConfigureAwait(false);
            anonymousCreate.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await _Host.SendAsync(HttpMethod.Get, "/v1.0/tenants", null, null).ConfigureAwait(false)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await _Host.SendAsync(HttpMethod.Get, "/v1.0/tenants/" + _Host.TenantId, null, null).ConfigureAwait(false)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await _Host.SendAsync(HttpMethod.Put, "/v1.0/tenants/" + _Host.OtherTenantId, "{\"Name\":\"Renamed\"}", null).ConfigureAwait(false)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await _Host.SendAsync(HttpMethod.Delete, "/v1.0/tenants/" + _Host.OtherTenantId, null, null).ConfigureAwait(false)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            HttpResponseMessage tenantAdminCreate = await _Host.SendAsync(HttpMethod.Post, "/v1.0/tenants", "{\"Name\":\"By Tenant Admin\"}", _Host.TenantAdminToken).ConfigureAwait(false);
            ((int)tenantAdminCreate.StatusCode).Should().BeOneOf(401, 403);

            EnumerationResult<TenantMetadata> tenants = await _Host.Database.Tenant.EnumerateAsync(new EnumerationRequest { MaxResults = 100 }).ConfigureAwait(false);
            tenants.Data.Should().NotContain(item => item.Name == "Anonymous Tenant" || item.Name == "By Tenant Admin");
            (await _Host.Database.Tenant.ReadAsync(_Host.OtherTenantId).ConfigureAwait(false)).Name.Should().Be("Rest Other Tenant");

            string standardList = await (await _Host.SendAsync(HttpMethod.Get, "/v1.0/tenants", null, _Host.StandardUserToken).ConfigureAwait(false)).Content.ReadAsStringAsync().ConfigureAwait(false);
            standardList.Should().Contain(_Host.TenantId).And.NotContain(_Host.OtherTenantId);
            string globalList = await (await _Host.SendAsync(HttpMethod.Get, "/v1.0/tenants", null, _Host.GlobalAdminToken).ConfigureAwait(false)).Content.ReadAsStringAsync().ConfigureAwait(false);
            globalList.Should().Contain(_Host.TenantId).And.Contain(_Host.OtherTenantId);

            HttpResponseMessage adminCreate = await _Host.SendAsAdminAsync(HttpMethod.Post, "/v1.0/tenants", "{\"Name\":\"By System Admin\"}").ConfigureAwait(false);
            adminCreate.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        /// <summary>
        /// UT-027: deleting a virtual model runner that has request history succeeds (204) and removes that history, and
        /// deleting a tenant whose runners have request history also succeeds.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task DeleteVmrAndTenant_WithRequestHistory_Succeeds()
        {
            using (FakeUpstreamServer upstream = new FakeUpstreamServer())
            {
                upstream.Handler = request => new FakeUpstreamResponse { Body = "{\"id\":\"c1\",\"object\":\"chat.completion\",\"choices\":[]}" };

                VirtualModelRunner vmr = await CreateRunnerWithHistoryAsync(_Host.TenantId, _Host.TenantAdminToken, upstream, "history-a").ConfigureAwait(false);
                HttpResponseMessage delete = await _Host.SendAsync(HttpMethod.Delete, "/v1.0/virtualmodelrunners/" + vmr.Id, null, _Host.TenantAdminToken).ConfigureAwait(false);
                delete.StatusCode.Should().Be(HttpStatusCode.NoContent, await delete.Content.ReadAsStringAsync().ConfigureAwait(false));
                (await _Host.Database.VirtualModelRunner.ReadAsync(_Host.TenantId, vmr.Id).ConfigureAwait(false)).Should().BeNull();
                RequestHistorySearchResult remaining = await _Host.Database.RequestHistory.SearchAsync(new RequestHistorySearchFilter { TenantGuid = _Host.TenantId, VirtualModelRunnerGuid = vmr.Id, Page = 1, PageSize = 10 }).ConfigureAwait(false);
                remaining.Data.Should().BeEmpty();

                await CreateRunnerWithHistoryAsync(_Host.OtherTenantId, _Host.OtherTenantToken, upstream, "history-b").ConfigureAwait(false);
                HttpResponseMessage deleteTenant = await _Host.SendAsAdminAsync(HttpMethod.Delete, "/v1.0/tenants/" + _Host.OtherTenantId, null).ConfigureAwait(false);
                deleteTenant.StatusCode.Should().Be(HttpStatusCode.NoContent, await deleteTenant.Content.ReadAsStringAsync().ConfigureAwait(false));
                (await _Host.Database.Tenant.ReadAsync(_Host.OtherTenantId).ConfigureAwait(false)).Should().BeNull();
            }
        }

        /// <summary>
        /// UT-028: creating a user without an explicit non-empty Password returns 400 and stores nothing (the model default
        /// "password" is never stored); an explicit Password still works. Administrator create rejects a missing Password
        /// the same way.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task CreateUserOrAdministrator_WithoutPassword_Returns400()
        {
            string email = "nopw-" + Guid.NewGuid().ToString("N").Substring(0, 6) + "@example.test";
            HttpResponseMessage omitted = await _Host.SendAsync(HttpMethod.Post, "/v1.0/users",
                "{\"FirstName\":\"No\",\"LastName\":\"Password\",\"Email\":\"" + email + "\"}", _Host.TenantAdminToken).ConfigureAwait(false);
            string omittedBody = await omitted.Content.ReadAsStringAsync().ConfigureAwait(false);
            omitted.StatusCode.Should().Be(HttpStatusCode.BadRequest, omittedBody);
            omittedBody.Should().Contain("Password is required");

            HttpResponseMessage empty = await _Host.SendAsync(HttpMethod.Post, "/v1.0/users",
                "{\"FirstName\":\"No\",\"LastName\":\"Password\",\"Email\":\"" + email + "\",\"Password\":\"\"}", _Host.TenantAdminToken).ConfigureAwait(false);
            string emptyBody = await empty.Content.ReadAsStringAsync().ConfigureAwait(false);
            empty.StatusCode.Should().Be(HttpStatusCode.BadRequest, emptyBody);
            emptyBody.Should().Contain("Password");

            HttpResponseMessage defaultLogin = await _Host.SendAsync(HttpMethod.Post, "/v1.0/auth/login/credential",
                "{\"TenantId\":\"" + _Host.TenantId + "\",\"Email\":\"" + email + "\",\"Password\":\"password\"}", null).ConfigureAwait(false);
            defaultLogin.StatusCode.Should().NotBe(HttpStatusCode.OK);
            (await _Host.Database.User.ReadByEmailAsync(_Host.TenantId, email).ConfigureAwait(false)).Should().BeNull();

            HttpResponseMessage explicitPassword = await _Host.SendAsync(HttpMethod.Post, "/v1.0/users",
                "{\"FirstName\":\"Has\",\"LastName\":\"Password\",\"Email\":\"" + email + "\",\"Password\":\"explicit-password\"}", _Host.TenantAdminToken).ConfigureAwait(false);
            explicitPassword.StatusCode.Should().Be(HttpStatusCode.Created, await explicitPassword.Content.ReadAsStringAsync().ConfigureAwait(false));

            HttpResponseMessage adminOmitted = await _Host.SendAsAdminAsync(HttpMethod.Post, "/v1.0/administrators",
                "{\"Email\":\"admin-" + email + "\",\"FirstName\":\"No\",\"LastName\":\"Password\"}").ConfigureAwait(false);
            adminOmitted.StatusCode.Should().Be(HttpStatusCode.BadRequest, await adminOmitted.Content.ReadAsStringAsync().ConfigureAwait(false));
            (await adminOmitted.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().Contain("Password");
        }

        /// <summary>
        /// UT-029: the action routes that change state (endpoint and VMR load-model, keep-warm run, runtime-stats reset,
        /// runtime-backoff clear, Ollama model pull and delete) apply the write tenant rule: a system administrator without
        /// tenantId gets 400 naming TenantId, and a global admin user without tenantId acts on its own tenant.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task StateChangingActionRoutes_RequireTenantForSystemAdmin()
        {
            List<string> paths = new List<string>
            {
                "/v1.0/modelrunnerendpoints/mre_missing/load-model",
                "/v1.0/modelrunnerendpoints/mre_missing/ollama/models/pull",
                "/v1.0/modelrunnerendpoints/mre_missing/ollama/models/delete",
                "/v1.0/virtualmodelrunners/vmr_missing/load-model",
                "/v1.0/virtualmodelrunners/vmr_missing/keep-warm/run",
                "/v1.0/virtualmodelrunners/vmr_missing/runtime-stats/reset",
                "/v1.0/virtualmodelrunners/vmr_missing/runtime-backoff/clear"
            };

            foreach (string path in paths)
            {
                HttpResponseMessage noTenant = await _Host.SendAsAdminAsync(HttpMethod.Post, path, "{}").ConfigureAwait(false);
                string noTenantBody = await noTenant.Content.ReadAsStringAsync().ConfigureAwait(false);
                noTenant.StatusCode.Should().Be(HttpStatusCode.BadRequest, path + ": " + noTenantBody);
                noTenantBody.Should().Contain("TenantId is required", path);

                HttpResponseMessage withTenant = await _Host.SendAsAdminAsync(HttpMethod.Post, path + "?tenantId=" + _Host.TenantId, "{}").ConfigureAwait(false);
                (await withTenant.Content.ReadAsStringAsync().ConfigureAwait(false)).Should().NotContain("TenantId is required", path);
            }

            ModelRunnerEndpoint endpoint = await CreateEndpointAsync(ApiTypeEnum.OpenAI, "upstream-key").ConfigureAwait(false);
            VirtualModelRunner vmr = await _Host.Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = _Host.TenantId,
                Name = "ut029",
                BasePath = "/v1.0/api/ut029/",
                ApiType = ApiTypeEnum.OpenAI,
                ModelRunnerEndpointIds = new List<string> { endpoint.Id }
            }).ConfigureAwait(false);

            HttpResponseMessage globalAdmin = await _Host.SendAsync(HttpMethod.Post, "/v1.0/virtualmodelrunners/" + vmr.Id + "/runtime-stats/reset", "{}", _Host.GlobalAdminToken).ConfigureAwait(false);
            globalAdmin.StatusCode.Should().Be(HttpStatusCode.OK, await globalAdmin.Content.ReadAsStringAsync().ConfigureAwait(false));

            HttpResponseMessage systemAdmin = await _Host.SendAsAdminAsync(HttpMethod.Post, "/v1.0/virtualmodelrunners/" + vmr.Id + "/runtime-backoff/clear?tenantId=" + _Host.TenantId, "{}").ConfigureAwait(false);
            systemAdmin.StatusCode.Should().Be(HttpStatusCode.OK, await systemAdmin.Content.ReadAsStringAsync().ConfigureAwait(false));
        }

        private async Task<VirtualModelRunner> CreateRunnerWithHistoryAsync(string tenantId, string token, FakeUpstreamServer upstream, string slug)
        {
            ModelRunnerEndpoint endpoint = await _Host.Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = tenantId,
                Name = "openai " + slug,
                Hostname = upstream.Hostname,
                Port = upstream.Port,
                ApiType = ApiTypeEnum.OpenAI,
                ApiKey = "upstream-key",
                MaxParallelRequests = 0
            }).ConfigureAwait(false);
            VirtualModelRunner vmr = await _Host.Database.VirtualModelRunner.CreateAsync(new VirtualModelRunner
            {
                TenantId = tenantId,
                Name = slug,
                BasePath = "/v1.0/api/" + slug + "/",
                ApiType = ApiTypeEnum.OpenAI,
                ModelRunnerEndpointIds = new List<string> { endpoint.Id },
                RequestHistoryEnabled = true
            }).ConfigureAwait(false);

            HttpResponseMessage proxied = await _Host.SendAsync(HttpMethod.Post, vmr.BasePath + "v1/chat/completions", "{\"model\":\"gpt-test\",\"messages\":[]}", token).ConfigureAwait(false);
            proxied.StatusCode.Should().Be(HttpStatusCode.OK, await proxied.Content.ReadAsStringAsync().ConfigureAwait(false));
            (await ProxyTestHelpers.WaitForHistoryAsync(_Host.Database, tenantId, vmr.Id).ConfigureAwait(false)).Should().NotBeNull();
            return vmr;
        }

        private async Task<ModelRunnerEndpoint> CreateEndpointAsync(ApiTypeEnum type, string apiKey)
        {
            return await _Host.Database.ModelRunnerEndpoint.CreateAsync(new ModelRunnerEndpoint
            {
                TenantId = _Host.TenantId,
                Name = type + " fake " + Guid.NewGuid().ToString("N").Substring(0, 6),
                Hostname = "127.0.0.1",
                Port = 9,
                ApiType = type,
                ApiKey = apiKey
            }).ConfigureAwait(false);
        }
    }
}
