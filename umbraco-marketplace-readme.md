# Transfr

Content compare for Umbraco 17: compare the content and media trees of two environments side by side, see a field-level diff per node or media item, and copy nodes and media from one environment to another, key-preserving and always via a dry run first.

> Copying works both ways, from this environment to a remote and from a remote to this environment: whole nodes or only some of their fields, with the media they use. Try it on environments you can restore first.

- NuGet: `Neirijnck.Transfr`
- Umbraco: 17.7.0 and later 17.x, on both sides
- Versions: the major version follows Umbraco's, so Transfr 17.x is for Umbraco 17 (1.0.x were its first releases for Umbraco 17). Changes per version: [CHANGELOG.md](https://github.com/Neirijnck/umbraco.transfr/blob/master/CHANGELOG.md)
- Licence: MIT

## What it does

Install it on the environment you work in (typically your own machine or a staging site): it compares that environment with a remote, and copies to the remote or from it. The remote needs nothing installed: Transfr talks to it through its own Management API, as an API user you create there.

- **Compare** the content tree here with the remote's, by key, one level at a time or a whole branch at once. Nodes that exist on one side only, live under another parent, sit in another order among their siblings, or differ in content are marked in the trees.
- **Diff** one node on both sides, property by property and block by block, with the changed words marked.
- **Compare media** the same way: the media tree here next to the remote's, and one media item on both sides with its values and its file.
- **Check the schema** behind every node: a node whose document type (or anything it uses: compositions, data types, block element types) is missing or different on the other side can't be copied. A remote that was installed separately, so that the same document types have other keys, is refused as a whole.
- **Dry run** a copy of the nodes you pick, whole or only some of their fields, and of the media items you pick or the nodes use: per node or media item, create, update, leave unchanged, skip as a conflict, or refuse with the reason.
- **Transfer** exactly that dry run, with the same keys, published where they are published on the source, in the same place among their siblings; media with their folders, copying a file only when it is new or changed.
- **Copy either way**: from this environment to the remote, or from the remote to this environment.
- **History** of every transfer with a remote, either way: when, by whom, and which nodes and media items it created or updated.

## Safety

- Nothing is ever deleted, unpublished or moved, on the remote or here. Every request to a remote is checked against a fixed list (reads, creating, updating, publishing and sorting documents, uploading files and creating and updating media, and reading a media file from its public path), and the remote's API user is refused deleting, unpublishing and moving documents by its own permissions. Umbraco has no such permissions for media, so for media the fixed list is the guard.
- A copy from a remote writes here as you, through Umbraco's own editing services: it needs your user's permissions on every node and media item it writes, exactly as editing them would, and is refused per item otherwise.
- Copying from a remote to this environment is allowed when `Transfr:AllowPull` is true; when it is not set, only in the `Development` and `Local` environments, so a deployed site doesn't take in content unless you switch it on.
- A transfer always follows a dry run, and is refused, with the reasons, when either side changed since that dry run. Transfr plans again on the server and writes only what that fresh plan says.
- A node or media item changed on the side being written to since the last copy, either way, is a **conflict**: it is skipped unless you choose to overwrite it. The dry run says who last changed it there, and when. Sorting a node's siblings counts as a change, since Umbraco saves the sorted nodes again.
- One transfer per remote runs at a time, either way.
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

The environment Transfr is installed on must be able to reach the remote: a developer machine can reach a hosted site, not the other way round. To copy media from the remote, its media files must be served publicly at their usual path (`/media/<folder>/<file>`, Umbraco's default).

## Connecting a remote

Connect to the remote from the environment Transfr is installed on, in one of two ways.

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

To copy from a remote to this environment outside `Development` and `Local`, switch it on:

```json
{
  "Transfr": {
    "AllowPull": true
  }
}
```

## Using Transfr

The Transfr section has four views.

### Content

This environment's content tree next to a remote's, compared by key one level at a time as you expand it. Every node that exists on both sides is marked when its content or its place among its siblings differs.

![The Content view: this environment's tree next to the remote's, with changed nodes, a node in another order and nodes on one side only marked](https://raw.githubusercontent.com/Neirijnck/umbraco.transfr/master/assets/screenshots/compare.png)

- **Compare everything**, or the button on a node, compares a whole branch at once (up to 2,000 nodes) and opens the tree down to each difference.
- Clicking a node that is on both sides shows it on both sides, property by property and block by block, with the changed words marked.

  ![The diff panel: one node on both sides, with the changed words in the rich text and in a block marked](https://raw.githubusercontent.com/Neirijnck/umbraco.transfr/master/assets/screenshots/diff.png)

- A node whose document type is missing or different on the other side is greyed out with the reason.
- Choose which way to copy above the trees: **To** the remote, or **From** the remote to this environment (shown where copying from a remote is allowed). Tick nodes in the tree you copy from, each on its own or with everything below it. The selection holds the media picked in the Media view too, stays in view while you scroll, and is kept per browser tab, remote and direction until a transfer completes.
- To copy only some fields of a node, either way, tick the names and properties that differ in its diff and add them to the selection. A block list or grid is one field. Only those fields are written; everything else on the side being written to stays as it is. They are published only where both sides are published without unpublished changes, so nobody's draft goes live; otherwise they are saved as a draft there. A node ticked whole in the tree is copied whole.
- **Dry run** shows per node what the copy would do: create, update, leave unchanged, skip as a conflict (with who last changed it on the side being written to, and when), or refuse with the reason; which cultures it would publish, and when and by whom it was last copied, either way. The media a node uses get rows of their own: created with their folders when the side being written to lacks them, updated when they changed, skipped as a conflict when they changed there. A missing parent or linked page can be added to the selection from there. A dry run reads both sides and changes nothing.
- Tick **Overwrite** on a conflict to replace that version anyway, and choose **Transfer** to carry out exactly that dry run. The report shows per node what happened.

### Media

This environment's media tree next to the remote's, compared the same way: media items on one side only, in another folder, or changed (their file, focal point, crops or other values) are marked, and **Compare everything** compares the whole tree at once. Tick media items in the tree you copy from, a folder on its own or with everything below it, to copy them without a node that uses them: they join the same selection as the nodes, and one dry run covers both. Clicking a media item that is on both sides shows it on both sides: its values, and its file (an image shown, any other file as a link).

### History

Every transfer between this environment and a remote, either way, newest first, with which way, when, by whom and the nodes and media items it created or updated. Clicking a node or media item that is still here shows it on both sides.

![The History view: two transfers to the remote, each with its time, user and the nodes it created or updated](https://raw.githubusercontent.com/Neirijnck/umbraco.transfr/master/assets/screenshots/history.png)

### Remotes

The remotes, with a connection test per remote, and the form to connect one at runtime.

## Current limitations

- Fields can only be copied to a node the side being written to has already.
- A media item is compared by its data, not by its file: a file replaced by another with the same name, size and dimensions shows as unchanged.
- A media file is copied from the remote only when the remote serves it publicly at `/media/<folder>/<file>`; while it is copied, it is held in memory.
- A node that references a member can't be copied: members are never copied.
- Both environments must come from the same installation (for example staging restored from production), so their document types share keys.
- Measured against Umbraco 17.7.0 on both sides.

## Roadmap

Planned, not in this version:

- **Compare with…**: an action on a content node that opens the compare at that branch.

## More

- Source code, development and test setup: [github.com/Neirijnck/umbraco.transfr](https://github.com/Neirijnck/umbraco.transfr)
- Questions, issues and ideas: [GitHub issues](https://github.com/Neirijnck/umbraco.transfr/issues)