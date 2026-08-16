// Author: wyf

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Executes navigation and construction work after the policy selects a task.
/// Material acquisition and storage management are intentionally out of scope.
/// </summary>
public class ConstructionAgent : MonoBehaviour
{
    public int StepCount;
    public GameObject area;
    public SchedulingArea m_MyArea;
    public TaskSelectionAgent childAgent;
    public List<float> AgentState;
    public List<string> StateLog;
    public string Type;
    public float e;
    public float CongesIndex;
    public string Target;
    public List<Vector3> Tarpos;
    public List<Vector3> nearTarpos;
    public Vector3 TarposBest;
    public Tuple<Tasks, float> TaskTarposBest;
    public List<Tasks> TarTask;
    public Tasks RegisteredTask;
    public GameObject RegisteredTaskArea;
    public Vector3 navTarget;
    public UnityEngine.AI.NavMeshAgent navAgent;
    public UnityEngine.AI.NavMeshObstacle navObstacle;

    private Rigidbody agentRigidbody;
    private readonly List<Vector3> positionHistory = new List<Vector3>();
    private Renderer statusRenderer;
    private int waitTime;

    private void Start()
    {
        agentRigidbody = GetComponent<Rigidbody>();
        m_MyArea = area.GetComponent<SchedulingArea>();
        childAgent = GetComponentInChildren<TaskSelectionAgent>();
        navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        navObstacle = GetComponent<UnityEngine.AI.NavMeshObstacle>();
        statusRenderer = transform.Find("Cylinder")?.GetComponent<Renderer>();

        if (m_MyArea == null || childAgent == null || navAgent == null || navObstacle == null)
        {
            throw new MissingComponentException(
                "ConstructionAgent requires SchedulingArea, TaskSelectionAgent, NavMeshAgent, and NavMeshObstacle."
            );
        }

        AgentState = new List<float> { 0f, 0f, 0f, 0f, 0f };
        StateLog = new List<string>();
        Tarpos = new List<Vector3>();
        nearTarpos = new List<Vector3>();
        TarTask = new List<Tasks>();
        positionHistory.Add(transform.localPosition);

        navAgent.speed = 3f;
        navAgent.acceleration = 10f;
        navObstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
        navObstacle.radius = 0.2f;
        navObstacle.carving = true;
        navObstacle.enabled = false;
    }

    private void FixedUpdate()
    {
        InternalDecision();
    }

    /// <summary>Advances the agent's navigation and task-execution state.</summary>
    public void InternalDecision()
    {
        StepCount = childAgent.StepCount;

        if (AgentState[0] == 1f)
        {
            ExecuteRegisteredTask();
            return;
        }

        if (AgentState[0] == 2f)
        {
            SetStatusColor(new Color(0.8f, 0f, 0.6f, 0.3f));
            waitTime++;
            if (waitTime > 200)
            {
                waitTime = 0;
                AgentState[0] = 0f;
            }

            StateLog.Add($"ActiveWait,{StepCount}");
            return;
        }

        SetStatusColor(new Color(0.5f, 0.5f, 0.5f, 0.3f));
        if (!string.IsNullOrWhiteSpace(Target) && Tarpos.Count > 0)
        {
            if (TarTask.Count > 0 && TaskTarposBest == null)
            {
                RefreshEligibleTasks();
            }
            else
            {
                NavigateToSelectedTask();
                StateLog.Add($"ReachingTarget,{StepCount}");
            }

            return;
        }

        if (AgentState[4] != 1f)
        {
            RefreshEligibleTasks();
            navAgent.enabled = false;
            navObstacle.enabled = true;
        }
        else
        {
            StateLog.Add($"Done,{StepCount}");
        }
    }

    private void ExecuteRegisteredTask()
    {
        if (RegisteredTask == null || RegisteredTaskArea == null)
        {
            ResetCurrentTarget();
            return;
        }

        navAgent.enabled = false;
        navObstacle.enabled = true;
        Vector3 direction = RegisteredTaskArea.transform.localPosition - transform.localPosition;
        agentRigidbody.AddForce(direction.normalized * 0.02f, ForceMode.VelocityChange);

        float workIncrement = RegisteredTask.quan > 0f ? e / RegisteredTask.quan : 0f;
        SetStatusColor(new Color(0f, 0.5f, 0f, 0.3f));
        StateLog.Add($"Tasking,{StepCount}");
        RegisteredTask.progress += workIncrement;
        positionHistory.Clear();

        if (RegisteredTask.progress < 1f)
        {
            return;
        }

        AgentState[0] = 0f;
        AgentState[2] = 0f;
        RegisteredTask.endstep = StepCount + 1;
        m_MyArea.TaskPoolEndDrive(RegisteredTask);
        RegisteredTask = null;
        RegisteredTaskArea = null;
        Target = null;
    }

    public void RefreshEligibleTasks()
    {
        m_MyArea.ReleaseTaskReservation(this);
        Tarpos.Clear();
        nearTarpos.Clear();
        TarTask.Clear();
        TarposBest = Vector3.zero;
        TaskTarposBest = null;
        navTarget = Vector3.zero;
        positionHistory.Clear();

        var candidateTasks = new List<Tasks>();
        foreach (Tasks task in m_MyArea.taskque)
        {
            if (task.Type != Type || task.progress != 0f)
            {
                continue;
            }

            candidateTasks.Add(task);
            Target = task.Type;
            if (IsTaskAreaAvailable(task) && m_MyArea.CanSelectTask(this, task))
            {
                TarTask.Add(task);
                Tarpos.Add(task.pos);
            }
        }

        bool hasOutstandingTask = m_MyArea.taskque.Exists(task => task.Type == Type)
            || m_MyArea.taskwait.Exists(task => task.Type == Type);
        if (!hasOutstandingTask)
        {
            AgentState[3] = 0f;
            Target = "InletOutlet";
            Tarpos.Add(new Vector3(8f, 0f, 5f));
            StateLog.Add($"Done,{StepCount}");
            return;
        }

        AgentState[3] += 1f;
        if (TarTask.Count > 0)
        {
            childAgent.RequestDecision();
            StateLog.Add($"ReachingTargetDecision,{StepCount}");
        }
        else if (candidateTasks.Count > 0)
        {
            childAgent.TarRecorder.Add("Idle Due Space Constraint", 1f, StatAggregationMethod.Sum);
            StateLog.Add($"NoTargetDueArea,{StepCount}");
        }
        else
        {
            StateLog.Add($"NoTargetDuePrecedence,{StepCount}");
        }
    }

