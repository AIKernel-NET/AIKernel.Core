from __future__ import annotations

import pytest

from aikernel import Failure, Nothing, Option, Result, Some, Success, Try, do


def test_result_map_bind_success_path() -> None:
    result = Success(2).map(lambda value: value + 3).bind(lambda value: Success(value * 2))

    assert result.is_ok
    assert result.unwrap() == 10


def test_result_bind_short_circuits_failure() -> None:
    called = False

    def binder(value: int):
        nonlocal called
        called = True
        return Success(value)

    result = Failure("fail-closed").bind(binder)

    assert result.is_err
    assert result.error == "fail-closed"
    assert called is False


def test_result_captures_map_exception() -> None:
    result = Success(1).map(lambda _: int("not-an-int"))

    assert result.is_err
    assert isinstance(result.error, ValueError)


def test_try_converts_exception_to_failure() -> None:
    result = Try(lambda: int("not-an-int"))

    assert result.is_err
    assert isinstance(result.error, ValueError)


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


def test_do_result_short_circuits_failure() -> None:
    @do(Result)
    def pipeline():
        _ = yield Failure("stop")
        return "unreachable"

    result = pipeline()

    assert result.is_err
    assert result.error == "stop"


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
