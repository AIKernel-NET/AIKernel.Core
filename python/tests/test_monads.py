from __future__ import annotations

import pytest

import asyncio

from aikernel import (
    Either,
    Failure,
    Left,
    Nothing,
    Option,
    Result,
    Right,
    Some,
    Success,
    Try,
    async_do,
    async_either,
    async_option,
    async_result,
    do,
)


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


def test_result_linq_aliases_compose_and_capture_failures() -> None:
    observed: list[int] = []

    result = (
        Success(2)
        .select(lambda value: value + 1)
        .select_many(lambda value: Success(value * 2), lambda left, right: left + right)
        .tap(lambda value: observed.append(value))
        .where(lambda value: value == 9)
    )
    failed = result.where(lambda value: value < 0)
    exception = Success(1).tap(lambda _: int("not-an-int"))

    assert result.is_ok
    assert result.unwrap() == 9
    assert observed == [9]
    assert failed.is_err
    assert isinstance(failed.error, ValueError)
    assert exception.is_err
    assert isinstance(exception.error, ValueError)
    assert result.match(lambda value: f"ok:{value}", lambda error: f"err:{error}") == "ok:9"


def test_result_csharp_linq_aliases_are_available() -> None:
    result = (
        Success(2)
        .Select(lambda value: value + 1)
        .SelectMany(lambda value: Success(value * 2), lambda left, right: left + right)
        .Tap(lambda _: None)
        .Where(lambda value: value == 9)
    )

    assert result.is_ok
    assert result.Match(lambda value: value, lambda _: 0) == 9


def test_async_result_linq_aliases_compose_and_capture_exceptions() -> None:
    observed: list[int] = []

    async def start():
        return Success(2, metadata={"step": "start"})

    async def next_value(value: int):
        return Success(value * 2, metadata={"step2": "next"})

    async def observe(value: int) -> None:
        observed.append(value)

    result = _run_async(
        async_result(start())
        .Select(lambda value: value + 1)
        .SelectMany(next_value, lambda left, right: left + right)
        .Tap(observe)
        .Where(lambda value: value == 9)
    )
    exception = _run_async(async_result(Success(1)).Tap(lambda _: int("not-an-int")))

    assert result.is_ok
    assert result.unwrap() == 9
    assert result.metadata == {"step": "start", "step2": "next"}
    assert observed == [9]
    assert exception.is_err
    assert isinstance(exception.error, ValueError)


def test_async_result_short_circuits_and_rejects_non_result() -> None:
    called = False

    async def binder(value: int):
        nonlocal called
        called = True
        return Success(value)

    short = _run_async(async_result(Failure("blocked")).Bind(binder))
    invalid = _run_async(async_result(Success(1)).Bind(lambda _: "not-result"))

    assert short.is_err
    assert short.error == "blocked"
    assert called is False
    assert invalid.is_err
    assert isinstance(invalid.error, TypeError)


def test_async_result_where_accepts_async_predicates_and_captures_exceptions() -> None:
    called = False

    async def passes(value: int) -> bool:
        return value == 3

    async def fails(value: int) -> bool:
        return value < 0

    async def raises(_: int) -> bool:
        raise ValueError("predicate failed")

    async def unreachable(value: int) -> bool:
        nonlocal called
        called = True
        return value == 3

    passed = _run_async(async_result(Success(3)).Where(passes))
    rejected = _run_async(async_result(Success(3)).Where(fails))
    exception = _run_async(async_result(Success(3)).Where(raises))
    short = _run_async(async_result(Failure("blocked")).Where(unreachable))

    assert passed.is_ok
    assert passed.unwrap() == 3
    assert rejected.is_err
    assert isinstance(rejected.error, ValueError)
    assert exception.is_err
    assert isinstance(exception.error, ValueError)
    assert short.is_err
    assert short.error == "blocked"
    assert called is False


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


def test_option_linq_aliases_filter_or_else_and_match() -> None:
    observed: list[int] = []

    option = (
        Some(2)
        .select(lambda value: value + 1)
        .select_many(lambda value: Some(value * 2), lambda left, right: left + right)
        .tap(lambda value: observed.append(value))
    )
    filtered = option.where(lambda value: value < 0)

    assert option.is_some
    assert option.unwrap() == 9
    assert observed == [9]
    assert filtered.is_none
    assert filtered.or_else(42) == 42
    assert option.match(lambda value: f"some:{value}", lambda: "none") == "some:9"


def test_option_csharp_linq_aliases_are_available() -> None:
    option = (
        Some(2)
        .Select(lambda value: value + 1)
        .SelectMany(lambda value: Some(value * 2), lambda left, right: left + right)
        .Tap(lambda _: None)
        .Where(lambda value: value == 9)
    )

    assert option.is_some
    assert option.OrElse(0) == 9
    assert option.Match(lambda value: value, lambda: 0) == 9


