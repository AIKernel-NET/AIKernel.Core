"""[EN]
Provider manifest descriptors for dynamic AIKernel provider loading.

[JA]
AIKernel provider dynamic loading のための provider manifest descriptor です。
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class ProviderCliManifest:
    """[EN]
    Describes CLI-facing settings embedded in a provider manifest.

    [JA]
    provider manifest に含まれる CLI 向け設定を表します。
    """

    command: str = ""
    default_operation: str = ""
    config_keys: tuple[str, ...] = ()
    required_environment: tuple[str, ...] = ()


@dataclass(frozen=True)
class ProviderManifest:
    """[EN]
    Describes an external provider manifest without loading the provider.

    [JA]
    provider を読み込まずに外部 provider manifest を表します。
    """

    provider_id: str
    name: str
    version: str
    capabilities: tuple[str, ...]
    metadata: dict[str, str] = field(default_factory=dict)
    assembly: str | None = None
    cli: ProviderCliManifest | None = None

    def to_capability_metadata(self) -> dict[str, str]:
        """[EN]
        Returns deterministic metadata matching the Core dynamic registry shape.

        [JA]
        Core dynamic registry の形に合わせた決定論的 metadata を返します。
        """

        items = dict(sorted(self.metadata.items()))
        if self.cli is not None:
            if self.cli.command:
                items["cli.command"] = self.cli.command
            if self.cli.default_operation:
                items["cli.default_operation"] = self.cli.default_operation
            for key in sorted(self.cli.config_keys):
                items[f"cli.config.{key}"] = key
            for variable in sorted(self.cli.required_environment):
                items[f"cli.env.{variable}"] = variable
        return items


def load_provider_manifest(path: str | Path) -> ProviderManifest:
    """[EN]
    Loads and validates a provider manifest JSON file.

    [JA]
    provider manifest JSON file を読み込み検証します。
    """

    document = json.loads(Path(path).read_text(encoding="utf-8"))
    if not isinstance(document, dict):
        raise ValueError("Provider manifest must be a JSON object.")
    return provider_manifest_from_dict(document)


def provider_manifest_from_dict(document: dict[str, Any]) -> ProviderManifest:
    """[EN]
    Converts a dictionary into a validated provider manifest descriptor.

    [JA]
    dictionary を検証済み provider manifest descriptor に変換します。
    """

    provider_id = _required_string(document, "providerId")
    name = _required_string(document, "name")
    version = _required_string(document, "version")
    capabilities = _string_tuple(document.get("capabilities", ()))
    if not capabilities:
        raise ValueError("Provider manifest must declare at least one capability.")

    metadata = {
        str(key): str(value)
        for key, value in (document.get("metadata") or {}).items()
    }
    cli = _read_cli(document.get("cli"))

    return ProviderManifest(
        provider_id=provider_id,
        name=name,
        version=version,
        capabilities=capabilities,
        metadata=dict(sorted(metadata.items())),
        assembly=_optional_string(document.get("assembly")),
        cli=cli,
    )


def _read_cli(value: Any) -> ProviderCliManifest | None:
    """[EN]
    Reads optional CLI manifest settings from a decoded JSON value.

    [JA]
    decode 済み JSON value から任意の CLI manifest setting を読み取ります。
    """

    if value is None:
        return None
    if not isinstance(value, dict):
        raise ValueError("Provider manifest cli must be an object.")
    return ProviderCliManifest(
        command=_optional_string(value.get("command")) or "",
        default_operation=_optional_string(value.get("defaultOperation")) or "",
        config_keys=_string_tuple(value.get("configKeys", ())),
        required_environment=_string_tuple(value.get("requiredEnvironment", ())),
    )


def _required_string(document: dict[str, Any], key: str) -> str:
    """[EN]
    Reads a required non-empty string field from a manifest dictionary.

    [JA]
    manifest dictionary から必須の非空 string field を読み取ります。
    """

    value = _optional_string(document.get(key))
    if value is None:
        raise ValueError(f"Provider manifest field is required: {key}")
    return value


def _optional_string(value: Any) -> str | None:
    """[EN]
    Normalizes an optional JSON value into a non-empty string when present.

    [JA]
    任意の JSON value を、存在する場合に非空 string へ正規化します。
    """

    if value is None:
        return None
    text = str(value).strip()
    return text or None


def _string_tuple(value: Any) -> tuple[str, ...]:
    """[EN]
    Converts a JSON array value into a string tuple while preserving manifest order.

    [JA]
    JSON array value を manifest order を保った string tuple に変換します。
    """

    if value is None:
        return ()
    if not isinstance(value, list | tuple):
        raise ValueError("Provider manifest list field must be an array.")
    return tuple(text for item in value if (text := str(item).strip()))
