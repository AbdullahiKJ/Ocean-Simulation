using UnityEngine;
using UnityEngine.UI;

public class SelectableButton : MonoBehaviour
{
    [Header("Button")]
    [SerializeField] private Button button;
    [SerializeField] private Image buttonImage;

    [Header("Sprites")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;

    private SelectableButtonGroup buttonGroup;
    private bool isSelected;

    public bool IsSelected => isSelected;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (buttonImage == null)
            buttonImage = GetComponent<Image>();
    }

    private void Start()
    {
        button.onClick.AddListener(OnClicked);
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(OnClicked);
    }

    public void SetGroup(SelectableButtonGroup group)
    {
        buttonGroup = group;
    }

    private void OnClicked()
    {
        if (buttonGroup != null)
        {
            buttonGroup.SelectButton(this);
        }
        else
        {
            SetSelected(true);
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (buttonImage != null)
        {
            buttonImage.sprite = selected
                ? selectedSprite
                : normalSprite;
        }
    }
}
