using UnityEngine;


/// <summary>
/// S-05: 서바이벌 수치를 시간에 따라 변화시키는 시스템.
/// - PlayerStatus를 직접 수정하여 허기, 갈증, 피로도, 스태미나를 갱신.
/// - HP 0 이후 데드엔딩 연출/씬 전환은 GameManager에서 처리.
/// - 여기서는 "수치 변화 로직"에만 집중.
/// </summary>
public class SurvivalSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStatus playerStatus;

    /// <summary>
    /// 달리기/점프 등 격한 행동 중인지 여부.
    /// PlayerController에서 달리기/점프 상태에 따라 SetExerting(true/false)를 호출하는 식으로 사용.
    /// </summary>
    private bool isExerting;

    [Header("Hunger (허기)")]
    [Tooltip("분당 허기 감소량. (포만감 → 배고픔으로 감소)")]
    [SerializeField] private float hungerDecreasePerMinute = 5f;

    [Tooltip("허기 임계값. 이 값 미만이면 페널티(예: 스태미나 최대치 감소, HP 감소 등) 적용 후보.")]
    [SerializeField] private float hungerCriticalThreshold = 20f;

    [Header("Thirst (갈증)")]
    [Tooltip("분당 갈증 감소량. (충분한 수분 → 갈증으로 감소)")]
    [SerializeField] private float thirstDecreasePerMinute = 8f;

    [Tooltip("갈증 임계값.")]
    [SerializeField] private float thirstCriticalThreshold = 20f;

    [Header("Fatigue (피로도)")]
    [Tooltip("분당 피로도 증가량. (0 = 상쾌, 100 = 극도로 피곤)")]
    [SerializeField] private float fatigueIncreasePerMinute = 3f;

    [Tooltip("휴식 상태일 때 분당 피로도 회복량 (음수 값으로 두고 감소시켜도 됨).")]
    [SerializeField] private float fatigueRecoveryPerMinute = -6f;

    [Tooltip("피로도 임계값. 이 값 이상이면 페널티 후보.")]
    [SerializeField] private float fatigueCriticalThreshold = 80f;

    [Header("Stamina (스태미나)")]
    [Tooltip("초당 스태미나 자연 회복량 (비격한 행동 중이 아닐 때).")]
    [SerializeField] private float staminaRecoverPerSecond = 15f;

    [Tooltip("허기/갈증/피로도가 나쁠 때 스태미나 회복에 곱해질 배율.")]
    [SerializeField] private float staminaRecoveryPenaltyMultiplier = 0.5f;

    [Header("Critical HP Effects")]
    [Tooltip("굶주림/탈수/과로가 심할 때 HP를 깎을지 여부.")]
    [SerializeField] private bool enableCriticalHpDamage = true;

    [Tooltip("심각한 허기/갈증/피로 상태에서 분당 HP 감소량.")]
    [SerializeField] private float criticalHpDamagePerMinute = 5f;

    private void Reset()
    {
        // 에디터에서 컴포넌트 붙일 때 자동 할당 용도
        if (playerStatus == null)
        {
            playerStatus = FindObjectOfType<PlayerStatus>();
        }
    }

    private void Awake()
    {
        if (playerStatus == null && PlayerStatus.Instance != null)
        {
            playerStatus = PlayerStatus.Instance;
        }

        if (playerStatus == null)
        {
            Debug.LogError("[SurvivalSystem] PlayerStatus 레퍼런스가 없습니다.");
        }
    }

    private void Update()
    {
        if (playerStatus == null) return;
        if (GameManager.Instance != null &&
            GameManager.Instance.GetCurrentState() == GameManager.GameState.Paused)
        {
            // 일시정지 상태에서는 수치 변화 없음
            return;
        }

        float dt = Time.deltaTime;
        float minutes = dt / 60f;

        UpdateHunger(minutes);
        UpdateThirst(minutes);
        UpdateFatigue(minutes);
        UpdateStamina(dt);
        ApplyCriticalHpDamage(minutes);
    }

    #region Public API (PlayerController 등에서 호출)

    /// <summary>
    /// 플레이어가 격한 행동(달리기/점프/전투 등)을 하고 있는지 외부에서 알려주는 메서드.
    /// PlayerController에서 달리기 키 입력/점프 중일 때 true, 아닐 때 false로 세팅하면 된다.
    /// </summary>
    public void SetExerting(bool value)
    {
        isExerting = value;
    }

    #endregion

    #region Internal Updates

    private void UpdateHunger(float minutes)
    {
        if (hungerDecreasePerMinute <= 0f) return;

        // 0에 가까울수록 배고픔, 100에 가까울수록 포만감.
        float delta = -hungerDecreasePerMinute * minutes;
        playerStatus.AddHunger(delta);
    }

    private void UpdateThirst(float minutes)
    {
        if (thirstDecreasePerMinute <= 0f) return;

        float delta = -thirstDecreasePerMinute * minutes;
        playerStatus.AddThirst(delta);
    }

    private void UpdateFatigue(float minutes)
    {
        // 피로도는 기본적으로 증가 방향 (분당 양수)
        float delta = fatigueIncreasePerMinute * minutes;

        // TODO: 휴식 상태(침대/의자 사용, 벙커에서 휴식 등)일 때는
        // fatigueRecoveryPerMinute(보통 음수)로 덮어쓰거나 더해주는 로직을 넣으면 된다.

        playerStatus.AddFatigue(delta);
    }

    private void UpdateStamina(float dt)
    {
        if (staminaRecoverPerSecond <= 0f) return;

        // 격한 행동 중에는 PlayerController가 직접 ConsumeStamina() 호출한다고 가정.
        // 여기서는 "자연 회복"만 다룸.
        if (isExerting) return;

        float multiplier = 1f;

        // 허기/갈증/피로도 상태에 따라 회복량 패널티 적용
        bool isHungerBad = playerStatus.Hunger <= hungerCriticalThreshold;
        bool isThirstBad = playerStatus.Thirst <= thirstCriticalThreshold;
        bool isFatigueBad = playerStatus.Fatigue >= fatigueCriticalThreshold;

        if (isHungerBad || isThirstBad || isFatigueBad)
        {
            multiplier *= staminaRecoveryPenaltyMultiplier;
        }

        float amount = staminaRecoverPerSecond * multiplier * dt;
        if (amount > 0f)
        {
            playerStatus.RecoverStamina(amount);
        }
    }

    private void ApplyCriticalHpDamage(float minutes)
    {
        if (!enableCriticalHpDamage) return;
        if (criticalHpDamagePerMinute <= 0f) return;
        if (playerStatus.IsDead) return;

        bool isHungerBad = playerStatus.Hunger <= 0f;
        bool isThirstBad = playerStatus.Thirst <= 0f;
        bool isFatigueBad = playerStatus.Fatigue >= 100f;

        // 최소 한 가지 이상이 심각하면 HP를 서서히 깎는다.
        if (isHungerBad || isThirstBad || isFatigueBad)
        {
            float damage = criticalHpDamagePerMinute * minutes;
            playerStatus.ApplyDamage(damage);
            // HP가 0이 되는 순간 OnDeath 이벤트가 PlayerStatus에서 호출되고,
            // GameManager가 그 이벤트를 구독하여 데드엔딩 처리하면 된다.
        }
    }

    #endregion
}

