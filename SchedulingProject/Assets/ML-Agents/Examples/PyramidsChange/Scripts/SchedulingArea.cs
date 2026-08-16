// Author: wyf

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using Unity.MLAgents;
using Unity.MLAgentsExamples;
using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Owns task state, multi-agent coordination, and task-area lifecycle for the 86-task scene.
/// </summary>
public class SchedulingArea : Area
{
    [SerializeField]
    private SchedulingProblemSize problemSize = SchedulingProblemSize.Auto;

    public int TaskCount { get; private set; }
    public string TaskCsvFileName { get; private set; }

    public List<Tasks> allTasks;
    public List<Tasks> taskwait = new List<Tasks>();
    public List<Tasks> taskque = new List<Tasks>();
    public List<Tasks> taskon = new List<Tasks>();
    public List<Tasks> taskend = new List<Tasks>();

    public GameObject ComTaskArea;
    public int maxStep;
    private readonly Dictionary<Tasks, ConstructionAgent> taskReservations =
        new Dictionary<Tasks, ConstructionAgent>();
    public List<ConstructionAgent> AgentsList = new List<ConstructionAgent>();

    private const float TaskAreaOffset = 2f;
    private const float TaskAreaY = 4f;
    public SimpleMultiAgentGroup m_AgentGroup;

    public NavMeshSurface navMeshSurface;

    private void Awake()
    {
        ResolveProblem();
    }

    public void ResolveProblem()
    {
        if (TaskCount > 0)
        {
            return;
        }

        TaskCount = SchedulingProblem.ResolveTaskCount(problemSize, gameObject.scene.name);
        TaskCsvFileName = SchedulingProblem.GetCsvFileName(TaskCount);
    }

    public void Start()
    {
        navMeshSurface = GetComponent<NavMeshSurface>();
        if (navMeshSurface == null)
        {
            throw new MissingComponentException("SchedulingArea requires a NavMeshSurface component.");
        }

        navMeshSurface.BuildNavMesh();

        GenerateTasks();
        ResetScene();

        for (int i = 0; i < this.transform.childCount; i++)
        {
            var child = this.transform.GetChild(i);
            if (child.tag  is "agent" & child.gameObject.activeSelf)
            {
                this.AgentsList.Add(child.gameObject.GetComponent<ConstructionAgent>());
            }
        }

        m_AgentGroup = new SimpleMultiAgentGroup();
        foreach (var agent in AgentsList)
        {
            m_AgentGroup.RegisterAgent(agent.GetComponentInChildren<TaskSelectionAgent>());
        }

    }

    public void FixedUpdate()
    {
        int currentIdelnum = 0;
        foreach (var a in AgentsList)
        {
            if (a.StepCount > 0)
            {
                if (a.StateLog.Last().Contains("NoTarget"))
                {
                    currentIdelnum += 1;
                }
                else if (a.StateLog.Last().Contains("ActiveWaiting"))
                {
                    currentIdelnum += 1;
                }
            }
        }

        for (int i = 0; i < AgentsList.Count(); i++)
        {
            var TarAgent = AgentsList[i].childAgent;
            TarAgent.TarRecorder.Add("Accumulate Idel Steps", currentIdelnum, Unity.MLAgents.StatAggregationMethod.Sum);

        }

        if (this.taskend.Count == this.allTasks.Count|| AgentsList[0].StepCount > maxStep)
        {
            var TotalPathStep = 0;
            foreach (var a in AgentsList)
            {
                TotalPathStep += a.StateLog.Count(s => s.Contains("ReachingTarget"));
            }

            foreach (var a in AgentsList)
            {
                var TarAgent = a.childAgent;
                TarAgent.TarRecorder.Add("Total Steps",a.StepCount);
                TarAgent.TarRecorder.Add("Total Reaching Steps",TotalPathStep);
            }

            m_AgentGroup.AddGroupReward((28000-AgentsList[0].StepCount)*0.01f*2f);

            if (this.taskend.Count == this.allTasks.Count)
            {
                m_AgentGroup.EndGroupEpisode();
            }
            else if (AgentsList[0].StepCount > maxStep)
            {
                m_AgentGroup.GroupEpisodeInterrupted();
            }
            ResetScene();
        }
    }

