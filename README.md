# D3sk.ChatUi

The chat desk of D3SKHAND as libraries (private repo).

- `D3sk.ChatUi`: rules with no UI types (markdown to escaped HTML, status labels, view models).
  It never references Avalonia; a test fails if it does.
- `D3sk.ChatUi.Avalonia`: the Avalonia controls (transcript, composer, quick settings, theme).

## Build and test

Needs the .NET 10 SDK (`global.json`).

```bash
dotnet test D3sk.ChatUi.slnx -c Release
```

Neither package references `D3sk.Contracts` yet. When one does, restoring it needs a classic
personal access token with `read:packages` in the environment (see `nuget.config`), or, until
then, a gitignored `Directory.Build.local.targets` copied from the `.example` beside it, pointing
at a `../D3sk.Contracts` checkout.

## CI and release

`.github/workflows/ci.yml` runs on the self-hosted Linux runner (`[self-hosted, kyber]`; set the
repository variable `CI_RUNS_ON` to a JSON label list to override). Tag `vX.Y.Z` and
`publish.yml` tests, packs both packages and pushes them to GitHub Packages with the job's
`GITHUB_TOKEN`.

## Licence

MIT, see `LICENSE`.
