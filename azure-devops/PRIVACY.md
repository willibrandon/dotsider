# Privacy

The Dotsider Azure Pipelines extension analyzes files selected by the pipeline and writes reports into the agent workspace. It does not send application contents or reports to Dotsider services.

Unless `dotsiderPath` is supplied, the task contacts GitHub to resolve or download an official Dotsider release and its checksum. Azure Pipelines handles any reports published as build artifacts according to the project's retention and access settings.
