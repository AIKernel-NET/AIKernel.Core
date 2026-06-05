"""AIKernel Python bindings."""

from .bindings import (
    AIKernelNativeError,
    ForwardResult,
    forward,
    forward_result,
    load_model,
    load_model_result,
    unload_model,
    unload_model_result,
)
from .managed import (
    ManagedAssemblySet,
    RuntimeLayout,
    managed_assemblies,
    require_managed_assemblies,
    runtime_layout,
)
from .monads import (
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
    do,
)

__all__ = [
    "AIKernelNativeError",
    "Either",
    "Failure",
    "ForwardResult",
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
    "do",
    "forward",
    "forward_result",
    "load_model",
    "load_model_result",
    "managed_assemblies",
    "require_managed_assemblies",
    "runtime_layout",
    "unload_model",
    "unload_model_result",
]

__version__ = "0.0.5"
