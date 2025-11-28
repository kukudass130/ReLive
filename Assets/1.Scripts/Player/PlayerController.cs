using UnityEngine;
using System;

/// <summary>
/// 2D 플랫폼 전용 Player Controller.
/// - S-12: Player Controller 시스템 역할
/// - 로우 레벨 입력을 추상화된 행동으로 변환한다.
/// - 실제 상호작용/인벤토리/퀘스트 등은 다른 시스템(S-02~S-06)이 구독/호출.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    #region Components

    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.1f;
    [SerializeField] private LayerMask groundLayer;

    #endregion

    #region Movement Settings

    [Header("Movement")]
    [SerializeField] public bool canMove = true;
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float runMaxSpeed = 9f;
    [SerializeField] private float runAcceleration = 40f;
    [SerializeField] private float runDeceleration = 50f;
    [SerializeField] private float airControlMultiplier = 0.5f;

    #endregion

    #region Jump Settings

    [Header("Jump")]
    [SerializeField] private float jumpForce = 13f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Tooltip("속도 절대값이 이 값 이하일 때 '점프 고점(Apex)'으로 간주")]
    [SerializeField] private float apexThreshold = 1f;

    [Header("Gravity Multipliers")]
    [SerializeField] private float baseGravityScale = 3f;
    [SerializeField] private float fallGravityMultiplier = 2.0f;
    [SerializeField] private float lowJumpGravityMultiplier = 2.5f;
    [SerializeField] private float apexGravityMultiplier = 0.5f;

    #endregion

    #region Input State

    private Vector2 moveInput;
    private bool jumpHeld;
    private bool jumpPressedThisFrame;
    private bool runHeld;
    private bool menuPressedThisFrame;
    private bool interactPressedThisFrame;
    private bool grabPressedThisFrame;

    #endregion

    #region Runtime State

    private bool isGrounded;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float currentHorizontalSpeed;
    private bool menuOpen = false;

    #endregion

    #region Events (연동용)

    /// <summary>F 키 입력 시 호출되는 이벤트 (상자, NPC, 출구 등).</summary>
    public event Action OnInteractPressed;

    /// <summary>E 키 입력 시 호출되는 이벤트 (아이템 줍기/잡기 등).</summary>
    public event Action OnGrabPressed;

    /// <summary>메뉴(Tab) 토글 시 호출되는 이벤트.</summary>
    public event Action<bool> OnMenuToggled;

    #endregion

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        rb.gravityScale = baseGravityScale;
    }

    private void Update()
    {
        ReadInput();

        if (menuOpen)
        {
            // 메뉴 열려있을 때 이동/점프 입력은 막고, UI 쪽 입력만 허용하는 식으로 사용 가능.
            HandleMenuInput();
            ClearOneFrameInputFlags();
            return;
        }

        HandleGroundCheck();
        HandleTimers();
        HandleJumpInput();
        HandleMenuInput();
        HandleInteractionInput();
        ClearOneFrameInputFlags();
    }

    private void FixedUpdate()
    {
        if (menuOpen) return;

        HandleHorizontalMovement();
        ApplyBetterJumpPhysics();
    }

    #region Input

    /// <summary>
    /// Unity 기본 Input 시스템을 사용한 입력 처리.
    /// 나중에 New Input System으로 교체 가능.
    /// </summary>
    private void ReadInput()
    {
        // 수평 이동 (A/D or 좌우 키)
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical"); // 혹시 상하 입력 필요시

        moveInput = new Vector2(moveX, moveY).normalized;

        // 점프 (Spacebar)
        if (Input.GetButtonDown("Jump"))
        {
            jumpPressedThisFrame = true;
            jumpHeld = true;
        }
        else if (Input.GetButtonUp("Jump"))
        {
            jumpHeld = false;
        }

        // Shift 달리기
        runHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // F 상호작용
        if (Input.GetKeyDown(KeyCode.F))
            interactPressedThisFrame = true;

        // E 잡기/줍기
        if (Input.GetKeyDown(KeyCode.E))
            grabPressedThisFrame = true;

        // Tab 메뉴
        if (Input.GetKeyDown(KeyCode.Tab))
            menuPressedThisFrame = true;
    }

    private void ClearOneFrameInputFlags()
    {
        jumpPressedThisFrame = false;
        interactPressedThisFrame = false;
        grabPressedThisFrame = false;
        menuPressedThisFrame = false;
    }

    #endregion

    #region Ground & Timers

    private void HandleGroundCheck()
    {
        if (groundCheck == null)
        {
            // groundCheck 미설정 시, 발 위치를 따로 넣어줄 것.
            isGrounded = false;
            return;
        }

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
    }

    private void HandleTimers()
    {
        // 코요테 타임 감소
        if (!isGrounded)
        {
            coyoteTimer -= Time.deltaTime;
        }

        // 점프 버퍼
        if (jumpPressedThisFrame)
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpBufferTimer -= Time.deltaTime;
        }
    }

    #endregion

    #region Movement Logic

    private void HandleHorizontalMovement()
    {
        float targetSpeed = moveInput.x * (runHeld ? runMaxSpeed : walkSpeed);

        float accel = runHeld ? runAcceleration : runDeceleration;

        // 공중 제어는 감소
        if (!isGrounded)
        {
            accel *= airControlMultiplier;
        }

        // 현재 속도를 타겟 속도로 보간 (가속/감속)
        currentHorizontalSpeed = Mathf.MoveTowards(
            currentHorizontalSpeed,
            targetSpeed,
            accel * Time.fixedDeltaTime
        );

        Vector2 vel = rb.linearVelocity;
        vel.x = currentHorizontalSpeed;
        rb.linearVelocity = vel;

        // TODO: 방향에 따라 스프라이트 Flip 처리 등
    }

    private void HandleJumpInput()
    {
        // 점프 조건: 점프 버퍼가 남아 있고, 코요테 타임이 남아 있을 때
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            PerformJump();
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }
    }

    private void PerformJump()
    {
        Vector2 vel = rb.linearVelocity;
        vel.y = jumpForce;
        rb.linearVelocity = vel;
    }

    private void ApplyBetterJumpPhysics()
    {
        float yVel = rb.linearVelocity.y;

        // Apex(점프 고점)에서 중력 약하게
        if (Mathf.Abs(yVel) < apexThreshold)
        {
            rb.gravityScale = baseGravityScale * apexGravityMultiplier;
        }
        else if (yVel < 0f)
        {
            // 하강 시 더 무거운 중력
            rb.gravityScale = baseGravityScale * fallGravityMultiplier;
        }
        else if (yVel > 0f && !jumpHeld)
        {
            // 점프 키를 일찍 떼면 더 무거운 중력 → 숏점프
            rb.gravityScale = baseGravityScale * lowJumpGravityMultiplier;
        }
        else
        {
            rb.gravityScale = baseGravityScale;
        }
    }

    #endregion

    #region Interaction / Menu

    private void HandleInteractionInput()
    {
        if (interactPressedThisFrame)
        {
            OnInteractPressed?.Invoke();
        }

        if (grabPressedThisFrame)
        {
            OnGrabPressed?.Invoke();
        }
    }

    private void HandleMenuInput()
    {
        if (!menuPressedThisFrame) return;

        menuOpen = !menuOpen;
        OnMenuToggled?.Invoke(menuOpen);

        // 일시정지와 연결하고 싶다면 GameManager와 연동하면 됨.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPaused(menuOpen);
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }

    #endregion

    #region Wall Jump (미구현 예시)

    /*
    // TODO: 벽 점프용 상태/체크 예시
    // - 좌우에 Raycast를 쏴서 벽 접촉 여부를 확인
    // - isGrounded == false && isOnWall == true 상태에서 점프 입력 시,
    //   수평 속도를 벽 반대 방향으로, 수직 속도를 jumpForce로 세팅.
    // - Celeste처럼 '클라이밍'까지 가져가려면 별도 스태미나/슬라이딩 로직 필요.

    private bool isOnWall;
    private void CheckWall()
    {
        // 예시) transform.position에서 좌우로 Raycast
    }
    */

    #endregion
}
