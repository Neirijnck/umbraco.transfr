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

## Test environments

Three throwaway Umbraco sites under `.sites/` (gitignored), each with its own SQLite database, media and logs:

| Site | URL | What |
|---|---|---|
| A | https://localhost:44371/umbraco | Fresh install, seeded with test schema and content |
| B | https://localhost:44372/umbraco | Copy of A's database and media: same keys. Diverge it by editing in its backoffice |
| C | https://localhost:44373/umbraco | Separate fresh install, seeded the same way: same aliases, different keys |

Create them (requires `appsettings.local.json`, see above), then check how their keys relate:

```powershell
./tools/new-test-sites.ps1 -Force
```

```powershell
./tools/compare-test-site-keys.ps1
```

Start one site in the foreground:

```powershell
./tools/start-test-site.ps1 -Name A
```