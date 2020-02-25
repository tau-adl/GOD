using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BatteryPanelManager : MonoBehaviour
{
    private Sprite batteryNA;
    private Sprite battery100;
    private Sprite battery75;
    private Sprite battery50;
    private Sprite battery25;
    private Sprite batteryLow;

    public TelloSdkClient telloClient;

    private int _frameNumber;

    private RawImage batteryIcon;
    private TMP_Text batteryText;

    // Start is called before the first frame update
    void Start()
    {
        batteryNA = Resources.Load<Sprite>("Battery/battery_na");
        battery100 = Resources.Load<Sprite>("Battery/battery_100");
        battery75 = Resources.Load<Sprite>("Battery/battery_75");
        battery50 = Resources.Load<Sprite>("Battery/battery_50");
        battery25 = Resources.Load<Sprite>("Battery/battery_25");
        batteryLow = Resources.Load<Sprite>("Battery/battery_low");

        batteryIcon = GetComponentInChildren<RawImage>();
        batteryText = GetComponentInChildren<TMP_Text>();
    }

    // Update is called once per frame
    void Update()
    {
        if (++_frameNumber % 30 == 0) {
            var telemetry = telloClient?.Telemetry;
            if (telemetry != null)
            {
                batteryText.text = $"{telemetry.BatteryPercent}%";
                if (telemetry.BatteryPercent > 87)
                    batteryIcon.texture = battery100.texture;
                else if (telemetry.BatteryPercent > 62)
                    batteryIcon.texture = battery75.texture;
                else if (telemetry.BatteryPercent > 37)
                    batteryIcon.texture = battery50.texture;
                else if (telemetry.BatteryPercent >= 25)
                    batteryIcon.texture = battery25.texture;
                else
                    batteryIcon.texture = batteryLow.texture;
            }
            else
            {
                batteryText.text = "N/A";
                batteryIcon.texture = batteryNA.texture;
            }
       }
    }
}
