// Author: wyf

using System;

namespace Unity.MLAgents.Sensors
{
    /// <summary>
    /// Sensor for a fixed-size graph represented as a task-by-feature matrix.
    /// </summary>
    public sealed class GraphSensor : ISensor, IBuiltInSensor
    {
        private readonly string sensorName;
        private readonly int taskCount;
        private readonly int featureDimension;
        private readonly ObservationSpec observationSpec;
        private readonly Func<float[,]> getTaskAttributes;

        public GraphSensor(
            string name,
            int numTasks,
            int featureDim,
            Func<float[,]> taskAttributeProvider,
            ObservationType observationType = ObservationType.Default
        )
        {
            if (numTasks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numTasks));
            }

            if (featureDim <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(featureDim));
            }

            sensorName = string.IsNullOrWhiteSpace(name) ? "GraphSensor" : name;
            taskCount = numTasks;
            featureDimension = featureDim;
            getTaskAttributes = taskAttributeProvider ??
                throw new ArgumentNullException(nameof(taskAttributeProvider));
            observationSpec = ObservationSpec.Graph(numTasks, featureDim, observationType);
        }

        public string GetName() => sensorName;

        public ObservationSpec GetObservationSpec() => observationSpec;

        public int Write(ObservationWriter writer)
        {
            var data = getTaskAttributes();
            if (data == null)
            {
                return WriteZeros(writer);
            }

            if (data.GetLength(0) != taskCount || data.GetLength(1) != featureDimension)
            {
                throw new InvalidOperationException(
                    $"Graph sensor '{sensorName}' expected [{taskCount}, {featureDimension}] " +
                    $"but received [{data.GetLength(0)}, {data.GetLength(1)}]."
                );
            }

            var index = 0;
            for (var taskIndex = 0; taskIndex < taskCount; taskIndex++)
            {
                for (var featureIndex = 0; featureIndex < featureDimension; featureIndex++)
                {
                    writer[index++] = data[taskIndex, featureIndex];
                }
            }

            return index;
        }

        private int WriteZeros(ObservationWriter writer)
        {
            var observationSize = taskCount * featureDimension;
            for (var index = 0; index < observationSize; index++)
            {
                writer[index] = 0f;
            }

            return observationSize;
        }

        public byte[] GetCompressedObservation() => null;

        public void Update() { }

        public void Reset() { }

        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();

        public BuiltInSensorType GetBuiltInSensorType() => BuiltInSensorType.Unknown;
    }
}
