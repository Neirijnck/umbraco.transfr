# Changelog

From 17.0.0 on, Transfr's major version is the Umbraco major it supports: 17.x for Umbraco 17, 18.x for Umbraco 18. Each supported Umbraco major has its own branch that its versions are tagged on (`feature/17` for Umbraco 17). The 1.0.x releases are for Umbraco 17.

## [Unreleased]

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