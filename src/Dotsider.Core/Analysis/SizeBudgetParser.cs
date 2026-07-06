using System.Globalization;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Parses size-budget spec strings. The grammar is
/// <c>[scope:]limit(,limit)*</c> where scope is <c>total</c> (the default), <c>ns=NAME</c>, or
/// <c>asm=NAME</c>, and each limit is <c>max=SIZE</c> or <c>growth=SIZE|PERCENT</c>. Sizes
/// accept <c>b</c>, <c>kb</c>, <c>mb</c>, and <c>gb</c> suffixes (1 kb = 1024 bytes; a bare
/// number is bytes); percentages (<c>growth=1%</c>) apply to growth only. Examples:
/// <c>max=25mb</c> · <c>growth=1%</c> · <c>total:max=25mb,growth=50kb</c> ·
/// <c>ns=System.Text.Json:growth=10kb</c> · <c>asm=MyApp:max=2mb</c>.
/// </summary>
public static class SizeBudgetParser
{
    /// <summary>
    /// Parses one budget spec.
    /// </summary>
    /// <param name="spec">The spec string.</param>
    /// <returns>The parsed budget, at error severity.</returns>
    /// <exception cref="FormatException">The spec does not match the grammar; the message names the offending part.</exception>
    public static SizeBudget Parse(string spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var trimmed = spec.Trim();
        if (trimmed.Length == 0)
            throw new FormatException("Budget spec is empty; expected [scope:]limit(,limit)* such as 'max=25mb' or 'ns=System.Text.Json:growth=10kb'.");

        var (scope, target, limits) = SplitScope(trimmed);

        long? maxBytes = null;
        long? maxGrowthBytes = null;
        double? maxGrowthPercent = null;

        foreach (var raw in limits.Split(','))
        {
            var limit = raw.Trim();
            if (limit.StartsWith("max=", StringComparison.OrdinalIgnoreCase))
            {
                var value = limit[4..];
                if (value.EndsWith('%'))
                    throw new FormatException($"'{limit}': a percentage limits growth, not an absolute size; use growth={value}.");
                if (maxBytes is not null)
                    throw new FormatException($"'{trimmed}': duplicate max= limit.");
                maxBytes = ParseSize(value, limit);
            }
            else if (limit.StartsWith("growth=", StringComparison.OrdinalIgnoreCase))
            {
                var value = limit[7..];
                if (value.EndsWith('%'))
                {
                    if (maxGrowthPercent is not null)
                        throw new FormatException($"'{trimmed}': duplicate percentage growth= limit.");
                    var digits = value[..^1];
                    if (!double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct) || pct < 0)
                        throw new FormatException($"'{limit}': '{digits}' is not a valid percentage.");
                    maxGrowthPercent = pct;
                }
                else
                {
                    if (maxGrowthBytes is not null)
                        throw new FormatException($"'{trimmed}': duplicate byte growth= limit.");
                    maxGrowthBytes = ParseSize(value, limit);
                }
            }
            else
            {
                throw new FormatException($"'{limit}': expected max=SIZE or growth=SIZE|PERCENT.");
            }
        }

        return new SizeBudget(scope, target, maxBytes, maxGrowthBytes, maxGrowthPercent);
    }

    private static (SizeBudgetScope Scope, string? Target, string Limits) SplitScope(string spec)
    {
        var colon = spec.IndexOf(':');
        if (colon < 0)
        {
            // No scope part — unless the spec itself is a bare scope with no limits.
            if (spec.Equals("total", StringComparison.OrdinalIgnoreCase)
                || spec.StartsWith("ns=", StringComparison.OrdinalIgnoreCase)
                || spec.StartsWith("asm=", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException($"'{spec}': a scope needs at least one limit, such as '{spec}:growth=0'.");
            }

            return (SizeBudgetScope.Total, null, spec);
        }

        var scopePart = spec[..colon].Trim();
        var limits = spec[(colon + 1)..];
        if (limits.Length == 0)
            throw new FormatException($"'{spec}': a scope needs at least one limit after the colon.");

        if (scopePart.Equals("total", StringComparison.OrdinalIgnoreCase))
            return (SizeBudgetScope.Total, null, limits);

        if (scopePart.StartsWith("ns=", StringComparison.OrdinalIgnoreCase))
        {
            var target = scopePart[3..].Trim();
            if (target.Length == 0)
                throw new FormatException($"'{spec}': ns= needs a namespace.");
            return (SizeBudgetScope.Namespace, target, limits);
        }

        if (scopePart.StartsWith("asm=", StringComparison.OrdinalIgnoreCase))
        {
            var target = scopePart[4..].Trim();
            if (target.Length == 0)
                throw new FormatException($"'{spec}': asm= needs an assembly simple name.");
            return (SizeBudgetScope.Assembly, target, limits);
        }

        throw new FormatException($"'{scopePart}': unknown scope; expected total, ns=NAME, or asm=NAME.");
    }

    private static long ParseSize(string value, string context)
    {
        var trimmed = value.Trim();
        var multiplier = 1L;
        if (trimmed.EndsWith("kb", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024;
            trimmed = trimmed[..^2];
        }
        else if (trimmed.EndsWith("mb", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024 * 1024;
            trimmed = trimmed[..^2];
        }
        else if (trimmed.EndsWith("gb", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024L * 1024 * 1024;
            trimmed = trimmed[..^2];
        }
        else if (trimmed.EndsWith('b') || trimmed.EndsWith('B'))
        {
            trimmed = trimmed[..^1];
        }

        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || number < 0)
            throw new FormatException($"'{context}': '{value}' is not a valid size; expected a number with an optional b/kb/mb/gb suffix.");

        return (long)(number * multiplier);
    }
}
