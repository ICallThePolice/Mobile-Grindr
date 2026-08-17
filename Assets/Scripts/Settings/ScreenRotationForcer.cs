using UnityEngine;

namespace SpellSystem.Core
{
    public class ScreenRotationForcer : MonoBehaviour
    {
        private void Start()
        {
            // Отключаем засыпание экрана, чтобы телефон не гас во время долгой игры
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // На всякий случай дублируем настройки из Project Settings
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;

            // Ставим стартовую ориентацию
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }

        private void Update()
        {
            // Читаем физический наклон телефона (акселерометр)
            // Игнорируем блокировку поворота экрана в самом Android

            if (Input.acceleration.x <= -0.5f)
            {
                // Наклон влево
                if (Screen.orientation != ScreenOrientation.LandscapeLeft)
                {
                    Screen.orientation = ScreenOrientation.LandscapeLeft;
                }
            }
            else if (Input.acceleration.x >= 0.5f)
            {
                // Наклон вправо
                if (Screen.orientation != ScreenOrientation.LandscapeRight)
                {
                    Screen.orientation = ScreenOrientation.LandscapeRight;
                }
            }
        }
    }
}