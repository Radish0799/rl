using System.Collections;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class HallwayAgent : Agent
{
    public GameObject ground;
    public GameObject area;
    public GameObject symbolOGoal;
    public GameObject symbolXGoal;
    public GameObject symbolO;
    public GameObject symbolX;
    public bool useVectorObs;
    Rigidbody m_AgentRb;
    Material m_GroundMaterial;
    Renderer m_GroundRenderer;
    HallwaySettings m_HallwaySettings;
    int m_Selection;
    StatsRecorder m_statsRecorder;

    // 新增：用於與MoveBehaviour整合的變數
    private MoveBehaviour moveBehaviour;
    private BasicBehaviour basicBehaviour;

    public override void Initialize()
    {
        m_HallwaySettings = FindFirstObjectByType<HallwaySettings>();
        m_AgentRb = GetComponent<Rigidbody>();
        m_GroundRenderer = ground.GetComponent<Renderer>();
        m_GroundMaterial = m_GroundRenderer.material;
        m_statsRecorder = Academy.Instance.StatsRecorder;
        
        // 新增：獲取移動控制組件
        moveBehaviour = GetComponent<MoveBehaviour>();
        basicBehaviour = GetComponent<BasicBehaviour>();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (useVectorObs)
        {
            sensor.AddObservation(StepCount / (float)MaxStep);
        }
    }

    IEnumerator GoalScoredSwapGroundMaterial(Material mat, float time)
    {
        m_GroundRenderer.material = mat;
        yield return new WaitForSeconds(time);
        m_GroundRenderer.material = m_GroundMaterial;
    }

    public void MoveAgent(ActionSegment<int> act)
    {
        // 使用離散動作：[前進動作, 轉向動作]
        // 前進動作: 0=不前進, 1=前進
        // 轉向動作: 0=不轉, 1=左轉, 2=右轉
        
        int forwardAction = act[0]; // 前進動作
        int rotateAction = act[1];  // 轉向動作
        
        // 轉換為MoveBehaviour和BasicBehaviour所需的格式
        int[] mlActions = new int[4];
        
        // 設定前進動作 (對應mlDiscreteActions[0])
        if (forwardAction == 1)
        {
            mlActions[0] = 1; // 向前
        }
        else
        {
            mlActions[0] = 0; // 不動
        }
        
        mlActions[1] = 0; // 左右移動設為不動
        
        // 設定轉向動作 (對應mlDiscreteActions[2])
        if (rotateAction == 1)
        {
            mlActions[2] = 2; // 左轉
        }
        else if (rotateAction == 2)
        {
            mlActions[2] = 1; // 右轉
        }
        else
        {
            mlActions[2] = 0; // 不轉
        }
        
        mlActions[3] = 0; // 跳躍設為不跳
        
        // 確保ML模式開啟並將動作傳送給MoveBehaviour和BasicBehaviour
        if (basicBehaviour != null)
        {
            basicBehaviour.SetMLMode(true);
            basicBehaviour.SetMLDiscreteActions(mlActions);
        }
        
        if (moveBehaviour != null)
        {
            moveBehaviour.SetMLDiscreteActions(mlActions);
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        AddReward(-1f / MaxStep);
        MoveAgent(actionBuffers.DiscreteActions);
    }

    void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.CompareTag("symbol_O_Goal") || col.gameObject.CompareTag("symbol_X_Goal"))
        {
            if ((m_Selection == 0 && col.gameObject.CompareTag("symbol_O_Goal")) ||
                (m_Selection == 1 && col.gameObject.CompareTag("symbol_X_Goal")))
            {
                SetReward(1f);
                StartCoroutine(GoalScoredSwapGroundMaterial(m_HallwaySettings.goalScoredMaterial, 0.5f));
                m_statsRecorder.Add("Goal/Correct", 1, StatAggregationMethod.Sum);
                Debug.Log($"Correct Answer | Selection: {(m_Selection == 0 ? "O" : "X")} | Hit: {col.gameObject.tag} | Step: {StepCount}");
            }
            else
            {
                SetReward(-0.35f);
                StartCoroutine(GoalScoredSwapGroundMaterial(m_HallwaySettings.failMaterial, 0.5f));
                m_statsRecorder.Add("Goal/Wrong", 1, StatAggregationMethod.Sum);
                 Debug.Log($"Wrong Answer | Selection: {(m_Selection == 0 ? "O" : "X")} | Hit: {col.gameObject.tag} | Step: {StepCount}");
            }
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        
        // 前進控制
        if (Input.GetKey(KeyCode.W))
        {
            discreteActionsOut[0] = 1; // 前進
        }
        else
        {
            discreteActionsOut[0] = 0; // 不前進
        }
        
        // 轉向控制
        if (Input.GetKey(KeyCode.A))
        {
            discreteActionsOut[1] = 1; // 左轉
        }
        else if (Input.GetKey(KeyCode.D))
        {
            discreteActionsOut[1] = 2; // 右轉
        }
        else
        {
            discreteActionsOut[1] = 0; // 不轉
        }
    }

    public override void OnEpisodeBegin()
    {
        // 重置物理狀態
        if (m_AgentRb != null)
        {
            m_AgentRb.linearVelocity = Vector3.zero;
            m_AgentRb.angularVelocity = Vector3.zero;
        }
        
        // 重置ML模式狀態
        if (basicBehaviour != null)
        {
            basicBehaviour.SetMLMode(true); // 確保在ML模式
        }
        
        var agentOffset = -15f;
        var blockOffset = 0f;
        m_Selection = Random.Range(0, 2);
        if (m_Selection == 0)
        {
            symbolO.transform.position =
                new Vector3(0f + Random.Range(-3f, 3f), 2f, blockOffset + Random.Range(-5f, 5f))
                + ground.transform.position;
            symbolX.transform.position =
                new Vector3(0f, -1000f, blockOffset + Random.Range(-5f, 5f))
                + ground.transform.position;
        }
        else
        {
            symbolO.transform.position =
                new Vector3(0f, -1000f, blockOffset + Random.Range(-5f, 5f))
                + ground.transform.position;
            symbolX.transform.position =
                new Vector3(0f, 2f, blockOffset + Random.Range(-5f, 5f))
                + ground.transform.position;
        }

        transform.position = new Vector3(0f + Random.Range(-3f, 3f),
            1f, agentOffset + Random.Range(-5f, 5f))
            + ground.transform.position;
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        
        // 確保Rigidbody的旋轉也被重置
        if (m_AgentRb != null)
        {
            m_AgentRb.MoveRotation(transform.rotation);
        }

        var goalPos = Random.Range(0, 2);
        if (goalPos == 0)
        {
            symbolOGoal.transform.position = new Vector3(7f, 0.5f, 22.29f) + area.transform.position;
            symbolXGoal.transform.position = new Vector3(-7f, 0.5f, 22.29f) + area.transform.position;
        }
        else
        {
            symbolXGoal.transform.position = new Vector3(7f, 0.5f, 22.29f) + area.transform.position;
            symbolOGoal.transform.position = new Vector3(-7f, 0.5f, 22.29f) + area.transform.position;
        }
        m_statsRecorder.Add("Goal/Correct", 0, StatAggregationMethod.Sum);
        m_statsRecorder.Add("Goal/Wrong", 0, StatAggregationMethod.Sum);
    }
}