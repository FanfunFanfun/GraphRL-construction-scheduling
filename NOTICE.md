# Modification notice

This project is derived from Unity ML-Agents release_22. Upstream copyright and
license notices are retained. The scheduling environment, graph sensor
extension, experiment-scale resolver, and subsequent refactoring are authored
by wyf.

The custom package changes are principally located in:

- `com.unity.ml-agents/Runtime/Sensors/GraphSensor.cs`
- `com.unity.ml-agents/Runtime/Sensors/GraphSensorComponent.cs`
- `com.unity.ml-agents/Runtime/Sensors/ISensor.cs`
- `com.unity.ml-agents/Runtime/Sensors/ObservationSpec.cs`

The corresponding project-side implementation is under
`Assets/ML-Agents/Examples/PyramidsChange/Scripts`.
