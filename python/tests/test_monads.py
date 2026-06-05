from __future__ import annotations

import pytest

from aikernel import Failure, Nothing, Option, Result, Some, Success, Try, do


def test_result_map_bind_success_path() -> None:
    result = Success(2).map(lambda value: value + 3).bind(lambda value: Success(value * 2))

    assert result.is_ok
    assert result.unwrap() == 10


def test_result_metadata_flows_through_bind() -> None:
    result = Success(2, metadata={"step": "load"}).bind(
        lambda value: Success(value + 1, metadata={"next": "forward"})
    )

    assert result.is_ok
    assert result.unwrap() == 3
    assert result.metadata == {"step": "load", "next": "forward"}


def test_result_bind_short_circuits_failure() -> None:
    called = False

    def binder(value: int):
        nonlocal called
        called = True
        return Success(value)

    result = Failure("fail-closed", metadata={"failure_kind": "fail_closed"}).bind(binder)

    assert result.is_err
    assert result.error == "fail-closed"
    assert result.metadata == {"failure_kind": "fail_closed"}
    assert called is False


def test_result_captures_map_exception() -> None:
    result = Success(1).map(lambda _: int("not-an-int"))

    assert result.is_err
    assert isinstance(result.error, ValueError)


def test_result_bind_rejects_non_result_return() -> None:
    result = Success(1).bind(lambda _: "not-a-result")

    assert result.is_err
    assert isinstance(result.error, TypeError)


def test_try_converts_exception_to_failure() -> None:
    result = Try(lambda: int("not-an-int"))

    assert result.is_err
    assert isinstance(result.error, ValueError)


def test_try_run_matches_callable_form() -> None:
    callable_result = Try(lambda: 41 + 1)
    run_result = Try.run(lambda: 41 + 1)

    assert callable_result.is_ok
    assert run_result.is_ok
    assert callable_result.unwrap() == run_result.unwrap() == 42


def test_option_map_bind_success_path() -> None:
    option = Some(3).map(lambda value: value + 1).bind(lambda value: Some(value * 2))

    assert option.is_some
    assert option.unwrap() == 8


def test_option_bind_short_circuits_none() -> None:
    called = False

    def binder(value: int):
        nonlocal called
        called = True
        return Some(value)

    option = Nothing().bind(binder)

    assert option.is_none
    assert called is False


def test_option_propagates_exceptions() -> None:
    with pytest.raises(ValueError):
        Some(1).map(lambda _: int("not-an-int"))


def test_do_result_success_path() -> None:
    @do(Result)
    def pipeline():
        first = yield Success(2)
        second = yield Try(lambda: first + 3)
        return second * 2

    result = pipeline()

    assert result.is_ok
    assert result.unwrap() == 10


def test_do_result_preserves_metadata() -> None:
    @do(Result)
    def pipeline():
        first = yield Success(2, metadata={"step": "load"})
        second = yield Try(lambda: first + 3, metadata={"next": "forward"})
        return second

    result = pipeline()

    assert result.is_ok
    assert result.unwrap() == 5
    assert result.metadata == {"step": "load", "next": "forward"}


def test_do_result_short_circuits_failure() -> None:
    @do(Result)
    def pipeline():
        _ = yield Failure("stop")
        return "unreachable"

    result = pipeline()

    assert result.is_err
    assert result.error == "stop"


def test_do_result_short_circuit_preserves_metadata() -> None:
    @do(Result)
    def pipeline():
        _ = yield Success("started", metadata={"step": "load"})
        _ = yield Failure("stop", metadata={"failure_kind": "fail_closed"})
        return "unreachable"

    result = pipeline()

    assert result.is_err
    assert result.error == "stop"
    assert result.metadata == {"step": "load", "failure_kind": "fail_closed"}


def test_do_result_rejects_non_result_yield() -> None:
    @do(Result)
    def pipeline():
        _ = yield Some("wrong-monad")
        return "unreachable"

    result = pipeline()

    assert result.is_err
    assert isinstance(result.error, TypeError)


def test_do_option_success_and_none_paths() -> None:
    @do(Option)
    def success_pipeline():
        first = yield Some(2)
        second = yield Some(first + 3)
        return second

    @do(Option)
    def none_pipeline():
        _ = yield Nothing()
        return "unreachable"

    assert success_pipeline().unwrap() == 5
    assert none_pipeline().is_none


def test_do_option_rejects_non_option_yield() -> None:
    @do(Option)
    def pipeline():
        _ = yield Success("wrong-monad")
        return "unreachable"

    with pytest.raises(TypeError):
        pipeline()
