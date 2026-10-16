# Transfr

Content compare for Umbraco 17: compare the content trees of two environments side by side, see a field-level diff per node, and copy nodes from one environment to another, key-preserving and always via a dry run first.

> Copying documents from this environment to a remote works end to end; media, the other direction and field-level copies are on the [roadmap](#roadmap). Try it on environments you can restore first.

- NuGet: `Neirijnck.Transfr`
- Umbraco: 17.7.0 and later 17.x, on both sides
- Versions: the major version follows Umbraco's, so Transfr 17.x is for Umbraco 17 (1.0.x were its first releases for Umbraco 17). Changes per version: [CHANGELOG.md](https://github.com/Neirijnck/umbraco.transfr/blob/master/CHANGELOG.md)
- Licence: MIT

## What it does

Install it on the environment you compare **from** (typically your own machine or a staging site). The remote needs nothing installed: Transfr talks to it through its own Management API, as an API user you create there.

- **Compare** the content tree here with the remote's, by key, one level at a time or a whole branch at once. Nodes that exist on one side only, live under another parent, sit in another order among their siblings, or differ in content are marked in the trees.
- **Diff** one node on both sides, property by property and block by block, with the changed words marked.
- **Check the schema** behind every node: a node whose document type (or anything it uses: compositions, data types, block element types) is missing or different on the other side can't be copied. A remote that was installed separately, so that the same document types have other keys, is refused as a whole.
- **Dry run** a copy of the nodes you pick: per node, create, update, leave unchanged, skip as a conflict, or refuse with the reason.
- **Transfer** exactly that dry run to the remote, with the same keys, published where they are published here, in the same place among their siblings.
- **History** of every transfer to a remote: when, by whom, and which nodes it created or updated.

## Safety

- Nothing is ever deleted, unpublished or moved on a remote. Every request to a remote is checked against a fixed list of Management API endpoints (reads, plus creating, updating, publishing and sorting documents), and the remote's API user is refused everything else by its own permissions.
- A transfer always follows a dry run, and is refused, with the reasons, when either side changed since that dry run. Transfr plans again on the server and writes only what that fresh plan says.
- A node changed on the remote since the last copy from here is a **conflict**: it is skipped unless you choose to overwrite it. The dry run says who last changed it there, and when.
- One transfer per remote runs at a time.
- Only administrators can use Transfr, and an environment without remotes shows no Transfr UI at all.

## Installation

```bash
dotnet add package Neirijnck.Transfr
```

On first boot the package grants its section to the Administrators group and creates its own table, `transfrCopyHistory`, which records what was copied where (package migrations). Other groups can be given the section by hand, but the endpoints still require administrator rights.

## Setting up a remote

Transfr reads and writes a remote through the remote's own Management API, signed in as an API user. Nothing is installed on the remote. Give that user the least it needs, so the remote itself refuses anything destructive even if the tool had a bug:

1. A user group with the sections **Content** and **Media**, access to all languages, start nodes at the root, and only these document permissions: Read, Create, Update, Publish, Sort, and read/write property values. Not Delete, Unpublish or Move.
2. An **API user** in that group.
3. **Client credentials** for that user. The client id must start with `umbraco-back-office-`.

Transfr also reads the remote's document types and data types, by key, to check that a node's schema matches before it can be copied. This group can read those without the Settings section, so don't add it. Without these permissions a delete, unpublish or move is answered with 403; with them, `DELETE /document/{id}` deletes a document permanently, without the recycle bin.

The environment you compare from must be able to reach the remote: a developer machine can reach a hosted site, not the other way round.

## Connecting a remote

Connect to the remote from the environment you compare **from**, in one of two ways.

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

`BaseUrl` is the site root and must use https (plain http only for this machine). An environment without configured remotes and without runtime connections shows no Transfr UI, and its Transfr endpoints answer 404.

## Using Transfr

The Transfr section has three views.

### Content

This environment's content tree next to a remote's, compared by key one level at a time as you expand it. Every node that exists on both sides is marked when its content or its place among its siblings differs.

![The Content view: this environment's tree next to the remote's, with changed nodes, a node in another order and nodes on one side only marked](https://raw.githubusercontent.com/Neirijnck/umbraco.transfr/master/assets/screenshots/compare.png)

- **Compare everything**, or the button on a node, compares a whole branch at once (up to 2,000 nodes) and opens the tree down to each difference.
- Clicking a node that is on both sides shows it on both sides, property by property and block by block, with the changed words marked.

  ![The diff panel: one node on both sides, with the changed words in the rich text and in a block marked](https://raw.githubusercontent.com/Neirijnck/umbraco.transfr/master/assets/screenshots/diff.png)

- A node whose document type is missing or different on the other side is greyed out with the reason.
- Tick nodes in the left tree, each on its own or with everything below it. The selection is kept per browser tab and remote until a transfer completes.
- **Dry run** shows per node what a copy to the remote would do: create, update, leave unchanged, skip as a conflict (with who last changed it on the remote, and when), or refuse with the reason; which cultures it would publish, which media it needs, and when and by whom it was last copied there. A missing parent or linked page can be added to the selection from there. A dry run reads both sides and changes nothing.
- Tick **Overwrite** on a conflict to replace the remote's version anyway, and choose **Transfer** to carry out exactly that dry run. The report shows per node what happened.

### History

Every transfer from this environment to a remote, newest first, with when, by whom and the nodes it created or updated. Clicking a node that is still here shows it on both sides.

![The History view: two transfers to the remote, each with its time, user and the nodes it created or updated](https://raw.githubusercontent.com/Neirijnck/umbraco.transfr/master/assets/screenshots/history.png)

### Remotes

The remotes, with a connection test per remote, and the form to connect one at runtime.

## Current limitations

- Only from this environment to a remote, and only whole nodes.
- Media are not copied yet. A node that uses a media item the remote doesn't have is still copied, with the reference as it is; the dry run lists the missing media.
- A node that references a member can't be copied: members are never copied.
- Both environments must come from the same installation (for example staging restored from production), so their document types share keys.
- Measured against Umbraco 17.7.0 on both sides.

## Roadmap

Planned, not in this version:

- **Field-level transfer**: pick properties (per culture), the name or the publish state in the diff panel, and copy only those.
- **Media**: copy missing media items, with their folders and files, with the same keys.
- **The other direction**: copy from a remote to this environment.
- **Compare with…**: an action on a content node that opens the compare at that branch.

## More

- Source code, development and test setup: [github.com/Neirijnck/umbraco.transfr](https://github.com/Neirijnck/umbraco.transfr)
- Questions, issues and ideas: [GitHub issues](https://github.com/Neirijnck/umbraco.transfr/issues)