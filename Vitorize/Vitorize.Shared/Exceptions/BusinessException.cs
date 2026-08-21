using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitorize.Shared.Exceptions
{
    public class BusinessException : Exception
    {
        public BusinessException(string message) : base(message)
        {
        }

        /// <summary>
        /// Optional machine-readable outcome for callers that must branch on the reason rather than
        /// on the Persian message. Null for the vast majority of business rules, whose message is
        /// the whole contract.
        /// </summary>
        public BusinessException(string message, string errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        public string? ErrorCode { get; }
    }
}
