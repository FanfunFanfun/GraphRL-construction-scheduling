using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EleAttributes : MonoBehaviour
{
    //将该属性代码设为所有对象的通用属性代码

    //构件需要的属性
    public float Quantity;
    //public string name;
    [SerializeField]
    public Tasks ExecutableTask;
    public Tasks PerformingTask;

    //任务区域需要的属性
    public Tasks Task;


    // Start is called before the first frame update
    void Start()
    {
        //name = this.gameObject.name;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
