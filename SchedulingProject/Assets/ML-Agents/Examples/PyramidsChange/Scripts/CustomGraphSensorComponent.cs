// Author: wyf

using System;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Produces the task-node feature matrix consumed by the Python RGCN encoder.
/// </summary>
public sealed class CustomGraphSensorComponent : GraphSensorComponent
{
    private const float PositionXScale = 28f;
    private const float PositionZScale = -35f;
    private const float DurationScale = 1800f;

    private SchedulingArea schedulingArea;
    private TaskSelectionAgent selectionAgent;

    private void Awake()
    {
        ConfigureSensor();
    }

    public override ISensor[] CreateSensors()
    {
        // GraphSensorComponent stores this callback in a non-serialized field.
        // Recreate it here so an editor refresh or domain reload cannot leave the
        // component marked as initialized while the base callback is null.
        ConfigureSensor();
        return base.CreateSensors();
    }

    private void ConfigureSensor()
    {
        schedulingArea = GetComponentInParent<SchedulingArea>();
        selectionAgent = GetComponentInParent<TaskSelectionAgent>();

        if (schedulingArea == null)
        {
            throw new MissingComponentException(
                "CustomGraphSensorComponent must be a child of a SchedulingArea."
            );
        }

        if (selectionAgent == null)
        {
            throw new MissingComponentException(
                "CustomGraphSensorComponent must be attached to a TaskSelectionAgent hierarchy."
            );
        }

        schedulingArea.ResolveProblem();
        NumTasks = schedulingArea.TaskCount;
        FeatureDim = SchedulingProblem.FeatureDimension;
        SetTaskAttributeFunc(BuildTaskAttributes);
    }

    private float[,] BuildTaskAttributes()
    {
        var attributes = new float[NumTasks, FeatureDim];
        if (schedulingArea.allTasks == null || schedulingArea.allTasks.Count == 0)
        {
            return attributes;
        }

        if (schedulingArea.allTasks.Count != NumTasks)
        {
            throw new InvalidOperationException(
                $"Graph sensor expects {NumTasks} tasks, but the scene loaded " +
                $"{schedulingArea.allTasks.Count}."
            );
        }

        var parentAgent = selectionAgent.parentAgent ??
            selectionAgent.GetComponentInParent<ConstructionAgent>();
        if (parentAgent == null)
        {
            throw new MissingComponentException("TaskSelectionAgent requires a parent ConstructionAgent.");
        }

        for (var taskIndex = 0; taskIndex < NumTasks; taskIndex++)
        {
            var task = schedulingArea.allTasks[taskIndex];
            WriteTaskState(attributes, taskIndex, task);

            attributes[taskIndex, 4] = task.pos.x / PositionXScale;
            attributes[taskIndex, 5] = task.pos.z / PositionZScale;
            attributes[taskIndex, 6] = task.quan / GetProductionRate(task.Type) / DurationScale;

            WriteTypeOneHot(attributes, taskIndex, 7, task.Type);
            attributes[taskIndex, 12] = schedulingArea.tSuccessorCount(task) / 8f;
            attributes[taskIndex, 13] = schedulingArea.CalAllGque(task) / 5f;
            attributes[taskIndex, 14] = parentAgent.TarTask.Contains(task) ? 1f : 0f;

            attributes[taskIndex, 15] = parentAgent.transform.localPosition.x / PositionXScale;
            attributes[taskIndex, 16] = parentAgent.transform.localPosition.z / PositionZScale;
            WriteTypeOneHot(attributes, taskIndex, 17, parentAgent.Type);
        }

        return attributes;
    }

    private void WriteTaskState(float[,] attributes, int taskIndex, Tasks task)
    {
        if (schedulingArea.taskwait.Contains(task))
        {
            attributes[taskIndex, 0] = 1f;
        }
        else if (schedulingArea.taskque.Contains(task))
        {
            attributes[taskIndex, 1] = 1f;
        }
        else if (schedulingArea.taskon.Contains(task))
        {
            attributes[taskIndex, 2] = 1f;
        }
        else if (schedulingArea.taskend.Contains(task))
        {
            attributes[taskIndex, 3] = 1f;
        }
    }

    private static void WriteTypeOneHot(
        float[,] attributes,
        int taskIndex,
        int firstColumn,
        string type
    )
    {
        int offset;
        switch (type)
        {
            case "JC": offset = 0; break;
            case "HI": offset = 1; break;
            case "G": offset = 2; break;
            case "R": offset = 3; break;
            case "F": offset = 4; break;
            default: return;
        }

        attributes[taskIndex, firstColumn + offset] = 1f;
    }

    private static float GetProductionRate(string taskType)
    {
        switch (taskType)
        {
            case "R": return 0.0003051f;
            case "F": return 0.0007167f;
            case "HI": return 1f / 925f;
            default: return 1f;
        }
    }
}
