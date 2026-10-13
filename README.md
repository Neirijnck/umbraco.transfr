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

Every test site also gets the API user a remote needs (see below), with the client id and secret from `TransfrTestSite:ApiUser` in `appsettings.local.json`. With a site running, check that its API user can read but is refused everything the tool never does:

```powershell
./tools/probe-test-site-api-user.ps1 -Name B
```

## Setting up a remote

Transfr reads and writes a remote through the remote's own Management API, signed in as an API user. Nothing is installed on the remote. Give that user the least it needs, so the remote itself refuses anything destructive even if the tool had a bug:

1. A user group with the sections **Content** and **Media**, access to all languages, start nodes at the root, and only these document permissions: Read, Create, Update, Publish, Sort, and read/write property values. Not Delete, Unpublish or Move.
2. An **API user** in that group.
3. **Client credentials** for that user. The client id must start with `umbraco-back-office-`.

The test sites create exactly this in code (`tests/Neirijnck.Transfr.TestSite/Seeding/TestApiUserSeeder.cs`). Without these permissions a delete, unpublish or move is answered with 403; with them, `DELETE /document/{id}` deletes a document permanently, without the recycle bin.