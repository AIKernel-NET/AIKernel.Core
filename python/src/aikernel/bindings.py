from __future__ import annotations

import ctypes
import os
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

from .monads import Result, Try


class AIKernelNativeError(RuntimeError):
    """Raised when the AIKernel Native ABI returns a fail-closed status."""


class _ForwardResultNative(ctypes.Structure):
    _fields_ = [
        ("status", ctypes.c_int32),
        ("output_token_count", ctypes.c_int32),
        ("output_token_ids", ctypes.c_int32 * 64),
        ("logit_count", ctypes.c_int32),
        ("logits", ctypes.c_float * 4096),
    ]


@dataclass(frozen=True)
class ForwardResult:
    status: int
    output_token_ids: tuple[int, ...]
    logits: tuple[float, ...]


_active_handle: int | None = None


def load_model(path: str | os.PathLike[str]) -> int:
    global _active_handle

    native = _native()
    encoded = os.fsencode(path)
    handle = native.load_model(encoded)
    if handle <= 0:
        raise AIKernelNativeError(f"load_model failed with status {handle}.")

    _active_handle = int(handle)
    return int(handle)


def load_model_result(path: str | os.PathLike[str]) -> Result[int]:
    return Try(
        lambda: load_model(path),
        metadata=_capability_feedback("load_model", model_path=os.fspath(path)),
    )


def unload_model(handle: int) -> None:
    global _active_handle

    native = _native()
    parsed_handle = _positive_int32(handle, "handle")
    status = native.unload_model(parsed_handle)
    if status != 0:
        raise AIKernelNativeError(f"unload_model failed with status {status}.")

    if _active_handle == parsed_handle:
        _active_handle = None


def unload_model_result(handle: int) -> Result[None]:
    return Try(
        lambda: unload_model(handle),
        metadata=_capability_feedback("unload_model", handle=handle),
    )


def forward(
    input_ids: Iterable[int],
    *,
    handle: int | None = None,
) -> ForwardResult:
    parsed_handle = _resolve_handle(handle)
    tokens = tuple(_int32(token, "input_ids") for token in input_ids)
    if not tokens:
        raise ValueError("input_ids must contain at least one token.")

    native = _native()
    input_array = (ctypes.c_int32 * len(tokens))(*tokens)
    result = _ForwardResultNative()
    status = native.forward(
        parsed_handle,
        input_array,
        ctypes.c_int32(len(tokens)),
        ctypes.byref(result),
    )
    if status != 0:
        raise AIKernelNativeError(f"forward failed with status {status}.")

    if result.status != 0:
        raise AIKernelNativeError(f"forward result failed with status {result.status}.")

    output_count = max(0, min(int(result.output_token_count), 64))
    logit_count = max(0, min(int(result.logit_count), 4096))

    return ForwardResult(
        status=int(result.status),
        output_token_ids=tuple(int(result.output_token_ids[i]) for i in range(output_count)),
        logits=tuple(float(result.logits[i]) for i in range(logit_count)),
    )


def forward_result(
    input_ids: Iterable[int],
    *,
    handle: int | None = None,
) -> Result[ForwardResult]:
    return Try(
        lambda: forward(input_ids, handle=handle),
        metadata=_capability_feedback("forward", handle=handle),
    )


def _capability_feedback(action: str, **details) -> dict[str, object]:
    metadata: dict[str, object] = {
        "capability.action": action,
        "capability.hot_swap.status": "synchronous_native_abi",
        "capability.page_in.status": "not_observable_from_current_abi",
        "capability.page_out.status": "not_observable_from_current_abi",
        "capability.defrag.status": "not_observable_from_current_abi",
        "capability.feedback.channel": "result.metadata",
    }
    for key, value in details.items():
        if value is not None:
            metadata[f"capability.{key}"] = value
    return metadata


def _native() -> ctypes.CDLL:
    cached = getattr(_native, "_cached", None)
    if cached is not None:
        return cached

    library_path = _resolve_library_path()
    _configure_windows_dll_search(library_path)
    native = ctypes.CDLL(str(library_path))
    native.load_model.argtypes = [ctypes.c_char_p]
    native.load_model.restype = ctypes.c_int32
    native.unload_model.argtypes = [ctypes.c_int32]
    native.unload_model.restype = ctypes.c_int32
    native.forward.argtypes = [
        ctypes.c_int32,
        ctypes.POINTER(ctypes.c_int32),
        ctypes.c_int32,
        ctypes.POINTER(_ForwardResultNative),
    ]
    native.forward.restype = ctypes.c_int32
    setattr(_native, "_cached", native)
    return native


def _configure_windows_dll_search(library_path: Path) -> None:
    if not sys.platform.startswith("win") or not hasattr(os, "add_dll_directory"):
        return

    handles = getattr(_native, "_dll_directory_handles", [])
    paths = getattr(_native, "_dll_directory_paths", set())

    for directory in _windows_dll_search_directories(library_path):
        resolved = str(directory.resolve())
        if resolved in paths:
            continue
        handles.append(os.add_dll_directory(resolved))
        paths.add(resolved)

    setattr(_native, "_dll_directory_handles", handles)
    setattr(_native, "_dll_directory_paths", paths)


def _windows_dll_search_directories(library_path: Path) -> tuple[Path, ...]:
    directories = [library_path.parent]

    libtorch_root = os.environ.get("AIKERNEL_LIBTORCH_PATH")
    if libtorch_root:
        root = Path(libtorch_root)
        directories.append(root / "lib" if (root / "lib").exists() else root)

    cuda_root = os.environ.get("CUDA_PATH")
    if cuda_root:
        root = Path(cuda_root)
        directories.append(root / "bin" if (root / "bin").exists() else root)

    unique: list[Path] = []
    seen: set[str] = set()
    for directory in directories:
        if not directory.exists():
            continue
        resolved = str(directory.resolve())
        if resolved in seen:
            continue
        seen.add(resolved)
        unique.append(directory)

    return tuple(unique)


def _resolve_library_path() -> Path:
    override = os.environ.get("AIKERNEL_LIBTORCH_BRIDGE_PATH")
    if override:
        path = Path(override)
        if path.is_dir():
            path = path / _library_name()
        if path.exists():
            return path
        raise FileNotFoundError(f"AIKernel native bridge was not found: {path}")

    candidates = [
        Path(__file__).resolve().parent / "native" / _library_name(),
        Path(__file__).resolve().parent.parent / "native" / _library_name(),
    ]
    for candidate in candidates:
        if candidate.exists():
            return candidate

    raise FileNotFoundError(
        "AIKernel native bridge was not found. Build the package with scikit-build-core "
        "or set AIKERNEL_LIBTORCH_BRIDGE_PATH."
    )


def _library_name() -> str:
    if sys.platform.startswith("win"):
        return "libtorch_bridge.dll"
    if sys.platform == "darwin":
        return "liblibtorch_bridge.dylib"
    return "liblibtorch_bridge.so"


def _positive_int32(value: int, name: str) -> int:
    parsed = _int32(value, name)
    if parsed <= 0:
        raise ValueError(f"{name} must be a positive int32.")
    return parsed


def _resolve_handle(handle: int | None) -> int:
    if handle is not None:
        return _positive_int32(handle, "handle")

    if _active_handle is None:
        raise ValueError("No active model handle. Call load_model(path) first or pass handle=.")

    return _active_handle


def _int32(value: int, name: str) -> int:
    parsed = int(value)
    if parsed < -(2**31) or parsed > 2**31 - 1:
        raise ValueError(f"{name} must fit in int32.")
    return parsed
