from __future__ import annotations

import pytest

import aikernel
from aikernel import managed


def test_managed_assembly_manifest_is_stable() -> None:
    assemblies = managed.managed_assemblies()

    assert assemblies.root.name == "managed"
    assert tuple(path.name for path in assemblies.assemblies) == (
        "AIKernel.Common.dll",
        "AIKernel.Core.dll",
        "AIKernel.Kernel.dll",
        "AIKernel.Cuda.Libtorch.Cuda13.dll",
    )


def test_require_managed_assemblies_fails_closed_when_not_bundled() -> None:
    assemblies = managed.managed_assemblies()
    if assemblies.is_complete:
        pytest.skip("Managed assemblies are bundled in this environment.")

    with pytest.raises(FileNotFoundError, match="managed assemblies"):
        managed.require_managed_assemblies()


def test_managed_api_is_exported() -> None:
    assert "managed_assemblies" in aikernel.__all__
    assert "require_managed_assemblies" in aikernel.__all__
    assert aikernel.managed_assemblies().root.name == "managed"