def test_async_option_linq_aliases_compose_and_propagate_exceptions() -> None:
    observed: list[int] = []

    async def start():
        return Some(2)

    async def next_value(value: int):
        return Some(value * 2)

    option = _run_async(
        async_option(start())
        .Select(lambda value: value + 1)
        .SelectMany(next_value, lambda left, right: left + right)
        .Tap(lambda value: observed.append(value))
        .Where(lambda value: value == 9)
    )

    assert option.is_some
    assert option.unwrap() == 9
    assert observed == [9]

    with pytest.raises(ValueError):
        _run_async(async_option(Some(1)).Tap(lambda _: int("not-an-int")))


def test_async_option_short_circuits_and_rejects_non_option() -> None:
    called = False

    async def binder(value: int):
        nonlocal called
        called = True
        return Some(value)

    short = _run_async(async_option(Nothing()).Bind(binder))

    assert short.is_none
    assert called is False

    with pytest.raises(TypeError, match="AsyncOption"):
        _run_async(async_option(Some(1)).Bind(lambda _: "not-option"))


def test_async_option_where_accepts_async_predicates_and_propagates_exceptions() -> None:
    called = False

    async def passes(value: int) -> bool:
        return value == 3

    async def fails(value: int) -> bool:
        return value < 0

    async def raises(_: int) -> bool:
        raise ValueError("predicate failed")

    async def unreachable(value: int) -> bool:
        nonlocal called
        called = True
        return value == 3

    passed = _run_async(async_option(Some(3)).Where(passes))
    rejected = _run_async(async_option(Some(3)).Where(fails))
    short = _run_async(async_option(Nothing()).Where(unreachable))

    assert passed.is_some
    assert passed.unwrap() == 3
    assert rejected.is_none
    assert short.is_none
    assert called is False

    with pytest.raises(ValueError, match="predicate failed"):
        _run_async(async_option(Some(3)).Where(raises))


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


def test_either_map_bind_success_path() -> None:
    either = Right(3).map(lambda value: value + 1).bind(lambda value: Right(value * 2))

    assert either.is_right
    assert either.unwrap() == 8


def test_either_linq_aliases_filter_and_match() -> None:
    observed: list[int] = []

    either = (
        Right(2)
        .select(lambda value: value + 1)
        .select_many(lambda value: Right(value * 2), lambda left, right: left + right)
        .tap(lambda value: observed.append(value))
    )
    filtered = either.where(lambda value: value < 0, lambda: "too-small")

    assert either.is_right
    assert either.unwrap() == 9
    assert observed == [9]
    assert filtered.is_left
    assert filtered.left_value == "too-small"
    assert either.match(lambda left: f"left:{left}", lambda right: f"right:{right}") == "right:9"


def test_either_csharp_linq_aliases_are_available() -> None:
    either = (
        Right(2)
        .Select(lambda value: value + 1)
        .SelectMany(lambda value: Right(value * 2), lambda left, right: left + right)
        .Tap(lambda _: None)
        .Where(lambda value: value == 9, lambda: "blocked")
    )

    assert either.is_right
    assert either.Match(lambda _: 0, lambda value: value) == 9


def test_async_either_linq_aliases_compose_and_propagate_exceptions() -> None:
    observed: list[int] = []

    async def start():
        return Right(2)

    async def next_value(value: int):
        return Right(value * 2)

    either = _run_async(
        async_either(start())
        .Select(lambda value: value + 1)
        .SelectMany(next_value, lambda left, right: left + right)
        .Tap(lambda value: observed.append(value))
        .Where(lambda value: value == 9, lambda: "blocked")
    )

    assert either.is_right
    assert either.unwrap() == 9
    assert observed == [9]

    with pytest.raises(ValueError):
        _run_async(async_either(Right(1)).Tap(lambda _: int("not-an-int")))


def test_async_either_short_circuits_and_rejects_non_either() -> None:
    called = False

    async def binder(value: int):
        nonlocal called
        called = True
        return Right(value)

    short = _run_async(async_either(Left("blocked")).Bind(binder))

    assert short.is_left
    assert short.left_value == "blocked"
    assert called is False

    with pytest.raises(TypeError, match="AsyncEither"):
        _run_async(async_either(Right(1)).Bind(lambda _: "not-either"))


def test_async_either_where_accepts_async_predicates_and_propagates_exceptions() -> None:
    called = False

    async def passes(value: int) -> bool:
        return value == 3

    async def fails(value: int) -> bool:
        return value < 0

    async def raises(_: int) -> bool:
        raise ValueError("predicate failed")

    async def unreachable(value: int) -> bool:
        nonlocal called
        called = True
        return value == 3

    passed = _run_async(async_either(Right(3)).Where(passes, lambda: "blocked"))
    rejected = _run_async(async_either(Right(3)).Where(fails, lambda: "blocked"))
    short = _run_async(async_either(Left("already-left")).Where(unreachable, lambda: "blocked"))

    assert passed.is_right
    assert passed.unwrap() == 3
    assert rejected.is_left
    assert rejected.left_value == "blocked"
    assert short.is_left
    assert short.left_value == "already-left"
    assert called is False

    with pytest.raises(ValueError, match="predicate failed"):
        _run_async(async_either(Right(3)).Where(raises, lambda: "blocked"))


