# AIKernel Provider Placement

Reference this package alongside AIKernel 0.1.4. Use `ProviderPlacementScheduler` to match a request, node capabilities, and a separate registry snapshot. CPU support is required. CUDA is an additional target on nodes that advertise a compatible device. Feasibility uses the available RAM and AIKernel-managed swap; physical VRAM size does not disqualify a node.

Candidates are ranked by model residency, CUDA availability, active jobs, queued jobs, and ordinal node ID. Device IDs break ties between CUDA devices on the same node. The scheduler returns stable exclusion codes and never includes request content. A host can use `RequireCandidate` to return the standard no-candidate error.

Use `LocalNodeCapabilityDescriptorSource` with the host's RAM, GPU, AIKernel swap, and job observations. Use `ProviderScoreOrdering` for best-first sorting with ties in original input order. The result retains the model's declared score semantics and values.
