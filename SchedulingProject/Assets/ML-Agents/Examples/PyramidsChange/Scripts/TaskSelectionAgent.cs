// Author: wyf

using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using UnityEngine;

/// <summary>
/// Selects the next feasible scheduling task from the graph policy output.
/// </summary>
public class TaskSelectionAgent : Agent
{
    public GameObject area;
    public SchedulingArea m_MyArea;
    public StatsRecorder TarRecorder;
    public ConstructionAgent parentAgent;
    public Vector3 posBest;

    public override void Initialize()
    {
        if (area == null)
        {
            throw new MissingReferenceException("TaskSelectionAgent requires an area reference.");
        }

        m_MyArea = area.GetComponent<SchedulingArea>();
        parentAgent = GetComponentInParent<ConstructionAgent>();
        if (m_MyArea == null || parentAgent == null)
        {
            throw new MissingComponentException(
                "TaskSelectionAgent requires SchedulingArea and parent ConstructionAgent components."
            );
        }

        TarRecorder = Academy.Instance.StatsRecorder;
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (parentAgent.Tarpos.Count == 0 || parentAgent.TarTask.Count == 0)
        {
            return;
        }

        int selectedTaskIndex = actionBuffers.DiscreteActions[0];
        if (selectedTaskIndex < 0 || selectedTaskIndex >= m_MyArea.TaskCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actionBuffers),
                selectedTaskIndex,
                $"Action must select one of {m_MyArea.TaskCount} tasks."
            );
        }

        Tasks selectedTask = m_MyArea.allTasks[selectedTaskIndex];
        if (!parentAgent.TarTask.Contains(selectedTask) ||
            !m_MyArea.TryReserveTask(parentAgent, selectedTask))
        {
            TarRecorder.Add("Reserved Task Conflict", 1f, StatAggregationMethod.Sum);
            return;
        }

        posBest = selectedTask.pos;
        parentAgent.TarposBest = posBest;
        parentAgent.TaskTarposBest = new Tuple<Tasks, float>(selectedTask, StepCount);
        TarRecorder.Add(
            "Distance between Agent and Best Position",
            Vector3.Distance(parentAgent.transform.localPosition, selectedTask.pos)
        );
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (m_MyArea.allTasks.Count != m_MyArea.TaskCount)
        {
            throw new InvalidOperationException(
                $"Expected {m_MyArea.TaskCount} tasks, but loaded {m_MyArea.allTasks.Count}."
            );
        }

        for (int taskIndex = 0; taskIndex < m_MyArea.TaskCount; taskIndex++)
        {
            bool isFeasible = parentAgent.TarTask.Contains(m_MyArea.allTasks[taskIndex]) &&
                m_MyArea.CanSelectTask(parentAgent, m_MyArea.allTasks[taskIndex]);
            actionMask.SetActionEnabled(0, taskIndex, isFeasible);
        }
    }
}
