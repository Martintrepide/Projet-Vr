using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum GameState { Menu, Playing, GameOver }

// Boucle de jeu : menu, partie, fin de partie, score.
// Démarrer / recommencer : gâchette d'une manette (ou Entrée au clavier).
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private Player player;
    [SerializeField] private Enemy_Spawner spawner;
    [SerializeField] private string gameTitle = "VR SHOOTER";
    [SerializeField] private float panelDistance = 2.2f;
    [SerializeField] private float inputDelayAfterGameOver = 2f;

    public event Action<GameState> StateChanged;

    public GameState State { get; private set; } = GameState.Menu;
    public int Score { get; private set; }
    public int Kills { get; private set; }
    public int Wave => spawner != null ? spawner.CurrentWave : 0;
    public int BestScore { get; private set; }
    public Player Player => player;
    public Enemy_Spawner Spawner => spawner;

    const string BestKey = "VRShooter.BestScore";
    static bool startImmediatelyAfterReload;

    private InputAction startAction;
    private Canvas panel;
    private TextMeshProUGUI panelText;
    private float stateTime;
    private bool panelPlaced;

    private void Awake()
    {
        Instance = this;
        // Les murs low poly sont souvent à une seule face : sans ceci, les rayons qui les touchent par l'arrière passent à travers.
        Physics.queriesHitBackfaces = true;
        if (player == null) player = FindAnyObjectByType<Player>();
        if (spawner == null) spawner = FindAnyObjectByType<Enemy_Spawner>();
        BestScore = PlayerPrefs.GetInt(BestKey, 0);

        startAction = new InputAction("StartGame", InputActionType.Button);
        startAction.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
        startAction.AddBinding("<XRController>{RightHand}/{TriggerButton}");
        startAction.AddBinding("<Keyboard>/enter");
        startAction.performed += OnStartPressed;
        startAction.Enable();

        BuildPanel();
    }

    private void Start()
    {
        if (player != null) player.Died += OnPlayerDied;
        if (spawner != null)
        {
            spawner.EnemyKilled += OnEnemyKilled;
            spawner.WaveStarted += OnWaveStarted;
        }

        if (startImmediatelyAfterReload)
        {
            startImmediatelyAfterReload = false;
            BeginGame();
        }
        else
        {
            SetState(GameState.Menu);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (startAction != null)
        {
            startAction.performed -= OnStartPressed;
            startAction.Dispose();
        }
        if (player != null) player.Died -= OnPlayerDied;
        if (spawner != null)
        {
            spawner.EnemyKilled -= OnEnemyKilled;
            spawner.WaveStarted -= OnWaveStarted;
        }
    }

    private void Update()
    {
        stateTime += Time.unscaledDeltaTime;
        // Le casque n'est suivi qu'après quelques images : on place le panneau un peu plus tard.
        if (State != GameState.Playing && (!panelPlaced || stateTime < 0.5f || PanelOutOfView()))
            PlacePanel();
    }

    private void OnStartPressed(InputAction.CallbackContext ctx)
    {
        if (State == GameState.Menu && stateTime > 0.5f)
            BeginGame();
        else if (State == GameState.GameOver && stateTime > inputDelayAfterGameOver)
            Restart();
    }

    public void BeginGame()
    {
        Score = 0;
        Kills = 0;
        if (player != null) player.ResetHealth();
        SetState(GameState.Playing);
        if (spawner != null) spawner.StartWaves();
    }

    public void Restart()
    {
        startImmediatelyAfterReload = true;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);
    }

    private void OnPlayerDied()
    {
        if (State != GameState.Playing) return;
        if (spawner != null) spawner.StopWaves();
        if (Score > BestScore)
        {
            BestScore = Score;
            PlayerPrefs.SetInt(BestKey, BestScore);
            PlayerPrefs.Save();
        }
        SetState(GameState.GameOver);
    }

    private void OnEnemyKilled(EnemyAI1 enemy)
    {
        Kills++;
        Score += enemy.ScoreValue * Mathf.Max(1, Wave);
    }

    private void OnWaveStarted(int wave) { }

    private void SetState(GameState s)
    {
        State = s;
        stateTime = 0f;
        panelPlaced = false;
        panel.gameObject.SetActive(s != GameState.Playing);

        if (s == GameState.Menu)
            panelText.text = "<size=140%><b>" + gameTitle + "</b></size>\n\n"
                + "Prenez une arme et survivez aux vagues.\n"
                + "Stick : se téléporter.  A / X : recharger.\n\n"
                + "<b>Gâchette pour commencer</b>"
                + (BestScore > 0 ? "\n\nMeilleur score : " + BestScore : "");
        else if (s == GameState.GameOver)
            panelText.text = "<size=140%><b>PARTIE TERMINÉE</b></size>\n\n"
                + "Vague " + Wave + "   Ennemis abattus : " + Kills + "\n"
                + "Score : " + Score + "   Meilleur : " + BestScore + "\n\n"
                + "<b>Gâchette pour recommencer</b>";

        StateChanged?.Invoke(s);
    }

    private void BuildPanel()
    {
        panel = UiFactory.WorldCanvas("Game Panel", null, new Vector2(1.2f, 0.7f));
        UiFactory.Panel(panel.transform, new Color(0f, 0f, 0f, 0.75f));
        panelText = UiFactory.Text(panel.transform, "Text", 52f, TextAlignmentOptions.Center);
    }

    private bool PanelOutOfView()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 toPanel = panel.transform.position - cam.transform.position;
        return Vector3.Angle(cam.transform.forward, toPanel) > 70f;
    }

    private void PlacePanel()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        panel.transform.position = cam.transform.position + fwd * panelDistance;
        panel.transform.rotation = Quaternion.LookRotation(fwd);
        panelPlaced = true;
    }
}
