using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameSettingsManager : MonoBehaviour
{
    public InputField scaleFactorInputField;
    public InputField biasInputField;
    public Slider joystickSensitivitySlider;
    public Slider ballSpeedSlider;

    public Toggle demoModeToggle;
    public Toggle gravityToggle;

    [UsedImplicitly]
    private void Start()
    {
        demoModeToggle.isOn = GodSettings.GetDemoMode();
        scaleFactorInputField.text = GodSettings.GetDronePositionScaleFactorText();
        biasInputField.text = GodSettings.GetDronePositionBiasText();
        joystickSensitivitySlider.value = GodSettings.GetJoystickSensitivity();
        ballSpeedSlider.value = GodSettings.GetBallSpeed();
        gravityToggle.isOn = Math.Abs(GodSettings.GetGravity()) > 0;
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
        GodSettings.TrySetBallSpeed(ballSpeedSlider.value);
        GodSettings.SetGravity(gravityToggle.isOn ? -9.81F : 0);
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
