// Author: wyf

using System;
using UnityEngine;

namespace Unity.MLAgents.Sensors
{
    /// <summary>
    /// Base component that exposes a GraphSensor to an Agent.
    /// </summary>
    public class GraphSensorComponent : SensorComponent
    {
        public string SensorName = "GraphSensor";
        public int NumTasks;
        public int FeatureDim;

        private Func<float[,]> getTaskAttributes;

        protected void SetTaskAttributeFunc(Func<float[,]> taskAttributeProvider)
        {
            getTaskAttributes = taskAttributeProvider ??
                throw new ArgumentNullException(nameof(taskAttributeProvider));
        }

        public override ISensor[] CreateSensors()
        {
            if (getTaskAttributes == null)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} on '{name}' did not configure its task attribute provider."
                );
            }

            return new ISensor[]
            {
                new GraphSensor(SensorName, NumTasks, FeatureDim, getTaskAttributes),
            };
        }
    }
}
