from __future__ import annotations

import aikernel_net
from aikernel_net import (
    load_provider_manifest,
    provider_manifest_from_dict,
    rom_storage_contract,
    vfs_git_contract,
)


def test_provider_manifest_from_dict_preserves_cli_metadata() -> None:
    manifest = provider_manifest_from_dict(
        {
            "providerId": "openai.chat",
            "name": "Chat OpenAI",
            "version": "0.1.0",
            "capabilities": ["chat.completion", "embedding"],
            "metadata": {"model": "gpt-4o", "endpoint": "https://api.openai.com/v1"},
            "assembly": "ChatOpenAIProvider.dll",
            "cli": {
                "command": "dynamic-pipeline",
                "defaultOperation": "chat.completion",
                "configKeys": ["model"],
                "requiredEnvironment": ["OPENAI_API_KEY"],
            },
        }
    )

    assert manifest.provider_id == "openai.chat"
    assert manifest.capabilities == ("chat.completion", "embedding")
    assert manifest.assembly == "ChatOpenAIProvider.dll"
    assert manifest.to_capability_metadata()["cli.command"] == "dynamic-pipeline"
    assert manifest.to_capability_metadata()["cli.env.OPENAI_API_KEY"] == "OPENAI_API_KEY"


def test_provider_manifest_loader_fails_closed_without_capabilities(tmp_path) -> None:
    path = tmp_path / "provider.json"
    path.write_text(
        """
        {
          "providerId": "empty",
          "name": "Empty",
          "version": "0.1.0",
          "capabilities": []
        }
        """,
        encoding="utf-8",
    )

    try:
        load_provider_manifest(path)
    except ValueError as error:
        assert "at least one capability" in str(error)
    else:
        raise AssertionError("Provider manifest without capabilities should fail closed")


def test_provider_manifest_preserves_capability_order() -> None:
    manifest = provider_manifest_from_dict(
        {
            "providerId": "ordered",
            "name": "Ordered",
            "version": "0.1.0",
            "capabilities": ["moderation", "chat.completion", "embedding"],
        }
    )

    assert manifest.capabilities == ("moderation", "chat.completion", "embedding")


def test_core_owned_rom_storage_contract_descriptor() -> None:
    contract = rom_storage_contract(
        "aikernel.rom.storage",
        "memory",
        {"version": "0.1.0.2"},
    )

    assert contract.name == "ROM Storage"
    assert contract.entry_point == "AIKernel.Core.Storage"
    assert contract.operations == ("rom.save", "rom.load", "rom.list")
    assert contract.permissions == ("rom.read", "rom.write")
    assert contract.metadata["storageScheme"] == "memory"


def test_core_owned_vfs_git_contract_descriptor() -> None:
    contract = vfs_git_contract(
        "aikernel.vfs.git",
        "readonly",
        {"version": "0.1.0.2"},
    )

    assert contract.name == "VFS Git"
    assert contract.entry_point == "AIKernel.Core.Vfs.VfsGit"
    assert contract.operations == ("vfs.git.read", "vfs.git.list", "vfs.git.checkout")
    assert contract.permissions == ("git.read", "vfs.read")
    assert contract.metadata["repositoryMode"] == "readonly"


def test_provider_manifest_and_core_contract_api_is_exported() -> None:
    for name in (
        "ProviderManifest",
        "ProviderCliManifest",
        "load_provider_manifest",
        "provider_manifest_from_dict",
        "CoreCapabilityModuleContract",
        "rom_storage_contract",
        "vfs_git_contract",
    ):
        assert name in aikernel_net.__all__
        assert hasattr(aikernel_net, name)
