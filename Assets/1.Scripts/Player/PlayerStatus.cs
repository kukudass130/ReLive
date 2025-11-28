using System;
using UnityEngine;


/// <summary>
/// 플레이어의 상태 수치를 보관하는 중간 DB 역할.
/// - HP, 허기, 갈증, 피로도, MAX 중량, 스태미나를 관리.
/// - S-05(Survival System)가 이 데이터를 읽고/수정하면서
///   수치 감소, 임계 효과, 회복 로직을 구현.
/// - 다른 시스템은 이 컴포넌트만 참조하면 플레이어 상태를 알 수 있다.
/// </summary>
public class PlayerStatus : MonoBehaviour
{
    /// <summary>
    /// 전역에서 플레이어 상태에 접근해야 할 때 사용할 수 있는 단일 인스턴스.
    /// 멀티플레이 계획이 없다면 간단히 Singleton으로 써도 무방.
    /// </summary>
    public static PlayerStatus Instance { get; private set; }

    [Header("HP (Health)")]
    [Tooltip("플레이어 생명력. 0이 되면 데드 엔딩 조건.")]
    [Range(0f, 100f)]
    [SerializeField] private float hp = 100f;

    [Tooltip("HP 최대값 (기본 100). 회복 시 이 값을 넘지 않는다.")]
    [SerializeField] private float maxHp = 100f;

    [Header("Hunger (허기)")]
    [Tooltip("0에 가까울수록 배고픔, 100에 가까울수록 포만감.")]
    [Range(0f, 100f)]
    [SerializeField] private float hunger = 100f;

    [Header("Thirst (갈증)")]
    [Tooltip("0에 가까울수록 갈증, 100에 가까울수록 충분한 수분 상태.")]
    [Range(0f, 100f)]
    [SerializeField] private float thirst = 100f;

    [Header("Fatigue (피로도)")]
    [Tooltip("0에 가까울수록 컨디션 좋음, 100에 가까울수록 피곤.")]
    [Range(0f, 100f)]
    [SerializeField] private float fatigue = 0f;

    [Header("Max Carry Weight (MAX 중량)")]
    [Tooltip("플레이어가 들 수 있는 무게 한계. 초과 시 이동 불가 등 페널티 발생.")]
    [Range(0f, 100f)]
    [SerializeField] private float maxCarryWeight = 100f;

    [Header("Stamina (스태미나)")]
    [Tooltip("점프/달리기 등 행동에 소모되는 체력. 시간이 지나면 회복.")]
    [Range(0f, 100f)]
    [SerializeField] private float stamina = 100f;

    [Tooltip("스태미나 최대값 (허기/갈증/피로도에 따라 동적으로 바뀔 수 있음).")]
    [SerializeField] private float maxStamina = 100f;

    #region Public Properties

    public float HP => hp;
    public float MaxHP => maxHp;

    public float Hunger => hunger;
    public float Thirst => thirst;
    public float Fatigue => fatigue;

    public float MaxCarryWeight => maxCarryWeight;

    public float Stamina => stamina;
    public float MaxStamina => maxStamina;

    /// <summary>HP가 0 이하인지 여부 (데드 엔딩 후보 상태).</summary>
    public bool IsDead => hp <= 0f;

    #endregion

    #region Events

    /// <summary>HP 값이 변경될 때(old, new) 호출.</summary>
    public event Action<float, float> OnHpChanged;

    /// <summary>허기 값이 변경될 때(old, new) 호출.</summary>
    public event Action<float, float> OnHungerChanged;

    /// <summary>갈증 값이 변경될 때(old, new) 호출.</summary>
    public event Action<float, float> OnThirstChanged;

    /// <summary>피로도 값이 변경될 때(old, new) 호출.</summary>
    public event Action<float, float> OnFatigueChanged;

    /// <summary>스태미나 값이 변경될 때(old, new) 호출.</summary>
    public event Action<float, float> OnStaminaChanged;

