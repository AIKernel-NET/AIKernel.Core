"""[EN]
Reference module for aikernel_net.__init__.

[JA]
aikernel_net.__init__ の参照モジュールです。
"""

from .managed import (
    ManagedAssemblySet,
    RuntimeLayout,
    managed_assemblies,
    require_managed_assemblies,
    runtime_layout,
)
from .monads import (
    AsyncEither,
    AsyncOption,
    AsyncResult,
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

__all__ = [
    "AsyncEither",
    "AsyncOption",
    "AsyncResult",
    "Either",
    "Failure",
    "Left",
    "ManagedAssemblySet",
    "Nothing",
    "Option",
    "Result",
    "RuntimeLayout",
    "Right",
    "Some",
    "Success",
    "Try",
    "async_do",
    "async_either",
    "async_option",
    "async_result",
    "do",
    "managed_assemblies",
    "require_managed_assemblies",
    "runtime_layout",
]

__version__ = "0.1.0"
