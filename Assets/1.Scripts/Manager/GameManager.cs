using UnityEngine;

using UnityEngine.SceneManagement;

/// <summary>
/// ReLive 전체 게임 흐름을 관리하는 상위 매니저.
/// - S-00: Game State / Scene Flow 역할
/// - 씬 전환, Day 카운트, 일시정지 상태 등 상위 상태를 관리한다.
/// 다른 시스템(S-01~S-12)은 여기의 상태를 참고하여 동작을 제한/허용한다.
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>전역에서 접근 가능한 단일 인스턴스.</summary>
    public static GameManager Instance { get; private set; }

    /// <summary>게임의 상위 상태.</summary>
    public enum GameState
    {
        Title,
        Bunker,
        Ground,
        Ending,
        Paused
    }

    [Header("State")]
    [Tooltip("디버그용 현재 상태 표시")]
    [SerializeField] private GameState currentState = GameState.Title;

    /// <summary>현재 일차 (Day).</summary>
    public int CurrentDay { get; private set; } = 1;

    [Header("Scene Names")]
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private string bunkerSceneName = "Bunker";
    [SerializeField] private string groundSceneName = "Ground";
    [SerializeField] private string endingSceneName = "Ending";

    private void Awake()
    {
        // Singleton 세팅
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 초기 상태는 Title 씬 기준
        currentState = GameState.Title;
    }

    /// <summary>
    /// 새 회차를 시작한다.
    /// - Day를 1로 리셋
    /// - 벙커 씬으로 이동
    /// - 추후 S-01(데이 루프), S-07(세이브) 초기화 로직 연동 예정
    /// </summary>
    public void StartNewRun()
    {
        CurrentDay = 1;
        LoadScene(bunkerSceneName);
        currentState = GameState.Bunker;
    }

    /// <summary>
    /// 벙커 씬으로 이동한다. (지상 → 벙커 귀환 등)
    /// </summary>
    public void GoToBunker()
    {
        LoadScene(bunkerSceneName);
        currentState = GameState.Bunker;
    }

    /// <summary>
    /// 지상 파밍 씬으로 이동한다.
    /// Day 시간 흐름은 S-01(데이 루프 시스템)에서 실제로 처리하도록 위임 예정.
    /// </summary>
    public void GoToGround()
    {
        LoadScene(groundSceneName);
        currentState = GameState.Ground;
    }

    /// <summary>
    /// 엔딩 씬으로 이동한다.
    /// 여기서 S-08(진행도/해금) 업데이트 후 메인 타이틀로 복귀 가능.
    /// </summary>
    public void GoToEnding()
    {
        LoadScene(endingSceneName);
        currentState = GameState.Ending;
    }

    /// <summary>
    /// 일시정지 On/Off.
    /// 실제 UI 동작은 S-06(UI/UX Shell)에서 구현하고,
    /// 여기서는 Time.timeScale과 상위 상태만 관리한다.
    /// </summary>
    public void SetPaused(bool paused)
    {
        if (paused)
        {
            if (currentState == GameState.Paused) return;
            Time.timeScale = 0f;
            currentState = GameState.Paused;
        }
        else
        {
            if (currentState != GameState.Paused) return;
            Time.timeScale = 1f;
            // TODO: 이전 상태 복원 로직(Title/ Bunker/ Ground/ Ending 등) 필요 시 추가
            // 일단은 벙커/지상 등에서만 Pause를 쓴다고 가정하고,
            // 외부에서 줄 때 현재 씬에 맞는 상태를 다시 세팅하도록 할 수도 있다.
        }
    }

    /// <summary>
    /// 다음 Day로 넘길 때 호출.
    /// 실제 세이브/로직은 S-01, S-07과 연동 예정.
    /// </summary>
    public void AdvanceDay()
    {
        CurrentDay++;
        // TODO: Day 증가에 따른 난이도 인덱스, 파밍 난이도 조정 등은 별도 시스템에서 처리.
    }

    /// <summary>
    /// 지정한 씬 이름을 로드한다.
    /// 추후 로딩 화면/비동기 로딩으로 확장 가능.
    /// </summary>
    private void LoadScene(string sceneName)
    {
        // TODO: 로딩 화면, 페이드 인/아웃 등 연출 추가 가능
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// 현재 상위 상태를 반환한다.
    /// PlayerController(S-12)나 UI(S-06)에서 입력 허용 여부 판단에 사용.
    /// </summary>
    public GameState GetCurrentState()
    {
        return currentState;
    }
}

