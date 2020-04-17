using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BatteryPanelManager : MonoBehaviour
{
    private Sprite _batteryUnknown;
    private Sprite _battery100;
    private Sprite _battery75;
    private Sprite _battery50;
    private Sprite _battery25;
    private Sprite _batteryLow;

    public TelloSdkClient telloClient;

    private DroneTelemetry _droneTelemetry;

    private RawImage _batteryIcon;
    private TMP_Text _batteryText;

    private void Awake()
    {
        _batteryUnknown = Resources.Load<Sprite>("Battery/battery_na");
        _battery100 = Resources.Load<Sprite>("Battery/battery_100");
        _battery75 = Resources.Load<Sprite>("Battery/battery_75");
        _battery50 = Resources.Load<Sprite>("Battery/battery_50");
        _battery25 = Resources.Load<Sprite>("Battery/battery_25");
        _batteryLow = Resources.Load<Sprite>("Battery/battery_low");

        _batteryIcon = GetComponentInChildren<RawImage>();
        _batteryText = GetComponentInChildren<TMP_Text>();

        _droneTelemetry = FindObjectOfType<DroneTelemetry>();
        if (_droneTelemetry != null)
            InvokeRepeating("UpdateBatteryStatus", 0, 1);
    }

    private void UpdateBatteryStatus()
    {
        var telemetry = _droneTelemetry.Status == ConnectionStatus.Online
            ? _droneTelemetry.Telemetry
            : null;
        if (telemetry != null)
        {
            _batteryText.text = $"{telemetry.BatteryPercent}%";
            if (telemetry.BatteryPercent > 87)
                _batteryIcon.texture = _battery100.texture;
            else if (telemetry.BatteryPercent > 62)
                _batteryIcon.texture = _battery75.texture;
            else if (telemetry.BatteryPercent > 37)
                _batteryIcon.texture = _battery50.texture;
            else if (telemetry.BatteryPercent >= 25)
                _batteryIcon.texture = _battery25.texture;
            else
                _batteryIcon.texture = _batteryLow.texture;
        }
        else
        {
            _batteryText.text = "N/A";
            _batteryIcon.texture = _batteryUnknown.texture;
        }
    }
}