    public void ResetScene()
    {
        this.taskend.Clear();
        this.taskon.Clear();
        this.taskwait.Clear();
        this.taskque.Clear();
        taskReservations.Clear();

        foreach (var t in this.allTasks)
        {
            t.progress = 0;
            var tpos_area = t.TaskArea.transform.position;
            tpos_area.y = -TaskAreaOffset - TaskAreaY/2 + transform.position.y;
            t.TaskArea.transform.position = tpos_area;
            if (t.pre.Count == 0)
            {
                this.taskque.Add(t);
                foreach (var Ele in t.Eles)
                {
                    Ele.GetComponent<EleAttributes>().PerformingTask = null;
                    Ele.GetComponent<EleAttributes>().ExecutableTask = t;
                }
            }
            else
            {
                this.taskwait.Add(t);
            }
        }

        for (int i = 0; i < AgentsList.Count; i++)
        {
            AgentsList[i].transform.position = new Vector3(2+i * 1.6f, 1.12f, -6) + transform.position;
            AgentsList[i].AgentState = new List<float> { 0, 0, 0, 0, 0 };
            AgentsList[i].Target = null;
            AgentsList[i].StateLog.Clear();
        }

    }

    public void GenerateTasks()
    {
        ResolveProblem();
        string csvPath = Path.Combine(Application.streamingAssetsPath, TaskCsvFileName);

        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"Task CSV was not found: {csvPath}", csvPath);
        }

        var lines = File.ReadAllLines(csvPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length != TaskCount)
        {
            throw new InvalidDataException(
                $"Scene '{gameObject.scene.name}' expects {TaskCount} tasks, " +
                $"but '{TaskCsvFileName}' contains {lines.Length} non-empty rows."
            );
        }

        var data = from line in lines
                   let values = line.Split(',')
                   where values.Any(val => !string.IsNullOrEmpty(val.Trim()))
                   select new
                   {
                       Col1 = values[0].Trim(),
                       Col2 = values[2].Trim(),
                       Col3 = values[3].Trim(),
                        Col4 = values[4].Trim()
                   };

        var ElesModel = this.transform.Find("南塔8F-墙-轴名修改").gameObject;

        foreach (var item in data)
        {
            var TaskArea = Instantiate(this.ComTaskArea, new Vector3(0,0,0)+transform.position,
                    Quaternion.Euler(0f, 0f, 0f),this.transform);
            TaskArea.tag = "TaskArea";
            Tasks t = TaskArea.AddComponent<Tasks>();
            t.id = int.Parse(item.Col1);
            TaskArea.name = "TaskArea" + t.id;
            t.Type = item.Col2;
            t.CongestionIndex = 0f;
            t.TaskArea = TaskArea;
            t.Eles = new List<GameObject>() { };

            foreach (var ele in item.Col4.Split('+'))
            {
                try
                {
                    t.Eles.Add(ElesModel.transform.Find(ele).gameObject);
                }
                catch (System.Exception)
                {
                    throw;
                }
            }
            allTasks.Add(t);
            var maxX = t.Eles[0].GetComponent<Renderer>().bounds.max.x;
            var minX = t.Eles[0].GetComponent<Renderer>().bounds.min.x;
            var maxZ = t.Eles[0].GetComponent<Renderer>().bounds.max.z;
            var minZ = t.Eles[0].GetComponent<Renderer>().bounds.min.z;
            t.quan = 0;
            foreach (var Ele in t.Eles)
            {
                var Elemaxx = Ele.GetComponent<Renderer>().bounds.max.x;
                var Eleminx = Ele.GetComponent<Renderer>().bounds.min.x;
                var Elemaxz = Ele.GetComponent<Renderer>().bounds.max.z;
                var Eleminz = Ele.GetComponent<Renderer>().bounds.min.z;
                t.quan += (Elemaxx - Eleminx) * (Elemaxz - Eleminz);

                if (maxX <= Elemaxx)
                {
                    maxX = Elemaxx;
                }
                if (minX >= Eleminx)
                {
                    minX = Eleminx;
                }
                if (maxZ <= Elemaxz)
                {
                    maxZ = Elemaxz;
                }
                if (minZ >= Eleminz)
                {
                    minZ = Eleminz;
                }

            }
            var AreaExtra = 0f;
            switch (t.Type)
            {
                case "HI":
                    t.quan = 1;
                    AreaExtra = 4.2f;
                    t.CongestionIndex = 0.6f;
                    break;
                case "JC":
                    t.quan = 750;
                    AreaExtra = 2.9f;
                    t.CongestionIndex = 0.4f;
                    break;
                case "G":
                    t.quan = 720;
                    AreaExtra = 2.5f;
                    t.CongestionIndex = 0.3f;
                    break;
                case "R":
                    AreaExtra = 3.2f;
                    t.CongestionIndex = 0.4f;
                    break;
                case "F":
                    AreaExtra = 4f;
                    t.CongestionIndex = 0.5f;
                    break;
            }
            Vector3 pos = new Vector3((minX + maxX) / 2,-TaskAreaOffset - TaskAreaY/2, (minZ + maxZ) / 2);
            TaskArea.transform.position = pos;
            Vector3 tpos = new Vector3((minX + maxX) / 2, 0, (minZ + maxZ) / 2);
            t.pos = tpos-transform.position;
            TaskArea.transform.localScale = new Vector3(maxX-minX+AreaExtra, TaskAreaY, maxZ-minZ+AreaExtra);
        }

        foreach (var item in data) {
            if ( !string.IsNullOrEmpty(item.Col3))
            {
                Tasks t = allTasks.Where(t => t.id == int.Parse(item.Col1)).FirstOrDefault();
                foreach (var pre in item.Col3.Split('+'))
                {
                    t.pre.Add(allTasks.Where(t => t.id == int.Parse(pre)).FirstOrDefault());
                }
            }
        }

    }

    /// <summary>
    /// Returns whether an agent may select a queued task. Reservations also block
    /// tasks whose work areas overlap a reserved task.
    /// </summary>
    public bool CanSelectTask(ConstructionAgent requestingAgent, Tasks candidateTask)
    {
        if (requestingAgent == null || candidateTask == null || !taskque.Contains(candidateTask))
        {
            return false;
        }

        foreach (var reservation in taskReservations)
        {
            if (reservation.Value == requestingAgent)
            {
                continue;
            }

            if (reservation.Key == candidateTask || AreaConflict(candidateTask, reservation.Key))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reserves a task before an agent begins navigating to it.</summary>
    public bool TryReserveTask(ConstructionAgent requestingAgent, Tasks candidateTask)
    {
        if (!CanSelectTask(requestingAgent, candidateTask) ||
            !requestingAgent.IsTaskAreaAvailable(candidateTask))
        {
            return false;
        }

        ReleaseTaskReservation(requestingAgent);
        taskReservations.Add(candidateTask, requestingAgent);
        return true;
    }

    public bool IsTaskReservedBy(ConstructionAgent requestingAgent, Tasks task)
    {
        return requestingAgent != null && task != null &&
            taskReservations.TryGetValue(task, out var owner) && owner == requestingAgent;
    }

    public void ReleaseTaskReservation(ConstructionAgent requestingAgent)
    {
        if (requestingAgent == null)
        {
            return;
        }

        var tasksToRelease = taskReservations
            .Where(reservation => reservation.Value == requestingAgent)
            .Select(reservation => reservation.Key)
            .ToList();
        foreach (var task in tasksToRelease)
        {
            taskReservations.Remove(task);
        }
    }

    public void ReleaseTaskReservation(Tasks task)
    {
        if (task != null)
        {
            taskReservations.Remove(task);
        }
    }

    public void TaskPoolEndDrive(Tasks t)
    {

        taskon.Remove(t);
        t.TaskArea.transform.position += new Vector3(0, -TaskAreaOffset - TaskAreaY, 0);
        taskend.Add(t);
        foreach (var Ele in t.Eles)
        {
            Ele.GetComponent<EleAttributes>().PerformingTask = null;
        }

        foreach (var task in taskwait)
        {
            if (task.pre.All(pre => taskend.Any(a => a == pre)))
            {
                if (!taskque.Contains(task))
                {
                    taskque.Add(task);
                    task.queuestep = AgentsList[0].StepCount;
                    foreach (var Ele in task.Eles)
                    {
                        Ele.GetComponent<EleAttributes>().ExecutableTask = task;
                    }
                }
            }
        }
        foreach (var item in taskque)
        {
            if (taskwait.Contains(item))
            {
                taskwait.Remove(item);
            }
        }
    }

    public void TaskPoolOnDrive(Tasks t) {
        try
        {
            ReleaseTaskReservation(t);
            taskque.Remove(t);
            taskon.Add(t);
            t.TaskArea.transform.position += new Vector3(0, TaskAreaOffset+TaskAreaY, 0);
            foreach (var Ele in t.Eles)
            {
                Ele.GetComponent<EleAttributes>().ExecutableTask = null;
                Ele.GetComponent<EleAttributes>().PerformingTask = t;
            }
        }
        catch (System.Exception)
        {

            throw;
        }
    }

    bool AreaConflict(Tasks this_t, Tasks Other_t)
    {

        var x1 = math.abs(this_t.TaskArea.GetComponent<Renderer>().bounds.max.x - this_t.TaskArea.GetComponent<Renderer>().bounds.min.x);
        var z1 = math.abs(this_t.TaskArea.GetComponent<Renderer>().bounds.max.z - this_t.TaskArea.GetComponent<Renderer>().bounds.min.z);
        var delt_x = math.abs(this_t.TaskArea.transform.localPosition.x - Other_t.TaskArea.transform.localPosition.x);
        var delt_z = math.abs(this_t.TaskArea.transform.localPosition.z - Other_t.TaskArea.transform.localPosition.z);
        if (delt_x + delt_z < 10)
        {
            var x2 = math.abs(Other_t.TaskArea.GetComponent<Renderer>().bounds.max.x - Other_t.TaskArea.GetComponent<Renderer>().bounds.min.x);
            var z2 = math.abs(Other_t.TaskArea.GetComponent<Renderer>().bounds.max.z - Other_t.TaskArea.GetComponent<Renderer>().bounds.min.z);

            if ((delt_x < (x1 + x2) / 2) & (delt_z < (z1 + z2) / 2))
            {
                return true;
            }
        }
        return false;
    }

    public int CalAllGque(Tasks p)
    {
        int g = 0;
        foreach (var t in taskque)
        {
            if (p != t & AreaConflict(p,t))
            {
                g += 1;
            }
        }
        return g;
    }

    public int tSuccessorCount(Tasks t) {
        var tSucNum = 0;
        foreach (var tt in taskwait)
        {
            if (tt.pre.Contains(t))
            {
                tSucNum += 1;
            }
            else
            {
                foreach (var ttpre in tt.pre)
                {
                    if (ttpre.pre.Contains(t))
                    {
                        tSucNum += 1;
                    }
                    else
                    {
                        foreach (var ttppre in ttpre.pre)
                        {
                            if (ttppre.pre.Contains(t))
                            {
                                tSucNum += 1;
                            }
                        }
                    }
                }
            }
        }
        return tSucNum;
    }
}
