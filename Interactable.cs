using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
public class Interactable : MonoBehaviour
{
    public List<SpriteRenderer> outlines;

    [Header("Outline Alpha")]
    [SerializeField] private float hoverOutlineAlpha = 0.5f;
    [SerializeField] private float clickedOutlineAlpha = 1f;

    private static Interactable s_clicked;
    protected bool clicked;

    protected bool hovered = false;

    public bool IsClicked()
    {
        return clicked;
    }

    public static Interactable GetClickedInteractable()
    {
        return s_clicked;
    }

    public virtual void OnMouseDown()
    {
    }

    public virtual void OnMouseUp()
    {
        
    }

    public virtual void Awake()
    {
        HideOutlines();
    }

    /// <summary>
    /// Sets outline enabled + alpha. Does nothing for null outlines.
    /// </summary>
    private void SetOutlines(bool enabled, float alpha)
    {
        float a = Mathf.Clamp01(alpha);

        foreach (var outline in outlines)
        {
            if (outline == null) continue;

            outline.enabled = enabled;

            var c = outline.color;
            c.a = a;
            outline.color = c;
        }
    }

    public void ShowOutlines()
    {
        // Backwards compatibility: show with hover alpha unless clicked.
        SetOutlines(true, clicked ? clickedOutlineAlpha : hoverOutlineAlpha);
    }

    public void HideOutlines()
    {
        SetOutlines(false, hoverOutlineAlpha);
    }

    private void SetClicked(bool clicked)
    {
        this.clicked = clicked;

        if (this.clicked)
        {
            // Persist outline at full alpha.
            SetOutlines(true, clickedOutlineAlpha);
        }
        else
        {
            // Not clicked: only show outline while hovered.
            SetOutlines(false, hoverOutlineAlpha);
        }
    }

    public virtual void InteractableOnMouseEnter()
    {
        hovered = true;
        if (clicked)
        {
            // Keep clicked alpha.
            SetOutlines(true, clickedOutlineAlpha);
        }
        else
        {
            // Hover shows at 0.5 alpha.
            SetOutlines(true, hoverOutlineAlpha);
        }

        OnCursorToolTipEnter();
    }

    public virtual void InteractableOnMouseExit()
    {
        hovered = false;
        // If clicked, outline persists.
        if (clicked)
        {
            SetOutlines(true, clickedOutlineAlpha);
        }
        else
        {
            HideOutlines();
        }

        OnCursorToolTipExit();
    }
    public virtual void OnMouseStay() 
    {
        
    }

    public virtual string GetCursorToolTipText()
    {
        return string.Empty;
    }

    public virtual void OnCursorToolTipEnter()
    {
        if (CursorToolTipController.instance == null) return;
        CursorToolTipController.instance.ShowForInteractable(this);
    }

    public virtual void OnCursorToolTipExit()
    {
        if (CursorToolTipController.instance == null) return;
        CursorToolTipController.instance.HideForInteractable(this);
    }

    /// <summary>
    /// Called by input/controller code to mark this interactable as the one selected/clicked.
    /// Only one interactable can be clicked at a time.
    /// </summary>
    public void ClickSelect()
    {
        if (s_clicked != null && s_clicked != this)
        {
            s_clicked.SetClicked(false);
        }

        s_clicked = this;
        SetClicked(true);
    }
}
