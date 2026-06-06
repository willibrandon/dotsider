namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A single Source Link document mapping.
/// </summary>
/// <param name="DocumentPattern">The document path pattern.</param>
/// <param name="UrlTemplate">The URL template.</param>
public sealed record SourceLinkMapping(string DocumentPattern, string UrlTemplate);
