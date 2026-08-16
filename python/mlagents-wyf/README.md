# Graph Scheduling ML-Agents (wyf)

This repository contains the Python side of the graph-based construction-task
scheduling environment. It is derived from Unity ML-Agents `release_22`
(`mlagents`/`mlagents-envs` 1.1.0) and adds a relational graph encoder for the
86-, 172-, and 226-task experiments.

## What is customized

- a two-dimensional graph observation property shared with the Unity package;
- an RGCN actor/critic encoder with forward precedence, reverse precedence,
  and spatial-conflict relation types;
- task-shared discrete logits for graph observations while retaining the
  standard ML-Agents categorical head for ordinary vector observations;
- packaged, size-specific graph topology data selected from observation shape;
- a larger gRPC message limit for graph observations.

All custom source files and extension points identify `wyf` as the author.

## Installation

Use Python 3.10. Install the PyTorch build that matches the Linux server's CUDA
runtime first, then install this package:

```bash
python -m venv .venv
source .venv/bin/activate
python -m pip install --upgrade pip
python -m pip install -e .
```

The package contains both `mlagents` and `mlagents_envs`; do not install a
second copy of the official packages in the same environment.

## Training

The same configuration works for all supported scales. The executable's scene
and observation shape select the matching graph topology automatically.

```bash
mlagents-learn configs/rgcn_ppo.yaml \
  --env /path/to/86-Graph-Linux/86-Graph-Linux.x86_64 \
  --run-id rgcn-86 \
  --train
```

Replace the executable and run ID with the 172- or 226-task build as needed.

## Scale consistency checks

At startup, the Python encoder checks the supported node count, CSV row count,
unique task IDs, referenced task IDs, acyclic precedence graph, and one action
per task node.

## License and attribution

The upstream ML-Agents code remains under Apache License 2.0. See `LICENSE.md`,
`Third Party Notices.md`, and `NOTICE.md`. Custom modifications are authored by
wyf.
