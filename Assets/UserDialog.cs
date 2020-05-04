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

    private TMP_Text _header;
    private TMP_Text _body;

    private string _headerText;
    private string _bodyText;
    private bool _showOkButton;
    private bool _showCancelButton;

    #endregion Private Fields

    #region Events

    public event EventHandler OkButtonClick;

    public event EventHandler CancelButtonClick;

    #endregion Events

    #region Properties

    public string HeaderText
    {
        get => _headerText;
        set
        {
            _headerText = value;
            if (_header != null)
                _header.text = value;
        }
    }

    public string BodyText
    {
        get => _bodyText;
        set
        {
            _bodyText = value;
            if (_body != null)
                _body.text = value;
        }
    }

    public bool ShowOkButton
    {
        get => _showOkButton;
        set
        {
            _showOkButton = value;
            if (okButton != null)
                okButton.SetActive(value);
        }
    }

    public bool ShowCancelButton
    {
        get => _showCancelButton;
        set
        {
            _showCancelButton = value; 
            if (cancelButton != null)
                cancelButton.SetActive(value);
        }
    }

    public bool IsVisible
    {
        get => gameObject.activeSelf;
        set => gameObject.SetActive(value);
    }

    #endregion Properties

    #region MonoBehaviour

    [UsedImplicitly]
    private void Awake()
    {
        _header = dialogHeader.GetComponentInChildren<TMP_Text>();
        _body = dialogBody.GetComponentInChildren<TMP_Text>();
        _header.text = _headerText;
        _body.text = _bodyText;
        cancelButton.SetActive(ShowCancelButton);
        okButton.SetActive(ShowOkButton);
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
