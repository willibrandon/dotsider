# Privacy

The Dotsider Azure Pipelines extension analyzes files selected by the pipeline and writes reports into the agent workspace. It does not send application contents or reports to Dotsider services.

Unless `dotsiderPath` is supplied, the task contacts GitHub to resolve or download an official Dotsider release and its checksum. To manage baselines, it uses the pipeline's short-lived SystemVssConnection token to read successful builds and artifacts from the current Azure DevOps project. The token is removed from the environment before Dotsider runs and is never written to reports or logs.

Azure Pipelines handles size reports and managed baseline artifacts according to the project's retention and access settings. Dotsider does not send their contents to an external service.
