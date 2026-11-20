# Prepare a Cosmos 3 checkpoint for CPU inference

Use the Edge reasoner for a first text, image or video-to-text request. Its imported BF16 tensor payload is 4,871,232,992 bytes (4.87 GB). Nano's is 17,534,247,392 bytes (17.53 GB). Allow extra disk space for the original download shards, temporary download files and the imported files. Working memory and the KV cache are separate budgets.

Use a Python environment with PyTorch, `huggingface_hub` and a Cosmos 3-capable Transformers build. The recorded import used Torch 2.15.0+cpu and Transformers commit `3a134bc37f7c22c801b618e289b0255425f22647`. The CPU runtime itself runs in .NET.

From the AIKernel.Core checkout in Bash, import the model you intend to run. The two commands are alternatives. Use the same `AIKERNEL_STATE` location when configuring Monolith.

```bash
export AIKERNEL_STATE="$HOME/aikernel-local"
python tools/models/import_cosmos3.py Edge "$AIKERNEL_STATE/models/edge"
```

```bash
python tools/models/import_cosmos3.py Nano "$AIKERNEL_STATE/models/nano"
```

The importer pins the Edge revision to `6afc1256321c3a8e623d89be3bc5117a1e90f96b` and Nano to `45666784803b3a7ad577e0def405f9afcc3bddb2`. It checks source shard hashes against the publisher's LFS metadata, verifies tensor shapes and writes `reasoner-*.safetensors`, `model.safetensors.index.json` and `import-manifest.json` alongside the tokenizer, template and configuration. Keep the imported directory read-only to API callers.

For Edge, follow the [Monolith setup](https://github.com/AIKernel-NET/AIKernel.Monolith/blob/dev/README.md#run-the-monolith) to install the example registration. Its installer compares the manifest with the recorded Edge import and pins the byte digest on your filesystem. For another import, obtain an independently trusted manifest digest before registering it.

For the recorded four-worker test on Server, a 7,000,000,000-byte host budget, a 3 GiB weight cache and a 128 MB KV cache yielded a peak process RSS of 3.564 GB across six text, image and video requests. The host's cooperative memory budget and the operating system's hard memory limit are separate settings. [Test inputs, generated tokens and storage measurements (Japanese)](development/evidence/cosmos3-server-7gb-20261120/README.md) · [SDK examples (Japanese)](cosmos3-inference.md)
