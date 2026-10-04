using System;
using UnityEngine.UI;
using UnityEngine;

namespace MobaTrainer.UI
{
    /// <summary>Pause overlay: Resume / Restart / Settings / Main Menu.</summary>
    public class PausePanel : UIPanel
    {
        [Header("Buttons")]
        public Button resumeButton;
        public Button restartButton;
        public Button settingsButton;
        public Button menuButton;

        public event Action ResumeRequested;
        public event Action RestartRequested;
        public event Action SettingsRequested;
        public event Action MenuRequested;

        private void Awake()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(() => ResumeRequested?.Invoke());
            if (restartButton != null) restartButton.onClick.AddListener(() => RestartRequested?.Invoke());
            if (settingsButton != null) settingsButton.onClick.AddListener(() => SettingsRequested?.Invoke());
            if (menuButton != null) menuButton.onClick.AddListener(() => MenuRequested?.Invoke());
        }
    }
}
