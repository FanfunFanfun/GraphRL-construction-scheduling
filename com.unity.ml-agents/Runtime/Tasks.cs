using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class Tasks : MonoBehaviour
{
    public int id;
    public Vector3 pos;
    public List<Tasks> pre = new List<Tasks>() { };
    public int queuestep;  // 2025.11.11
    public int startstep;
    public int endstep;
    public List<GameObject> Eles;
    public GameObject TaskArea;
    public string Type;
    public float duration;
    public float progress;
    public float CongestionIndex;
    public float quan;
    public int groupId;
    public string agentName;

    public void GenerateTaskArea( ) {
        GameObject ComTaskArea;
        ComTaskArea = GameObject.Find("TaskArea_HI");
        switch (this.Type)
        {
            case "HI":
                ComTaskArea = GameObject.Find("TaskArea_HI");
                break;
        }
        var maxX = 0f;
        var minX = 0f;
        var maxY = 0f;
        var minY = 0f;
        foreach (var item in this.Eles)
        {
            if (maxX <= item.GetComponent<Renderer>().bounds.max.x) {
                maxX = item.GetComponent<Renderer>().bounds.max.x;
            }
            if (minX >= item.GetComponent<Renderer>().bounds.min.x) {
                minX = item.GetComponent<Renderer>().bounds.min.x;
            }
            if (maxY <= item.GetComponent<Renderer>().bounds.max.y)
            {
                maxY = item.GetComponent<Renderer>().bounds.max.y;
            }
            if (minY >= item.GetComponent<Renderer>().bounds.min.y)
            {
                minY = item.GetComponent<Renderer>().bounds.min.y;
            }

        }
        var position = new Vector3((minX + maxX) / 2,0, (minY + maxY) / 2); //待修改
        this.TaskArea = Instantiate(ComTaskArea, position,
                Quaternion.Euler(0f, 0f, 0f), transform);
        this.TaskArea.transform.localScale = new Vector3(5, 5, 5);  // 修改newObject的render尺寸和collision尺寸
        this.TaskArea.AddComponent<EleAttributes>().Task = this;

    }

    public void DestoryTaskArea() {
        Destroy(TaskArea);
    }



    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
