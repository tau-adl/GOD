using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameSettingsManager : MonoBehaviour
{
    public InputField scaleFactorInputField;
    public InputField biasInputField;
    public Slider joystickSensitivitySlider;

    public Toggle demoModeToggle;

    [UsedImplicitly]
    private void Start()
    {
        demoModeToggle.isOn = GodSettings.GetDemoMode();
        scaleFactorInputField.text = GodSettings.GetDronePositionScaleFactorText();
        biasInputField.text = GodSettings.GetDronePositionBiasText();
        joystickSensitivitySlider.value = GodSettings.GetJoystickSensitivity();
    }

    public void ShowMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void SaveSettings()
    {
        GodSettings.SetDemoMode(demoModeToggle.isOn);
        GodSettings.TrySetDronePositionBias(biasInputField.text);
        GodSettings.TrySetDronePositionScaleFactor(scaleFactorInputField.text);
        GodSettings.TrySetJoystickSensitivity((int) joystickSensitivitySlider.value);
    }

    [UsedImplicitly]
    public void OnOkButtonClicked()
    {
        SaveSettings();
        ShowMainMenu();
    }

    [UsedImplicitly]
    public void OnCancelButtonClicked()
    {
        ShowMainMenu();
    }
}
