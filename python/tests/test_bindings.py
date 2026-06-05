from __future__ import annotations

import os
from pathlib import Path

import pytest

import aikernel
from aikernel import bindings


def test_package_exports_version() -> None:
    assert aikernel.__version__ == "0.0.5"
    assert "Result" in aikernel.__all__
    assert "Try" in aikernel.__all__
    assert "load_model_result" in aikernel.__all__


def test_forward_rejects_empty_input_ids(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(bindings, "_native", lambda: object())

    with pytest.raises(ValueError, match="input_ids"):
        bindings.forward([], handle=1)


def test_forward_requires_active_handle(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(bindings, "_active_handle", None)

    with pytest.raises(ValueError, match="No active model handle"):
        bindings.forward([1, 2, 3])


def test_forward_result_wraps_failure(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(bindings, "_active_handle", None)

    result = bindings.forward_result([1, 2, 3])

    assert result.is_err
    assert isinstance(result.error, ValueError)
    assert result.metadata["capability.action"] == "forward"
    assert result.metadata["capability.hot_swap.status"] == "synchronous_native_abi"


def test_load_model_skips_when_native_bridge_missing(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv("AIKERNEL_LIBTORCH_BRIDGE_PATH", raising=False)

    if _native_bridge_exists():
        pytest.skip("Native bridge is present in this environment.")

    with pytest.raises(FileNotFoundError):
        bindings.load_model("model.pt")


def test_load_model_uses_explicit_native_bridge_path(monkeypatch: pytest.MonkeyPatch) -> None:
    path = Path(os.devnull)
    monkeypatch.setenv("AIKERNEL_LIBTORCH_BRIDGE_PATH", str(path))

    resolved = bindings._resolve_library_path()

    assert resolved == path


def test_active_handle_is_used_by_forward_and_cleared_on_unload(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    native = _FakeNative()
    monkeypatch.setattr(bindings, "_native", lambda: native)
    monkeypatch.setattr(bindings, "_active_handle", None)

    handle = bindings.load_model("model.pt")
    result = bindings.forward([10, 20])
    result_wrapped = bindings.forward_result([10, 20])
    bindings.unload_model(handle)

    assert handle == 7
    assert native.loaded_path == b"model.pt"
    assert native.forward_handle == 7
    assert native.forward_length == 2
    assert result.output_token_ids == (42,)
    assert result.logits == (0.25, 0.75)
    assert result_wrapped.is_ok
    assert result_wrapped.unwrap().output_token_ids == (42,)
    assert result_wrapped.metadata["capability.page_in.status"] == "not_observable_from_current_abi"
    assert bindings._active_handle is None


def test_load_and_unload_result_wrappers(monkeypatch: pytest.MonkeyPatch) -> None:
    native = _FakeNative()
    monkeypatch.setattr(bindings, "_native", lambda: native)
    monkeypatch.setattr(bindings, "_active_handle", None)

    loaded = bindings.load_model_result("model.pt")
    unloaded = bindings.unload_model_result(loaded.unwrap())

    assert loaded.is_ok
    assert loaded.metadata["capability.action"] == "load_model"
    assert loaded.metadata["capability.model_path"] == "model.pt"
    assert unloaded.is_ok
    assert unloaded.metadata["capability.action"] == "unload_model"
    assert bindings._active_handle is None


def _native_bridge_exists() -> bool:
    try:
        bindings._resolve_library_path()
    except FileNotFoundError:
        return False
    return True


class _FakeNative:
    loaded_path: bytes | None = None
    forward_handle: int | None = None
    forward_length: int | None = None

    def load_model(self, path: bytes) -> int:
        self.loaded_path = path
        return 7

    def unload_model(self, handle: int) -> int:
        return 0 if handle == 7 else -1

    def forward(self, handle, input_ids, length, out_result) -> int:
        self.forward_handle = int(handle)
        self.forward_length = int(length.value)
        result = out_result._obj
        result.status = 0
        result.output_token_count = 1
        result.output_token_ids[0] = 42
        result.logit_count = 2
        result.logits[0] = 0.25
        result.logits[1] = 0.75
        return 0
