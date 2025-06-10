/*
Copyright 2017 Google Inc

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Apis.Tests.Mocks
{
    /// <summary>
    /// A mock of <see cref="HttpMessageHandler"/> that records incoming requests and returns queued responses.
    /// </summary>
    public class MockMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();
        public Queue<HttpResponseMessage> Responses { get; } = new Queue<HttpResponseMessage>();
        public string LastRequestContent { get; private set; }
        public string RequestContent { get; private set; }


        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Content != null)
            {
                // For new tests and better async practice
                LastRequestContent = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                // For compatibility with old tests
                RequestContent = request.Content.ReadAsStringAsync().Result;
            }

            if (Responses.Count > 0)
            {
                return Responses.Dequeue();
            }

            // Default response or throw, depending on desired behavior if no response is queued.
            // For now, let's return a default to avoid breaking tests that might not queue a response
            // if they don't care about the SendAsync result directly.
            // However, the tests in CustomSubjectTokenExternalAccountCredentialTests *do* care.
            // A better approach for those tests is to ensure a response is always queued.
            // Throwing here might be better to catch tests that don't set up responses.
            throw new System.InvalidOperationException("No response configured for MockMessageHandler.");
        }
    }
}
