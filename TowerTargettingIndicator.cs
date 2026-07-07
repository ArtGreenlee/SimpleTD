using Unity.VisualScripting;
using UnityEngine;

public class TowerTool : Interactable
{
    public float hoverAlphaDelta;
    private float startingAlpha;
    private LineRenderer lr;
    private SRC src;
    private Tower tower;
    public override void Awake()
    {
        base.Awake();
        src = GetComponent<SRC>();
        lr = GetComponent<LineRenderer>();
        startingAlpha = src.srColorInfos[0].alphaOverrideValue;
        tower = GetComponentInParent<Tower>();
        if (tower == null)
        {
            Debug.LogError("unattached targetting indicator");
        }
    }

    public void Start()
    {
        src.ApplyColorToAll(tower.GetDamageType());
        lr.startColor = CM.i.ColorTypeToColor(tower.GetDamageType());
        lr.endColor = CM.i.ColorTypeToColor(tower.GetDamageType());
        SyncLineRendererAlphaWithSrc();
    }

    private void OnEnable()
    {
        if (tower == null) tower = GetComponentInParent<Tower>();
        if (tower != null)
        {
            tower.EnsureTargetIndicatorInValidPosition();
        }
    }

    private void OnDisable()
    {
        if (tower == null) tower = GetComponentInParent<Tower>();
        if (tower != null)
        {
            tower.EnsureTargetIndicatorInValidPosition();
        }
    }

    public override void InteractableOnMouseEnter()
    {
        src.SetSpriteRendererAlpha(startingAlpha + hoverAlphaDelta);
        SyncLineRendererAlphaWithSrc();

        base.InteractableOnMouseEnter();
    }

    public override void OnMouseDown()
    {

        base.OnMouseDown();
    }

    public void Update()
    {
        SyncLineRendererAlphaWithSrc();

        if (Input.GetMouseButtonDown(0) && hovered)
        {
            clicked = true;
            
            // LaserObjectPool.instance.ShowLaser(tower.transform.position, transform.position, CM.i.ColorTypeToColor(tower.GetDamageType()));
        }
        if (Input.GetMouseButton(0) && clicked)
        {
            lr.positionCount = 2;
            lr.SetPosition(0, tower.transform.position);
            lr.SetPosition(1, transform.position);
            lr.enabled = true;
        }
        else
        {
            lr.enabled = false;
        }
        if (Input.GetMouseButtonUp(0) && clicked)
        {
            clicked = false;
            if (tower != null)
            {
                tower.OnTargetIndicatorDropped();
            }
        }
        if (IsClicked())
        {
            Vector3 mousePos = PIC.instance.GetMousePosition();
            var rm = tower.GetRangeManager();
            if (rm == null)
            {
                transform.position = mousePos;
                return;
            }

            Vector3 towerPos = tower.transform.position;
            Vector3 toMouse = mousePos - towerPos;
            toMouse.z = 0f;

            float radius = rm.GetRangeRadius();
            if (radius > 0f && toMouse.sqrMagnitude > radius * radius)
            {
                return;
            }

            mousePos.z = transform.position.z;
            transform.position = mousePos;
        }

    }

    public override void InteractableOnMouseExit()
    {
        src.SetSpriteRendererAlpha(startingAlpha);
        SyncLineRendererAlphaWithSrc();
        base.InteractableOnMouseExit();
    }

    private void SyncLineRendererAlphaWithSrc()
    {
        if (lr == null || src == null) return;

        float alpha = 1f;
        SpriteRenderer primary = src.GetPrimarySpriteRenderer();
        if (primary != null)
        {
            alpha = primary.color.a;
        }

        Color start = lr.startColor;
        Color end = lr.endColor;
        if (!Mathf.Approximately(start.a, alpha) || !Mathf.Approximately(end.a, alpha))
        {
            start.a = alpha;
            end.a = alpha;
            lr.startColor = start;
            lr.endColor = end;
        }
    }
}
