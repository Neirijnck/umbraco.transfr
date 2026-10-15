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

Requirements: .NET 10 SDK, Node 22.22.0 (`src/Neirijnck.Transfr/Client/.nvmrc`), and PowerShell 7 (`pwsh`) for the scripts in `tools/`. Windows PowerShell 5.1 refuses them. `@umbraco-cms/backoffice` 17.7 declares Node 24.13+ and npm 11+; npm warns about that on Node 22, and the client builds and runs fine with it.

1. Copy `tests/Neirijnck.Transfr.TestSite/appsettings.local.example.json` to `appsettings.local.json` and fill in the admin email and password for the unattended install. The file is gitignored.
2. Run the test site.

```bash
dotnet run --project tests/Neirijnck.Transfr.TestSite
```

`dotnet build` also builds the backoffice client (`npm ci` + `npm run build`). Pass `-p:SkipClientBuild=true` to skip that step.

Checks for the client, run from `src/Neirijnck.Transfr/Client`:

```bash
npm run typecheck
```

```bash
npm run lint
```

```bash
npm test
```

```bash
npm run format:check
```

`npm audit` reports advisories in `js-yaml` (through `@hey-api/openapi-ts`, the client generator) and `dompurify` (through `@umbraco-cms/backoffice`). Both are development dependencies: the generator never ships, and the backoffice package is external to the bundle (the CMS serves its own). The suggested fixes are major downgrades, so they are accepted.

To regenerate the TypeScript API client, keep test site A running (see below) and run:

```bash
npm run generate-client --prefix src/Neirijnck.Transfr/Client
```

## Test environments

Three throwaway Umbraco sites under `.sites/` (gitignored), each with its own SQLite database, media and logs:

| Site | URL | What |
|---|---|---|
| A | https://localhost:44371/umbraco | Fresh install, seeded with test schema and content. The local environment: connect B and C at runtime (the start script prints the client id and secret), or start it with `-ConfiguredRemotes` |
| B | https://localhost:44372/umbraco | Copy of A's database and media: same keys. Diverge it by editing in its backoffice. Acts as a deployed remote: no Transfr UI |
| C | https://localhost:44373/umbraco | Separate fresh install, seeded the same way: same aliases, different keys. Acts as a deployed remote: no Transfr UI |

Create them (requires `appsettings.local.json`, see above), then check how their keys relate:

```powershell
./tools/new-test-sites.ps1 -Force
```

```powershell
./tools/compare-test-site-keys.ps1
```

Start all three. The script builds once, starts the sites, and prints the client id and secret for connecting B or C from A. Ctrl+C stops all three:

```powershell
./tools/start-test-sites.ps1
```

Or start one site in the foreground. Add `-NoBuild` while other test sites are running, so a build doesn't rewrite what they serve:

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

Transfr also reads the remote's document types and data types, by key, to check that a node's schema matches before it can be copied. This group can read those without the Settings section, so don't add it.

Then connect to the remote from the environment you compare **from**, in one of two ways.

**At runtime.** In the Remotes view of the Transfr section, fill in a name, the remote's address, the client id and the client secret, and choose **Connect**. Transfr signs in right away; if that fails, nothing is kept and the reason is shown. A connection is kept in server memory for your user only, never in the database or a file, until you disconnect it or the site restarts. Runtime connections are allowed when `Transfr:AllowRuntimeRemotes` is true; when it is not set, only in the `Development` and `Local` environments, so a deployed site doesn't offer them unless you switch it on.

**In configuration**, for remotes you use every day: add them to a file or secret store that is never committed (it holds the client secret):

```json
{
  "Transfr": {
    "Remotes": [
      {
        "Alias": "staging",
        "Name": "Staging",
        "BaseUrl": "https://staging.example.com",
        "ClientId": "umbraco-back-office-transfr",
        "ClientSecret": "<the client secret>"
      }
    ]
  }
}
```

`BaseUrl` is the site root and must use https (plain http only for this machine). The Transfr section has a Content view, which shows this environment's content tree next to a remote's, compared by key one level at a time as you expand it, with every node that exists on both sides marked when its content or its place among its siblings differs (**Compare everything**, or the button on a node, compares a whole branch at once, up to 2,000 nodes, and opens the tree down to each difference; clicking a node's name shows it on both sides, property by property and block by block, with the changed words marked; a node whose document type is missing or different on the other side is greyed out with the reason, and a remote that was installed separately is refused as a whole; tick nodes in the left tree, each on its own or with everything below it, and choose **Dry run** to see per node what a copy to the remote would do: create, update, leave unchanged, skip as a conflict (with who last changed it on the remote, and when), or refuse with the reason, which cultures it would publish and which media it needs, with a way to add a missing parent or linked page to the selection; a dry run reads both sides and changes nothing; tick **Overwrite** on a conflict to replace the remote's version anyway, and choose **Transfer** to carry out exactly that dry run: it is refused, with the reasons, when either side changed since, otherwise the nodes are created or updated on the remote with the same keys, published where they are published here, and put in their place among their siblings, and the report shows per node what happened; nothing is ever deleted, unpublished or moved; the selection is kept per browser tab and remote until a transfer completes; copying media comes later), and a Remotes view, which lists the remotes with a connection test. An environment without configured remotes and without runtime connections shows no Transfr UI, and its Transfr endpoints answer 404. Only administrators can use Transfr. On first boot the package grants its section to the Administrators group and creates its own table, `transfrCopyHistory`, which records what was copied where (package migrations); other groups can be given the section by hand, but the endpoints still require administrator rights.

The environment you compare from must be able to reach the remote: a developer machine can reach a hosted site, not the other way round.

The test sites create exactly this in code (`tests/Neirijnck.Transfr.TestSite/Seeding/TestApiUserSeeder.cs`). Without these permissions a delete, unpublish or move is answered with 403; with them, `DELETE /document/{id}` deletes a document permanently, without the recycle bin.