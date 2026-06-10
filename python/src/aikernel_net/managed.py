"""[EN]
Managed assembly and runtime layout discovery helpers for aikernel-net.

[JA]
aikernel-net の managed assembly / runtime layout discovery helper です。
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path


_CORE_PACKAGE_VERSION = "0.1.1"
_CONTRACT_PACKAGE_VERSION = "0.1.1"
_MANAGED_ASSEMBLIES = (
    "AIKernel.Abstractions.dll",
    "AIKernel.Common.dll",
    "AIKernel.Core.dll",
    "AIKernel.Kernel.dll",
    "AIKernel.Dtos.dll",
    "AIKernel.Enums.dll",
)
_ASSEMBLY_PACKAGES = {
    "AIKernel.Abstractions.dll": ("AIKernel.Abstractions", _CONTRACT_PACKAGE_VERSION),
    "AIKernel.Common.dll": ("AIKernel.Common", _CORE_PACKAGE_VERSION),
    "AIKernel.Core.dll": ("AIKernel.Core", _CORE_PACKAGE_VERSION),
    "AIKernel.Kernel.dll": ("AIKernel.Kernel", _CORE_PACKAGE_VERSION),
    "AIKernel.Dtos.dll": ("AIKernel.Dtos", _CONTRACT_PACKAGE_VERSION),
    "AIKernel.Enums.dll": ("AIKernel.Enums", _CONTRACT_PACKAGE_VERSION),
}


@dataclass(frozen=True)
class ManagedAssemblySet:
    """[EN]
    Describes resolved managed AIKernel assemblies and their root directory.
    
    [JA]
    解決済み managed AIKernel assembly と root directory を表します。
    """
    root: Path
    assemblies: tuple[Path, ...]

    @property
    def is_complete(self) -> bool:
        """[EN]
        Returns whether every required managed assembly exists.
        
        [JA]
        必須 managed assembly がすべて存在するかを返します。
        """
        return all(path.exists() for path in self.assemblies)

    @property
    def missing(self) -> tuple[str, ...]:
        """[EN]
        Returns the file names of required managed assemblies that are missing.
        
        [JA]
        不足している必須 managed assembly の file name を返します。
        """
        return tuple(path.name for path in self.assemblies if not path.exists())

    @property
    def dlls(self) -> tuple[Path, ...]:
        """[EN]
        Returns DLL files found beside the resolved managed assemblies.
        
        [JA]
        解決済み managed assembly の近くにある DLL file を返します。
        """
        roots = {
            self.root,
            *(path.parent for path in self.assemblies),
        }
        return tuple(sorted({path for root in roots for path in root.glob("*.dll")}))

    @property
    def dependency_manifests(self) -> tuple[Path, ...]:
        """[EN]
        Returns .deps.json manifests found beside the resolved assemblies.
        
        [JA]
        解決済み assembly の近くにある .deps.json manifest を返します。
        """
        roots = {
            self.root,
            *(path.parent for path in self.assemblies),
        }
        return tuple(sorted({path for root in roots for path in root.glob("*.deps.json")}))


@dataclass(frozen=True)
class RuntimeLayout:
    """[EN]
    Describes managed and native runtime locations used by the Python binding.
    
    [JA]
    Python binding が利用する managed / native runtime location を表します。
    """
    managed: ManagedAssemblySet
    native_root: Path

    @property
    def native_libraries(self) -> tuple[Path, ...]:
        """[EN]
        Returns native libraries located under the package native directory.
        
        [JA]
        package native directory 配下の native library を返します。
        """
        patterns = ("*.dll", "*.so", "*.dylib")
        return tuple(sorted(path for pattern in patterns for path in self.native_root.glob(pattern)))


def managed_assemblies() -> ManagedAssemblySet:
    """[EN]
    Resolves required AIKernel managed assemblies from bundle, environment, or NuGet cache.
    
    [JA]
    bundle、environment、NuGet cache から必須 AIKernel managed assembly を解決します。
    """
    root = _managed_package_root()
    return ManagedAssemblySet(
        root=root,
        assemblies=tuple(_resolve_assembly(name) for name in _MANAGED_ASSEMBLIES),
    )


def runtime_layout() -> RuntimeLayout:
    """[EN]
    Returns the resolved runtime layout for managed and native assets.
    
    [JA]
    managed / native asset の解決済み runtime layout を返します。
    """
    package_root = Path(__file__).resolve().parent
    return RuntimeLayout(
        managed=managed_assemblies(),
        native_root=package_root / "native",
    )


def require_managed_assemblies() -> ManagedAssemblySet:
    """[EN]
    Resolves managed assemblies and fails closed when any required assembly is missing.
    
    [JA]
    managed assembly を解決し、必須 assembly が不足する場合は fail-closed します。
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
    package, version = _ASSEMBLY_PACKAGES[name]
    package_root = _nuget_root() / package.lower() / version / "lib" / "net10.0"
    candidate = package_root / name
    if candidate.exists():
        return candidate
    return None


def _nuget_root() -> Path:
    configured = os.environ.get("NUGET_PACKAGES")
    if configured:
        return Path(configured)
    return Path.home() / ".nuget" / "packages"
