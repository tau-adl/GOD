using System;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameSettingsManager : MonoBehaviour
{
    public static class SettingKeys
    {
        public const string MissionPadNumber = "MissionPadNumber";
        public const string DroneStickerName = "DroneStickerName";
        public const string DemoMode = "DemoMode";
    }

    public TMP_Dropdown missionPadDropDown;
    public TMP_Dropdown droneStickerDropDown;

    public Toggle demoModeToggle;

    private void SetMissionPadNumber(int missionPadNumber)
    {
        if (missionPadNumber < 1 || missionPadNumber > 8)
            throw new ArgumentOutOfRangeException(nameof(missionPadNumber), missionPadNumber,
                $"Argument '{nameof(missionPadNumber)}' must be between 1 and 8.");
        missionPadDropDown.value = missionPadNumber - 1;
    }

    private void SetDroneSticker([NotNull] string stickerName)
    {
        for (var i = 0; i < droneStickerDropDown.options.Count; ++i)
        {
            var option = droneStickerDropDown.options[i];
            if (stickerName.Equals(option.text, StringComparison.InvariantCultureIgnoreCase))
            {
                droneStickerDropDown.value = i;
                break;
            }
        }
    }

    private void SetDemoMode(int demoMode)
    {
        demoModeToggle.isOn = demoMode != 0;
    }

    private int GetMissionPadNumber()
    {
        return missionPadDropDown.value + 1;
    }

    private string GetDroneStickerName()
    {
        var index = droneStickerDropDown.value;
        return droneStickerDropDown.options[index].text;
    }

    private int GetDemoMode()
    {
        return demoModeToggle.isOn ? 1 : 0;
    }

    [UsedImplicitly]
    private void Start()
    {
        var selectedMissionPadNumber = PlayerPrefs.GetInt(SettingKeys.MissionPadNumber, 6);
        var selectedSticker = PlayerPrefs.GetString(SettingKeys.DroneStickerName, "Simple-Blue");
        var demoMode = PlayerPrefs.GetInt(SettingKeys.DemoMode, 0);
        SetMissionPadNumber(selectedMissionPadNumber);
        SetDroneSticker(selectedSticker);
        SetDemoMode(demoMode);
    }

    public void ShowMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void SaveSettings()
    {
        var missionPadNumber = GetMissionPadNumber();
        var droneStickerName = GetDroneStickerName();
        var demoMode = GetDemoMode();
        PlayerPrefs.SetInt(SettingKeys.MissionPadNumber, missionPadNumber);
        PlayerPrefs.SetString(SettingKeys.DroneStickerName, droneStickerName);
        PlayerPrefs.SetInt(SettingKeys.DemoMode, demoMode);
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
