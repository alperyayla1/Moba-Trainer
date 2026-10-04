using MobaTrainer.Core;
using MobaTrainer.Gameplay;
using MobaTrainer.Input;
using MobaTrainer.Persistence;
using MobaTrainer.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MobaTrainer.Flow
{
    /// <summary>
    /// Top-level state machine: Menu (title / drills / stats) → Countdown → Playing ⇄ Paused → Results,
    /// plus the Settings overlay. The arena stays alive behind the menus as a slowly orbiting background.
    /// A run is either Free Practice (the sandbox) or a <see cref="Scenario"/> drill.
    /// </summary>
    public class GameFlowController : MonoBehaviour
    {
        public enum State { Menu, Countdown, Playing, Paused, Results }

        public static GameFlowController Instance { get; private set; }

        [Header("Panels")]
        public MainMenuController mainMenu;
        public DrillsPanel drillsPanel;
        public HUDController hud;
        public PausePanel pausePanel;
        public SettingsPanel settingsPanel;
        public ResultsPanel resultsPanel;
        public StatsPanel statsPanel;

        [Header("World")]
        public GameObject arenaRoot;
        public SandboxController sandbox;
        public PlayerController player;
        public MobaCameraController cameraController;

        [Header("Drills")]
        public SkillshotScenario skillshot;
        public DodgeScenario dodge;
        public DuelScenario duel;
        public KitingScenario kiting;
        public LastHitScenario lastHit;
        public BlinkScenario blink;
        public ComboScenario combo;
        public AwarenessScenario awareness;
        public float countdownSeconds = 3f;

        public State CurrentState { get; private set; } = State.Menu;

        private Scenario activeScenario;     // null = free practice
        private State stateBeforePause;
        private float countdown;
        private int shownCount = -1;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            mainMenu.AddItem("Practice", StartSandbox);
            mainMenu.AddItem("Drills", OpenDrills);
            if (statsPanel != null) mainMenu.AddItem("Stats", OpenStatsFromMenu);
            if (statsPanel == null || resultsPanel == null || skillshot == null)
            {
                Debug.LogError("[MOBA Trainer] The scene is out of date. Run Tools > MOBA Trainer > Build Main Scene.");
            }
            mainMenu.AddItem("Settings", OpenSettings);
            mainMenu.AddItem("Quit", Quit);

            drillsPanel.AddDrill("Free Practice", UIIcon.Play, true, StartSandbox);
            drillsPanel.AddDrill("Skillshot", UIIcon.Skillshot, skillshot != null, () => StartScenario(skillshot));
            drillsPanel.AddDrill("Dodge", UIIcon.Dodge, dodge != null, () => StartScenario(dodge));
            drillsPanel.AddDrill("Duel", UIIcon.Duel, duel != null, () => StartScenario(duel));
            drillsPanel.AddDrill("Kiting", UIIcon.Kiting, kiting != null, () => StartScenario(kiting));
            drillsPanel.AddDrill("Last Hit", UIIcon.LastHit, lastHit != null, () => StartScenario(lastHit));
            drillsPanel.AddDrill("Blink", UIIcon.Blink, blink != null, () => StartScenario(blink));
            drillsPanel.AddDrill("Combo", UIIcon.Combo, combo != null, () => StartScenario(combo));
            drillsPanel.AddDrill("Awareness", UIIcon.Radar, awareness != null, () => StartScenario(awareness));
            drillsPanel.BackRequested += CloseDrills;

            pausePanel.ResumeRequested += Resume;
            pausePanel.RestartRequested += Restart;
            pausePanel.SettingsRequested += OpenSettings;
            pausePanel.MenuRequested += ReturnToMenu;

            if (resultsPanel != null)
            {
                resultsPanel.RetryRequested += Restart;
                resultsPanel.StatsRequested += OpenStatsFromResults;
                resultsPanel.MenuRequested += ReturnToMenu;
            }
            if (statsPanel != null) statsPanel.BackRequested += CloseStats;

            foreach (Scenario s in Scenarios())
            {
                s.Completed += OnScenarioCompleted;
                s.gameObject.SetActive(false);
            }

            hud.Bind(player.Abilities, cameraController);

            hud.Hide(true);
            pausePanel.Hide(true);
            settingsPanel.Hide(true);
            drillsPanel.Hide(true);
            if (resultsPanel != null) resultsPanel.Hide(true);
            if (statsPanel != null) statsPanel.Hide(true);
            EnterMenuWorld();
            mainMenu.Show(true);
        }

        private Scenario[] Scenarios()
        {
            var list = new System.Collections.Generic.List<Scenario>();
            foreach (Scenario s in new Scenario[] { skillshot, dodge, duel, kiting, lastHit, blink, combo, awareness })
            {
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }

        private void Update()
        {
            InputManager input = InputManager.Instance;
            if (input == null) return;

            if (!input.SuppressShortcuts && input.Pressed(GameAction.Pause))
            {
                if (settingsPanel.IsOpen) settingsPanel.Close();
                else if (statsPanel != null && statsPanel.IsOpen) CloseStats();
                else if (drillsPanel.IsOpen) CloseDrills();
                else if (CurrentState == State.Playing || CurrentState == State.Countdown) Pause();
                else if (CurrentState == State.Paused) Resume();
                else if (CurrentState == State.Results) ReturnToMenu();
            }

            if (CurrentState == State.Menu) MenuStroll(input);

            if (CurrentState != State.Menu && !settingsPanel.IsOpen && !input.SuppressShortcuts && input.Pressed(GameAction.Restart))
            {
                Restart();
            }

            if (CurrentState == State.Countdown) TickCountdown();
        }

        // ------------------------------------------------------------------ menu world

        /// <summary>Arena as a living backdrop: no dummies, idle player, orbiting camera.</summary>
        private void EnterMenuWorld()
        {
            Time.timeScale = 1f;
            EndActivities();
            FxSystem.Instance?.ClearAll();
            PlayerSkillSystem.Instance?.ClearAll();
            EnemySkillSystem.Instance?.ClearAll();
            if (arenaRoot != null) arenaRoot.SetActive(true);
            player.ResetPlayer(Vector3.zero);
            player.InputEnabled = false;
            if (player.Unit.healthBar != null) player.Unit.healthBar.gameObject.SetActive(false);
            cameraController.GameplayActive = false;
            cameraController.OrbitMode = true;
            CursorService.Instance?.SetGameplayMode(false);
            hud.SetCenterText(string.Empty);
            CurrentState = State.Menu;
        }

        /// <summary>Title screen toy: clicking the arena (not the menu) walks the hero there.</summary>
        private void MenuStroll(InputManager input)
        {
            if (settingsPanel.IsOpen || drillsPanel.IsOpen || (statsPanel != null && statsPanel.IsOpen)) return;
            if (!input.Pressed(GameAction.Move) && !input.Pressed(GameAction.Select)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            CursorService cursor = CursorService.Instance;
            Camera cam = cameraController != null ? cameraController.GetComponent<Camera>() : Camera.main;
            if (cursor == null || cam == null || !cursor.TryGetGroundPoint(cam, out Vector3 point)) return;
            player.Motor.MoveTo(point);
            FxSystem.Instance?.ClickIndicator(Arena.ClampStatic(point, player.Unit.radius), Palette.Hex(0x6FD35A, 0.9f));
        }

        private void OpenDrills()
        {
            mainMenu.Hide();
            drillsPanel.Show();
        }

        private void CloseDrills()
        {
            drillsPanel.Hide();
            mainMenu.Show();
        }

        private void OpenStatsFromMenu()
        {
            if (statsPanel == null) return;
            mainMenu.Hide();
            statsPanel.Show();
        }

        private void OpenStatsFromResults()
        {
            if (statsPanel == null) return;
            string mode = activeScenario != null ? activeScenario.Id : "skillshot";
            ReturnToMenu();
            mainMenu.Hide(true);
            statsPanel.ShowMode(mode);
        }

        private void CloseStats()
        {
            if (statsPanel != null) statsPanel.Hide();
            mainMenu.Show();
        }

        // ------------------------------------------------------------------ runs

        public void StartSandbox()
        {
            activeScenario = null;
            HideMenus();
            BeginRun();
            hud.SetTitle(string.Empty, string.Empty);
            hud.SetStatsProvider(null);
            hud.SetCounters(sandbox);
            hud.Show();
        }

        public void StartScenario(Scenario scenario)
        {
            if (scenario == null) return;
            activeScenario = scenario;
            HideMenus();
            BeginRun();
            hud.SetTitle(string.Empty, string.Empty);
            hud.SetStatsProvider(null);
            hud.SetCounters(scenario);
            hud.Show();
        }

        private void HideMenus()
        {
            mainMenu.Hide();
            drillsPanel.Hide();
            if (statsPanel != null) statsPanel.Hide();
            if (resultsPanel != null) resultsPanel.Hide(true);
        }

        private void EndActivities()
        {
            if (sandbox != null) sandbox.End();
            foreach (Scenario s in Scenarios()) if (s.gameObject.activeSelf) s.End();
        }

        private void BeginRun()
        {
            Time.timeScale = 1f;
            EndActivities();
            FxSystem.Instance?.ClearAll();
            PlayerSkillSystem.Instance?.ClearAll();
            EnemySkillSystem.Instance?.ClearAll();

            if (activeScenario == null) sandbox.Begin();
            else activeScenario.Begin();

            if (player.Unit.healthBar != null) player.Unit.healthBar.gameObject.SetActive(true);
            cameraController.OrbitMode = false;
            cameraController.target = player.transform;
            cameraController.ResetCamera();
            cameraController.GameplayActive = true;
            CursorService.Instance?.SetGameplayMode(true);

            if (activeScenario == null)
            {
                player.InputEnabled = true;
                hud.SetCenterText(string.Empty);
                CurrentState = State.Playing;
            }
            else
            {
                // Countdown: you can look around, but nothing moves yet.
                player.InputEnabled = false;
                countdown = countdownSeconds;
                shownCount = -1;
                CurrentState = State.Countdown;
            }
        }

        private void TickCountdown()
        {
            countdown -= Time.deltaTime;
            int n = Mathf.CeilToInt(countdown);
            if (n != shownCount)
            {
                shownCount = n;
                hud.SetCenterText(n > 0 ? n.ToString() : "GO");
            }
            if (countdown > 0f) return;
            hud.SetCenterText(string.Empty);
            player.InputEnabled = true;
            CurrentState = State.Playing;
            activeScenario.StartRunning();
        }

        private void OnScenarioCompleted(Scenario s, RunRecord record)
        {
            if (s != activeScenario) return;
            int previousBest = StatsStore.Add(record);
            player.InputEnabled = false;
            player.Motor.Stop();
            CursorService.Instance?.SetGameplayMode(false);
            CurrentState = State.Results;
            if (resultsPanel != null) resultsPanel.ShowResult(s.Title, record, previousBest);
        }

        public void Pause()
        {
            if (CurrentState != State.Playing && CurrentState != State.Countdown) return;
            stateBeforePause = CurrentState;
            CurrentState = State.Paused;
            Time.timeScale = 0f;
            player.InputEnabled = false;
            CursorService.Instance?.SetGameplayMode(false);
            pausePanel.Show();
        }

        public void Resume()
        {
            if (CurrentState != State.Paused) return;
            if (settingsPanel.IsOpen) settingsPanel.Close();
            pausePanel.Hide();
            Time.timeScale = 1f;
            CurrentState = stateBeforePause;
            player.InputEnabled = CurrentState == State.Playing;
            CursorService.Instance?.SetGameplayMode(true);
        }

        public void Restart()
        {
            if (CurrentState == State.Menu) return;
            if (settingsPanel.IsOpen) settingsPanel.Close();
            pausePanel.Hide(true);
            if (resultsPanel != null) resultsPanel.Hide(true);
            BeginRun();
        }

        public void ReturnToMenu()
        {
            if (settingsPanel.IsOpen) settingsPanel.Close();
            pausePanel.Hide();
            if (resultsPanel != null) resultsPanel.Hide();
            hud.Hide();
            hud.SetStatsProvider(null);
            hud.SetCounters(null);
            EnterMenuWorld();
            mainMenu.Show();
        }

        public void OpenSettings()
        {
            settingsPanel.Show();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
