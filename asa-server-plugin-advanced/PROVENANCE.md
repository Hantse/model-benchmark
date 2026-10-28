# Provenance and limits

This is an independently written successor to the offline `asa-server-plugin`
exercise. It inherits that exercise's upstream context pinned to AsaApi revision
`13cc5e5cf8dfbe7767b974dbe4ba8d97e054d32d`, reviewed on 26 October 2026.
No additional upstream SDK or binary qualification is claimed for this version.

Inherited primary reference pointers:

- [AsaApi README](https://github.com/ArkServerApi/AsaApi/blob/13cc5e5cf8dfbe7767b974dbe4ba8d97e054d32d/README.md)
- [Command interface](https://github.com/ArkServerApi/AsaApi/blob/13cc5e5cf8dfbe7767b974dbe4ba8d97e054d32d/AsaApi/Core/Public/ICommands.h)
- [Plugin loader](https://github.com/ArkServerApi/AsaApi/blob/13cc5e5cf8dfbe7767b974dbe4ba8d97e054d32d/AsaApi/Core/Private/PluginManager/PluginManager.cpp)

The header deliberately defines an ordinary C++ Host, Clock, EOS player value,
opaque ownership handles, award idempotency keys and outcome queries. These are
synthetic test contracts, not AsaApi SDK types, ABI declarations or claims about
a built-in economy/permission service. No external source code, SDK headers,
game assets, binaries or real player information are bundled.

Passing tests qualify only this core against its 108 visible cases. They do not
establish native DLL loading, SDK compatibility, game-thread safety, persistence
across an actual server crash or production exactly-once rewards. A real plugin
would require a separately authorized SDK adapter, Windows build, proper exports
and qualification on a chosen ASA test server. A DevKit UGC mod is a different
target. All operations in this benchmark are fake and local.