    public void NavigateToSelectedTask()
    {
        TarTask.RemoveAll(task =>
            !IsTaskAreaAvailable(task) || !m_MyArea.IsTaskReservedBy(this, task)
        );
        Tarpos.RemoveAll(position => !TarTask.Any(task => Vector3.Distance(task.pos, position) < 0.01f));

        if (Tarpos.Count == 0 || TaskTarposBest == null)
        {
            ResetCurrentTarget();
            return;
        }

        Vector3 taskPosition = TaskTarposBest.Item1.pos;
        nearTarpos.Clear();
        nearTarpos.Add(taskPosition);

        if (navTarget != taskPosition)
        {
            navObstacle.enabled = false;
            navAgent.enabled = true;
            navAgent.SetDestination(taskPosition + m_MyArea.transform.position);
            navTarget = taskPosition;
        }

        if (Vector3.Distance(transform.localPosition, taskPosition) < 2f + transform.localScale.x / 2f)
        {
            StartTaskExecution(TaskTarposBest.Item1);
            navTarget = Vector3.zero;
        }

        TrackPosition();
    }

    public void StartTaskExecution(Tasks task)
    {
        if (task == null || !TarTask.Contains(task) || !IsTaskAreaAvailable(task) ||
            !m_MyArea.IsTaskReservedBy(this, task))
        {
            childAgent.TarRecorder.Add("Conflict", 1f, StatAggregationMethod.Sum);
            TarTask.Remove(task);
            Tarpos.RemoveAll(position => Vector3.Distance(position, task.pos) < 0.1f);
            ResetCurrentTarget();
            return;
        }

        RegisteredTask = task;
        RegisteredTaskArea = task.TaskArea;
        task.agentName = name;
        task.startstep = StepCount;
        m_MyArea.TaskPoolOnDrive(task);
        Target = RegisteredTaskArea.name;
        AgentState[0] = 1f;
        AgentState[2] = 1f;
        positionHistory.Clear();
        childAgent.TarRecorder.Add("Finished Tasks", 1f, StatAggregationMethod.Sum);
    }

    public bool IsTaskAreaAvailable(Tasks task)
    {
        foreach (Tasks activeTask in m_MyArea.taskon)
        {
            if (AreaConflict(task, activeTask))
            {
                return false;
            }
        }

        return true;
    }

    private bool AreaConflict(Tasks firstTask, Tasks secondTask)
    {
        Renderer firstRenderer = firstTask.TaskArea.GetComponent<Renderer>();
        Renderer secondRenderer = secondTask.TaskArea.GetComponent<Renderer>();
        float firstWidth = math.abs(firstRenderer.bounds.max.x - firstRenderer.bounds.min.x);
        float firstDepth = math.abs(firstRenderer.bounds.max.z - firstRenderer.bounds.min.z);
        float secondWidth = math.abs(secondRenderer.bounds.max.x - secondRenderer.bounds.min.x);
        float secondDepth = math.abs(secondRenderer.bounds.max.z - secondRenderer.bounds.min.z);
        float deltaX = math.abs(firstTask.TaskArea.transform.localPosition.x - secondTask.TaskArea.transform.localPosition.x);
        float deltaZ = math.abs(firstTask.TaskArea.transform.localPosition.z - secondTask.TaskArea.transform.localPosition.z);
        return deltaX < (firstWidth + secondWidth) / 2f
            && deltaZ < (firstDepth + secondDepth) / 2f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.tag.Contains("Task"))
        {
            return;
        }

        Tasks task = other.GetComponent<Tasks>();
        if (task != null)
        {
            CongesIndex = Mathf.Min(0.3f, CongesIndex + task.CongestionIndex);
            childAgent.TarRecorder.Add("Enter Task Area", 1f, StatAggregationMethod.Sum);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.tag.Contains("Task"))
        {
            Tasks task = other.GetComponent<Tasks>();
            if (task != null)
            {
                CongesIndex = Mathf.Max(0f, CongesIndex - task.CongestionIndex);
            }
        }

        if (!string.IsNullOrWhiteSpace(Target) && AgentState[2] == 1f)
        {
            AgentState[2] = 0f;
        }
    }

    private void ResetCurrentTarget()
    {
        m_MyArea.ReleaseTaskReservation(this);
        AgentState[0] = 0f;
        Target = string.Empty;
        TaskTarposBest = null;
        nearTarpos.Clear();
        navTarget = Vector3.zero;
        navAgent.enabled = false;
        navObstacle.enabled = true;
    }

    private void TrackPosition()
    {
        positionHistory.Add(transform.localPosition);
        if (positionHistory.Count > 240)
        {
            positionHistory.RemoveAt(0);
        }
    }

    private void SetStatusColor(Color color)
    {
        if (statusRenderer != null)
        {
            statusRenderer.material.SetColor("_Color", color);
        }
    }
}
