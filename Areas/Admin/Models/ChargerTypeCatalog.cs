using System;
using System.Collections.Generic;
using System.Linq;

namespace tramsac99.Areas.Admin.Models
{
    public static class ChargerTypeCatalog
    {
        public static readonly IReadOnlyList<string> Options = new[]
        {
            "CCS2",
            "Type 2 (AC)",
            "GB/T AC",
            "GB/T DC",
            "CHAdeMO"
        };

        public static bool IsValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return Options.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return Options.FirstOrDefault(x => string.Equals(x, value.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
