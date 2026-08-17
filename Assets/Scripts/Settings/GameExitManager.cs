using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Обязательно подключаем новую систему ввода

namespace SpellSystem.Core
{
    public class GameExitManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject exitMenuPanel;
        [SerializeField] private Button btnYes;
        [SerializeField] private Button btnNo;

        private bool isExitMenuOpen = false;

        private void Start()
        {
            if (exitMenuPanel != null)
                exitMenuPanel.SetActive(false);

            if (btnYes != null)
                btnYes.onClick.AddListener(QuitGame);

            if (btnNo != null)
                btnNo.onClick.AddListener(CloseExitMenu);
        }

        private void Update()
        {
            // ИСПРАВЛЕНИЕ: Читаем кнопку через новую систему ввода
            // На Android кнопка "Назад" эмулируется как Escape
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (isExitMenuOpen)
                {
                    CloseExitMenu();
                }
                else
                {
                    OpenExitMenu();
                }
            }
        }

        public void OpenExitMenu()
        {
            isExitMenuOpen = true;
            if (exitMenuPanel != null)
                exitMenuPanel.SetActive(true);

            Time.timeScale = 0f;
        }

        public void CloseExitMenu()
        {
            isExitMenuOpen = false;
            if (exitMenuPanel != null)
                exitMenuPanel.SetActive(false);

            Time.timeScale = 1f;
        }

        private void QuitGame()
        {
            Time.timeScale = 1f;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}