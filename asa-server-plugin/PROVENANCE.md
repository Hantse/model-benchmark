# Upstream context and scope

Reviewed on 2026-10-26. Upstream AsaApi revision:
`13cc5e5cf8dfbe7767b974dbe4ba8d97e054d32d`.

AsaApi is the community C++ server API for ARK: Survival Ascended. Its upstream
README describes it as an extension of the distributed server executable and
specifies a Windows MSVC toolset for compiling the API. Its plugin loader locates
native DLLs and calls the `Plugin_Init` and `Plugin_Unload` exports. Its commands
interface provides chat registration/removal and timer registration/removal.

Primary references pinned to that revision:

- [AsaApi README](https://github.com/ArkServerApi/AsaApi/blob/13cc5e5cf8dfbe7767b974dbe4ba8d97e054d32d/README.md)
- [ICommands.h](https://github.com/ArkServerApi/AsaApi/blob/13cc5e5cf8dfbe7767b974dbe4ba8d97e054d32d/AsaApi/Core/Public/ICommands.h)
- [PluginManager.cpp](https://github.com/ArkServerApi/AsaApi/blob/13cc5e5cf8dfbe7767b974dbe4ba8d97e054d32d/AsaApi/Core/Private/PluginManager/PluginManager.cpp)

In contrast, the official [ARK DevKit Creating New Mods guide](https://devkit.studiowildcard.com/getting-started/creating-new-mods)
describes ASA UGC mods as plugins managed through the editor's UGC menu, including
cooking and publication. That use of the word plugin does not make a native
AsaApi DLL interchangeable with a DevKit mod.

This exercise contains independently written benchmark code. No SDK headers,
server binaries, game assets, loader implementation or external template source
are bundled. Its Host and Player declarations deliberately use ordinary standard
C++ types rather than Unreal's `FString` or `AShooterPlayerController`. The credits
and permission calls are synthetic exercise ports, not claims about built-in
AsaApi economy or permission APIs. Its Load/Unload methods test core lifecycle
behavior; they are not DLL export names or a full loader implementation.

A real plugin would still require a separate Windows build against a chosen
AsaApi SDK revision, an adapter, correct exports/metadata, and qualification on
an authorized test ASA server. The offline score does not establish DLL loading,
ABI compatibility, game-thread safety, server performance or production behavior.
