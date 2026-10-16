# Changelog

From 17.0.0 on, Transfr's major version is the Umbraco major it supports: 17.x for Umbraco 17, 18.x for Umbraco 18. Each supported Umbraco major has its own branch that its versions are tagged on (`feature/17` for Umbraco 17). The 1.0.x releases are for Umbraco 17.

## [Unreleased]

### Added

- Field-level transfer: in a node's diff, tick the names and properties that differ and add them to the selection. Only those fields are written to the remote (a block list or grid is one field); everything else there stays as it is. They are published only where both sides are published without unpublished changes, otherwise saved as a draft there, so nobody's draft goes live. A node ticked whole is copied whole.

### Changed

- The Transfr section comes after a site's own sections in the backoffice header.

## [18.0.1] - 2026-10-16

### Changed

- Supports Umbraco 18.1.0 and later 18.x, instead of 18.2.1 and later. Measured on 18.1.0 like on 18.2.1.
- The README says that the remote must run such a version too: a remote on Umbraco 18.0.x can be compared but can't receive copies, because it lacks the endpoints that save and publish in one step.

## [18.0.0] - 2026-10-16

First release for Umbraco 18 (18.2.1 and later 18.x), with the same features as 17.0.0. Both environments must run Umbraco 18; for Umbraco 17, stay on the 17.x line.

### Changed

- The package's API description is now built with Umbraco 18's own OpenAPI support. For developers: the document is served at `/umbraco/openapi/transfr.json` instead of `/umbraco/swagger/transfr/swagger.json`.
- The README and the marketplace description cover both lines, including how to install the 17.x line on Umbraco 17.

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