from __future__ import annotations

import aikernel_net
from aikernel_net import (
    LOCAL_EXECUTION_PROVIDER,
    MINIMAL_RUNTIME_PROVIDER,
    SKILL_PROVIDER,
    SYSTEM_INFO_PROVIDER,
    VFS_PROVIDER,
    standard_capability,
    standard_provider,
    standard_provider_contracts,
    standard_provider_managed_types,
)


def test_standard_provider_contracts_are_exported_in_boot_order() -> None:
    assert standard_provider_contracts() == (
        MINIMAL_RUNTIME_PROVIDER,
        LOCAL_EXECUTION_PROVIDER,
        VFS_PROVIDER,
        SKILL_PROVIDER,
        SYSTEM_INFO_PROVIDER,
    )
    assert "standard_provider_contracts" in aikernel_net.__all__


def test_standard_provider_lookup_resolves_identity_and_capability() -> None:
    provider = standard_provider("aikernel.local")

    assert provider.name == "Local Execution Provider"
    assert provider.capabilities == (provider.capability,)
    assert provider.capability.capability_id == "aikernel.local.execute"
    assert provider.capability.operations == ("pipeline.execute",)
    assert provider.capability.kind == "Execution"
    assert provider.capability.invocation_mode == "Inline"


def test_standard_capability_names_match_core_descriptors() -> None:
    assert standard_capability("aikernel.runtime.ping").name == "Minimal Runtime Ping"
    assert standard_capability("aikernel.vfs").name == "Virtual File System Read"


def test_standard_capability_lookup_resolves_vfs_operations() -> None:
    capability = standard_capability("aikernel.vfs")

    assert capability.provider_id == "aikernel.vfs"
    assert capability.operations == (
        "vfs.exists",
        "vfs.list",
        "vfs.metadata",
        "vfs.read_file",
    )
    assert "storage" in capability.tags


def test_standard_capability_lookup_preserves_legacy_vfs_alias() -> None:
    assert standard_capability("aikernel.vfs.read") is standard_capability("aikernel.vfs")


def test_standard_capability_lookup_resolves_system_info_operations() -> None:
    capability = standard_capability("aikernel.system.info")

    assert capability.operations == (
        "system.capabilities",
        "system.info",
        "system.providers",
        "system.runtime",
        "system.vfs",
    )
    assert capability.managed_invoker_type == "AIKernel.Core.Providers.SystemInfoProvider.SystemInfoInvoker"


def test_standard_provider_managed_types_include_provider_invoker_and_contracts() -> None:
    managed_types = standard_provider_managed_types()

    assert "AIKernel.Core.Providers.SystemInfoProvider.SystemInfoProvider" in managed_types
    assert "AIKernel.Core.Providers.SystemInfoProvider.SystemInfoInvoker" in managed_types
    assert "AIKernel.Core.Providers.SystemInfoProvider.SystemInfoCapabilityContracts" in managed_types
    assert "AIKernel.Core.Providers.SkillProvider.SkillProvider" in managed_types


def test_skill_provider_exposes_dynamic_skill_capabilities() -> None:
    assert SKILL_PROVIDER.capabilities == ()
    assert SKILL_PROVIDER.managed_provider_type == "AIKernel.Core.Providers.SkillProvider.SkillProvider"
    assert SKILL_PROVIDER.managed_invoker_type == "AIKernel.Core.Providers.SkillProvider.SkillProvider"


def test_standard_provider_lookup_fails_closed_for_unknown_ids() -> None:
    try:
        standard_provider("unknown")
    except KeyError as error:
        assert "Unknown AIKernel standard provider" in str(error)
    else:
        raise AssertionError("standard_provider should fail closed for unknown provider IDs")


def test_standard_capability_lookup_fails_closed_for_unknown_ids() -> None:
    try:
        standard_capability("unknown")
    except KeyError as error:
        assert "Unknown AIKernel standard capability" in str(error)
    else:
        raise AssertionError("standard_capability should fail closed for unknown capability IDs")
