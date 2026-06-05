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
from .monads import (
    Failure,
    Nothing,
    Option,
    Result,
    Some,
    Success,
    Try,
    do,
)

__all__ = [
    "AIKernelNativeError",
    "Failure",
    "ForwardResult",
    "Nothing",
    "Option",
    "Result",
    "Some",
    "Success",
    "Try",
    "do",
    "forward",
    "forward_result",
    "load_model",
    "load_model_result",
    "unload_model",
    "unload_model_result",
]

__version__ = "0.0.5"
