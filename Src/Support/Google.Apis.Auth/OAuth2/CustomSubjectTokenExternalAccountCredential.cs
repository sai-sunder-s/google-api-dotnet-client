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

using Google.Apis.Http;
using Google.Apis.Util;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Apis.Auth.OAuth2
{
    /// <summary>
    /// External account credential that allows a custom <see cref="ISubjectTokenProvider"/>
    /// to be used for obtaining the subject token.
    /// </summary>
    public sealed class CustomSubjectTokenExternalAccountCredential : ExternalAccountCredential, IGoogleCredential
    {
        /// <summary>
        /// Initializer for <see cref="CustomSubjectTokenExternalAccountCredential"/>.
        /// Extends <see cref="ExternalAccountCredential.Initializer"/> to allow for custom subject token providers.
        /// </summary>
        internal new class Initializer : ExternalAccountCredential.Initializer
        {
            /// <summary>
            /// Constructs a new initializer from the given <see cref="ExternalAccountCredential.Initializer"/>.
            /// </summary>
            /// <param name="initializer">The base initializer.</param>
            public Initializer(ExternalAccountCredential.Initializer initializer) : base(initializer) { }

            /// <summary>
            /// Constructs a new initializer with basic parameters.
            /// </summary>
            /// <param name="tokenUrl">The token server URL.</param>
            /// <param name="audience">The audience.</param>
            /// <param name="subjectTokenType">The subject token type.</param>
            public Initializer(string tokenUrl, string audience, string subjectTokenType)
                : base(tokenUrl, audience, subjectTokenType) { }

            /// <summary>
            /// Constructs a new initializer from the given <see cref="CustomSubjectTokenExternalAccountCredential"/>.
            /// This is used for the With* methods.
            /// </summary>
            /// <param name="credential">The credential to copy settings from.</param>
            public Initializer(CustomSubjectTokenExternalAccountCredential credential) : base(credential) { }
        }

        private readonly ISubjectTokenProvider _customSubjectTokenProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomSubjectTokenExternalAccountCredential"/> class.
        /// </summary>
        /// <param name="initializer">The initializer for this credential.</param>
        /// <param name="subjectTokenProvider">The custom subject token provider. Must not be null.</param>
        internal CustomSubjectTokenExternalAccountCredential(Initializer initializer, ISubjectTokenProvider subjectTokenProvider)
            : base(initializer, subjectTokenProvider.ThrowIfNull(nameof(subjectTokenProvider)))
        {
            // We store the custom provider separately only to pass it to WithoutImpersonationConfigurationImpl.
            // The base class already stores it for GetSubjectTokenAsyncImpl.
            _customSubjectTokenProvider = subjectTokenProvider;
        }

        // Private constructor for With* methods.
        private CustomSubjectTokenExternalAccountCredential(CustomSubjectTokenExternalAccountCredential other, Initializer initializer)
            : base(initializer, other._customSubjectTokenProvider)
        {
            _customSubjectTokenProvider = other._customSubjectTokenProvider;
        }

        /// <inheritdoc/>
        private protected override GoogleCredential WithoutImpersonationConfigurationImpl() =>
            ServiceAccountImpersonationUrl is null
            ? new GoogleCredential(this) // This will effectively be a CustomSubjectTokenExternalAccountCredential if 'this' is one.
            : new GoogleCredential(new CustomSubjectTokenExternalAccountCredential(new CustomSubjectTokenExternalAccountCredential.Initializer(this)
                {
                    ServiceAccountImpersonationUrl = null
                }, _customSubjectTokenProvider));


        // Implement IGoogleCredential members by creating a new instance with potentially updated initializers.
        // This pattern is similar to other ExternalAccountCredential implementations.

        /// <inheritdoc/>
        string IGoogleCredential.QuotaProject => QuotaProject;

        /// <inheritdoc/>
        bool IGoogleCredential.HasExplicitScopes => HasExplicitScopes;

        /// <inheritdoc/>
        bool IGoogleCredential.SupportsExplicitScopes => true; // External account credentials support explicit scopes.

        /// <inheritdoc/>
        Task<string> IGoogleCredential.GetUniverseDomainAsync(CancellationToken _) => Task.FromResult(UniverseDomain);

        /// <inheritdoc/>
        string IGoogleCredential.GetUniverseDomain() => UniverseDomain;

        /// <inheritdoc/>
        IGoogleCredential IGoogleCredential.WithQuotaProject(string quotaProject) =>
            new CustomSubjectTokenExternalAccountCredential(this, new CustomSubjectTokenExternalAccountCredential.Initializer(this) { QuotaProject = quotaProject });

        /// <inheritdoc/>
        IGoogleCredential IGoogleCredential.MaybeWithScopes(IEnumerable<string> scopes) =>
            new CustomSubjectTokenExternalAccountCredential(this, new CustomSubjectTokenExternalAccountCredential.Initializer(this) { Scopes = scopes });

        /// <inheritdoc/>
        IGoogleCredential IGoogleCredential.WithUserForDomainWideDelegation(string user) =>
            // This will call the base class implementation which throws InvalidOperationException.
            WithUserForDomainWideDelegation(user);

        /// <inheritdoc/>
        IGoogleCredential IGoogleCredential.WithHttpClientFactory(IHttpClientFactory httpClientFactory) =>
            new CustomSubjectTokenExternalAccountCredential(this, new CustomSubjectTokenExternalAccountCredential.Initializer(this) { HttpClientFactory = httpClientFactory });

        /// <inheritdoc/>
        IGoogleCredential IGoogleCredential.WithUniverseDomain(string universeDomain) =>
            new CustomSubjectTokenExternalAccountCredential(this, new CustomSubjectTokenExternalAccountCredential.Initializer(this) { UniverseDomain = universeDomain });
    }
}
