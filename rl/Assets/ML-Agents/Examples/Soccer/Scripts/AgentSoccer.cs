using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;

public enum Team
{
    Blue = 0,
    Purple = 1
}

public class AgentSoccer : Agent
{
    public enum Position
    {
        Striker,
        Goalie,
        Generic
    }

    [HideInInspector]
    public Team team;
    float m_KickPower;
    float m_BallTouch;
    public Position position;

    const float k_Power = 2000f;
    float m_Existential;
    float m_ForwardSpeed;

    [HideInInspector]
    public Rigidbody agentRb;
    SoccerSettings m_SoccerSettings;
    BehaviorParameters m_BehaviorParameters;
    public Vector3 initialPos;
    public float rotSign;

    EnvironmentParameters m_ResetParams;

    // 角色控制系統組件引用
    private BasicBehaviour basicBehaviour;
    private MoveBehaviour moveBehaviour;

    public override void Initialize()
    {
        SoccerEnvController envController = GetComponentInParent<SoccerEnvController>();
        if (envController != null)
        {
            m_Existential = 1f / envController.MaxEnvironmentSteps;
        }
        else
        {
            m_Existential = 1f / MaxStep;
        }

        m_BehaviorParameters = gameObject.GetComponent<BehaviorParameters>();
        if (m_BehaviorParameters.TeamId == (int)Team.Blue)
        {
            team = Team.Blue;
            initialPos = new Vector3(transform.position.x - 5f, .5f, transform.position.z);
            rotSign = 1f;
        }
        else
        {
            team = Team.Purple;
            initialPos = new Vector3(transform.position.x + 5f, .5f, transform.position.z);
            rotSign = -1f;
        }
        
        if (position == Position.Goalie)
        {
            m_ForwardSpeed = 1.0f;
        }
        else if (position == Position.Striker)
        {
            m_ForwardSpeed = 1.3f;
        }
        else
        {
            m_ForwardSpeed = 1.0f;
        }
        
        m_SoccerSettings = FindFirstObjectByType<SoccerSettings>();
        agentRb = GetComponent<Rigidbody>();
        agentRb.maxAngularVelocity = 500;

        m_ResetParams = Academy.Instance.EnvironmentParameters;

        // 初始化角色控制系統組件
        basicBehaviour = GetComponent<BasicBehaviour>();
        moveBehaviour = GetComponent<MoveBehaviour>();

        // 啟用ML模式
        if (basicBehaviour != null)
        {
            basicBehaviour.SetMLMode(true);
        }
    }

    public void MoveAgent(ActionSegment<int> act)
    {
        m_KickPower = 0f;

        // 簡單版本：2個動作空間
        var forwardAction = act[0];  // 0=不動, 1=向前
        var rotateAction = act[1];   // 0=不轉, 1=左轉, 2=右轉

        // 準備離散動作陣列給角色控制系統
        int[] discreteActions = new int[4];

        // 處理前進（只有停止和前進）
        switch (forwardAction)
        {
            case 1:
                m_KickPower = 1f; // 向前時可以踢球
                discreteActions[0] = 1; // 向前
                break;
            default:
                discreteActions[0] = 0; // 不動
                break;
        }

        // 處理旋轉
        switch (rotateAction)
        {
            case 1:
                discreteActions[2] = 2; // 左轉（A鍵）
                break;
            case 2:
                discreteActions[2] = 1; // 右轉（D鍵）
                break;
            default:
                discreteActions[2] = 0; // 不轉
                break;
        }

        // 不使用側向移動和跳躍
        discreteActions[1] = 0; // 不側向移動
        discreteActions[3] = 0; // 不跳躍

        // 使用角色控制系統處理移動
        if (basicBehaviour != null && moveBehaviour != null)
        {
            basicBehaviour.SetMLDiscreteActions(discreteActions);
            moveBehaviour.SetMLDiscreteActions(discreteActions);
        }
        else
        {
            // 備用邏輯
            if (forwardAction == 1)
            {
                Vector3 dirToGo = transform.forward * m_ForwardSpeed;
                agentRb.AddForce(dirToGo * m_SoccerSettings.agentRunSpeed, ForceMode.VelocityChange);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (position == Position.Goalie)
        {
            AddReward(m_Existential);
        }
        else if (position == Position.Striker)
        {
            AddReward(-m_Existential);
        }
        MoveAgent(actionBuffers.DiscreteActions);
    }   

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        
        // 前進（只有W鍵有效）
        if (Input.GetKey(KeyCode.W))
        {
            discreteActionsOut[0] = 1; // 向前
        }
        else
        {
            discreteActionsOut[0] = 0; // 不動
        }
        
        // 旋轉
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

    void OnCollisionEnter(Collision c)
    {
        var force = k_Power * m_KickPower;
        if (position == Position.Goalie)
        {
            force = k_Power;
        }
        if (c.gameObject.CompareTag("ball"))
        {
            float reward = 0.2f * m_BallTouch;
            AddReward(reward);
            Debug.Log($"{name} touched the ball. Reward: +{reward}, TotalReward: {GetCumulativeReward()}");

            var dir = c.contacts[0].point - transform.position;
            dir = dir.normalized;
            c.gameObject.GetComponent<Rigidbody>().AddForce(dir * force);
        }
    }

    public override void OnEpisodeBegin()
    {
        m_BallTouch = 0.015f;
    }
}