from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path


_PACKAGE_VERSION = "0.0.5"
_MANAGED_ASSEMBLIES = (
    "AIKernel.Abstractions.dll",
    "AIKernel.Common.dll",
    "AIKernel.Core.dll",
    "AIKernel.Kernel.dll",
    "AIKernel.Dtos.dll",
    "AIKernel.Enums.dll",
)
_OPTIONAL_MANAGED_ASSEMBLIES = (
    "AIKernel.Cuda.Libtorch.Cuda13.dll",
)
_ASSEMBLY_PACKAGES = {
    "AIKernel.Abstractions.dll": "AIKernel.Abstractions",
    "AIKernel.Common.dll": "AIKernel.Common",
    "AIKernel.Core.dll": "AIKernel.Core",
    "AIKernel.Kernel.dll": "AIKernel.Kernel",
    "AIKernel.Cuda.Libtorch.Cuda13.dll": "AIKernel.Cuda.Libtorch.2.12-cuda13.0",
    "AIKernel.Dtos.dll": "AIKernel.Dtos",
    "AIKernel.Enums.dll": "AIKernel.Enums",
}


@dataclass(frozen=True)
class ManagedAssemblySet:
    root: Path
    assemblies: tuple[Path, ...]
    optional_assemblies: tuple[Path, ...] = ()

    @property
    def is_complete(self) -> bool:
        return all(path.exists() for path in self.assemblies)

    @property
    def missing(self) -> tuple[str, ...]:
        return tuple(path.name for path in self.assemblies if not path.exists())

    @property
    def dlls(self) -> tuple[Path, ...]:
        roots = {
            self.root,
            *(path.parent for path in self.assemblies),
            *(path.parent for path in self.optional_assemblies),
        }
        return tuple(sorted({path for root in roots for path in root.glob("*.dll")}))

    @property
    def dependency_manifests(self) -> tuple[Path, ...]:
        roots = {
            self.root,
            *(path.parent for path in self.assemblies),
            *(path.parent for path in self.optional_assemblies),
        }
        return tuple(sorted({path for root in roots for path in root.glob("*.deps.json")}))


@dataclass(frozen=True)
class RuntimeLayout:
    managed: ManagedAssemblySet
    native_root: Path

    @property
    def native_libraries(self) -> tuple[Path, ...]:
        patterns = ("*.dll", "*.so", "*.dylib")
        return tuple(sorted(path for pattern in patterns for path in self.native_root.glob(pattern)))


def managed_assemblies() -> ManagedAssemblySet:
    root = _managed_package_root()
    return ManagedAssemblySet(
        root=root,
        assemblies=tuple(_resolve_assembly(name) for name in _MANAGED_ASSEMBLIES),
        optional_assemblies=tuple(
            _resolve_assembly(name)
            for name in _OPTIONAL_MANAGED_ASSEMBLIES
        ),
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
