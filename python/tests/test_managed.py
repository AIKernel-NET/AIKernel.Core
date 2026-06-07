from __future__ import annotations

import pytest
from importlib import resources

import aikernel_net
from aikernel_net import managed


def test_managed_assembly_manifest_is_stable() -> None:
    assemblies = managed.managed_assemblies()

    assert assemblies.root.name == "managed"
    assert tuple(path.name for path in assemblies.assemblies) == (
        "AIKernel.Abstractions.dll",
        "AIKernel.Common.dll",
        "AIKernel.Core.dll",
        "AIKernel.Kernel.dll",
        "AIKernel.Dtos.dll",
        "AIKernel.Enums.dll",
    )


def test_require_managed_assemblies_fails_closed_when_not_bundled() -> None:
    assemblies = managed.managed_assemblies()
    if assemblies.is_complete:
        pytest.skip("Managed assemblies are bundled in this environment.")

    with pytest.raises(FileNotFoundError, match="managed assemblies"):
        managed.require_managed_assemblies()


def test_managed_runtime_file_lists_are_safe_when_empty() -> None:
    assemblies = managed.managed_assemblies()

    assert isinstance(assemblies.dlls, tuple)
    assert isinstance(assemblies.dependency_manifests, tuple)


def test_managed_assemblies_resolve_from_env_override(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path,
) -> None:
    _write_required_assemblies(tmp_path)
    monkeypatch.setenv("AIKERNEL_MANAGED_ASSEMBLY_PATH", str(tmp_path))
    monkeypatch.setenv("NUGET_PACKAGES", str(tmp_path / "empty-nuget"))

    assemblies = managed.managed_assemblies()

    assert assemblies.is_complete
    assert all(path.parent == tmp_path for path in assemblies.assemblies)


def test_managed_assemblies_resolve_from_nuget_cache(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path,
) -> None:
    nuget_root = tmp_path / "nuget"
    for assembly_name, package_name in {
        "AIKernel.Abstractions.dll": "AIKernel.Abstractions",
        "AIKernel.Common.dll": "AIKernel.Common",
        "AIKernel.Core.dll": "AIKernel.Core",
        "AIKernel.Kernel.dll": "AIKernel.Kernel",
        "AIKernel.Dtos.dll": "AIKernel.Dtos",
        "AIKernel.Enums.dll": "AIKernel.Enums",
    }.items():
        assembly_path = nuget_root / package_name.lower() / "0.1.0" / "lib" / "net10.0" / assembly_name
        assembly_path.parent.mkdir(parents=True, exist_ok=True)
        assembly_path.write_text("", encoding="utf-8")

    monkeypatch.setenv("NUGET_PACKAGES", str(nuget_root))

    assemblies = managed.managed_assemblies()

    assert assemblies.is_complete
    assert all(str(path).startswith(str(nuget_root)) for path in assemblies.assemblies)


def test_runtime_layout_reports_managed_and_native_roots() -> None:
    layout = managed.runtime_layout()

    assert layout.managed.root.name == "managed"
    assert layout.native_root.name == "native"
    assert isinstance(layout.native_libraries, tuple)


def test_managed_api_is_exported() -> None:
    assert "managed_assemblies" in aikernel_net.__all__
    assert "require_managed_assemblies" in aikernel_net.__all__
    assert "runtime_layout" in aikernel_net.__all__
    assert aikernel_net.managed_assemblies().root.name == "managed"


def test_package_declares_inline_types() -> None:
    marker = resources.files("aikernel_net").joinpath("py.typed")

    assert marker.is_file()


def _write_required_assemblies(root) -> None:
    for assembly_name in (
        "AIKernel.Abstractions.dll",
        "AIKernel.Common.dll",
        "AIKernel.Core.dll",
        "AIKernel.Kernel.dll",
        "AIKernel.Dtos.dll",
        "AIKernel.Enums.dll",
    ):
        (root / assembly_name).write_text("", encoding="utf-8")
