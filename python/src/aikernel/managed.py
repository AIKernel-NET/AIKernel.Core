from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path


_MANAGED_ASSEMBLIES = (
    "AIKernel.Abstractions.dll",
    "AIKernel.Common.dll",
    "AIKernel.Core.dll",
    "AIKernel.Kernel.dll",
    "AIKernel.Cuda.Libtorch.Cuda13.dll",
    "AIKernel.Dtos.dll",
    "AIKernel.Enums.dll",
)


@dataclass(frozen=True)
class ManagedAssemblySet:
    root: Path
    assemblies: tuple[Path, ...]

    @property
    def is_complete(self) -> bool:
        return all(path.exists() for path in self.assemblies)

    @property
    def missing(self) -> tuple[str, ...]:
        return tuple(path.name for path in self.assemblies if not path.exists())

    @property
    def dlls(self) -> tuple[Path, ...]:
        return tuple(sorted(self.root.glob("*.dll")))

    @property
    def dependency_manifests(self) -> tuple[Path, ...]:
        return tuple(sorted(self.root.glob("*.deps.json")))


@dataclass(frozen=True)
class RuntimeLayout:
    managed: ManagedAssemblySet
    native_root: Path

    @property
    def native_libraries(self) -> tuple[Path, ...]:
        patterns = ("*.dll", "*.so", "*.dylib")
        return tuple(sorted(path for pattern in patterns for path in self.native_root.glob(pattern)))


def managed_assemblies() -> ManagedAssemblySet:
    root = Path(__file__).resolve().parent / "managed"
    return ManagedAssemblySet(
        root=root,
        assemblies=tuple(root / name for name in _MANAGED_ASSEMBLIES),
    )


def runtime_layout() -> RuntimeLayout:
    package_root = Path(__file__).resolve().parent
    return RuntimeLayout(
        managed=managed_assemblies(),
        native_root=package_root / "native",
    )


def require_managed_assemblies() -> ManagedAssemblySet:
    assemblies = managed_assemblies()
    if not assemblies.is_complete:
        missing = ", ".join(assemblies.missing)
        raise FileNotFoundError(
            "AIKernel managed assemblies are not bundled in this Python package: "
            f"{missing}. Build with AIKERNEL_PYTHON_INCLUDE_MANAGED=ON or use the "
            "repository .NET packages directly."
        )

    return assemblies
