# Transfr

Content compare for Umbraco 17: compare the content trees of two environments side by side, see a field-level diff per node, and copy nodes between environments, key-preserving and always via a dry run first.

> Status: early development. Not usable yet.

- NuGet: `Neirijnck.Transfr`
- Umbraco: 17.7.0 and later 17.x
- Licence: MIT

## Repository layout

| Path | What |
|---|---|
| `src/Neirijnck.Transfr` | The package: Razor class library with Management API controllers and the backoffice client (`Client/`, Vite + Lit) |
| `src/Neirijnck.Transfr.Core` | Pure logic (tree diff, value comparison, planning). No Umbraco reference |
| `tests/Neirijnck.Transfr.Core.Tests` | Unit tests for Core |
| `tests/Neirijnck.Transfr.TestSite` | Umbraco 17.7.0 site referencing the package |

## Development

Requirements: .NET 10 SDK, Node 22+.

1. Copy `tests/Neirijnck.Transfr.TestSite/appsettings.local.example.json` to `appsettings.local.json` and fill in the admin email and password for the unattended install. The file is gitignored.
2. Run the test site.

```bash
dotnet run --project tests/Neirijnck.Transfr.TestSite
```

`dotnet build` also builds the backoffice client (`npm ci` + `npm run build`). Pass `-p:SkipClientBuild=true` to skip that step.

To regenerate the TypeScript API client, keep the test site running and run:

```bash
npm run generate-client --prefix src/Neirijnck.Transfr/Client
```