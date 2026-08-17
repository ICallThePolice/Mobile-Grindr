using UnityEngine;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        Application.targetFrameRate = 60;
    }

    void Update()
    {
        // ХАК: Принудительно читаем гироскоп и переворачиваем экран, даже если в системе стоит "замок"
        if (Input.deviceOrientation == DeviceOrientation.LandscapeLeft)
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }
        else if (Input.deviceOrientation == DeviceOrientation.LandscapeRight)
        {
            Screen.orientation = ScreenOrientation.LandscapeRight;
        }
    }
}