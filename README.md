# GraphRL Construction Scheduling

Author: wyf

<img width="3837" height="1504" alt="Overall framework" src="https://github.com/user-attachments/assets/2a7a021d-f147-4232-8761-0a4a8874b2a2" />

This repository provides a complete research implementation for graph-based
multi-agent construction task scheduling. It includes a custom Unity scheduling
environment and construction scene, task data and conflict-aware coordination
logic, graph observations, a dedicated Python training extension, and editor
training configurations.

## Requirements

- Unity `6000.0.46f1` (the version used to validate this release);
- Python `3.10.1` to `3.10.12`;

## Directory layout

- `SchedulingProject/`: Unity project; open this folder in Unity Hub.
- `com.unity.ml-agents/` and `com.unity.ml-agents.extensions/`: local Unity
  packages referenced by the project. Keep them beside `SchedulingProject/`.
- `python/mlagents-wyf/`: custom Python training package, version
  `1.1.0.post1`.
- `config/scheduling_86_editor.yaml`: editor training configuration for the
  included example.

## Training in Unity Editor

1. Install the included package in the intended Python environment:

   ```bat
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
<img width="2025" height="1023" alt="屏幕截图 2026-08-16 213821" src="https://github.com/user-attachments/assets/b2bcfb61-d822-453d-bab9-6fd36242064a" />

Results are written to `results/`, which is intentionally ignored by version
control.

## License and notices

The original Python-side training extensions, Unity-side scheduling
implementation, construction scenes, task data, and configurations are released
under the MIT License; see `LICENSE`. Third-party components retain their
respective licenses; see `LICENSE.md` and `Third Party Notices.md`.
