"""[EN]
Public Python descriptors for AIKernel.Core standard providers.

[JA]
AIKernel.Core 標準 Provider の公開 Python ディスクリプターです。
"""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class CapabilityContract:
    """[EN]
    Describes a public AIKernel capability contract exposed by a standard provider.

    [JA]
    標準 Provider が公開する AIKernel Capability 契約を表します。
    """

    capability_id: str
    provider_id: str
    name: str
    version: str
    operations: tuple[str, ...]
    kind: str
    invocation_mode: str
    tags: tuple[str, ...]
    managed_contract_type: str
    managed_invoker_type: str


@dataclass(frozen=True)
class StandardProviderContract:
    """[EN]
    Describes the public identity and capability surface of a standard provider.
    The managed invoker type is present when the provider itself exposes dynamic
    capability invocation without a fixed static capability descriptor.

    [JA]
    標準 Provider の公開 identity と Capability surface を表します。
    provider 自身が固定の静的 Capability descriptor なしで動的 invocation を公開する場合、
    managed invoker type を保持します。
    """

    provider_id: str
    name: str
    version: str
    managed_provider_type: str
    managed_invoker_type: str | None = None
    capabilities: tuple[CapabilityContract, ...] = ()

    @property
    def capability(self) -> CapabilityContract:
        """[EN]
        Returns the single static capability for providers that expose exactly one.

        [JA]
        静的 Capability を 1 つだけ公開する Provider の Capability を返します。
        """

        if len(self.capabilities) != 1:
            raise AttributeError(f"{self.provider_id} does not expose one static capability")
        return self.capabilities[0]


MINIMAL_RUNTIME_PROVIDER = StandardProviderContract(
    provider_id="aikernel.runtime.minimal",
    name="Minimal Runtime Provider",
    version="1.0.0",
    managed_provider_type="AIKernel.Core.Providers.MinimalRuntimeProvider.MinimalRuntimeProvider",
    capabilities=(CapabilityContract(
        capability_id="aikernel.runtime.ping",
        provider_id="aikernel.runtime.minimal",
        name="Minimal Runtime Ping",
        version="1.0.0",
        operations=("runtime.ping",),
        kind="Utility",
        invocation_mode="Inline",
        tags=("runtime", "minimal", "ping"),
        managed_contract_type=(
            "AIKernel.Core.Providers.MinimalRuntimeProvider.MinimalRuntimeCapabilityContracts"
        ),
        managed_invoker_type="AIKernel.Core.Providers.MinimalRuntimeProvider.MinimalRuntimeInvoker",
    ),),
)
LOCAL_EXECUTION_PROVIDER = StandardProviderContract(
    provider_id="aikernel.local",
    name="Local Execution Provider",
    version="1.0.0",
    managed_provider_type="AIKernel.Core.Providers.LocalExecutionProvider.LocalExecutionProvider",
    capabilities=(CapabilityContract(
        capability_id="aikernel.local.execute",
        provider_id="aikernel.local",
        name="Local Execution",
        version="1.0.0",
        operations=("pipeline.execute",),
        kind="Execution",
        invocation_mode="Inline",
        tags=("local", "execution", "dsl"),
        managed_contract_type=(
            "AIKernel.Core.Providers.LocalExecutionProvider.LocalExecutionCapabilityContracts"
        ),
        managed_invoker_type="AIKernel.Core.Providers.LocalExecutionProvider.LocalExecutionInvoker",
    ),),
)
VFS_PROVIDER = StandardProviderContract(
    provider_id="aikernel.vfs",
    name="Virtual File System Provider",
    version="1.0.0",
    managed_provider_type="AIKernel.Core.Providers.VfsProvider.VfsProvider",
    capabilities=(CapabilityContract(
        capability_id="aikernel.vfs",
        provider_id="aikernel.vfs",
        name="Virtual File System Read",
        version="1.0.0",
        operations=("vfs.exists", "vfs.list", "vfs.metadata", "vfs.read_file"),
        kind="Storage",
        invocation_mode="Inline",
        tags=("vfs", "filesystem", "storage", "core"),
        managed_contract_type="AIKernel.Core.Providers.VfsProvider.VfsCapabilityContracts",
        managed_invoker_type="AIKernel.Core.Providers.VfsProvider.VfsInvoker",
    ),),
)
SKILL_PROVIDER = StandardProviderContract(
    provider_id="aikernel.skill",
    name="Skill Provider",
    version="1.0.0",
    managed_provider_type="AIKernel.Core.Providers.SkillProvider.SkillProvider",
    managed_invoker_type="AIKernel.Core.Providers.SkillProvider.SkillProvider",
)
SYSTEM_INFO_PROVIDER = StandardProviderContract(
    provider_id="aikernel.system",
    name="System Information Provider",
    version="1.0.0",
    managed_provider_type="AIKernel.Core.Providers.SystemInfoProvider.SystemInfoProvider",
    capabilities=(CapabilityContract(
        capability_id="aikernel.system.info",
        provider_id="aikernel.system",
        name="System Information",
        version="1.0.0",
        operations=(
            "system.capabilities",
            "system.info",
            "system.providers",
            "system.runtime",
            "system.vfs",
        ),
        kind="Utility",
        invocation_mode="Inline",
        tags=("system", "info", "introspection", "core"),
        managed_contract_type="AIKernel.Core.Providers.SystemInfoProvider.SystemInfoCapabilityContracts",
        managed_invoker_type="AIKernel.Core.Providers.SystemInfoProvider.SystemInfoInvoker",
    ),),
)

