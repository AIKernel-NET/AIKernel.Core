from __future__ import annotations

import aikernel_net


def test_ctg_rom_sample_is_exported() -> None:
    assert "ctg_rom_sample_path" in aikernel_net.__all__
    assert "ctg_rom_sample_files" in aikernel_net.__all__


def test_ctg_rom_sample_contains_monolith_canon_and_locales() -> None:
    files = aikernel_net.ctg_rom_sample_files()

    assert len(files) == 16
    assert "governance/ctg.monolith.canon.md" in files
    assert "governance/ctg.monolith.canon.ja.md" in files
    assert "locales/en-US/ctg.monolith.minimal.en.yaml" in files
    assert "locales/ja-JP/ctg.monolith.minimal.ja.yaml" in files
