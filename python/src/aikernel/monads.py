from __future__ import annotations

from collections.abc import Callable, Generator, Mapping
from dataclasses import dataclass
from functools import wraps
from typing import Generic, TypeVar


T = TypeVar("T")
U = TypeVar("U")
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

    def with_metadata(self, metadata: Mapping[str, object]) -> Result[T]:
        if self.is_ok:
            return Success(self._value, metadata=metadata)  # type: ignore[arg-type]

        return Failure(self._error, metadata=metadata)

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

    def bind(self, func: Callable[[T], Option[U]]) -> Option[U]:
        if self.is_none:
            return Nothing()

        next_option = func(self._value)  # type: ignore[arg-type]
        if not isinstance(next_option, Option):
            raise TypeError("Option.bind callback must return Option.")

        return next_option

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

    def bind(self, func: Callable[[R], Either[L, U]]) -> Either[L, U]:
        if self.is_left:
            return Left(self._left)  # type: ignore[arg-type]

        next_either = func(self._right)  # type: ignore[arg-type]
        if not isinstance(next_either, Either):
            raise TypeError("Either.bind callback must return Either.")

        return next_either

    def unwrap(self) -> R:
        if self.is_right:
            return self._right  # type: ignore[return-value]

        raise ValueError(f"Cannot unwrap Left either: {self._left}")


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
