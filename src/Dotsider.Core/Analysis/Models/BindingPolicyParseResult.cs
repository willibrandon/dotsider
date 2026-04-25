namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Output of <see cref="BindingPolicy.ParseConfigFile"/>: the redirects, codeBase entries,
/// per-identity publisher-policy disablements, probing privatePath segments, and the
/// runtime-scoped publisher-policy bypass flag found in a single configuration file.
/// </summary>
/// <param name="Redirects">All <c>&lt;bindingRedirect&gt;</c> entries parsed from the file.</param>
/// <param name="CodeBases">All <c>&lt;codeBase&gt;</c> entries parsed from the file.</param>
/// <param name="Disabled">
/// Identities whose <c>&lt;dependentAssembly&gt;</c> block carried a
/// <c>&lt;publisherPolicy apply="no"/&gt;</c> child.
/// </param>
/// <param name="PrivatePaths">All <c>&lt;probing privatePath="..."/&gt;</c> segments.</param>
/// <param name="PublisherPolicyDisabledGlobally">
/// <see langword="true"/> when the file's <c>&lt;runtime&gt;</c> element carried a top-level
/// <c>&lt;publisherPolicy apply="no"/&gt;</c>, suppressing publisher policy for every bind in
/// the AppDomain regardless of <c>&lt;dependentAssembly&gt;</c>.
/// </param>
public sealed record BindingPolicyParseResult(
    IReadOnlyList<BindingRedirect> Redirects,
    IReadOnlyList<CodeBaseEntry> CodeBases,
    IReadOnlyCollection<(string Name, string? PublicKeyToken, string Culture)> Disabled,
    IReadOnlyList<string> PrivatePaths,
    bool PublisherPolicyDisabledGlobally);
