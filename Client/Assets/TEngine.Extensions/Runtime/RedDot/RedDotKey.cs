using System;
using System.Collections.Generic;
using System.Text;

namespace TEngine.Extensions.RedDot
{
    public static class RedDotKey
    {
        public static string Create(string name, IReadOnlyList<ulong>? parameters = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Red-dot name cannot be empty.", nameof(name));
            }

            if (parameters == null || parameters.Count == 0)
            {
                return name;
            }

            var builder = new StringBuilder(name);
            for (var i = 0; i < parameters.Count; i++)
            {
                builder.Append(':').Append(parameters[i]);
            }

            return builder.ToString();
        }
    }
}
