/*
Copyright 2023 Google LLC

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    https://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUTHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System.Threading;
using System.Threading.Tasks;

namespace Google.Apis.Auth.OAuth2
{
    /// <summary>
    /// Provides a subject token for external account credentials.
    /// Implement this interface to provide a custom way to fetch a subject token.
    /// </summary>
    public interface ISubjectTokenProvider
    {
        /// <summary>
        /// Gets the subject token.
        /// </summary>
        /// <param name="taskCancellationToken">The cancellation token.</param>
        /// <returns>The subject token.</returns>
        Task<string> GetSubjectTokenAsync(CancellationToken taskCancellationToken);
    }
}
