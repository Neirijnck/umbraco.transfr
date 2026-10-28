# Changelog

From 17.0.0 on, Transfr's major version is the Umbraco major it supports: 17.x for Umbraco 17, 18.x for Umbraco 18. Each supported Umbraco major has its own branch that its versions are tagged on (`feature/17` for Umbraco 17). The 1.0.x releases are for Umbraco 17.

## [Unreleased]

### Added

- Fields from a remote: with From chosen, tick the names and properties that differ in a node's diff to copy only those to this environment, as the other way. Everything else here stays as it is, and they are published here only where both sides are published without unpublished changes.

### Changed

- The dry run, its report and a node's diff (opened to pick fields) show which way the copy goes: this environment on the left and the remote on the right, as in the trees, with an arrow pointing at the side that is written to.
- The selection bar is always shown, also with nothing picked, so the trees no longer move down at the first tick.

### Fixed

- The buttons under a node's diff line up.

## [17.3.0] - 2026-10-28

### Added

- Copying from a remote to this environment. A To/From switch above the trees in the Content and Media views picks the way; tick nodes and media items in the remote's tree, and the dry run and the transfer work as they do the other way. The copy writes here as you, through Umbraco's own editing services, and needs your user's permissions on every node and media item it writes. Media files are downloaded from the remote's public media path. It is allowed in the Development and Local environments, elsewhere only when `Transfr:AllowPull` is true. Fields can still only be copied to the remote.
- The History view lists transfers both ways, and the dry run says which way an item was last copied.

### Changed

- A conflict now means that the side being written to changed since the last copy in either direction: after a copy to the remote and a later change there, copying it back here is an update as long as nothing changed here, and the other way round. The copy history keeps both sides' update times for this (a column is added to `transfrCopyHistory` on upgrade); copies recorded before count only for the direction they went.
- A remote installed separately is also recognised from its own side (a document type there whose alias is here under another key), so copying from it is refused as a whole too.

## [17.2.0] - 2026-10-21

### Added

- Media: the media a copied node uses are copied along, with the same keys: created with their folders when the remote lacks them, updated when they changed here, and skipped as a conflict (or overwritten, when chosen) when they changed there. A file is uploaded only when it is new or changed. The copy history records media too.
- A Media view: the media tree here next to the remote's, compared like the content tree. Media items (a folder with or without everything below it) can be picked there on their own; they join the same selection as the nodes. A media item opens on both sides, with its values and its file (images side by side), also from the History view.

### Changed

- The selection stays in view while the trees scroll, and stands out more.

## [17.1.0] - 2026-10-16

### Added

- Field-level transfer: in a node's diff, tick the names and properties that differ and add them to the selection. Only those fields are written to the remote (a block list or grid is one field); everything else there stays as it is. They are published only where both sides are published without unpublished changes, otherwise saved as a draft there, so nobody's draft goes live. A node ticked whole is copied whole.

### Changed

- The Transfr section comes after a site's own sections in the backoffice header.

## [17.0.0] - 2026-10-16

The major version now follows Umbraco's: 17.x is for Umbraco 17. This release follows 1.0.1 for the same Umbraco versions and changes nothing in how Transfr is installed or configured.

### Fixed

- A node created by a transfer is now put where the dry run placed it among its siblings, also when the remote has nodes of its own after that place. Before, it stayed last on the remote.

### Changed

- The README shows screenshots of the compare tree, the diff panel and the history, and explains the version numbers.

## [1.0.1] - 2026-10-15

### Fixed

- The schema check no longer reports a property's validation pattern as different when one environment has an empty pattern and the other has none. Umbraco treats both as no pattern, so nodes of those document types were blocked for no reason.

## [1.0.0] - 2026-10-15

First release, for Umbraco 17: compare the content of two environments side by side, see a node's differences property by property and block by block, check the schema behind every node, and copy nodes to a remote key-preserving after a dry run, with conflict detection and a history of every transfer.