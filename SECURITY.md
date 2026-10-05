# Security

Report a vulnerability privately through [GitHub Security Advisories](https://github.com/willibrandon/dotsider/security/advisories/new)
for the `willibrandon/dotsider` repository. Please do not open a public issue for a vulnerability.
Fixes go into the latest release. Include the affected version, steps to reproduce, and a minimal
input when possible.

dotsider inspects assemblies, native binaries, debug information, and packages with your
permissions. Static analysis should not execute the code being inspected. Runtime tracing
explicitly launches the selected application; it is not a sandbox. The MCP server and local
session interface can read files and start traces requested by their clients.

Reports are welcome for unintended code execution, file access or writes beyond the requested
operation, unsafe handling of crafted inputs, or vulnerabilities in the hosted demo and CI
integrations.