def test_either_bind_short_circuits_left() -> None:
    called = False

    def binder(value: int):
        nonlocal called
        called = True
        return Right(value)

    either = Left("fail-closed").bind(binder)

    assert either.is_left
    assert either.left_value == "fail-closed"
    assert called is False


def test_either_propagates_exceptions() -> None:
    with pytest.raises(ValueError):
        Right(1).map(lambda _: int("not-an-int"))


def test_either_bind_rejects_non_either_return() -> None:
    with pytest.raises(TypeError, match="Either.bind"):
        Right(1).bind(lambda _: "not-either")


def test_either_unwrap_rejects_left() -> None:
    with pytest.raises(ValueError, match="Cannot unwrap Left"):
        Left("stop").unwrap()


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


def test_async_do_result_composes_async_steps_and_preserves_metadata() -> None:
    async def start():
        return Success(2, metadata={"step": "start"})

    async def next_value(value: int):
        return Success(value + 3, metadata={"next": "forward"})

    @async_do(Result)
    def pipeline():
        first = yield start()
        second = yield async_result(next_value(first))
        return second * 2

    result = _run_async(pipeline())

    assert result.is_ok
    assert result.unwrap() == 10
    assert result.metadata == {"step": "start", "next": "forward"}


def test_async_do_result_short_circuits_failure() -> None:
    called = False

    async def blocked():
        return Failure("stop", metadata={"failure_kind": "fail_closed"})

    async def unreachable():
        nonlocal called
        called = True
        return Success("unreachable")

    @async_do(Result)
    def pipeline():
        _ = yield blocked()
        _ = yield unreachable()
        return "unreachable"

    result = _run_async(pipeline())

    assert result.is_err
    assert result.error == "stop"
    assert result.metadata == {"failure_kind": "fail_closed"}
    assert called is False


def test_async_do_result_rejects_non_result_yield() -> None:
    @async_do(Result)
    def pipeline():
        _ = yield Some("wrong-monad")
        return "unreachable"

    result = _run_async(pipeline())

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


def test_async_do_option_success_and_none_paths() -> None:
    async def start():
        return Some(2)

    async def missing():
        return Nothing()

    @async_do(Option)
    def success_pipeline():
        first = yield start()
        second = yield async_option(Some(first + 3))
        return second

    @async_do(Option)
    def none_pipeline():
        _ = yield missing()
        return "unreachable"

    assert _run_async(success_pipeline()).unwrap() == 5
    assert _run_async(none_pipeline()).is_none


def test_async_do_option_rejects_non_option_yield() -> None:
    @async_do(Option)
    def pipeline():
        _ = yield Success("wrong-monad")
        return "unreachable"

    with pytest.raises(TypeError, match="AsyncOption"):
        _run_async(pipeline())


def test_do_either_success_and_left_paths() -> None:
    @do(Either)
    def success_pipeline():
        first = yield Right(2)
        second = yield Right(first + 3)
        return second

    @do(Either)
    def left_pipeline():
        _ = yield Left("blocked")
        return "unreachable"

    success = success_pipeline()
    blocked = left_pipeline()

    assert success.is_right
    assert success.unwrap() == 5
    assert blocked.is_left
    assert blocked.left_value == "blocked"


def test_do_either_rejects_non_either_yield() -> None:
    @do(Either)
    def pipeline():
        _ = yield Success("wrong-monad")
        return "unreachable"

    with pytest.raises(TypeError, match="Either do blocks"):
        pipeline()


def test_async_do_either_success_and_left_paths() -> None:
    async def start():
        return Right(2)

    async def blocked():
        return Left("blocked")

    @async_do(Either)
    def success_pipeline():
        first = yield start()
        second = yield async_either(Right(first + 3))
        return second

    @async_do(Either)
    def left_pipeline():
        _ = yield blocked()
        return "unreachable"

    success = _run_async(success_pipeline())
    blocked_result = _run_async(left_pipeline())

    assert success.is_right
    assert success.unwrap() == 5
    assert blocked_result.is_left
    assert blocked_result.left_value == "blocked"


def test_async_do_either_rejects_non_either_yield() -> None:
    @async_do(Either)
    def pipeline():
        _ = yield Success("wrong-monad")
        return "unreachable"

    with pytest.raises(TypeError, match="AsyncEither"):
        _run_async(pipeline())


def test_do_either_propagates_exceptions() -> None:
    @do(Either)
    def pipeline():
        _ = yield Right("started")
        raise ValueError("boom")

    with pytest.raises(ValueError, match="boom"):
        pipeline()


def _run_async(awaitable):
    async def runner():
        return await awaitable

    return asyncio.run(runner())
