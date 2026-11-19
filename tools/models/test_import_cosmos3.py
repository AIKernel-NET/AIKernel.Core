"""Network-free checks for checkpoint preparation safety."""
import hashlib
from pathlib import Path
import tempfile
import unittest
from import_cosmos3 import fetch, rename


class ImportTests(unittest.TestCase):
    def test_wrong_hash_never_promotes_download(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);source=root/"input";source.write_bytes(b"checkpoint")
            output=root/"output"
            with self.assertRaisesRegex(ValueError,"SHA-256"):
                fetch(source.as_uri(),output,"0"*64)
            self.assertFalse(output.exists())
            fetch(source.as_uri(),output,hashlib.sha256(b"checkpoint").hexdigest())
            self.assertEqual(output.read_bytes(),b"checkpoint")

    def test_metadata_refresh_replaces_stale_snapshot(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);source=root/"source";output=root/"output"
            source.write_bytes(b"current");output.write_bytes(b"stale")
            fetch(source.as_uri(),output,refresh=True)
            self.assertEqual(output.read_bytes(),b"current")

    def test_official_names_remain_family_specific(self):
        self.assertEqual(rename("layers.0.mlp.up_proj.weight",True),"model.language_model.layers.0.mlp.fc1.weight")
        self.assertEqual(rename("layers.0.mlp.up_proj.weight",False),"model.language_model.layers.0.mlp.up_proj.weight")
        self.assertEqual(rename("layers.0.self_attn.norm_q.weight",False),"model.language_model.layers.0.self_attn.q_norm.weight")
        self.assertEqual(rename("blocks.0.attn.qkv.weight",False),"model.visual.blocks.0.attn.qkv.weight")
        self.assertEqual(rename("model.visual.encoder.layers.0.mlp.fc1.weight",True),"model.visual.encoder.layers.0.mlp.fc1.weight")


if __name__=="__main__":unittest.main()
