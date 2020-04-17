using System;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

// Some C# features are not supported in MonoBehaviour scripts:
// ReSharper disable UseNullPropagation

public class UserDialog : MonoBehaviour
{
    #region Public Fields

    public GameObject dialogHeader;
    public GameObject dialogBody;
    public GameObject cancelButton;
    public GameObject okButton;

    #endregion Public Fields

    #region Private Fields

    private TMP_Text _headerText;
    private TMP_Text _bodyText;

    #endregion Private Fields

    #region Events

    public event EventHandler OkButtonClick;

    public event EventHandler CancelButtonClick;

    #endregion Events

    #region Properties

    public string HeaderText
    {
        get => _headerText.text;
        set => _headerText.text = value;
    }

    public string BodyText
    {
        get => _bodyText.text;
        set => _bodyText.text = value;
    }

    public bool ShowOkButton
    {
        get => okButton.activeSelf;
        set => okButton.SetActive(value);
    }

    public bool ShowCancelButton
    {
        get => cancelButton.activeSelf;
        set => cancelButton.SetActive(value);
    }

    public bool IsVisible
    {
        get => gameObject.activeSelf;
        set
        {
            if (IsVisible != value)
                gameObject.SetActive(value);
        }
    }

    #endregion Properties

    #region MonoBehaviour

    [UsedImplicitly]
    private void Awake()
    {
        _headerText = dialogHeader.GetComponentInChildren<TMP_Text>();
        _bodyText = dialogBody.GetComponentInChildren<TMP_Text>();
    }

    #endregion MonoBehaviour

    #region Methods

    [UsedImplicitly]
    public void OnOkButtonClicked()
    {
        var handler = OkButtonClick;
        if (handler != null)
            handler.Invoke(this, EventArgs.Empty);
    }

    [UsedImplicitly]
    public void OnCancelButtonClicked()
    {
        var handler = CancelButtonClick;
        if (handler != null)
            handler.Invoke(this, EventArgs.Empty);
    }

    #endregion Methods
}
