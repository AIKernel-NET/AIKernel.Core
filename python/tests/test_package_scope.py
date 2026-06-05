from __future__ import annotations

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
