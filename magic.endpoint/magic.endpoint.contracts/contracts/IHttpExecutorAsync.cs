/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2021, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Threading.Tasks;
using magic.endpoint.contracts.poco;

namespace magic.endpoint.contracts.contracts
{
    /// <summary>
    /// Service interface for executing a dynamically resolved Magic URLs.
    /// </summary>
    public interface IHttpExecutorAsync
    {
        /// <summary>
        /// Executes an HTTP endpoint with the specified request encpasulating the request's data.
        /// </summary>
        /// <param name="request">Request arguments such as URL, payload, QUERY parameters, etc.</param>
        /// <returns>The result of the evaluation.</returns>
        Task<MagicResponse> ExecuteAsync(MagicRequest request);
    }
}
