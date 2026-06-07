"""[EN]
Reference module for aikernel_net.monads.

[JA]
aikernel_net.monads の参照モジュールです。
"""

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
    """[EN]
    Represents the Result public Python API surface.
    
    [JA]
    Result の公開 Python API サーフェスを表します。
    """
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
        """[EN]
        Executes the success operation.
        Args:
            value: Input value for success.
            metadata: Input value for success.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        success 操作を実行します。
        引数:
            value: success に渡す入力値です。
            metadata: success に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return cls(True, value=value, metadata=metadata)

    @classmethod
    def failure(cls, error: object, metadata: Mapping[str, object] | None = None) -> Result[T]:
        """[EN]
        Executes the failure operation.
        Args:
            error: Input value for failure.
            metadata: Input value for failure.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        failure 操作を実行します。
        引数:
            error: failure に渡す入力値です。
            metadata: failure に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return cls(False, error=error, metadata=metadata)

    @property
    def is_ok(self) -> bool:
        """[EN]
        Executes the is ok operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        is ok 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return self._is_ok

    @property
    def is_err(self) -> bool:
        """[EN]
        Executes the is err operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        is err 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return not self._is_ok

    @property
    def error(self) -> object | None:
        """[EN]
        Executes the error operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        error 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return self._error

    @property
    def metadata(self) -> Mapping[str, object]:
        """[EN]
        Executes the metadata operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        metadata 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return dict(self._metadata)

    def map(self, func: Callable[[T], U]) -> Result[U]:
        """[EN]
        Executes the map operation.
        Args:
            func: Input value for map.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        map 操作を実行します。
        引数:
            func: map に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.bind(lambda value: Success(func(value)))

    def Map(self, func: Callable[[T], U]) -> Result[U]:
        """[EN]
        Executes the Map operation.
        Args:
            func: Input value for Map.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Map 操作を実行します。
        引数:
            func: Map に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.map(func)

    def select(self, selector: Callable[[T], U]) -> Result[U]:
        """[EN]
        Executes the select operation.
        Args:
            selector: Input value for select.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        select 操作を実行します。
        引数:
            selector: select に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.map(selector)

    def Select(self, selector: Callable[[T], U]) -> Result[U]:
        """[EN]
        Executes the Select operation.
        Args:
            selector: Input value for Select.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Select 操作を実行します。
        引数:
            selector: Select に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.select(selector)

    def select_many(
        self,
        binder: Callable[[T], Result[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> Result[V] | Result[U]:
        """[EN]
        Executes the select many operation.
        Args:
            binder: Input value for select many.
            projector: Input value for select many.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        select many 操作を実行します。
        引数:
            binder: select many に渡す入力値です。
            projector: select many に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if projector is None:
            return self.bind(binder)

        return self.bind(lambda value: binder(value).map(lambda bound: projector(value, bound)))

    def SelectMany(
        self,
        binder: Callable[[T], Result[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> Result[V] | Result[U]:
        """[EN]
        Executes the SelectMany operation.
        Args:
            binder: Input value for SelectMany.
            projector: Input value for SelectMany.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        SelectMany 操作を実行します。
        引数:
            binder: SelectMany に渡す入力値です。
            projector: SelectMany に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.select_many(binder, projector)

    def bind(self, func: Callable[[T], Result[U]]) -> Result[U]:
        """[EN]
        Executes the bind operation.
        Args:
            func: Input value for bind.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        bind 操作を実行します。
        引数:
            func: bind に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
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
        """[EN]
        Executes the Bind operation.
        Args:
            func: Input value for Bind.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Bind 操作を実行します。
        引数:
            func: Bind に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.bind(func)

    def tap(self, action: Callable[[T], object]) -> Result[T]:
        """[EN]
        Executes the tap operation.
        Args:
            action: Input value for tap.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        tap 操作を実行します。
        引数:
            action: tap に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if self.is_err:
            return Failure(self._error, metadata=self._metadata)

        try:
            action(self._value)  # type: ignore[arg-type]
            return self
        except Exception as ex:  # noqa: BLE001 - Result intentionally captures exceptions.
            return Failure(ex, metadata=self._metadata)

    def Tap(self, action: Callable[[T], object]) -> Result[T]:
        """[EN]
        Executes the Tap operation.
        Args:
            action: Input value for Tap.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Tap 操作を実行します。
        引数:
            action: Tap に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.tap(action)

    def where(self, predicate: Callable[[T], bool]) -> Result[T]:
        """[EN]
        Executes the where operation.
        Args:
            predicate: Input value for where.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        where 操作を実行します。
        引数:
            predicate: where に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if self.is_err:
            return Failure(self._error, metadata=self._metadata)

        try:
            if predicate(self._value):  # type: ignore[arg-type]
                return self
            return Failure(ValueError("Predicate failed"), metadata=self._metadata)
        except Exception as ex:  # noqa: BLE001 - Result intentionally captures exceptions.
            return Failure(ex, metadata=self._metadata)

    def Where(self, predicate: Callable[[T], bool]) -> Result[T]:
        """[EN]
        Executes the Where operation.
        Args:
            predicate: Input value for Where.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Where 操作を実行します。
        引数:
            predicate: Where に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.where(predicate)

    def with_metadata(self, metadata: Mapping[str, object]) -> Result[T]:
        """[EN]
        Executes the with metadata operation.
        Args:
            metadata: Input value for with metadata.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        with metadata 操作を実行します。
        引数:
            metadata: with metadata に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if self.is_ok:
            return Success(self._value, metadata=metadata)  # type: ignore[arg-type]

        return Failure(self._error, metadata=metadata)

    def match(self, success: Callable[[T], U], failure: Callable[[object | None], U]) -> U:
        """[EN]
        Executes the match operation.
        Args:
            success: Input value for match.
            failure: Input value for match.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        match 操作を実行します。
        引数:
            success: match に渡す入力値です。
            failure: match に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return success(self._value) if self.is_ok else failure(self._error)  # type: ignore[arg-type]

    def Match(self, success: Callable[[T], U], failure: Callable[[object | None], U]) -> U:
        """[EN]
        Executes the Match operation.
        Args:
            success: Input value for Match.
            failure: Input value for Match.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Match 操作を実行します。
        引数:
            success: Match に渡す入力値です。
            failure: Match に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.match(success, failure)

    def unwrap(self) -> T:
        """[EN]
        Executes the unwrap operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        unwrap 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        if self.is_ok:
            return self._value  # type: ignore[return-value]

        if isinstance(self._error, BaseException):
            raise self._error

        raise RuntimeError(str(self._error))


class Option(Generic[T]):
    """[EN]
    Represents the Option public Python API surface.
    
    [JA]
    Option の公開 Python API サーフェスを表します。
    """
    __slots__ = ("_is_some", "_value")

    def __init__(self, is_some: bool, value: T | None = None) -> None:
        self._is_some = is_some
        self._value = value

    @classmethod
    def some(cls, value: T) -> Option[T]:
        """[EN]
        Executes the some operation.
        Args:
            value: Input value for some.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        some 操作を実行します。
        引数:
            value: some に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return cls(True, value=value)

    @classmethod
    def none(cls) -> Option[T]:
        """[EN]
        Executes the none operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        none 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return cls(False)

    @property
    def is_some(self) -> bool:
        """[EN]
        Executes the is some operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        is some 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return self._is_some

    @property
    def is_none(self) -> bool:
        """[EN]
        Executes the is none operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        is none 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return not self._is_some

    def map(self, func: Callable[[T], U]) -> Option[U]:
        """[EN]
        Executes the map operation.
        Args:
            func: Input value for map.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        map 操作を実行します。
        引数:
            func: map に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.bind(lambda value: Some(func(value)))

    def Map(self, func: Callable[[T], U]) -> Option[U]:
        """[EN]
        Executes the Map operation.
        Args:
            func: Input value for Map.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Map 操作を実行します。
        引数:
            func: Map に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.map(func)

    def select(self, selector: Callable[[T], U]) -> Option[U]:
        """[EN]
        Executes the select operation.
        Args:
            selector: Input value for select.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        select 操作を実行します。
        引数:
            selector: select に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.map(selector)

    def Select(self, selector: Callable[[T], U]) -> Option[U]:
        """[EN]
        Executes the Select operation.
        Args:
            selector: Input value for Select.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Select 操作を実行します。
        引数:
            selector: Select に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.select(selector)

    def select_many(
        self,
        binder: Callable[[T], Option[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> Option[V] | Option[U]:
        """[EN]
        Executes the select many operation.
        Args:
            binder: Input value for select many.
            projector: Input value for select many.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        select many 操作を実行します。
        引数:
            binder: select many に渡す入力値です。
            projector: select many に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if projector is None:
            return self.bind(binder)

        return self.bind(lambda value: binder(value).map(lambda bound: projector(value, bound)))

    def SelectMany(
        self,
        binder: Callable[[T], Option[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> Option[V] | Option[U]:
        """[EN]
        Executes the SelectMany operation.
        Args:
            binder: Input value for SelectMany.
            projector: Input value for SelectMany.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        SelectMany 操作を実行します。
        引数:
            binder: SelectMany に渡す入力値です。
            projector: SelectMany に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.select_many(binder, projector)

    def bind(self, func: Callable[[T], Option[U]]) -> Option[U]:
        """[EN]
        Executes the bind operation.
        Args:
            func: Input value for bind.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        bind 操作を実行します。
        引数:
            func: bind に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if self.is_none:
            return Nothing()

        next_option = func(self._value)  # type: ignore[arg-type]
        if not isinstance(next_option, Option):
            raise TypeError("Option.bind callback must return Option.")

        return next_option

    def Bind(self, func: Callable[[T], Option[U]]) -> Option[U]:
        """[EN]
        Executes the Bind operation.
        Args:
            func: Input value for Bind.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Bind 操作を実行します。
        引数:
            func: Bind に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.bind(func)

    def tap(self, action: Callable[[T], object]) -> Option[T]:
        """[EN]
        Executes the tap operation.
        Args:
            action: Input value for tap.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        tap 操作を実行します。
        引数:
            action: tap に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if self.is_some:
            action(self._value)  # type: ignore[arg-type]
        return self

    def Tap(self, action: Callable[[T], object]) -> Option[T]:
        """[EN]
        Executes the Tap operation.
        Args:
            action: Input value for Tap.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Tap 操作を実行します。
        引数:
            action: Tap に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.tap(action)

    def where(self, predicate: Callable[[T], bool]) -> Option[T]:
        """[EN]
        Executes the where operation.
        Args:
            predicate: Input value for where.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        where 操作を実行します。
        引数:
            predicate: where に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if self.is_none:
            return self
        return self if predicate(self._value) else Nothing()  # type: ignore[arg-type]

    def Where(self, predicate: Callable[[T], bool]) -> Option[T]:
        """[EN]
        Executes the Where operation.
        Args:
            predicate: Input value for Where.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Where 操作を実行します。
        引数:
            predicate: Where に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.where(predicate)

    def or_else(self, fallback: T) -> T:
        """[EN]
        Executes the or else operation.
        Args:
            fallback: Input value for or else.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        or else 操作を実行します。
        引数:
            fallback: or else に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self._value if self.is_some else fallback  # type: ignore[return-value]

    def OrElse(self, fallback: T) -> T:
        """[EN]
        Executes the OrElse operation.
        Args:
            fallback: Input value for OrElse.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        OrElse 操作を実行します。
        引数:
            fallback: OrElse に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.or_else(fallback)

    def match(self, some: Callable[[T], U], none: Callable[[], U]) -> U:
        """[EN]
        Executes the match operation.
        Args:
            some: Input value for match.
            none: Input value for match.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        match 操作を実行します。
        引数:
            some: match に渡す入力値です。
            none: match に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return some(self._value) if self.is_some else none()  # type: ignore[arg-type]

    def Match(self, some: Callable[[T], U], none: Callable[[], U]) -> U:
        """[EN]
        Executes the Match operation.
        Args:
            some: Input value for Match.
            none: Input value for Match.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Match 操作を実行します。
        引数:
            some: Match に渡す入力値です。
            none: Match に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.match(some, none)

    def unwrap(self) -> T:
        """[EN]
        Executes the unwrap operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        unwrap 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        if self.is_some:
            return self._value  # type: ignore[return-value]

        raise ValueError("Cannot unwrap None option.")


class Either(Generic[L, R]):
    """[EN]
    Represents the Either public Python API surface.
    
    [JA]
    Either の公開 Python API サーフェスを表します。
    """
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
        """[EN]
        Executes the right operation.
        Args:
            value: Input value for right.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        right 操作を実行します。
        引数:
            value: right に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return cls(True, right=value)

    @classmethod
    def left(cls, value: L) -> Either[L, R]:
        """[EN]
        Executes the left operation.
        Args:
            value: Input value for left.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        left 操作を実行します。
        引数:
            value: left に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return cls(False, left=value)

    @property
    def is_right(self) -> bool:
        """[EN]
        Executes the is right operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        is right 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return self._is_right

    @property
    def is_left(self) -> bool:
        """[EN]
        Executes the is left operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        is left 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return not self._is_right

    @property
    def left_value(self) -> L | None:
        """[EN]
        Executes the left value operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        left value 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return self._left

    @property
    def right_value(self) -> R | None:
        """[EN]
        Executes the right value operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        right value 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return self._right

    def map(self, func: Callable[[R], U]) -> Either[L, U]:
        """[EN]
        Executes the map operation.
        Args:
            func: Input value for map.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        map 操作を実行します。
        引数:
            func: map に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.bind(lambda value: Right(func(value)))

    def Map(self, func: Callable[[R], U]) -> Either[L, U]:
        """[EN]
        Executes the Map operation.
        Args:
            func: Input value for Map.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Map 操作を実行します。
        引数:
            func: Map に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.map(func)

    def select(self, selector: Callable[[R], U]) -> Either[L, U]:
        """[EN]
        Executes the select operation.
        Args:
            selector: Input value for select.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        select 操作を実行します。
        引数:
            selector: select に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.map(selector)

    def Select(self, selector: Callable[[R], U]) -> Either[L, U]:
        """[EN]
        Executes the Select operation.
        Args:
            selector: Input value for Select.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Select 操作を実行します。
        引数:
            selector: Select に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.select(selector)

    def select_many(
        self,
        binder: Callable[[R], Either[L, U]],
        projector: Callable[[R, U], V] | None = None,
    ) -> Either[L, V] | Either[L, U]:
        """[EN]
        Executes the select many operation.
        Args:
            binder: Input value for select many.
            projector: Input value for select many.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        select many 操作を実行します。
        引数:
            binder: select many に渡す入力値です。
            projector: select many に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if projector is None:
            return self.bind(binder)

        return self.bind(lambda value: binder(value).map(lambda bound: projector(value, bound)))

    def SelectMany(
        self,
        binder: Callable[[R], Either[L, U]],
        projector: Callable[[R, U], V] | None = None,
    ) -> Either[L, V] | Either[L, U]:
        """[EN]
        Executes the SelectMany operation.
        Args:
            binder: Input value for SelectMany.
            projector: Input value for SelectMany.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        SelectMany 操作を実行します。
        引数:
            binder: SelectMany に渡す入力値です。
            projector: SelectMany に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.select_many(binder, projector)

    def bind(self, func: Callable[[R], Either[L, U]]) -> Either[L, U]:
        """[EN]
        Executes the bind operation.
        Args:
            func: Input value for bind.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        bind 操作を実行します。
        引数:
            func: bind に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if self.is_left:
            return Left(self._left)  # type: ignore[arg-type]

        next_either = func(self._right)  # type: ignore[arg-type]
        if not isinstance(next_either, Either):
            raise TypeError("Either.bind callback must return Either.")

        return next_either

    def Bind(self, func: Callable[[R], Either[L, U]]) -> Either[L, U]:
        """[EN]
        Executes the Bind operation.
        Args:
            func: Input value for Bind.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Bind 操作を実行します。
        引数:
            func: Bind に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.bind(func)

    def tap(self, action: Callable[[R], object]) -> Either[L, R]:
        """[EN]
        Executes the tap operation.
        Args:
            action: Input value for tap.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        tap 操作を実行します。
        引数:
            action: tap に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if self.is_right:
            action(self._right)  # type: ignore[arg-type]
        return self

    def Tap(self, action: Callable[[R], object]) -> Either[L, R]:
        """[EN]
        Executes the Tap operation.
        Args:
            action: Input value for Tap.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Tap 操作を実行します。
        引数:
            action: Tap に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.tap(action)

    def where(self, predicate: Callable[[R], bool], left_factory: Callable[[], L]) -> Either[L, R]:
        """[EN]
        Executes the where operation.
        Args:
            predicate: Input value for where.
            left_factory: Input value for where.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        where 操作を実行します。
        引数:
            predicate: where に渡す入力値です。
            left_factory: where に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if self.is_left:
            return self
        return self if predicate(self._right) else Left(left_factory())  # type: ignore[arg-type]

    def Where(self, predicate: Callable[[R], bool], left_factory: Callable[[], L]) -> Either[L, R]:
        """[EN]
        Executes the Where operation.
        Args:
            predicate: Input value for Where.
            left_factory: Input value for Where.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Where 操作を実行します。
        引数:
            predicate: Where に渡す入力値です。
            left_factory: Where に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.where(predicate, left_factory)

    def match(self, left: Callable[[L], U], right: Callable[[R], U]) -> U:
        """[EN]
        Executes the match operation.
        Args:
            left: Input value for match.
            right: Input value for match.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        match 操作を実行します。
        引数:
            left: match に渡す入力値です。
            right: match に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return right(self._right) if self.is_right else left(self._left)  # type: ignore[arg-type]

    def Match(self, left: Callable[[L], U], right: Callable[[R], U]) -> U:
        """[EN]
        Executes the Match operation.
        Args:
            left: Input value for Match.
            right: Input value for Match.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Match 操作を実行します。
        引数:
            left: Match に渡す入力値です。
            right: Match に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.match(left, right)

    def unwrap(self) -> R:
        """[EN]
        Executes the unwrap operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        unwrap 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        if self.is_right:
            return self._right  # type: ignore[return-value]

        raise ValueError(f"Cannot unwrap Left either: {self._left}")


class AsyncResult(Generic[T]):
    """[EN]
    Represents the AsyncResult public Python API surface.
    
    [JA]
    AsyncResult の公開 Python API サーフェスを表します。
    """
    __slots__ = ("_awaitable",)

    def __init__(self, awaitable: Awaitable[Result[T]]) -> None:
        self._awaitable = awaitable

    def __await__(self):
        return self._awaitable.__await__()

    def map(self, func: Callable[[T], U]) -> AsyncResult[U]:
        """[EN]
        Executes the map operation.
        Args:
            func: Input value for map.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        map 操作を実行します。
        引数:
            func: map に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        async def run() -> Result[U]:
            """[EN]
            Executes the run operation.
            Returns:
                Result produced by the operation.
            
            [JA]
            run 操作を実行します。
            戻り値:
                操作によって生成される結果です。
            """
            try:
                return (await self._awaitable).map(func)
            except Exception as ex:  # noqa: BLE001 - AsyncResult intentionally captures exceptions.
                return Failure(ex)

        return AsyncResult(run())

    def Map(self, func: Callable[[T], U]) -> AsyncResult[U]:
        """[EN]
        Executes the Map operation.
        Args:
            func: Input value for Map.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Map 操作を実行します。
        引数:
            func: Map に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.map(func)

    def select(self, selector: Callable[[T], U]) -> AsyncResult[U]:
        """[EN]
        Executes the select operation.
        Args:
            selector: Input value for select.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        select 操作を実行します。
        引数:
            selector: select に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.map(selector)

    def Select(self, selector: Callable[[T], U]) -> AsyncResult[U]:
        """[EN]
        Executes the Select operation.
        Args:
            selector: Input value for Select.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Select 操作を実行します。
        引数:
            selector: Select に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.select(selector)

    def bind(self, func: Callable[[T], Result[U] | Awaitable[Result[U]] | AsyncResult[U]]) -> AsyncResult[U]:
        """[EN]
        Executes the bind operation.
        Args:
            func: Input value for bind.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        bind 操作を実行します。
        引数:
            func: bind に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        async def run() -> Result[U]:
            """[EN]
            Executes the run operation.
            Returns:
                Result produced by the operation.
            
            [JA]
            run 操作を実行します。
            戻り値:
                操作によって生成される結果です。
            """
            try:
                result = await self._awaitable
                if result.is_err:
                    return Failure(result.error, metadata=result.metadata)
                return await _resolve_result(func(result.unwrap()), metadata=result.metadata)
            except Exception as ex:  # noqa: BLE001 - AsyncResult intentionally captures exceptions.
                return Failure(ex)

        return AsyncResult(run())

    def Bind(self, func: Callable[[T], Result[U] | Awaitable[Result[U]] | AsyncResult[U]]) -> AsyncResult[U]:
        """[EN]
        Executes the Bind operation.
        Args:
            func: Input value for Bind.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Bind 操作を実行します。
        引数:
            func: Bind に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.bind(func)

    def select_many(
        self,
        binder: Callable[[T], Result[U] | Awaitable[Result[U]] | AsyncResult[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> AsyncResult[V] | AsyncResult[U]:
        """[EN]
        Executes the select many operation.
        Args:
            binder: Input value for select many.
            projector: Input value for select many.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        select many 操作を実行します。
        引数:
            binder: select many に渡す入力値です。
            projector: select many に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if projector is None:
            return self.bind(binder)

        return self.bind(lambda value: async_result(binder(value)).map(lambda bound: projector(value, bound)))

    def SelectMany(
        self,
        binder: Callable[[T], Result[U] | Awaitable[Result[U]] | AsyncResult[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> AsyncResult[V] | AsyncResult[U]:
        """[EN]
        Executes the SelectMany operation.
        Args:
            binder: Input value for SelectMany.
            projector: Input value for SelectMany.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        SelectMany 操作を実行します。
        引数:
            binder: SelectMany に渡す入力値です。
            projector: SelectMany に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.select_many(binder, projector)

    def tap(self, action: Callable[[T], object | Awaitable[object]]) -> AsyncResult[T]:
        """[EN]
        Executes the tap operation.
        Args:
            action: Input value for tap.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        tap 操作を実行します。
        引数:
            action: tap に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        async def run() -> Result[T]:
            """[EN]
            Executes the run operation.
            Returns:
                Result produced by the operation.
            
            [JA]
            run 操作を実行します。
            戻り値:
                操作によって生成される結果です。
            """
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
        """[EN]
        Executes the Tap operation.
        Args:
            action: Input value for Tap.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Tap 操作を実行します。
        引数:
            action: Tap に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.tap(action)

    def where(self, predicate: Callable[[T], bool | Awaitable[bool]]) -> AsyncResult[T]:
        """[EN]
        Executes the where operation.
        Args:
            predicate: Input value for where.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        where 操作を実行します。
        引数:
            predicate: where に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        async def run() -> Result[T]:
            """[EN]
            Executes the run operation.
            Returns:
                Result produced by the operation.
            
            [JA]
            run 操作を実行します。
            戻り値:
                操作によって生成される結果です。
            """
            result = await self._awaitable
            if result.is_err:
                return Failure(result.error, metadata=result.metadata)
            try:
                observed = predicate(result.unwrap())
                passed = await observed if inspect.isawaitable(observed) else observed
                if passed:
                    return result
                return Failure(ValueError("Predicate failed"), metadata=result.metadata)
            except Exception as ex:  # noqa: BLE001 - AsyncResult intentionally captures exceptions.
                return Failure(ex, metadata=result.metadata)

        return AsyncResult(run())

    def Where(self, predicate: Callable[[T], bool | Awaitable[bool]]) -> AsyncResult[T]:
        """[EN]
        Executes the Where operation.
        Args:
            predicate: Input value for Where.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Where 操作を実行します。
        引数:
            predicate: Where に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.where(predicate)


class AsyncOption(Generic[T]):
    """[EN]
    Represents the AsyncOption public Python API surface.
    
    [JA]
    AsyncOption の公開 Python API サーフェスを表します。
    """
    __slots__ = ("_awaitable",)

    def __init__(self, awaitable: Awaitable[Option[T]]) -> None:
        self._awaitable = awaitable

    def __await__(self):
        return self._awaitable.__await__()

    def map(self, func: Callable[[T], U]) -> AsyncOption[U]:
        """[EN]
        Executes the map operation.
        Args:
            func: Input value for map.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        map 操作を実行します。
        引数:
            func: map に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        async def run() -> Option[U]:
            """[EN]
            Executes the run operation.
            Returns:
                Result produced by the operation.
            
            [JA]
            run 操作を実行します。
            戻り値:
                操作によって生成される結果です。
            """
            return (await self._awaitable).map(func)

        return AsyncOption(run())

    def Map(self, func: Callable[[T], U]) -> AsyncOption[U]:
        """[EN]
        Executes the Map operation.
        Args:
            func: Input value for Map.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Map 操作を実行します。
        引数:
            func: Map に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.map(func)

    def select(self, selector: Callable[[T], U]) -> AsyncOption[U]:
        """[EN]
        Executes the select operation.
        Args:
            selector: Input value for select.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        select 操作を実行します。
        引数:
            selector: select に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.map(selector)

    def Select(self, selector: Callable[[T], U]) -> AsyncOption[U]:
        """[EN]
        Executes the Select operation.
        Args:
            selector: Input value for Select.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Select 操作を実行します。
        引数:
            selector: Select に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.select(selector)

    def bind(self, func: Callable[[T], Option[U] | Awaitable[Option[U]] | AsyncOption[U]]) -> AsyncOption[U]:
        """[EN]
        Executes the bind operation.
        Args:
            func: Input value for bind.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        bind 操作を実行します。
        引数:
            func: bind に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        async def run() -> Option[U]:
            """[EN]
            Executes the run operation.
            Returns:
                Result produced by the operation.
            
            [JA]
            run 操作を実行します。
            戻り値:
                操作によって生成される結果です。
            """
            option = await self._awaitable
            if option.is_none:
                return Nothing()
            return await _resolve_option(func(option.unwrap()))

        return AsyncOption(run())

    def Bind(self, func: Callable[[T], Option[U] | Awaitable[Option[U]] | AsyncOption[U]]) -> AsyncOption[U]:
        """[EN]
        Executes the Bind operation.
        Args:
            func: Input value for Bind.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Bind 操作を実行します。
        引数:
            func: Bind に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.bind(func)

    def select_many(
        self,
        binder: Callable[[T], Option[U] | Awaitable[Option[U]] | AsyncOption[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> AsyncOption[V] | AsyncOption[U]:
        """[EN]
        Executes the select many operation.
        Args:
            binder: Input value for select many.
            projector: Input value for select many.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        select many 操作を実行します。
        引数:
            binder: select many に渡す入力値です。
            projector: select many に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if projector is None:
            return self.bind(binder)

        return self.bind(lambda value: async_option(binder(value)).map(lambda bound: projector(value, bound)))

    def SelectMany(
        self,
        binder: Callable[[T], Option[U] | Awaitable[Option[U]] | AsyncOption[U]],
        projector: Callable[[T, U], V] | None = None,
    ) -> AsyncOption[V] | AsyncOption[U]:
        """[EN]
        Executes the SelectMany operation.
        Args:
            binder: Input value for SelectMany.
            projector: Input value for SelectMany.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        SelectMany 操作を実行します。
        引数:
            binder: SelectMany に渡す入力値です。
            projector: SelectMany に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.select_many(binder, projector)

    def tap(self, action: Callable[[T], object | Awaitable[object]]) -> AsyncOption[T]:
        """[EN]
        Executes the tap operation.
        Args:
            action: Input value for tap.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        tap 操作を実行します。
        引数:
            action: tap に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        async def run() -> Option[T]:
            """[EN]
            Executes the run operation.
            Returns:
                Result produced by the operation.
            
            [JA]
            run 操作を実行します。
            戻り値:
                操作によって生成される結果です。
            """
            option = await self._awaitable
            if option.is_some:
                observed = action(option.unwrap())
                if inspect.isawaitable(observed):
                    await observed
            return option

        return AsyncOption(run())

    def Tap(self, action: Callable[[T], object | Awaitable[object]]) -> AsyncOption[T]:
        """[EN]
        Executes the Tap operation.
        Args:
            action: Input value for Tap.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Tap 操作を実行します。
        引数:
            action: Tap に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.tap(action)

    def where(self, predicate: Callable[[T], bool | Awaitable[bool]]) -> AsyncOption[T]:
        """[EN]
        Executes the where operation.
        Args:
            predicate: Input value for where.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        where 操作を実行します。
        引数:
            predicate: where に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        async def run() -> Option[T]:
            """[EN]
            Executes the run operation.
            Returns:
                Result produced by the operation.
            
            [JA]
            run 操作を実行します。
            戻り値:
                操作によって生成される結果です。
            """
            option = await self._awaitable
            if option.is_none:
                return option

            observed = predicate(option.unwrap())
            passed = await observed if inspect.isawaitable(observed) else observed
            return option if passed else Nothing()

        return AsyncOption(run())

    def Where(self, predicate: Callable[[T], bool | Awaitable[bool]]) -> AsyncOption[T]:
        """[EN]
        Executes the Where operation.
        Args:
            predicate: Input value for Where.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Where 操作を実行します。
        引数:
            predicate: Where に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.where(predicate)


class AsyncEither(Generic[L, R]):
    """[EN]
    Represents the AsyncEither public Python API surface.
    
    [JA]
    AsyncEither の公開 Python API サーフェスを表します。
    """
    __slots__ = ("_awaitable",)

    def __init__(self, awaitable: Awaitable[Either[L, R]]) -> None:
        self._awaitable = awaitable

    def __await__(self):
        return self._awaitable.__await__()

    def map(self, func: Callable[[R], U]) -> AsyncEither[L, U]:
        """[EN]
        Executes the map operation.
        Args:
            func: Input value for map.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        map 操作を実行します。
        引数:
            func: map に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        async def run() -> Either[L, U]:
            """[EN]
            Executes the run operation.
            Returns:
                Result produced by the operation.
            
            [JA]
            run 操作を実行します。
            戻り値:
                操作によって生成される結果です。
            """
            return (await self._awaitable).map(func)

        return AsyncEither(run())

    def Map(self, func: Callable[[R], U]) -> AsyncEither[L, U]:
        """[EN]
        Executes the Map operation.
        Args:
            func: Input value for Map.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Map 操作を実行します。
        引数:
            func: Map に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.map(func)

    def select(self, selector: Callable[[R], U]) -> AsyncEither[L, U]:
        """[EN]
        Executes the select operation.
        Args:
            selector: Input value for select.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        select 操作を実行します。
        引数:
            selector: select に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.map(selector)

    def Select(self, selector: Callable[[R], U]) -> AsyncEither[L, U]:
        """[EN]
        Executes the Select operation.
        Args:
            selector: Input value for Select.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Select 操作を実行します。
        引数:
            selector: Select に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.select(selector)

    def bind(self, func: Callable[[R], Either[L, U] | Awaitable[Either[L, U]] | AsyncEither[L, U]]) -> AsyncEither[L, U]:
        """[EN]
        Executes the bind operation.
        Args:
            func: Input value for bind.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        bind 操作を実行します。
        引数:
            func: bind に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        async def run() -> Either[L, U]:
            """[EN]
            Executes the run operation.
            Returns:
                Result produced by the operation.
            
            [JA]
            run 操作を実行します。
            戻り値:
                操作によって生成される結果です。
            """
            either = await self._awaitable
            if either.is_left:
                return Left(either.left_value)  # type: ignore[arg-type]
            return await _resolve_either(func(either.unwrap()))

        return AsyncEither(run())

    def Bind(self, func: Callable[[R], Either[L, U] | Awaitable[Either[L, U]] | AsyncEither[L, U]]) -> AsyncEither[L, U]:
        """[EN]
        Executes the Bind operation.
        Args:
            func: Input value for Bind.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Bind 操作を実行します。
        引数:
            func: Bind に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.bind(func)

    def select_many(
        self,
        binder: Callable[[R], Either[L, U] | Awaitable[Either[L, U]] | AsyncEither[L, U]],
        projector: Callable[[R, U], V] | None = None,
    ) -> AsyncEither[L, V] | AsyncEither[L, U]:
        """[EN]
        Executes the select many operation.
        Args:
            binder: Input value for select many.
            projector: Input value for select many.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        select many 操作を実行します。
        引数:
            binder: select many に渡す入力値です。
            projector: select many に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        if projector is None:
            return self.bind(binder)

        return self.bind(lambda value: async_either(binder(value)).map(lambda bound: projector(value, bound)))

    def SelectMany(
        self,
        binder: Callable[[R], Either[L, U] | Awaitable[Either[L, U]] | AsyncEither[L, U]],
        projector: Callable[[R, U], V] | None = None,
    ) -> AsyncEither[L, V] | AsyncEither[L, U]:
        """[EN]
        Executes the SelectMany operation.
        Args:
            binder: Input value for SelectMany.
            projector: Input value for SelectMany.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        SelectMany 操作を実行します。
        引数:
            binder: SelectMany に渡す入力値です。
            projector: SelectMany に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.select_many(binder, projector)

    def tap(self, action: Callable[[R], object | Awaitable[object]]) -> AsyncEither[L, R]:
        """[EN]
        Executes the tap operation.
        Args:
            action: Input value for tap.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        tap 操作を実行します。
        引数:
            action: tap に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        async def run() -> Either[L, R]:
            """[EN]
            Executes the run operation.
            Returns:
                Result produced by the operation.
            
            [JA]
            run 操作を実行します。
            戻り値:
                操作によって生成される結果です。
            """
            either = await self._awaitable
            if either.is_right:
                observed = action(either.unwrap())
                if inspect.isawaitable(observed):
                    await observed
            return either

        return AsyncEither(run())

    def Tap(self, action: Callable[[R], object | Awaitable[object]]) -> AsyncEither[L, R]:
        """[EN]
        Executes the Tap operation.
        Args:
            action: Input value for Tap.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Tap 操作を実行します。
        引数:
            action: Tap に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.tap(action)

    def where(
        self,
        predicate: Callable[[R], bool | Awaitable[bool]],
        left_factory: Callable[[], L],
    ) -> AsyncEither[L, R]:
        """[EN]
        Executes the where operation.
        Args:
            predicate: Input value for where.
            left_factory: Input value for where.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        where 操作を実行します。
        引数:
            predicate: where に渡す入力値です。
            left_factory: where に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        async def run() -> Either[L, R]:
            """[EN]
            Executes the run operation.
            Returns:
                Result produced by the operation.
            
            [JA]
            run 操作を実行します。
            戻り値:
                操作によって生成される結果です。
            """
            either = await self._awaitable
            if either.is_left:
                return either

            observed = predicate(either.unwrap())
            passed = await observed if inspect.isawaitable(observed) else observed
            return either if passed else Left(left_factory())

        return AsyncEither(run())

    def Where(
        self,
        predicate: Callable[[R], bool | Awaitable[bool]],
        left_factory: Callable[[], L],
    ) -> AsyncEither[L, R]:
        """[EN]
        Executes the Where operation.
        Args:
            predicate: Input value for Where.
            left_factory: Input value for Where.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        Where 操作を実行します。
        引数:
            predicate: Where に渡す入力値です。
            left_factory: Where に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        return self.where(predicate, left_factory)


@dataclass(frozen=True)
class _DoState:
    kind: type[Result] | type[Option] | type[Either]


def Success(value: T, metadata: Mapping[str, object] | None = None) -> Result[T]:
    """[EN]
    Executes the Success operation.
    Args:
        value: Input value for Success.
        metadata: Input value for Success.
    
    Returns:
        Result produced by the operation.
    
    [JA]
    Success 操作を実行します。
    引数:
        value: Success に渡す入力値です。
        metadata: Success に渡す入力値です。
    
    戻り値:
        操作によって生成される結果です。
    """
    return Result.success(value, metadata=metadata)


def Failure(error: object, metadata: Mapping[str, object] | None = None) -> Result[T]:
    """[EN]
    Executes the Failure operation.
    Args:
        error: Input value for Failure.
        metadata: Input value for Failure.
    
    Returns:
        Result produced by the operation.
    
    [JA]
    Failure 操作を実行します。
    引数:
        error: Failure に渡す入力値です。
        metadata: Failure に渡す入力値です。
    
    戻り値:
        操作によって生成される結果です。
    """
    return Result.failure(error, metadata=metadata)


def Some(value: T) -> Option[T]:
    """[EN]
    Executes the Some operation.
    Args:
        value: Input value for Some.
    
    Returns:
        Result produced by the operation.
    
    [JA]
    Some 操作を実行します。
    引数:
        value: Some に渡す入力値です。
    
    戻り値:
        操作によって生成される結果です。
    """
    return Option.some(value)


def Nothing() -> Option[T]:
    """[EN]
    Executes the Nothing operation.
    Returns:
        Result produced by the operation.
    
    [JA]
    Nothing 操作を実行します。
    戻り値:
        操作によって生成される結果です。
    """
    return Option.none()


def Right(value: R) -> Either[L, R]:
    """[EN]
    Executes the Right operation.
    Args:
        value: Input value for Right.
    
    Returns:
        Result produced by the operation.
    
    [JA]
    Right 操作を実行します。
    引数:
        value: Right に渡す入力値です。
    
    戻り値:
        操作によって生成される結果です。
    """
    return Either.right(value)


def Left(value: L) -> Either[L, R]:
    """[EN]
    Executes the Left operation.
    Args:
        value: Input value for Left.
    
    Returns:
        Result produced by the operation.
    
    [JA]
    Left 操作を実行します。
    引数:
        value: Left に渡す入力値です。
    
    戻り値:
        操作によって生成される結果です。
    """
    return Either.left(value)


def async_result(value: Result[T] | Awaitable[Result[T]] | AsyncResult[T]) -> AsyncResult[T]:
    """[EN]
    Executes the async result operation.
    Args:
        value: Input value for async result.
    
    Returns:
        Result produced by the operation.
    
    [JA]
    async result 操作を実行します。
    引数:
        value: async result に渡す入力値です。
    
    戻り値:
        操作によって生成される結果です。
    """
    if isinstance(value, AsyncResult):
        return value

    async def run() -> Result[T]:
        """[EN]
        Executes the run operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        run 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return await _resolve_result(value)

    return AsyncResult(run())


def async_option(value: Option[T] | Awaitable[Option[T]] | AsyncOption[T]) -> AsyncOption[T]:
    """[EN]
    Executes the async option operation.
    Args:
        value: Input value for async option.
    
    Returns:
        Result produced by the operation.
    
    [JA]
    async option 操作を実行します。
    引数:
        value: async option に渡す入力値です。
    
    戻り値:
        操作によって生成される結果です。
    """
    if isinstance(value, AsyncOption):
        return value

    async def run() -> Option[T]:
        """[EN]
        Executes the run operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        run 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return await _resolve_option(value)

    return AsyncOption(run())


def async_either(value: Either[L, R] | Awaitable[Either[L, R]] | AsyncEither[L, R]) -> AsyncEither[L, R]:
    """[EN]
    Executes the async either operation.
    Args:
        value: Input value for async either.
    
    Returns:
        Result produced by the operation.
    
    [JA]
    async either 操作を実行します。
    引数:
        value: async either に渡す入力値です。
    
    戻り値:
        操作によって生成される結果です。
    """
    if isinstance(value, AsyncEither):
        return value

    async def run() -> Either[L, R]:
        """[EN]
        Executes the run operation.
        Returns:
            Result produced by the operation.
        
        [JA]
        run 操作を実行します。
        戻り値:
            操作によって生成される結果です。
        """
        return await _resolve_either(value)

    return AsyncEither(run())


class _Try:
    def __call__(self, thunk: Callable[[], T], metadata: Mapping[str, object] | None = None) -> Result[T]:
        return self.run(thunk, metadata=metadata)

    @staticmethod
    def run(thunk: Callable[[], T], metadata: Mapping[str, object] | None = None) -> Result[T]:
        """[EN]
        Executes the run operation.
        Args:
            thunk: Input value for run.
            metadata: Input value for run.
        
        Returns:
            Result produced by the operation.
        
        [JA]
        run 操作を実行します。
        引数:
            thunk: run に渡す入力値です。
            metadata: run に渡す入力値です。
        
        戻り値:
            操作によって生成される結果です。
        """
        try:
            return Success(thunk(), metadata=metadata)
        except Exception as ex:  # noqa: BLE001 - Try converts exceptions to Failure.
            return Failure(ex, metadata=metadata)


Try = _Try()


def do(monad_type: type[Result] | type[Option] | type[Either]):
    """[EN]
    Executes the do operation.
    Args:
        monad_type: Input value for do.
    
    Returns:
        None.
    
    [JA]
    do 操作を実行します。
    引数:
        monad_type: do に渡す入力値です。
    
    戻り値:
        ありません。
    """
    if monad_type not in (Result, Option, Either):
        raise TypeError("do currently supports Result, Option, and Either.")

    state = _DoState(kind=monad_type)

    def decorator(func):
        @wraps(func)
        def wrapper(*args, **kwargs):
            """[EN]
            Executes the wrapper operation.
            Args:
                args: Input value for wrapper.
                kwargs: Input value for wrapper.
            
            Returns:
                None.
            
            [JA]
            wrapper 操作を実行します。
            引数:
                args: wrapper に渡す入力値です。
                kwargs: wrapper に渡す入力値です。
            
            戻り値:
                ありません。
            """
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


def async_do(monad_type: type[Result] | type[Option] | type[Either]):
    """[EN]
    Executes the async do operation.
    Args:
        monad_type: Input value for async do.
    
    Returns:
        None.
    
    [JA]
    async do 操作を実行します。
    引数:
        monad_type: async do に渡す入力値です。
    
    戻り値:
        ありません。
    """
    if monad_type not in (Result, Option, Either):
        raise TypeError("async_do currently supports Result, Option, and Either.")

    state = _DoState(kind=monad_type)

    def decorator(func):
        @wraps(func)
        def wrapper(*args, **kwargs):
            """[EN]
            Executes the wrapper operation.
            Args:
                args: Input value for wrapper.
                kwargs: Input value for wrapper.
            
            Returns:
                None.
            
            [JA]
            wrapper 操作を実行します。
            引数:
                args: wrapper に渡す入力値です。
                kwargs: wrapper に渡す入力値です。
            
            戻り値:
                ありません。
            """
            if state.kind is Result:
                return AsyncResult(_run_async_result_do(state, func, args, kwargs))
            if state.kind is Either:
                return AsyncEither(_run_async_either_do(state, func, args, kwargs))

            return AsyncOption(_run_async_option_do(state, func, args, kwargs))

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


async def _run_async_result_do(state: _DoState, func, args, kwargs):
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

            result = await _resolve_result(monad, metadata=metadata)
            if result.is_err:
                return result

            metadata.update(result.metadata)
            current = result.unwrap()
    except Exception as ex:  # noqa: BLE001 - AsyncResult do notation is fail-closed.
        return Failure(ex, metadata=metadata)


async def _run_async_option_do(state: _DoState, func, args, kwargs):
    yielded = func(*args, **kwargs)
    if not isinstance(yielded, Generator):
        return _pure(state, yielded)

    current = None
    while True:
        try:
            monad = yielded.send(current)
        except StopIteration as stop:
            return _pure(state, stop.value)

        option = await _resolve_option(monad)
        if option.is_none:
            return option

        current = option.unwrap()


async def _run_async_either_do(state: _DoState, func, args, kwargs):
    yielded = func(*args, **kwargs)
    if not isinstance(yielded, Generator):
        return _pure(state, yielded)

    current = None
    while True:
        try:
            monad = yielded.send(current)
        except StopIteration as stop:
            return _pure(state, stop.value)

        either = await _resolve_either(monad)
        if either.is_left:
            return either

        current = either.unwrap()


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
