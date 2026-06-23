"""[EN]
Public package surface for the aikernel-net Python binding.

[JA]
aikernel-net Python binding の公開 package surface です。
"""

from .api_catalog import (
    ManagedMemberDescriptor,
    ManagedTypeDescriptor,
    find_managed_type,
    managed_api_catalog,
    managed_api_summary,
    managed_type_names,
)
from .managed import (
    ManagedAssemblySet,
    RuntimeLayout,
    managed_assemblies,
    require_managed_assemblies,
    runtime_layout,
)
from .core_contracts import (
    CoreCapabilityModuleContract,
    rom_storage_contract,
    vfs_git_contract,
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
from .standard_providers import (
    LOCAL_EXECUTION_PROVIDER,
    MINIMAL_RUNTIME_PROVIDER,
    SKILL_PROVIDER,
    SYSTEM_INFO_PROVIDER,
    VFS_PROVIDER,
    CapabilityContract,
    StandardProviderContract,
    standard_capability,
    standard_provider,
    standard_provider_contracts,
    standard_provider_managed_types,
)
from .provider_manifest import (
    ProviderCliManifest,
    ProviderManifest,
    load_provider_manifest,
    provider_manifest_from_dict,
)
from .samples import (
    ctg_rom_sample_files,
    ctg_rom_sample_path,
)

__all__ = [
    "ManagedMemberDescriptor",
    "ManagedTypeDescriptor",
    "find_managed_type",
    "managed_api_catalog",
    "managed_api_summary",
    "managed_type_names",
    "AsyncEither",
    "AsyncOption",
    "AsyncResult",
    "Either",
    "Failure",
    "Left",
    "LOCAL_EXECUTION_PROVIDER",
    "CoreCapabilityModuleContract",
    "ManagedAssemblySet",
    "MINIMAL_RUNTIME_PROVIDER",
    "ctg_rom_sample_files",
    "ctg_rom_sample_path",
    "Nothing",
    "Option",
    "ProviderCliManifest",
    "ProviderManifest",
    "Result",
    "RuntimeLayout",
    "Right",
    "SKILL_PROVIDER",
    "SYSTEM_INFO_PROVIDER",
    "Some",
    "Success",
    "CapabilityContract",
    "StandardProviderContract",
    "Try",
    "VFS_PROVIDER",
    "async_do",
    "async_either",
    "async_option",
    "async_result",
    "do",
    "load_provider_manifest",
    "managed_assemblies",
    "provider_manifest_from_dict",
    "require_managed_assemblies",
    "rom_storage_contract",
    "runtime_layout",
    "standard_capability",
    "standard_provider",
    "standard_provider_contracts",
    "standard_provider_managed_types",
    "vfs_git_contract",
]

__version__ = "0.1.3"
