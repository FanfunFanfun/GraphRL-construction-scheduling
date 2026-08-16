# GraphRL Construction Scheduling Environment

Author: wyf

This repository provides a reproducible Unity ML-Agents implementation for
graph-based multi-agent construction task scheduling. It includes an example
scene and task CSV, the local Unity ML-Agents packages, the custom
`mlagents-wyf` Python package, and an editor training configuration.

## Requirements

- Unity `6000.0.46f1` (the version used to validate this release);
- Python `3.10.1` to `3.10.12`;
- a Conda environment such as `mla_env`.

## Directory layout

- `SchedulingProject/`: Unity project; open this folder in Unity Hub.
- `com.unity.ml-agents/` and `com.unity.ml-agents.extensions/`: local Unity
  packages referenced by the project. Keep them beside `SchedulingProject/`.
- `python/mlagents-wyf/`: custom Python training package, version
  `1.1.0.post1`.
- `config/scheduling_86_editor.yaml`: editor training configuration for the
  included example.

## Training in Unity Editor

1. Activate the intended environment and install the included package:

   ```bat
   conda activate mla_env
   cd /d <release-root>\python\mlagents-wyf
   python -m pip install -e .
   ```

2. Open `<release-root>\SchedulingProject` in Unity Hub, then open
   `Assets/ML-Agents/Examples/PyramidsChange/Scenes/86.unity`.

3. Start the trainer from the release root:

   ```bat
   mlagents-learn config\scheduling_86_editor.yaml
   ```

4. When the trainer starts listening on port 5004, press Play in Unity.

The configuration uses `time_scale: 30`. Results are written to `results/`,
which is intentionally ignored by version control.

## Publishing note

`Robot_03_(White)_ok.fbx` is a required scene dependency and is about 175 MB.
For GitHub, use Git LFS as declared in `.gitattributes`; a ZIP release can be
uploaded without Git LFS. Confirm that the license of every redistributed
third-party asset permits redistribution before publishing.

## License and notices

Project-specific original contributions are released under the MIT License; see
`LICENSE`. Included Unity ML-Agents code and files derived from it remain under
Apache License 2.0; see `LICENSE.md`, `NOTICE.md`, and `Third Party Notices.md`.
