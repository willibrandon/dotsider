namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Records that a requested identity was rewritten by .NET Framework binding policy. Carried
/// on <see cref="GraphNavigationContext.AppliedPolicy"/> so the UI can render
/// "↪ redirected 1.0.0.0 → 13.0.0.0 via app.config" without inventing new
/// <see cref="AssemblyProvenance"/> values for redirected hits — a redirect-applied AppLocal
/// hit is still <see cref="AssemblyProvenance.AppLocal"/>, just with this annotation attached.
/// </summary>
/// <param name="Source">The policy layer that produced the rewrite.</param>
/// <param name="RequestedVersion">The version named by the metadata reference.</param>
/// <param name="BoundVersion">The version the binder actually loaded.</param>
/// <param name="CodeBaseHref">
/// When <paramref name="Source"/> is <see cref="PolicyLayer.CodeBase"/>, the configured
/// <c>href</c> attribute. <see langword="null"/> for non-codeBase sources.
/// </param>
public sealed record AppliedPolicy(
    PolicyLayer Source,
    Version RequestedVersion,
    Version BoundVersion,
    string? CodeBaseHref);
