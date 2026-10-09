# Security

## Supported versions

`ChromaDotNet.VectorData` is deprecated: 0.4.4 is its last release, and no version gets fixes any more, security fixes included. Move to [CommunityToolkit.VectorData.Chroma](https://www.nuget.org/packages/CommunityToolkit.VectorData.Chroma), the same provider in the [AI Community Toolkit](https://github.com/CommunityToolkit/AI).

The form below takes reports about this package. It also takes reports about `CommunityToolkit.VectorData.Chroma`, and we pass them privately to the maintainers of the AI Community Toolkit. A vulnerability in `ChromaDotNet.Client` goes to its [security policy](https://github.com/ChromaDotNet/ChromaDB.Client/blob/main/SECURITY.md).

## Reporting a vulnerability

Please do not report a vulnerability in a public issue, discussion or pull request.

Report it privately on GitHub instead, in the [ChromaDB.Client](https://github.com/ChromaDotNet/ChromaDB.Client) repository, which has the same maintainers: [Report a vulnerability](https://github.com/ChromaDotNet/ChromaDB.Client/security/advisories/new). This repository is archived and does not take reports. Only the maintainers see the report.

Include the version of the package, the version of Chroma or Chroma Cloud, and the steps to reproduce the problem.

## What happens next

- We acknowledge the report within 7 days.
- We confirm or rule out the vulnerability within 14 days, and keep you informed of the progress.
- A confirmed vulnerability gets a GitHub security advisory that says which package to move to, with credit to you unless you prefer otherwise. This package gets no new release.
- Please keep the details private until the advisory is published, and for at most 90 days from the report.

## How the package is published

- Only the CI publishes the package, from the version tags of this repository, with [NuGet trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing): no NuGet API key is kept by a person or in the repository.
- The organization requires two-factor authentication.
- The workflows pin their actions to commit hashes. Dependabot and CodeQL check the dependencies and the code.
