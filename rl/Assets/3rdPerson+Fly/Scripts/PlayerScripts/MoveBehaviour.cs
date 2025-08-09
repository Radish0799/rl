using UnityEngine;

public class MoveBehaviour : GenericBehaviour
{
    public float walkSpeed = 0.5f; // 初始化參數
    public float runSpeed = 1.0f;
    public float sprintSpeed = 2.0f;
    public float speedDampTime = 0.1f; // 動畫的平滑時間    
    public string jumpButton = "Jump";
    public float jumpHeight = 1.5f;
    public float jumpInertialForce = 10f; //慣性力量
    public float animSpeed = 1.0f;

    private float speed, speedSeeker;
    private int jumpBool;
    private int groundedBool;
    private bool jump;
    private bool isColliding;

    // ML Agent 離散動作輸入控制
    private int[] mlDiscreteActions = new int[4]; // [forward, right, rotate, jump]
    private bool useMLInput = false;

    void Start() // 首次啟動呼叫
    {
        jumpBool = Animator.StringToHash("Jump");
        groundedBool = Animator.StringToHash("Grounded");
        GetComponent<Animator>().speed = animSpeed;
        
        // 安全檢查
        if (behaviourManager != null && behaviourManager.GetAnim != null)
        {
            behaviourManager.GetAnim.SetBool(groundedBool, true);
        }

        if (behaviourManager != null)
        {
            behaviourManager.SubscribeBehaviour(this);
            behaviourManager.RegisterDefaultBehaviour(this.behaviourCode);
        }
        
        speedSeeker = runSpeed;
    }

    void Update()
    {
        // 只在非ML模式下處理跳躍輸入
        if (!useMLInput && !behaviourManager.IsMLMode && 
            !jump && Input.GetButtonDown(jumpButton) && 
            behaviourManager.IsCurrentBehaviour(this.behaviourCode) && 
            !behaviourManager.IsOverriding())
        {
            jump = true;
        }
        
        // ML模式下的跳躍處理
        if (useMLInput && behaviourManager.IsMLMode && mlDiscreteActions[3] == 1)
        {
            jump = true;
        }
    }

    // ML Agent 設定離散動作輸入
    public void SetMLDiscreteActions(int[] actions)
    {
        if (actions.Length >= 4)
        {
            mlDiscreteActions[0] = actions[0]; // forward/backward
            mlDiscreteActions[1] = actions[1]; // right/left
            mlDiscreteActions[2] = actions[2]; // rotate left/right
            mlDiscreteActions[3] = actions[3]; // jump
            useMLInput = true;
        }
    }

    public override void LocalFixedUpdate()
    {
        float h, v;
        float rotateInput = 0f;

        // 根據模式獲取輸入
        if (useMLInput && behaviourManager.IsMLMode)
        {
            // 轉換離散動作為連續值
            // forward: 0=不動, 1=向前, 2=向後
            switch (mlDiscreteActions[0])
            {
                case 1:
                    v = 1f; // 向前
                    break;
                case 2:
                    v = -1f; // 向後
                    break;
                default:
                    v = 0f; // 不動
                    break;
            }

            // right: 0=不動, 1=向右, 2=向左
            switch (mlDiscreteActions[1])
            {
                case 1:
                    h = 1f; // 向右
                    break;
                case 2:
                    h = -1f; // 向左
                    break;
                default:
                    h = 0f; // 不動
                    break;
            }

            // rotate: 0=不轉, 1=右轉, 2=左轉
            switch (mlDiscreteActions[2])
            {
                case 1:
                    rotateInput = 1f; // 右轉
                    break;
                case 2:
                    rotateInput = -1f; // 左轉
                    break;
                default:
                    rotateInput = 0f; // 不轉
                    break;
            }

            useMLInput = false; // 重置標記
        }
        else if (!behaviourManager.IsMLMode)
        {
            h = behaviourManager.GetH;
            v = behaviourManager.GetV;
            rotateInput = 0f; // 手動模式下不使用額外的旋轉輸入
        }
        else
        {
            // ML模式但沒有新輸入，使用上一幀的值或零
            h = 0f;
            v = 0f;
            rotateInput = 0f;
        }

        MovementManagement(h, v, rotateInput);
        JumpManagement();
    }

    void JumpManagement()
    {
        if (behaviourManager == null || behaviourManager.GetAnim == null || behaviourManager.GetRigidBody == null)
        {
            return;
        }

        // 執行跳躍
        if (jump && !behaviourManager.GetAnim.GetBool(jumpBool) && behaviourManager.IsGrounded())
        {
            behaviourManager.LockTempBehaviour(this.behaviourCode);
            behaviourManager.GetAnim.SetBool(jumpBool, true);
            
            if (behaviourManager.GetAnim.GetFloat(speedFloat) > 0.1f)
            {
                var collider = GetComponent<CapsuleCollider>();
                if (collider != null)
                {
                    collider.material.dynamicFriction = 0f;
                    collider.material.staticFriction = 0f;
                }

                RemoveVerticalVelocity();

                // 計算跳躍速度
                float velocity = 2f * Mathf.Abs(Physics.gravity.y) * jumpHeight;
                velocity = Mathf.Sqrt(velocity);
                behaviourManager.GetRigidBody.AddForce(Vector3.up * velocity, ForceMode.VelocityChange);
            }
        }
        // 跳躍過程中的處理
        else if (behaviourManager.GetAnim.GetBool(jumpBool))
        {
            // 空中移動慣性
            if (!behaviourManager.IsGrounded() && !isColliding && behaviourManager.GetTempLockStatus())
            {
                behaviourManager.GetRigidBody.AddForce(transform.forward * (jumpInertialForce * Physics.gravity.magnitude * sprintSpeed), ForceMode.Acceleration);
            }
            
            // 著陸檢測
            if ((behaviourManager.GetRigidBody.linearVelocity.y < 0) && behaviourManager.IsGrounded())
            {
                behaviourManager.GetAnim.SetBool(groundedBool, true);

                var collider = GetComponent<CapsuleCollider>();
                if (collider != null)
                {
                    collider.material.dynamicFriction = 0.6f;
                    collider.material.staticFriction = 0.6f;
                }

                jump = false;
                behaviourManager.GetAnim.SetBool(jumpBool, false);
                behaviourManager.UnlockTempBehaviour(this.behaviourCode);
            }
        }
    }

