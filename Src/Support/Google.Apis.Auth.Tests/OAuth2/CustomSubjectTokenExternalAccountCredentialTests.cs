/*
Copyright 2023 Google LLC

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    https://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Http;
using Google.Apis.Json;
using Google.Apis.Tests.Mocks; // Assumes MockClock, MockHttpMessageHandler, MockHttpClientFactory exist
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Google.Apis.Auth.Tests.OAuth2
{
    public class CustomSubjectTokenExternalAccountCredentialTests
    {
        private const string TestAudience = "audience";
        private const string TestSubjectTokenType = "urn:test:token-type";
        private const string TestTokenUrl = "https://example.com/token";
        private const string TestSubjectToken = "subject_token_123";
        private const string TestAccessToken = "access_token_456";
        private const string GrantTypeSts = "urn:ietf:params:oauth:grant-type:token-exchange";
        private const string RequestedTokenTypeSts = "urn:ietf:params:oauth:token-type:access_token";

        // Helper to parse query strings for tests
        private Dictionary<string, string> ParseQueryString(string query)
        {
            if (string.IsNullOrEmpty(query)) return new Dictionary<string, string>();
            return query.TrimStart('?').Split('&')
                .Select(pair => pair.Split(new[] { '=' }, 2))
                .Where(parts => parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
                .ToDictionary(
                    parts => parts[0],
                    parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : ""
                );
        }

        private CustomSubjectTokenExternalAccountCredential.Initializer CreateDefaultInitializer(HttpMessageHandler messageHandler) =>
            new CustomSubjectTokenExternalAccountCredential.Initializer(TestTokenUrl, TestAudience, TestSubjectTokenType)
            {
                HttpClientFactory = new MockHttpClientFactory(messageHandler),
                Clock = new MockClock(DateTime.UtcNow) // Often needed by underlying ServiceCredential logic
            };

        [Fact]
        public async Task GetAccessToken_Success_CallsProviderAndSts()
        {
            var mockTokenProvider = new Mock<ISubjectTokenProvider>();
            mockTokenProvider
                .Setup(p => p.GetSubjectTokenAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestSubjectToken);

            var mockMessageHandler = new MockMessageHandler();
            var tokenResponse = new TokenResponse { AccessToken = TestAccessToken, ExpiresInSeconds = 3600, TokenType = "Bearer" };
            mockMessageHandler.Responses.Enqueue(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(NewtonsoftJsonSerializer.Instance.Serialize(tokenResponse)) });

            var initializer = CreateDefaultInitializer(mockMessageHandler);
            var credential = new CustomSubjectTokenExternalAccountCredential(initializer, mockTokenProvider.Object);

            string token = await ((ITokenAccess)credential).GetAccessTokenForRequestAsync();

            Assert.Equal(TestAccessToken, token);
            mockTokenProvider.Verify(p => p.GetSubjectTokenAsync(It.IsAny<CancellationToken>()), Times.Once);

            Assert.Single(mockMessageHandler.Requests);
            var request = mockMessageHandler.Requests[0];
            Assert.Equal(TestTokenUrl, request.RequestUri.ToString());
            var content = await request.Content.ReadAsStringAsync();
            var requestParams = ParseQueryString(content);
            Assert.Equal(GrantTypeSts, requestParams["grant_type"]);
            Assert.Equal(TestSubjectToken, requestParams["subject_token"]);
            Assert.Equal(TestSubjectTokenType, requestParams["subject_token_type"]);
            Assert.Equal(TestAudience, requestParams["audience"]);
            Assert.Equal(RequestedTokenTypeSts, requestParams["requested_token_type"]);
        }

        [Fact]
        public async Task GetAccessToken_ProviderThrows_ThrowsSubjectTokenException()
        {
            var mockTokenProvider = new Mock<ISubjectTokenProvider>();
            var providerException = new InvalidOperationException("Provider error");
            mockTokenProvider
                .Setup(p => p.GetSubjectTokenAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(providerException);

            var initializer = CreateDefaultInitializer(new MockMessageHandler()); // STS not called
            var credential = new CustomSubjectTokenExternalAccountCredential(initializer, mockTokenProvider.Object);

            var thrownException = await Assert.ThrowsAsync<SubjectTokenException>(() => ((ITokenAccess)credential).GetAccessTokenForRequestAsync());
            Assert.Equal(providerException, thrownException.InnerException);
        }

        [Fact]
        public async Task WithQuotaProject_PreservesProviderAndSettings_AndFetchesToken()
        {
            var mockTokenProvider = new Mock<ISubjectTokenProvider>();
            mockTokenProvider
                .Setup(p => p.GetSubjectTokenAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestSubjectToken);

            var mockMessageHandler = new MockMessageHandler();
            var tokenResponse = new TokenResponse { AccessToken = TestAccessToken, ExpiresInSeconds = 3600, TokenType = "Bearer" };
            mockMessageHandler.Responses.Enqueue(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(NewtonsoftJsonSerializer.Instance.Serialize(tokenResponse)) });

            var initializer = CreateDefaultInitializer(mockMessageHandler);
            var originalCredential = new CustomSubjectTokenExternalAccountCredential(initializer, mockTokenProvider.Object);

            string newQuotaProject = "new_project_id_123";
            var newCredential = ((IGoogleCredential)originalCredential).WithQuotaProject(newQuotaProject) as CustomSubjectTokenExternalAccountCredential;

            Assert.NotNull(newCredential);
            Assert.Equal(newQuotaProject, ((IGoogleCredential)newCredential).QuotaProject);
            Assert.Equal(TestAudience, newCredential.Audience); // Check a few props are copied

            // Verify provider is still active and STS is called
            string token = await ((ITokenAccess)newCredential).GetAccessTokenForRequestAsync();
            Assert.Equal(TestAccessToken, token);
            mockTokenProvider.Verify(p => p.GetSubjectTokenAsync(It.IsAny<CancellationToken>()), Times.Once); // Called once for this new credential
            Assert.Single(mockMessageHandler.Requests); // STS called once
        }

        [Fact]
        public async Task WithoutImpersonation_WhenImpersonationNotSet_ReturnsSameProviderInstance()
        {
            var mockTokenProvider = new Mock<ISubjectTokenProvider>();
            mockTokenProvider
                .Setup(p => p.GetSubjectTokenAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestSubjectToken);

            var mockMessageHandler = new MockMessageHandler();
            var tokenResponse = new TokenResponse { AccessToken = TestAccessToken, ExpiresInSeconds = 3600, TokenType = "Bearer" };
            mockMessageHandler.Responses.Enqueue(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(NewtonsoftJsonSerializer.Instance.Serialize(tokenResponse)) });

            var initializer = CreateDefaultInitializer(mockMessageHandler); // No ServiceAccountImpersonationUrl
            var credential = new CustomSubjectTokenExternalAccountCredential(initializer, mockTokenProvider.Object);

            var nonImpersonatedCred = credential.WithoutImpersonationConfiguration.Value as GoogleCredential;
            Assert.NotNull(nonImpersonatedCred);
            // In this case, WithoutImpersonationConfigurationImpl returns a GoogleCredential wrapping itself.
            Assert.Same(credential, ((GoogleCredential)nonImpersonatedCred).UnderlyingCredential);

            // Verify provider is still active and STS is called
            string token = await ((ITokenAccess)nonImpersonatedCred).GetAccessTokenForRequestAsync();
            Assert.Equal(TestAccessToken, token);
            mockTokenProvider.Verify(p => p.GetSubjectTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.Single(mockMessageHandler.Requests);
        }

        [Fact]
        public async Task WithoutImpersonation_WhenImpersonationSet_ReturnsNewInstanceWithSameProvider()
        {
            var mockTokenProvider = new Mock<ISubjectTokenProvider>();
            mockTokenProvider
                .Setup(p => p.GetSubjectTokenAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestSubjectToken);

            var mockMessageHandlerForSts = new MockMessageHandler();
            var stsTokenResponse = new TokenResponse { AccessToken = "sts_token_for_impersonation", ExpiresInSeconds = 3600, TokenType = "Bearer" };
            mockMessageHandlerForSts.Responses.Enqueue(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(NewtonsoftJsonSerializer.Instance.Serialize(stsTokenResponse)) });

            var initializer = CreateDefaultInitializer(mockMessageHandlerForSts);
            initializer.ServiceAccountImpersonationUrl = "https://iamcredentials.googleapis.com/v1/projects/-/serviceAccounts/test@example.iam.gserviceaccount.com:generateAccessToken";

            var credential = new CustomSubjectTokenExternalAccountCredential(initializer, mockTokenProvider.Object);

            // This will be the CustomSubjectTokenExternalAccountCredential that doesn't do impersonation
            var nonImpersonatedBaseCred = ((ImpersonatedCredential)credential.ImplicitlyImpersonated.Value).SourceCredential.UnderlyingCredential as CustomSubjectTokenExternalAccountCredential;
            Assert.NotNull(nonImpersonatedBaseCred);
            Assert.NotSame(credential, nonImpersonatedBaseCred); // It's a new instance
            Assert.Null(nonImpersonatedBaseCred.ServiceAccountImpersonationUrl); // Impersonation URL removed

            // Verify this base credential (for STS) uses the provider
            string token = await ((ITokenAccess)nonImpersonatedBaseCred).GetAccessTokenForRequestAsync(); // This will call the provider and then STS
            Assert.Equal("sts_token_for_impersonation", token);
            mockTokenProvider.Verify(p => p.GetSubjectTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.Single(mockMessageHandlerForSts.Requests); // STS was called
        }
    }
}
