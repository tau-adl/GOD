using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatusPanelManager : MonoBehaviour
{
    #region Fields

    private Sprite _droneLedOff;
    private Sprite _droneLedGreen;
    private Sprite _droneLedYellow;
    private Sprite _droneLedRed;

    private Sprite _userLedOff;
    private Sprite _userLedYellow;
    private Sprite _userLedGreen;
    private Sprite _userLedCyan;

    private Sprite _arLedOff;
    private Sprite _arLedYellow;
    private Sprite _arLedGreen;

    private int _statusFlags;

    public RawImage droneLed;
    public RawImage userLed;
    public RawImage arLed;

    public TMP_Text text;

    #endregion Fields

    #region Properties

    public GameStatusFlags StatusFlags => (GameStatusFlags) _statusFlags;
    public float StatusLedsUpdateIntervalSeconds { get; set; } = 0.5F;

    #endregion Properties

    #region MonoBehaviour

    private void Awake()
    {
        _droneLedOff = Resources.Load<Sprite>("Leds/drone-led-off");
        _droneLedGreen = Resources.Load<Sprite>("Leds/drone-led-green");
        _droneLedYellow = Resources.Load<Sprite>("Leds/drone-led-yellow");
        _droneLedRed = Resources.Load<Sprite>("Leds/drone-led-red");

        _userLedOff = Resources.Load<Sprite>("Leds/user-led-off");
        _userLedYellow = Resources.Load<Sprite>("Leds/user-led-yellow");
        _userLedGreen = Resources.Load<Sprite>("Leds/user-led-green");
        _userLedCyan = Resources.Load<Sprite>("Leds/user-led-cyan");

        _arLedOff = Resources.Load<Sprite>("Leds/ar-led-off");
        _arLedYellow = Resources.Load<Sprite>("Leds/ar-led-yellow");
        _arLedGreen = Resources.Load<Sprite>("Leds/ar-led-green");
        text.text = string.Empty;
    }

    private void Start()
    {
        InvokeRepeating(nameof(UpdateStatusLeds), 0, StatusLedsUpdateIntervalSeconds);
    }

    #endregion MonoBehaviour

    #region Methods

    private void UpdateStatusLeds()
    {
        var statusFlags = GetStatusFlagsAndResetValueChanged();
        if (!statusFlags.HasFlag(GameStatusFlags.ValueChanged))
            return;

        droneLed.texture =
            statusFlags.HasFlag(GameStatusFlags.DroneReady)
                ? statusFlags.HasFlag(GameStatusFlags.DroneAirborne)
                    ? _droneLedGreen.texture
                    : _droneLedYellow.texture
                : _droneLedOff.texture;
        userLed.texture =
            statusFlags.HasFlag(GameStatusFlags.PartnerConnected)
                ? statusFlags.HasFlag(GameStatusFlags.PartnerReady)
                    ? _userLedGreen.texture
                    : _userLedYellow.texture
                : _userLedOff.texture;
        arLed.texture =
            statusFlags.HasFlag(GameStatusFlags.VuforiaReady)
                ? _arLedGreen.texture
                : _arLedOff.texture;
    }

    /// <summary>
    /// Safely set a flag on <see cref="GameStatus"/>.
    /// </summary>
    /// <param name="flag">the flag to set.</param>
    /// <returns>The new value of <see cref="GameStatus"/>.</returns>
    public GameStatusFlags SetStatusFlag(GameStatusFlags flag)
    {
        return flag.SetOnField(ref _statusFlags);
    }

    /// <summary>
    /// Safely unset a flag on <see cref="GameStatus"/>.
    /// </summary>
    /// <param name="flag">the flag to unset.</param>
    /// <returns>The new value of <see cref="GameStatus"/>.</returns>
    public GameStatusFlags UnsetStatusFlag(GameStatusFlags flag)
    {
        return flag.UnsetOnField(ref _statusFlags);
    }

    public GameStatusFlags ChangeStatusFlag(GameStatusFlags flag, bool flagValue)
    {
        return flagValue ? SetStatusFlag(flag) : UnsetStatusFlag(flag);
    }

    /// <summary>
    /// Safely get the value of <see cref="GameStatus"/> and reset the <seealso cref="GameStatusFlags.ValueChanged"/> flag.
    /// </summary>
    /// <returns>The original value of <see cref="GameStatus"/>, before the <seealso cref="GameStatusFlags.ValueChanged"/> flag was reset.</returns>
    private GameStatusFlags GetStatusFlagsAndResetValueChanged()
    {
        return GameStatusFlagsExtensions.GetAndResetChanges(ref _statusFlags);
    }

    public void SetText(string text)
    {
        this.text.text = text;
    }

    #endregion Methods
}