_STANDARD_PROVIDERS = (
    MINIMAL_RUNTIME_PROVIDER,
    LOCAL_EXECUTION_PROVIDER,
    VFS_PROVIDER,
    SKILL_PROVIDER,
    SYSTEM_INFO_PROVIDER,
)


def standard_provider_contracts() -> tuple[StandardProviderContract, ...]:
    """[EN]
    Returns the built-in standard provider contracts in boot registration order.

    [JA]
    組み込み標準 Provider 契約を boot registration order で返します。
    """

    return _STANDARD_PROVIDERS


def standard_provider(provider_id: str) -> StandardProviderContract:
    """[EN]
    Resolves a standard provider contract by provider ID.

    [JA]
    provider ID から標準 Provider 契約を解決します。
    """

    for provider in _STANDARD_PROVIDERS:
        if provider.provider_id == provider_id:
            return provider
    raise KeyError(f"Unknown AIKernel standard provider: {provider_id}")


def standard_capability(capability_id: str) -> CapabilityContract:
    """[EN]
    Resolves a standard capability contract by capability ID.

    [JA]
    capability ID から標準 Capability 契約を解決します。
    """

    canonical_id = _canonical_capability_id(capability_id)
    for provider in _STANDARD_PROVIDERS:
        for capability in provider.capabilities:
            if capability.capability_id == canonical_id:
                return capability
    raise KeyError(f"Unknown AIKernel standard capability: {capability_id}")


def standard_provider_managed_types() -> tuple[str, ...]:
    """[EN]
    Returns managed provider and invoker type names required for reflection loading.

    [JA]
    reflection loading に必要な managed provider / invoker type name を返します。
    """

    return tuple(
        managed_type
        for provider in _STANDARD_PROVIDERS
        for managed_type in _managed_types_for_provider(provider)
    )


def _managed_types_for_provider(provider: StandardProviderContract) -> tuple[str, ...]:
    """[EN]
    Returns managed type names associated with one provider descriptor.

    [JA]
    1 つの provider descriptor に紐づく managed type name を返します。
    """

    types: list[str] = []
    _append_unique(types, provider.managed_provider_type)
    if provider.managed_invoker_type is not None:
        _append_unique(types, provider.managed_invoker_type)
    for capability in provider.capabilities:
        _append_unique(types, capability.managed_invoker_type)
        _append_unique(types, capability.managed_contract_type)
    return tuple(types)


def _canonical_capability_id(capability_id: str) -> str:
    return "aikernel.vfs" if capability_id == "aikernel.vfs.read" else capability_id


def _append_unique(values: list[str], value: str) -> None:
    """[EN]
    Appends a managed type name while preserving deterministic first-seen order.

    [JA]
    managed type name を決定論的な初出順を保って追加します。
    """

    if value not in values:
        values.append(value)
