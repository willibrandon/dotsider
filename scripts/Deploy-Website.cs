#!/usr/bin/env -S dotnet --

#:property TargetFramework=net10.0
#:include ScriptSupport.cs
#:include Deploy/DeploymentOptions.cs
#:include Deploy/DeploymentProcessResult.cs
#:include Deploy/IDeploymentProcessRunner.cs
#:include Deploy/DeploymentProcessRunner.cs
#:include Deploy/SshContext.cs
#:include Deploy/DeploymentApp.cs

return DeploymentApp.Run(args);
