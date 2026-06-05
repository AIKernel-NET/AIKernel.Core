from __future__ import annotations

import inspect
from collections.abc import Awaitable, Callable, Generator, Mapping
from dataclasses import dataclass
from functools import wraps
from typing import Generic, TypeVar


T = TypeVar("T")
U = TypeVar("U")
V = TypeVar("V")
L = TypeVar("L")
R = TypeVar("R")


class Result(Generic[T]):
    __slots__ = ("_error", "_is_ok", "_metadata", "_value")

    def __init__(
        self,
        is_ok: bool,
        value: T | None = None,
        error: object | None = None,
        metadata: Mapping[str, object] | None = None,
    ) -> None:
        self._is_ok = is_ok
        self._value = value
        self._error = error
        self._metadata = dict(metadata or {})

    @classmethod
    def success(cls, value: T, metadata: Mapping[str, object] | None = None) -> Result[T]:
        return cls(True, value=value, metadata=metadata)

    @classmethod
    def failure(cls, error: object, metadata: Mapping[str, object] | None = None) -> Result[T]:
        return cls(False, error=error, metadata=metadata)

    @property
    def is_ok(self) -> bool:
        return self._is_ok

    @property
    def is_err(self) -> bool:
        return not self._is_ok

    @property
    def error(self) -> object | None:
        return self._error

    @property
    def metadata(self) -> Mapping[str, object]:
        return dict(self._metadata)

    def map(self, func: Callable[[T], U]) -> Result[U]:
        return self.bind(lambda value: Success(func(value)))

    def Map(self, func: Callable[[T], U]) -> Result[U]:
        return self.map(func)

    def select(self, selector: Callable[[T], U]) -> Result[U]:
        return self.map(selector)

    def Select(self, selector: Callable[[T], U]) -> Result[U]:
        return self.select(selector)

    def select_many(
        self,
        binder: Callable[[T], Result[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> Result[V] | Result[U]:
        if projector is None:
            return self.bind(binder)

        return self.bind(lambda value: binder(value).map(lambda bound: projector(value, bound)))

    def SelectMany(
        self,
        binder: Callable[[T], Result[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> Result[V] | Result[U]:
        return self.select_many(binder, projector)

    def bind(self, func: Callable[[T], Result[U]]) -> Result[U]:
        if self.is_err:
            return Failure(self._error, metadata=self._metadata)

        try:
            next_result = func(self._value)  # type: ignore[arg-type]
        except Exception as ex:  # noqa: BLE001 - Result intentionally captures exceptions.
            return Failure(ex, metadata=self._metadata)

        if not isinstance(next_result, Result):
            return Failure(TypeError("Result.bind callback must return Result."), metadata=self._metadata)

        metadata = {**self._metadata, **next_result.metadata}
        return next_result.with_metadata(metadata)

    def Bind(self, func: Callable[[T], Result[U]]) -> Result[U]:
        return self.bind(func)

    def tap(self, action: Callable[[T], object]) -> Result[T]:
        if self.is_err:
            return Failure(self._error, metadata=self._metadata)

        try:
            action(self._value)  # type: ignore[arg-type]
            return self
        except Exception as ex:  # noqa: BLE001 - Result intentionally captures exceptions.
            return Failure(ex, metadata=self._metadata)

    def Tap(self, action: Callable[[T], object]) -> Result[T]:
        return self.tap(action)

    def where(self, predicate: Callable[[T], bool]) -> Result[T]:
        if self.is_err:
            return Failure(self._error, metadata=self._metadata)

        try:
            if predicate(self._value):  # type: ignore[arg-type]
                return self
            return Failure(ValueError("Predicate failed"), metadata=self._metadata)
        except Exception as ex:  # noqa: BLE001 - Result intentionally captures exceptions.
            return Failure(ex, metadata=self._metadata)

    def Where(self, predicate: Callable[[T], bool]) -> Result[T]:
        return self.where(predicate)

    def with_metadata(self, metadata: Mapping[str, object]) -> Result[T]:
        if self.is_ok:
            return Success(self._value, metadata=metadata)  # type: ignore[arg-type]

        return Failure(self._error, metadata=metadata)

    def match(self, success: Callable[[T], U], failure: Callable[[object | None], U]) -> U:
        return success(self._value) if self.is_ok else failure(self._error)  # type: ignore[arg-type]

    def Match(self, success: Callable[[T], U], failure: Callable[[object | None], U]) -> U:
        return self.match(success, failure)

    def unwrap(self) -> T:
        if self.is_ok:
            return self._value  # type: ignore[return-value]

        if isinstance(self._error, BaseException):
            raise self._error

        raise RuntimeError(str(self._error))


class Option(Generic[T]):
    __slots__ = ("_is_some", "_value")

    def __init__(self, is_some: bool, value: T | None = None) -> None:
        self._is_some = is_some
        self._value = value

    @classmethod
    def some(cls, value: T) -> Option[T]:
        return cls(True, value=value)

    @classmethod
    def none(cls) -> Option[T]:
        return cls(False)

    @property
    def is_some(self) -> bool:
        return self._is_some

    @property
    def is_none(self) -> bool:
        return not self._is_some

    def map(self, func: Callable[[T], U]) -> Option[U]:
        return self.bind(lambda value: Some(func(value)))

    def Map(self, func: Callable[[T], U]) -> Option[U]:
        return self.map(func)

    def select(self, selector: Callable[[T], U]) -> Option[U]:
        return self.map(selector)

    def Select(self, selector: Callable[[T], U]) -> Option[U]:
        return self.select(selector)

    def select_many(
        self,
        binder: Callable[[T], Option[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> Option[V] | Option[U]:
        if projector is None:
            return self.bind(binder)

        return self.bind(lambda value: binder(value).map(lambda bound: projector(value, bound)))

    def SelectMany(
        self,
        binder: Callable[[T], Option[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> Option[V] | Option[U]:
        return self.select_many(binder, projector)

    def bind(self, func: Callable[[T], Option[U]]) -> Option[U]:
        if self.is_none:
            return Nothing()

        next_option = func(self._value)  # type: ignore[arg-type]
        if not isinstance(next_option, Option):
            raise TypeError("Option.bind callback must return Option.")

        return next_option

    def Bind(self, func: Callable[[T], Option[U]]) -> Option[U]:
        return self.bind(func)

    def tap(self, action: Callable[[T], object]) -> Option[T]:
        if self.is_some:
            action(self._value)  # type: ignore[arg-type]
        return self

    def Tap(self, action: Callable[[T], object]) -> Option[T]:
        return self.tap(action)

    def where(self, predicate: Callable[[T], bool]) -> Option[T]:
        if self.is_none:
            return self
        return self if predicate(self._value) else Nothing()  # type: ignore[arg-type]

    def Where(self, predicate: Callable[[T], bool]) -> Option[T]:
        return self.where(predicate)

    def or_else(self, fallback: T) -> T:
        return self._value if self.is_some else fallback  # type: ignore[return-value]

    def OrElse(self, fallback: T) -> T:
        return self.or_else(fallback)

    def match(self, some: Callable[[T], U], none: Callable[[], U]) -> U:
        return some(self._value) if self.is_some else none()  # type: ignore[arg-type]

    def Match(self, some: Callable[[T], U], none: Callable[[], U]) -> U:
        return self.match(some, none)

    def unwrap(self) -> T:
        if self.is_some:
            return self._value  # type: ignore[return-value]

        raise ValueError("Cannot unwrap None option.")


class Either(Generic[L, R]):
    __slots__ = ("_is_right", "_left", "_right")

    def __init__(
        self,
        is_right: bool,
        left: L | None = None,
        right: R | None = None,
    ) -> None:
        self._is_right = is_right
        self._left = left
        self._right = right

    @classmethod
    def right(cls, value: R) -> Either[L, R]:
        return cls(True, right=value)

    @classmethod
    def left(cls, value: L) -> Either[L, R]:
        return cls(False, left=value)

    @property
    def is_right(self) -> bool:
        return self._is_right

    @property
    def is_left(self) -> bool:
        return not self._is_right

    @property
    def left_value(self) -> L | None:
        return self._left

    @property
    def right_value(self) -> R | None:
        return self._right

    def map(self, func: Callable[[R], U]) -> Either[L, U]:
        return self.bind(lambda value: Right(func(value)))

    def Map(self, func: Callable[[R], U]) -> Either[L, U]:
        return self.map(func)

    def select(self, selector: Callable[[R], U]) -> Either[L, U]:
        return self.map(selector)

    def Select(self, selector: Callable[[R], U]) -> Either[L, U]:
        return self.select(selector)

    def select_many(
        self,
        binder: Callable[[R], Either[L, U]],
        projector: Callable[[R, U], V] | None = None,
    ) -> Either[L, V] | Either[L, U]:
        if projector is None:
            return self.bind(binder)

        return self.bind(lambda value: binder(value).map(lambda bound: projector(value, bound)))

    def SelectMany(
        self,
        binder: Callable[[R], Either[L, U]],
        projector: Callable[[R, U], V] | None = None,
    ) -> Either[L, V] | Either[L, U]:
        return self.select_many(binder, projector)

    def bind(self, func: Callable[[R], Either[L, U]]) -> Either[L, U]:
        if self.is_left:
            return Left(self._left)  # type: ignore[arg-type]

        next_either = func(self._right)  # type: ignore[arg-type]
        if not isinstance(next_either, Either):
            raise TypeError("Either.bind callback must return Either.")

        return next_either

    def Bind(self, func: Callable[[R], Either[L, U]]) -> Either[L, U]:
        return self.bind(func)

    def tap(self, action: Callable[[R], object]) -> Either[L, R]:
        if self.is_right:
            action(self._right)  # type: ignore[arg-type]
        return self

    def Tap(self, action: Callable[[R], object]) -> Either[L, R]:
        return self.tap(action)

    def where(self, predicate: Callable[[R], bool], left_factory: Callable[[], L]) -> Either[L, R]:
        if self.is_left:
            return self
        return self if predicate(self._right) else Left(left_factory())  # type: ignore[arg-type]

    def Where(self, predicate: Callable[[R], bool], left_factory: Callable[[], L]) -> Either[L, R]:
        return self.where(predicate, left_factory)

    def match(self, left: Callable[[L], U], right: Callable[[R], U]) -> U:
        return right(self._right) if self.is_right else left(self._left)  # type: ignore[arg-type]

    def Match(self, left: Callable[[L], U], right: Callable[[R], U]) -> U:
        return self.match(left, right)

    def unwrap(self) -> R:
        if self.is_right:
            return self._right  # type: ignore[return-value]

        raise ValueError(f"Cannot unwrap Left either: {self._left}")


class AsyncResult(Generic[T]):
    __slots__ = ("_awaitable",)

    def __init__(self, awaitable: Awaitable[Result[T]]) -> None:
        self._awaitable = awaitable

    def __await__(self):
        return self._awaitable.__await__()

    def map(self, func: Callable[[T], U]) -> AsyncResult[U]:
        async def run() -> Result[U]:
            try:
                return (await self._awaitable).map(func)
            except Exception as ex:  # noqa: BLE001 - AsyncResult intentionally captures exceptions.
                return Failure(ex)

        return AsyncResult(run())

    def Map(self, func: Callable[[T], U]) -> AsyncResult[U]:
        return self.map(func)

    def select(self, selector: Callable[[T], U]) -> AsyncResult[U]:
        return self.map(selector)

    def Select(self, selector: Callable[[T], U]) -> AsyncResult[U]:
        return self.select(selector)

    def bind(self, func: Callable[[T], Result[U] | Awaitable[Result[U]] | AsyncResult[U]]) -> AsyncResult[U]:
        async def run() -> Result[U]:
            try:
                result = await self._awaitable
                if result.is_err:
                    return Failure(result.error, metadata=result.metadata)
                return await _resolve_result(func(result.unwrap()), metadata=result.metadata)
            except Exception as ex:  # noqa: BLE001 - AsyncResult intentionally captures exceptions.
                return Failure(ex)

        return AsyncResult(run())

    def Bind(self, func: Callable[[T], Result[U] | Awaitable[Result[U]] | AsyncResult[U]]) -> AsyncResult[U]:
        return self.bind(func)

    def select_many(
        self,
        binder: Callable[[T], Result[U] | Awaitable[Result[U]] | AsyncResult[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> AsyncResult[V] | AsyncResult[U]:
        if projector is None:
            return self.bind(binder)

        return self.bind(lambda value: async_result(binder(value)).map(lambda bound: projector(value, bound)))

    def SelectMany(
        self,
        binder: Callable[[T], Result[U] | Awaitable[Result[U]] | AsyncResult[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> AsyncResult[V] | AsyncResult[U]:
        return self.select_many(binder, projector)

    def tap(self, action: Callable[[T], object | Awaitable[object]]) -> AsyncResult[T]:
        async def run() -> Result[T]:
            result = await self._awaitable
            if result.is_err:
                return Failure(result.error, metadata=result.metadata)
            try:
                observed = action(result.unwrap())
                if inspect.isawaitable(observed):
                    await observed
                return result
            except Exception as ex:  # noqa: BLE001 - AsyncResult intentionally captures exceptions.
                return Failure(ex, metadata=result.metadata)

        return AsyncResult(run())

    def Tap(self, action: Callable[[T], object | Awaitable[object]]) -> AsyncResult[T]:
        return self.tap(action)

    def where(self, predicate: Callable[[T], bool]) -> AsyncResult[T]:
        return self.bind(lambda value: Success(value).where(predicate))

    def Where(self, predicate: Callable[[T], bool]) -> AsyncResult[T]:
        return self.where(predicate)


class AsyncOption(Generic[T]):
    __slots__ = ("_awaitable",)

    def __init__(self, awaitable: Awaitable[Option[T]]) -> None:
        self._awaitable = awaitable

    def __await__(self):
        return self._awaitable.__await__()

    def map(self, func: Callable[[T], U]) -> AsyncOption[U]:
        async def run() -> Option[U]:
            return (await self._awaitable).map(func)

        return AsyncOption(run())

    def Map(self, func: Callable[[T], U]) -> AsyncOption[U]:
        return self.map(func)

    def select(self, selector: Callable[[T], U]) -> AsyncOption[U]:
        return self.map(selector)

    def Select(self, selector: Callable[[T], U]) -> AsyncOption[U]:
        return self.select(selector)

    def bind(self, func: Callable[[T], Option[U] | Awaitable[Option[U]] | AsyncOption[U]]) -> AsyncOption[U]:
        async def run() -> Option[U]:
            option = await self._awaitable
            if option.is_none:
                return Nothing()
            return await _resolve_option(func(option.unwrap()))

        return AsyncOption(run())

    def Bind(self, func: Callable[[T], Option[U] | Awaitable[Option[U]] | AsyncOption[U]]) -> AsyncOption[U]:
        return self.bind(func)

    def select_many(
        self,
        binder: Callable[[T], Option[U] | Awaitable[Option[U]] | AsyncOption[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> AsyncOption[V] | AsyncOption[U]:
        if projector is None:
            return self.bind(binder)

        return self.bind(lambda value: async_option(binder(value)).map(lambda bound: projector(value, bound)))

    def SelectMany(
        self,
        binder: Callable[[T], Option[U] | Awaitable[Option[U]] | AsyncOption[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> AsyncOption[V] | AsyncOption[U]:
        return self.select_many(binder, projector)

    def tap(self, action: Callable[[T], object | Awaitable[object]]) -> AsyncOption[T]:
        async def run() -> Option[T]:
            option = await self._awaitable
            if option.is_some:
                observed = action(option.unwrap())
                if inspect.isawaitable(observed):
                    await observed
            return option

        return AsyncOption(run())

    def Tap(self, action: Callable[[T], object | Awaitable[object]]) -> AsyncOption[T]:
        return self.tap(action)

    def where(self, predicate: Callable[[T], bool]) -> AsyncOption[T]:
        async def run() -> Option[T]:
            option = await self._awaitable
            return option.where(predicate)

        return AsyncOption(run())

    def Where(self, predicate: Callable[[T], bool]) -> AsyncOption[T]:
        return self.where(predicate)


class AsyncEither(Generic[L, R]):
    __slots__ = ("_awaitable",)

    def __init__(self, awaitable: Awaitable[Either[L, R]]) -> None:
        self._awaitable = awaitable

    def __await__(self):
        return self._awaitable.__await__()

    def map(self, func: Callable[[R], U]) -> AsyncEither[L, U]:
        async def run() -> Either[L, U]:
            return (await self._awaitable).map(func)

        return AsyncEither(run())

    def Map(self, func: Callable[[R], U]) -> AsyncEither[L, U]:
        return self.map(func)

    def select(self, selector: Callable[[R], U]) -> AsyncEither[L, U]:
        return self.map(selector)

    def Select(self, selector: Callable[[R], U]) -> AsyncEither[L, U]:
        return self.select(selector)

    def bind(self, func: Callable[[R], Either[L, U] | Awaitable[Either[L, U]] | AsyncEither[L, U]]) -> AsyncEither[L, U]:
        async def run() -> Either[L, U]:
            either = await self._awaitable
            if either.is_left:
                return Left(either.left_value)  # type: ignore[arg-type]
            return await _resolve_either(func(either.unwrap()))

        return AsyncEither(run())

    def Bind(self, func: Callable[[R], Either[L, U] | Awaitable[Either[L, U]] | AsyncEither[L, U]]) -> AsyncEither[L, U]:
        return self.bind(func)

    def select_many(
        self,
        binder: Callable[[R], Either[L, U] | Awaitable[Either[L, U]] | AsyncEither[L, U]],
        projector: Callable[[R, U], V] | None = None,
    ) -> AsyncEither[L, V] | AsyncEither[L, U]:
        if projector is None:
            return self.bind(binder)

        return self.bind(lambda value: async_either(binder(value)).map(lambda bound: projector(value, bound)))

    def SelectMany(
        self,
        binder: Callable[[R], Either[L, U] | Awaitable[Either[L, U]] | AsyncEither[L, U]],
        projector: Callable[[R, U], V] | None = None,
    ) -> AsyncEither[L, V] | AsyncEither[L, U]:
        return self.select_many(binder, projector)

    def tap(self, action: Callable[[R], object | Awaitable[object]]) -> AsyncEither[L, R]:
        async def run() -> Either[L, R]:
            either = await self._awaitable
            if either.is_right:
                observed = action(either.unwrap())
                if inspect.isawaitable(observed):
                    await observed
            return either

        return AsyncEither(run())

    def Tap(self, action: Callable[[R], object | Awaitable[object]]) -> AsyncEither[L, R]:
        return self.tap(action)

    def where(self, predicate: Callable[[R], bool], left_factory: Callable[[], L]) -> AsyncEither[L, R]:
        async def run() -> Either[L, R]:
            either = await self._awaitable
            return either.where(predicate, left_factory)

        return AsyncEither(run())

    def Where(self, predicate: Callable[[R], bool], left_factory: Callable[[], L]) -> AsyncEither[L, R]:
        return self.where(predicate, left_factory)


@dataclass(frozen=True)
class _DoState:
    kind: type[Result] | type[Option] | type[Either]


def Success(value: T, metadata: Mapping[str, object] | None = None) -> Result[T]:
    return Result.success(value, metadata=metadata)


def Failure(error: object, metadata: Mapping[str, object] | None = None) -> Result[T]:
    return Result.failure(error, metadata=metadata)


def Some(value: T) -> Option[T]:
    return Option.some(value)


def Nothing() -> Option[T]:
    return Option.none()


def Right(value: R) -> Either[L, R]:
    return Either.right(value)


def Left(value: L) -> Either[L, R]:
    return Either.left(value)


def async_result(value: Result[T] | Awaitable[Result[T]] | AsyncResult[T]) -> AsyncResult[T]:
    if isinstance(value, AsyncResult):
        return value

    async def run() -> Result[T]:
        return await _resolve_result(value)

    return AsyncResult(run())


def async_option(value: Option[T] | Awaitable[Option[T]] | AsyncOption[T]) -> AsyncOption[T]:
    if isinstance(value, AsyncOption):
        return value

    async def run() -> Option[T]:
        return await _resolve_option(value)

    return AsyncOption(run())


def async_either(value: Either[L, R] | Awaitable[Either[L, R]] | AsyncEither[L, R]) -> AsyncEither[L, R]:
    if isinstance(value, AsyncEither):
        return value

    async def run() -> Either[L, R]:
        return await _resolve_either(value)

    return AsyncEither(run())


class _Try:
    def __call__(self, thunk: Callable[[], T], metadata: Mapping[str, object] | None = None) -> Result[T]:
        return self.run(thunk, metadata=metadata)

    @staticmethod
    def run(thunk: Callable[[], T], metadata: Mapping[str, object] | None = None) -> Result[T]:
        try:
            return Success(thunk(), metadata=metadata)
        except Exception as ex:  # noqa: BLE001 - Try converts exceptions to Failure.
            return Failure(ex, metadata=metadata)


Try = _Try()


def do(monad_type: type[Result] | type[Option] | type[Either]):
    if monad_type not in (Result, Option, Either):
        raise TypeError("do currently supports Result, Option, and Either.")

    state = _DoState(kind=monad_type)

    def decorator(func):
        @wraps(func)
        def wrapper(*args, **kwargs):
            metadata: dict[str, object] = {}
            try:
                yielded = func(*args, **kwargs)
                if not isinstance(yielded, Generator):
                    return _pure(state, yielded, metadata=metadata)

                current = None
                while True:
                    try:
                        monad = yielded.send(current)
                    except StopIteration as stop:
                        return _pure(state, stop.value, metadata=metadata)

                    short = _short_circuit(state, monad, metadata=metadata)
                    if short is not None:
                        return short

                    if state.kind is Result:
                        metadata.update(monad.metadata)
                    current = monad.unwrap()
            except Exception as ex:  # noqa: BLE001 - Result do notation is fail-closed.
                if state.kind is Result:
                    return Failure(ex, metadata=metadata)
                raise

        return wrapper

    return decorator


def _pure(state: _DoState, value, metadata: Mapping[str, object] | None = None):
    if state.kind is Result:
        return Success(value, metadata=metadata)
    if state.kind is Either:
        return Right(value)

    return Some(value)


def _short_circuit(state: _DoState, monad, metadata: Mapping[str, object] | None = None):
    if state.kind is Result:
        if not isinstance(monad, Result):
            return Failure(TypeError("Result do blocks must yield Result."), metadata=metadata)
        if monad.is_err:
            merged = {**dict(metadata or {}), **monad.metadata}
            return monad.with_metadata(merged)
        return None

    if state.kind is Either:
        if not isinstance(monad, Either):
            raise TypeError("Either do blocks must yield Either.")
        if monad.is_left:
            return monad
        return None

    if not isinstance(monad, Option):
        raise TypeError("Option do blocks must yield Option.")
    if monad.is_none:
        return monad
    return None


async def _resolve_result(
    value: Result[T] | Awaitable[Result[T]] | AsyncResult[T],
    metadata: Mapping[str, object] | None = None,
) -> Result[T]:
    try:
        result = await value if inspect.isawaitable(value) else value
    except Exception as ex:  # noqa: BLE001 - AsyncResult intentionally captures exceptions.
        return Failure(ex, metadata=metadata)

    if not isinstance(result, Result):
        return Failure(TypeError("AsyncResult callback must return Result."), metadata=metadata)

    merged = {**dict(metadata or {}), **result.metadata}
    return result.with_metadata(merged)


async def _resolve_option(value: Option[T] | Awaitable[Option[T]] | AsyncOption[T]) -> Option[T]:
    option = await value if inspect.isawaitable(value) else value
    if not isinstance(option, Option):
        raise TypeError("AsyncOption callback must return Option.")
    return option


async def _resolve_either(value: Either[L, R] | Awaitable[Either[L, R]] | AsyncEither[L, R]) -> Either[L, R]:
    either = await value if inspect.isawaitable(value) else value
    if not isinstance(either, Either):
        raise TypeError("AsyncEither callback must return Either.")
    return either
