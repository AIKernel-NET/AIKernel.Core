"""[EN]
CTG-ROM sample asset helpers for package users.

[JA]
package user 向けの CTG-ROM sample asset helper です。
"""

from __future__ import annotations

from importlib import resources


def ctg_rom_sample_path() -> resources.abc.Traversable:
    """[EN]
    Return the package resource root containing the bundled CTG-ROM sample.

    [JA]
    同梱 CTG-ROM sample を含む package resource root を返します。
    """

    return resources.files(__package__)


def ctg_rom_sample_files() -> tuple[str, ...]:
    """[EN]
    Return deterministic relative paths for bundled CTG-ROM sample files.

    [JA]
    同梱 CTG-ROM sample file の相対 path を決定論的な順序で返します。
    """

    root = ctg_rom_sample_path()
    files: list[str] = []
    _collect_files(root, root, files)
    return tuple(sorted(files))


def _collect_files(
    root: resources.abc.Traversable,
    current: resources.abc.Traversable,
    files: list[str],
) -> None:
    for item in current.iterdir():
        if item.is_dir():
            _collect_files(root, item, files)
        elif item.name.endswith((".md", ".yaml")):
            files.append(_relative_name(root, item))


def _relative_name(
    root: resources.abc.Traversable,
    item: resources.abc.Traversable,
) -> str:
    root_text = str(root).replace("\\", "/").rstrip("/")
    item_text = str(item).replace("\\", "/")
    if item_text.startswith(root_text + "/"):
        return item_text[len(root_text) + 1 :]
    return item.name


__all__ = [
    "ctg_rom_sample_files",
    "ctg_rom_sample_path",
]