    /// <summary>HP 0 도달 시 호출. GameManager/S-05가 이 이벤트를 구독해서 데드 엔딩 처리.</summary>
    public event Action OnDeath;

    #endregion

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #region HP

    /// <summary>
    /// HP를 직접 세팅한다. (세이브 로드 등)
    /// </summary>
    public void SetHp(float value)
    {
        float old = hp;
        hp = Mathf.Clamp(value, 0f, maxHp);

        if (!Mathf.Approximately(old, hp))
        {
            OnHpChanged?.Invoke(old, hp);
            if (hp <= 0f)
            {
                OnDeath?.Invoke();
            }
        }
    }

    /// <summary>데미지 적용 (양수 값).</summary>
    public void ApplyDamage(float amount)
    {
        if (amount <= 0f) return;
        SetHp(hp - amount);
    }

    /// <summary>회복 적용 (양수 값).</summary>
    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        SetHp(hp + amount);
    }

    #endregion

    #region Hunger / Thirst / Fatigue

    public void SetHunger(float value)
    {
        float old = hunger;
        hunger = Mathf.Clamp(value, 0f, 100f);
        if (!Mathf.Approximately(old, hunger))
        {
            OnHungerChanged?.Invoke(old, hunger);
        }
    }

    public void AddHunger(float delta)
    {
        if (Mathf.Approximately(delta, 0f)) return;
        SetHunger(hunger + delta);
    }

    public void SetThirst(float value)
    {
        float old = thirst;
        thirst = Mathf.Clamp(value, 0f, 100f);
        if (!Mathf.Approximately(old, thirst))
        {
            OnThirstChanged?.Invoke(old, thirst);
        }
    }

    public void AddThirst(float delta)
    {
        if (Mathf.Approximately(delta, 0f)) return;
        SetThirst(thirst + delta);
    }

    public void SetFatigue(float value)
    {
        float old = fatigue;
        fatigue = Mathf.Clamp(value, 0f, 100f);
        if (!Mathf.Approximately(old, fatigue))
        {
            OnFatigueChanged?.Invoke(old, fatigue);
        }
    }

    public void AddFatigue(float delta)
    {
        if (Mathf.Approximately(delta, 0f)) return;
        SetFatigue(fatigue + delta);
    }

    #endregion

    #region Carry Weight

    /// <summary>
    /// MAX 중량 값을 세팅.
    /// 아이템/장비/버프에 따라 변동될 수 있음.
    /// </summary>
    public void SetMaxCarryWeight(float value)
    {
        maxCarryWeight = Mathf.Clamp(value, 0f, 100f);
        // 필요하면 여기에도 변경 이벤트 추가 가능
    }

    #endregion

    #region Stamina

    public void SetStamina(float value)
    {
        float old = stamina;
        stamina = Mathf.Clamp(value, 0f, maxStamina);
        if (!Mathf.Approximately(old, stamina))
        {
            OnStaminaChanged?.Invoke(old, stamina);
        }
    }

    /// <summary>스태미나 소모. (달리기/점프 등에서 호출)</summary>
    public bool ConsumeStamina(float amount)
    {
        if (amount <= 0f) return true;

        if (stamina < amount)
        {
            // 스태미나 부족 → 행동 불가 등 처리할 때 false 활용 가능
            return false;
        }

        SetStamina(stamina - amount);
        return true;
    }

    /// <summary>스태미나 회복.</summary>
    public void RecoverStamina(float amount)
    {
        if (amount <= 0f) return;
        SetStamina(stamina + amount);
    }

    /// <summary>스태미나 최대값 변경 (허기/갈증/피로도 상태에 따라 S-05에서 조정).</summary>
    public void SetMaxStamina(float value, bool clampCurrent = true)
    {
        maxStamina = Mathf.Max(0f, value);
        if (clampCurrent)
        {
            SetStamina(stamina); // 자동으로 새 max에 맞춰 clamp
        }
    }

    #endregion
}

