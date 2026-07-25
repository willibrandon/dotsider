using System.Globalization;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Bounds attacker-controlled ReadyToRun container traversal before the reader enters a loop.
/// Separate instances are used for method-map containers and import slots.
/// </summary>
internal sealed class ReadyToRunTraversalBudget
{
    /// <summary>The maximum traversal work accepted from one ReadyToRun image.</summary>
    public const int MaximumWork = 1_048_576;

    /// <summary>The unconsumed work remaining in this budget.</summary>
    public int Remaining { get; private set; } = MaximumWork;

    /// <summary>
    /// Attempts to consume <paramref name="amount"/> work units.
    /// </summary>
    /// <param name="amount">The non-negative number of units to consume.</param>
    /// <returns><see langword="true"/> when the complete amount was available and consumed.</returns>
    public bool TryCharge(int amount)
    {
        if (amount < 0 || amount > Remaining)
        {
            return false;
        }

        Remaining -= amount;
        return true;
    }

    /// <summary>
    /// Consumes <paramref name="amount"/> work units or rejects the image as malformed.
    /// </summary>
    /// <param name="amount">The non-negative number of units to consume.</param>
    /// <param name="description">The ReadyToRun structure being traversed.</param>
    public void Charge(int amount, string description)
    {
        if (!TryCharge(amount))
        {
            var limit = MaximumWork.ToString("N0", CultureInfo.InvariantCulture);
            throw new BadImageFormatException(
                $"{description} exceeds the {limit}-unit traversal limit.");
        }
    }
}
