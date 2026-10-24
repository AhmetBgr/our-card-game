using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A generic yes/no question: a title, a message, a confirm and a cancel button over a dimmed screen.
/// Open it from anywhere with <see cref="Show"/>; nothing has to be placed in the scene. The prefab
/// (Resources/<see cref="ResourcePath"/>, built by Tools ▸ UI ▸ Build Confirmation Popup) carries its
/// own overlay canvas, so it draws above every scene canvas and works in any scene, and is instantiated
/// on first use and destroyed with the scene.
///
/// Escape (or a click on the dim backdrop) cancels, Enter confirms. Animations run on unscaled time, so
/// it works over the paused pause menu too. Only one question is up at a time: asking a new one while
/// one is open replaces it, cancelling the first.
/// </summary>
public class ConfirmationPopup : MonoBehaviour
{
    public const string ResourcePath = "UI/ConfirmationPopup";

    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform frame;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TextMeshProUGUI confirmLabel;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI cancelLabel;

    [Tooltip("Optional: a click on the dimmed backdrop cancels.")]
    [SerializeField] private Button backdropButton;

    [SerializeField] private string defaultConfirmLabel = "Confirm";
    [SerializeField] private string defaultCancelLabel = "Cancel";

    [Tooltip("Tint of the confirm button for a destructive question (abandoning, deleting).")]
    [SerializeField] private Color destructiveTint = new Color(1f, 0.55f, 0.5f, 1f);

    [SerializeField] private float openDuration = 0.18f;
    [SerializeField] private float closeDuration = 0.12f;

    private static ConfirmationPopup _instance;

    private Action _onConfirm;
    private Action _onCancel;
    private Color _confirmBaseColor = Color.white;
    private bool _baseColorKnown;

    /// <summary>A question is on screen. Escape handlers elsewhere (the pause menu) should leave the key to it.</summary>
    public static bool IsOpen => _instance != null && _instance._open;
    private bool _open;

    /// <summary>
    /// True while a question is up, and for the rest of the frame one was closed in: the Escape that
    /// cancelled it must not also reach another Escape handler later in the same frame.
    /// </summary>
    public static bool ConsumesEscape => IsOpen || Time.frameCount == _closedFrame;
    private static int _closedFrame = -1;

    /// <summary>
    /// Asks the player to confirm. <paramref name="onConfirm"/> runs if they do, <paramref name="onCancel"/>
    /// if they back out. Labels left null use the prefab's defaults; <paramref name="destructive"/> tints
    /// the confirm button as a warning. Returns false when the popup prefab is missing.
    /// </summary>
    public static bool Show(string title, string message, Action onConfirm, Action onCancel = null,
        string confirmText = null, string cancelText = null, bool destructive = false)
    {
        var popup = Resolve();
        if (popup == null)
        {
            Debug.LogError($"ConfirmationPopup: no prefab at Resources/{ResourcePath}. Build it with Tools ▸ UI ▸ Build Confirmation Popup.");
            return false;
        }

        popup.Open(title, message, onConfirm, onCancel, confirmText, cancelText, destructive);
        return true;
    }

    /// <summary>Closes the question on screen as if cancelled. No-op when none is.</summary>
    public static void Dismiss()
    {
        if (IsOpen) _instance.Cancel();
    }

    private static ConfirmationPopup Resolve()
    {
        if (_instance != null) return _instance;

        var prefab = Resources.Load<ConfirmationPopup>(ResourcePath);
        if (prefab == null) return null;

        _instance = Instantiate(prefab);
        _instance.name = prefab.name;
        _instance.gameObject.SetActive(false);
        return _instance;
    }

    [Tooltip("Sorting order of the popup's overlay canvas: above every scene canvas.")]
    [SerializeField] private int sortingOrder = 1000;

    void Awake()
    {
        // Always an overlay above everything, however the prefab's canvas was left.
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }

        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
        if (backdropButton != null) backdropButton.onClick.AddListener(Cancel);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        DOTween.Kill(this);
    }

    void Update()
    {
        if (!_open) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Cancel();
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Confirm();
    }

    private void Open(string title, string message, Action onConfirm, Action onCancel,
        string confirmText, string cancelText, bool destructive)
    {
        // A question already up is answered "no" before this one replaces it.
        if (_open)
        {
            var previousCancel = _onCancel;
            _onConfirm = _onCancel = null;
            previousCancel?.Invoke();
        }

        _onConfirm = onConfirm;
        _onCancel = onCancel;

        if (titleText != null)
        {
            titleText.text = title ?? string.Empty;
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
        }
        if (messageText != null)
        {
            messageText.text = message ?? string.Empty;
            messageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
        if (confirmLabel != null) confirmLabel.text = string.IsNullOrEmpty(confirmText) ? defaultConfirmLabel : confirmText;
        if (cancelLabel != null) cancelLabel.text = string.IsNullOrEmpty(cancelText) ? defaultCancelLabel : cancelText;

        var confirmImage = confirmButton != null ? confirmButton.targetGraphic : null;
        if (confirmImage != null)
        {
            if (!_baseColorKnown)
            {
                _confirmBaseColor = confirmImage.color;
                _baseColorKnown = true;
            }
            confirmImage.color = destructive ? destructiveTint : _confirmBaseColor;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _open = true;
        SetInteractable(true);

        DOTween.Kill(this);
        if (group != null)
        {
            group.alpha = 0f;
            group.DOFade(1f, openDuration).SetUpdate(true).SetTarget(this);
        }
        if (frame != null)
        {
            frame.localScale = Vector3.one * 0.85f;
            frame.DOScale(1f, openDuration).SetEase(Ease.OutBack).SetUpdate(true).SetTarget(this);
        }
    }

    private void Confirm() => Close(_onConfirm);

    private void Cancel() => Close(_onCancel);

    private void Close(Action then)
    {
        if (!_open) return;
        _open = false;
        _closedFrame = Time.frameCount;
        _onConfirm = _onCancel = null;
        SetInteractable(false);

        DOTween.Kill(this);
        if (group != null && closeDuration > 0f)
            group.DOFade(0f, closeDuration).SetUpdate(true).SetTarget(this)
                .OnComplete(() => { if (!_open) gameObject.SetActive(false); });
        else
            gameObject.SetActive(false);

        // After the popup has let go of its state, so the callback may open another question.
        then?.Invoke();
    }

    private void SetInteractable(bool on)
    {
        if (group == null) return;
        group.interactable = on;
        group.blocksRaycasts = on;
    }
}
