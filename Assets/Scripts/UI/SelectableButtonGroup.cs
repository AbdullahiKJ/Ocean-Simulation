using System.Collections.Generic;
using UnityEngine;

public class SelectableButtonGroup : MonoBehaviour
{
    [Header("Buttons in this group")]
    [SerializeField] private List<SelectableButton> buttons = new List<SelectableButton>();

    [Header("Initial Selection")]
    [SerializeField] private int activeButtonIndex = 0;

    public int ActiveButtonIndex => activeButtonIndex;

    public SelectableButton ActiveButton
    {
        get
        {
            if (activeButtonIndex < 0 || activeButtonIndex >= buttons.Count)
                return null;

            return buttons[activeButtonIndex];
        }
    }

    private void Start()
    {
        InitialiseGroup();
    }

    private void InitialiseGroup()
    {
        // Assign this group to every button
        foreach (SelectableButton button in buttons)
        {
            if (button != null)
            {
                button.SetGroup(this);
                button.SetSelected(false);
            }
        }

        // Select the initial button
        if (buttons.Count > 0)
        {
            activeButtonIndex = Mathf.Clamp(
                activeButtonIndex,
                0,
                buttons.Count - 1
            );

            buttons[activeButtonIndex].SetSelected(true);
        }
        else
        {
            activeButtonIndex = -1;
        }
    }

    public void SelectButton(SelectableButton selectedButton)
    {
        int newIndex = buttons.IndexOf(selectedButton);

        // Button isn't part of this group
        if (newIndex == -1)
            return;

        // Reset every button in this group
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] != null)
            {
                buttons[i].SetSelected(i == newIndex);
            }
        }

        // Store the newly selected index
        activeButtonIndex = newIndex;
    }

    public void SelectButton(int index)
    {
        if (index < 0 || index >= buttons.Count)
            return;

        SelectButton(buttons[index]);
    }

    public void ResetGroup()
    {
        foreach (SelectableButton button in buttons)
        {
            if (button != null)
                button.SetSelected(false);
        }

        activeButtonIndex = -1;
    }

    public List<SelectableButton> GetButtons()
    {
        return buttons;
    }

    public SelectableButton GetButtonByIndex(int index)
    {
        if (index < 0 || index >= buttons.Count)
            return null;

        return buttons[index];
    }
}