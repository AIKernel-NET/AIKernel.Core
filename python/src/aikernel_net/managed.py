"""[EN]
Reference module for aikernel_net.managed.

[JA]
aikernel_net.managed の参照モジュールです。
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path


_PACKAGE_VERSION = "0.1.0.1"
_MANAGED_ASSEMBLIES = (
    "AIKernel.Abstractions.dll",
    "AIKernel.Common.dll",
    "AIKernel.Core.dll",
    "AIKernel.Kernel.dll",
    "AIKernel.Dtos.dll",
    "AIKernel.Enums.dll",
)
_ASSEMBLY_PACKAGES = {
    "AIKernel.Abstractions.dll": "AIKernel.Abstractions",
    "AIKernel.Common.dll": "AIKernel.Common",
    "AIKernel.Core.dll": "AIKernel.Core",
    "AIKernel.Kernel.dll": "AIKernel.Kernel",
    "AIKernel.Dtos.dll": "AIKernel.Dtos",
    "AIKernel.Enums.dll": "AIKernel.Enums",
}


@dataclass(frozen=True)
class ManagedAssemblySet:
    """[EN]
    Represents the ManagedAssemblySet public Python API surface.
    
    [JA]
    ManagedAssemblySet の公開 Python API サーフェスを表します。
    """
    root: Path
    assemblies: tuple[Path, ...]

    @property
    def is_complete(self) -> bool:
        """[EN]
        Executes the is complete operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        is complete 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return all(path.exists() for path in self.assemblies)

    @property
    def missing(self) -> tuple[str, ...]:
        """[EN]
        Executes the missing operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        missing 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return tuple(path.name for path in self.assemblies if not path.exists())

    @property
    def dlls(self) -> tuple[Path, ...]:
        """[EN]
        Executes the dlls operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        dlls 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        roots = {
            self.root,
            *(path.parent for path in self.assemblies),
        }
        return tuple(sorted({path for root in roots for path in root.glob("*.dll")}))

    @property
    def dependency_manifests(self) -> tuple[Path, ...]:
        """[EN]
        Executes the dependency manifests operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        dependency manifests 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        roots = {
            self.root,
            *(path.parent for path in self.assemblies),
        }
        return tuple(sorted({path for root in roots for path in root.glob("*.deps.json")}))


@dataclass(frozen=True)
class RuntimeLayout:
    """[EN]
    Represents the RuntimeLayout public Python API surface.
    
    [JA]
    RuntimeLayout の公開 Python API サーフェスを表します。
    """
    managed: ManagedAssemblySet
    native_root: Path

    @property
    def native_libraries(self) -> tuple[Path, ...]:
        """[EN]
        Executes the native libraries operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        native libraries 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        patterns = ("*.dll", "*.so", "*.dylib")
        return tuple(sorted(path for pattern in patterns for path in self.native_root.glob(pattern)))


def managed_assemblies() -> ManagedAssemblySet:
    """[EN]
    Executes the managed assemblies operation.
    Returns:
        Result produced by the operation.
    
    [JA]
    managed assemblies 操作を実行します。
    戻り値:
        操作によって生成される結果です。
    """
    root = _managed_package_root()
    return ManagedAssemblySet(
        root=root,
        assemblies=tuple(_resolve_assembly(name) for name in _MANAGED_ASSEMBLIES),
    )


def runtime_layout() -> RuntimeLayout:
    """[EN]
    Executes the runtime layout operation.
    Returns:
        Result produced by the operation.
    
    [JA]
    runtime layout 操作を実行します。
    戻り値:
        操作によって生成される結果です。
    """
    package_root = Path(__file__).resolve().parent
    return RuntimeLayout(
        managed=managed_assemblies(),
        native_root=package_root / "native",
    )


def require_managed_assemblies() -> ManagedAssemblySet:
    """[EN]
    Executes the require managed assemblies operation.
    Returns:
        Result produced by the operation.
    
    [JA]
    require managed assemblies 操作を実行します。
    戻り値:
        操作によって生成される結果です。
    """
    assemblies = managed_assemblies()
    if not assemblies.is_complete:
        missing = ", ".join(assemblies.missing)
        raise FileNotFoundError(
            "AIKernel managed assemblies are not bundled in this Python package: "
            f"{missing}. Build with AIKERNEL_PYTHON_INCLUDE_MANAGED=ON or use the "
            "repository .NET packages directly."
        )

    return assemblies


def _resolve_assembly(name: str) -> Path:
    for root in _managed_roots():
        candidate = root / name
        if candidate.exists():
            return candidate

    nuget_candidate = _resolve_nuget_assembly(name)
    if nuget_candidate is not None:
        return nuget_candidate

    return _managed_package_root() / name


def _managed_roots() -> tuple[Path, ...]:
    roots: list[Path] = [_managed_package_root()]
    override = os.environ.get("AIKERNEL_MANAGED_ASSEMBLY_PATH")
    if override:
        roots.extend(Path(path) for path in override.split(os.pathsep) if path)
    return tuple(roots)


def _managed_package_root() -> Path:
    return Path(__file__).resolve().parent / "managed"


def _resolve_nuget_assembly(name: str) -> Path | None:
    package = _ASSEMBLY_PACKAGES[name]
    package_root = _nuget_root() / package.lower() / _PACKAGE_VERSION / "lib" / "net10.0"
    candidate = package_root / name
    if candidate.exists():
        return candidate
    return None


def _nuget_root() -> Path:
    configured = os.environ.get("NUGET_PACKAGES")
    if configured:
        return Path(configured)
    return Path.home() / ".nuget" / "packages"