    void MovementManagement(float horizontal, float vertical, float rotateInput = 0f)
    {
        if (behaviourManager == null || behaviourManager.GetRigidBody == null || behaviourManager.GetAnim == null)
        {
            return;
        }

        // 重力處理
        if (behaviourManager.IsGrounded())
        {
            behaviourManager.GetRigidBody.useGravity = true;
        }
        else if (!behaviourManager.GetAnim.GetBool(jumpBool) && behaviourManager.GetRigidBody.linearVelocity.y > 0)
        {
            RemoveVerticalVelocity();
        }

        // 處理旋轉：在ML模式下優先使用直接旋轉
        if (behaviourManager.IsMLMode && Mathf.Abs(rotateInput) > 0.01f)
        {
            // 使用直接的Y軸旋轉，不使用Slerp避免回彈
            float rotationSpeed = 180f; // 每秒轉多少度
            transform.Rotate(0, rotateInput * rotationSpeed * Time.fixedDeltaTime, 0, Space.Self);
        }
        else if (!behaviourManager.IsMLMode)
        {
            // 非ML模式：基於移動方向的旋轉處理
            Rotating(horizontal, vertical);
        }

        // 速度計算
        Vector2 dir = new Vector2(horizontal, vertical);
        speed = Vector2.ClampMagnitude(dir, 1f).magnitude;
        
        // 在非ML模式下允許鼠標滾輪調整速度
        if (!behaviourManager.IsMLMode)
        {
            speedSeeker += Input.GetAxis("Mouse ScrollWheel");
            speedSeeker = Mathf.Clamp(speedSeeker, walkSpeed, runSpeed);
        }
        
        speed *= speedSeeker;

        // 衝刺處理
        if (behaviourManager.IsSprinting())
        {
            speed = sprintSpeed;
        }

        // 設定動畫器速度參數
        behaviourManager.GetAnim.SetFloat(speedFloat, speed, speedDampTime, Time.deltaTime);
    }

    private void RemoveVerticalVelocity()
    {
        if (behaviourManager != null && behaviourManager.GetRigidBody != null)
        {
            Vector3 horizontalVelocity = behaviourManager.GetRigidBody.linearVelocity;
            horizontalVelocity.y = 0;
            behaviourManager.GetRigidBody.linearVelocity = horizontalVelocity;
        }
    }

    Vector3 Rotating(float horizontal, float vertical)
    {
        if (behaviourManager == null || behaviourManager.GetRigidBody == null)
        {
            return Vector3.zero;
        }

        // 在ML模式下不執行基於移動方向的旋轉
        if (behaviourManager.IsMLMode)
        {
            return Vector3.zero;
        }

        // 使用世界座標系進行移動
        Vector3 targetDirection = new Vector3(horizontal, 0, vertical).normalized;

        if (behaviourManager.IsMoving() && targetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            // Quaternion newRotation = Quaternion.Slerp(behaviourManager.GetRigidBody.rotation, targetRotation, behaviourManager.turnSmoothing);
            // behaviourManager.GetRigidBody.MoveRotation(newRotation);
            behaviourManager.GetRigidBody.MoveRotation(targetRotation);
            behaviourManager.SetLastDirection(targetDirection);
        }

        // 精確控制時的重新定位（僅在非ML模式）
        if (!(Mathf.Abs(horizontal) > 0.9f || Mathf.Abs(vertical) > 0.9f))
        {
            behaviourManager.Repositioning();
        }

        return targetDirection;
    }

    private void OnCollisionStay(Collision collision)
    {
        isColliding = true;
        if (behaviourManager != null && 
            behaviourManager.IsCurrentBehaviour(this.GetBehaviourCode()) && 
            collision.GetContact(0).normal.y <= 0.1f)
        {
            var collider = GetComponent<CapsuleCollider>();
            if (collider != null && collider.material != null)
            {
                collider.material.dynamicFriction = 0f;
                collider.material.staticFriction = 0f;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        isColliding = false;
        var collider = GetComponent<CapsuleCollider>();
        if (collider != null && collider.material != null)
        {
            collider.material.dynamicFriction = 0.6f;
            collider.material.staticFriction = 0.6f;
        }
    }

    // 重寫以允許在ML模式下控制衝刺
    public override bool AllowSprint()
    {
        return canSprint;
    }
}