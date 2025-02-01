using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Publisher : MonoBehaviour
{
    public Client client;
    public float publishFrequency = 0.4f;

    public GameObject joint_RH_HIP;
    public GameObject joint_RH_THIGH;
    public GameObject joint_RH_SHANK;

    private float[] dof_pos_RH;
    private int step;
    private float count;
    // Start is called before the first frame update
    void Start()
    {
        count = 0f;
        step = 0;

        dof_pos_RH = new float[3];
        dof_pos_RH[0] = joint_RH_HIP.transform.localRotation.eulerAngles.x;
        dof_pos_RH[1] = joint_RH_THIGH.transform.localRotation.eulerAngles.x;
        dof_pos_RH[2] = joint_RH_SHANK.transform.localRotation.eulerAngles.x;

    }

    // Update is called once per frame
    void Update()
    {
        count += Time.deltaTime;
        if (count > publishFrequency) 
        {
            step += 1;
            dof_pos_RH = new float[3];
            dof_pos_RH[0] = joint_RH_HIP.transform.localRotation.eulerAngles.x;
            dof_pos_RH[1] = joint_RH_THIGH.transform.localRotation.eulerAngles.x;
            dof_pos_RH[2] = joint_RH_SHANK.transform.localRotation.eulerAngles.x;
            client.CallPublishMotionState(step,dof_pos_RH);

            count = 0f;
        }
        
    }
}
