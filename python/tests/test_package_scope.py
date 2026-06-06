from __future__ import annotations

import tomllib
from pathlib import Path


def test_python_binding_does_not_reimplement_os_memory_mapping() -> None:
    package_root = Path(__file__).resolve().parents[1] / "src" / "aikernel"
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
        text = path.read_text(encoding="utf-8")
        for token in forbidden:
            if token in text:
                offenders.append(f"{path.relative_to(package_root)}:{token}")

    assert offenders == []


def test_python_package_defaults_to_cuda_free_install() -> None:
    package_root = Path(__file__).resolve().parents[1]
    pyproject = tomllib.loads((package_root / "pyproject.toml").read_text(encoding="utf-8"))

    cmake_defines = pyproject["tool"]["scikit-build"]["cmake"]["define"]

    assert cmake_defines["AIKERNEL_PYTHON_BUILD_NATIVE"] == "OFF"
    assert cmake_defines["AIKERNEL_PYTHON_INCLUDE_CUDA_CAPABILITY"] == "OFF"
