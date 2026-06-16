from __future__ import annotations

import tomllib
from pathlib import Path

import aikernel_net


def test_python_binding_does_not_reimplement_os_memory_mapping() -> None:
    package_root = Path(__file__).resolve().parents[1] / "src" / "aikernel_net"
    forbidden = (
        "CreateFileMapping",
        "MapViewOfFile",
        "UnmapViewOfFile",
        "mmap",
        "munmap",
        "Win32MemoryMapper",
        "PosixMemoryMapper",
        "KernelContext",
    )

    offenders: list[str] = []
    for path in package_root.rglob("*.py"):
        if path.name == "api_catalog.py":
            continue
        text = path.read_text(encoding="utf-8")
        for token in forbidden:
            if token in text:
                offenders.append(f"{path.relative_to(package_root)}:{token}")

    assert offenders == []


def test_python_package_defaults_to_cuda_free_install() -> None:
    package_root = Path(__file__).resolve().parents[1]
    pyproject = tomllib.loads((package_root / "pyproject.toml").read_text(encoding="utf-8"))

    keywords = set(pyproject["project"]["keywords"])

    assert "cuda" not in keywords
    assert "libtorch" not in keywords
    assert "native-abi" not in keywords


def test_python_package_does_not_export_native_model_apis() -> None:
    forbidden = (
        "load_model",
        "load_model_result",
        "forward",
        "forward_result",
        "unload_model",
        "unload_model_result",
    )

    for name in forbidden:
        assert name not in aikernel_net.__all__
        assert not hasattr(aikernel_net, name)


def test_python_package_declares_apache_license_file() -> None:
    package_root = Path(__file__).resolve().parents[1]
    pyproject = tomllib.loads((package_root / "pyproject.toml").read_text(encoding="utf-8"))

    assert pyproject["project"]["license"] == "Apache-2.0"
    assert pyproject["project"]["license-files"] == ["LICENSE"]
    assert all(not item.startswith("License ::") for item in pyproject["project"]["classifiers"])
    assert (package_root / "LICENSE").is_file()


def test_python_package_builds_universal_cpu_only_wheel() -> None:
    package_root = Path(__file__).resolve().parents[1]
    pyproject = tomllib.loads((package_root / "pyproject.toml").read_text(encoding="utf-8"))
    wheel = pyproject["tool"]["scikit-build"]["wheel"]

    assert wheel["py-api"] == "py3"
    assert wheel["platlib"] is False
