# Deployment tests

`Dotsider.Deploy.Tests` covers manifest installation, metrics reporting, sample
integrity, provisioning, preflight, activation, and recovery with MSTest.

The normal test run executes the fast tests and skips the privileged container
tests:

```console
dotnet test tests/Dotsider.Deploy.Tests/Dotsider.Deploy.Tests.csproj
```

Set `DOTSIDER_RUN_DEPLOY_INTEGRATION` to `1` to run the complete Debian 13
fixture. Docker must be available and support privileged Linux containers.
The fixture publishes the Native AOT helper and website beneath `artifacts/`,
starts systemd as PID 1, provisions the real Caddy and Prometheus services, and
then exercises preflight, reporting, and integrity recovery.

Docker uses an isolated temporary configuration directory. The tests never
read or rewrite the user's Docker credential configuration.
