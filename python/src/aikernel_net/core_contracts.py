"""[EN]
Python descriptors for Core-owned storage and VFS capability contracts.

[JA]
Core 所有 storage / VFS capability contract の Python descriptor です。
"""

from __future__ import annotations

from dataclasses import dataclass, field


@dataclass(frozen=True)
class CoreCapabilityModuleContract:
    """[EN]
    Describes a Core-owned capability module contract without executing it.

    [JA]
    実行せずに Core 所有 capability module contract を表します。
    """

    capability_id: str
    name: str
    kind: str
    invocation_mode: str
    version: str
    entry_point: str
    operations: tuple[str, ...]
    permissions: tuple[str, ...]
    metadata: dict[str, str] = field(default_factory=dict)


def rom_storage_contract(
    capability_id: str,
    storage_scheme: str,
    metadata: dict[str, str] | None = None,
) -> CoreCapabilityModuleContract:
    """[EN]
    Creates the ROM storage capability contract descriptor.

    [JA]
    ROM storage capability contract descriptor を作成します。
    """

    values = dict(sorted((metadata or {}).items()))
    values["storageScheme"] = storage_scheme
    return CoreCapabilityModuleContract(
        capability_id=capability_id,
        name="ROM Storage",
        kind="ManagedAssembly",
        invocation_mode="AssemblyReference",
        version=values.get("version", "0.1.1"),
        entry_point="AIKernel.Core.Storage",
        operations=("rom.save", "rom.load", "rom.list"),
        permissions=("rom.read", "rom.write"),
        metadata=values,
    )


def vfs_git_contract(
    capability_id: str,
    repository_mode: str,
    metadata: dict[str, str] | None = None,
) -> CoreCapabilityModuleContract:
    """[EN]
    Creates the VFS Git capability contract descriptor.

    [JA]
    VFS Git capability contract descriptor を作成します。
    """

    values = dict(sorted((metadata or {}).items()))
    values["repositoryMode"] = repository_mode
    return CoreCapabilityModuleContract(
        capability_id=capability_id,
        name="VFS Git",
        kind="ManagedAssembly",
        invocation_mode="AssemblyReference",
        version=values.get("version", "0.1.1"),
        entry_point="AIKernel.Core.Vfs.VfsGit",
        operations=("vfs.git.read", "vfs.git.list", "vfs.git.checkout"),
        permissions=("git.read", "vfs.read"),
        metadata=values,
    )
