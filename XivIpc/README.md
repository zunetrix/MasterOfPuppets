# XivIpc

`XivIpc` contains the Unix-side implementation details behind the public shim. It owns the sidecar protocol, broker lifecycle, shared-file ring buffers, and the shared-file `TinyMemoryMappedFile` implementation.

## What It Is For

This project provides the transport and storage primitives that make the Linux, Wine, and Proton runtime path work.

- Broker-backed message delivery over a Unix socket control plane plus shared-file rings.
- Shared-file memory-mapped-file semantics for named payload storage.
- Runtime and filesystem helpers for shared directories, permissions, logging, and process stamps.

## How It Works

- `UnixSidecarTinyMessageBus` connects to the native broker, attaches to a channel ring, queues outbound messages, and reconnects in the background if the broker disappears.
- `UnixSidecarProcessManager` discovers or launches `XivIpc.NativeHost`, enforces the single-broker-per-directory rule, and waits for a live socket plus state file before handing the lease to callers.
- `BrokeredChannelRing` and related types manage the shared-file ring layout used to move payloads between the broker and clients.
- `UnixTinyMemoryMappedFile` implements named file-backed shared storage with a small on-disk header, process-local locking, and OS file locking.
- `UnixSharedStorageHelpers` centralizes path resolution, permission handling, and runtime-specific path normalization.

## Main Entry Points

- `Messaging/UnixSidecarTinyMessageBus.cs`
- `Messaging/UnixSidecarProcessManager.cs`
- `IO/UnixTinyMemoryMappedFile.cs`
- `Messaging/BrokeredChannelRing.cs`
