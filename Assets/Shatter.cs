using UnityEngine;

public class Shatter : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool disableSourceRendererOnShatter = true;

    [Header("Pieces")]
    [SerializeField, Min(1)] private int piecesX = 4;
    [SerializeField, Min(1)] private int piecesY = 4;
    [SerializeField] private string pieceNamePrefix = "ShatterPiece";
    [SerializeField, Min(0f)] private float pieceLifetimeMin = 2.5f;
    [SerializeField, Min(0f)] private float pieceLifetimeMax = 4f;

    [Header("Physics")]
    [SerializeField, Min(0f)] private float explosionForce = 6f;
    [SerializeField, Min(0f)] private float randomForce = 1.5f;
    [SerializeField, Min(0f)] private float randomTorque = 120f;
    [SerializeField, Min(0f)] private float gravityScaleMin = 0f;
    [SerializeField, Min(0f)] private float gravityScaleMax = 0f;
    [SerializeField, Min(0f)] private float linearDampingMin = 8f;
    [SerializeField, Min(0f)] private float linearDampingMax = 12f;
    [SerializeField, Min(0f)] private float angularDampingMin = 0.05f;
    [SerializeField, Min(0f)] private float angularDampingMax = 0.25f;
    [SerializeField, Min(0.0001f)] private float massMin = 1f;
    [SerializeField, Min(0.0001f)] private float massMax = 1f;
    [SerializeField] private bool addColliders = true;
    [SerializeField] private bool inheritSourceVelocity = true;

    private bool _hasShattered;

    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        _hasShattered = false;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }
    }

    private void OnDestroy()
    {
        TriggerShatter();
    }

    private void TriggerShatter()
    {
        if (_hasShattered) return;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            Debug.LogWarning("Shatter requires a SpriteRenderer with a Sprite.", this);
            return;
        }

        _hasShattered = true;

        Sprite src = spriteRenderer.sprite;
        Rect sourceRect = src.rect;
        Texture2D texture = src.texture;
        Vector2 sourcePivotPixels = src.pivot;
        float ppu = src.pixelsPerUnit;

        int safePiecesX = Mathf.Max(1, piecesX);
        int safePiecesY = Mathf.Max(1, piecesY);

        float chunkWidthPixels = sourceRect.width / safePiecesX;
        float chunkHeightPixels = sourceRect.height / safePiecesY;

        Vector3 sourceCenter = spriteRenderer.bounds.center;
        Rigidbody2D sourceRb = GetComponent<Rigidbody2D>();

        for (int y = 0; y < safePiecesY; y++)
        {
            for (int x = 0; x < safePiecesX; x++)
            {
                Rect chunkRect = new Rect(
                    sourceRect.x + x * chunkWidthPixels,
                    sourceRect.y + y * chunkHeightPixels,
                    chunkWidthPixels,
                    chunkHeightPixels);

                Vector2 chunkPivot = new Vector2(chunkRect.width * 0.5f, chunkRect.height * 0.5f);
                var chunkSprite = Sprite.Create(texture, chunkRect, chunkPivot / new Vector2(chunkRect.width, chunkRect.height), ppu, 0, SpriteMeshType.FullRect);
                chunkSprite.name = src.name + "_" + x + "_" + y;

                Vector2 sourcePivotOffsetPixels = sourcePivotPixels - new Vector2(sourceRect.width * 0.5f, sourceRect.height * 0.5f);
                Vector2 chunkCenterPixels = new Vector2(
                    (x + 0.5f) * chunkWidthPixels - sourceRect.width * 0.5f,
                    (y + 0.5f) * chunkHeightPixels - sourceRect.height * 0.5f);
                Vector2 localOffsetUnits = (chunkCenterPixels - sourcePivotOffsetPixels) / ppu;

                Vector3 worldPos = spriteRenderer.transform.TransformPoint(localOffsetUnits);

                var piece = new GameObject(pieceNamePrefix + "_" + x + "_" + y);
                piece.transform.SetPositionAndRotation(worldPos, spriteRenderer.transform.rotation);
                piece.transform.localScale = spriteRenderer.transform.lossyScale;

                var pieceSr = piece.AddComponent<SpriteRenderer>();
                pieceSr.sprite = chunkSprite;
                pieceSr.sortingLayerID = spriteRenderer.sortingLayerID;
                pieceSr.sortingOrder = spriteRenderer.sortingOrder + 1;
                pieceSr.color = spriteRenderer.color;
                pieceSr.flipX = spriteRenderer.flipX;
                pieceSr.flipY = spriteRenderer.flipY;
                pieceSr.sharedMaterial = spriteRenderer.sharedMaterial;

                if (addColliders)
                {
                    piece.AddComponent<BoxCollider2D>();
                }

                var rb = piece.AddComponent<Rigidbody2D>();
                rb.gravityScale = RandomRange(gravityScaleMin, gravityScaleMax);
                rb.linearDamping = RandomRange(linearDampingMin, linearDampingMax);
                rb.angularDamping = RandomRange(angularDampingMin, angularDampingMax);
                rb.mass = RandomRange(massMin, massMax);

                if (inheritSourceVelocity && sourceRb != null)
                {
                    rb.linearVelocity = sourceRb.linearVelocity;
                    rb.angularVelocity = sourceRb.angularVelocity;
                }

                Vector2 outward = ((Vector2)worldPos - (Vector2)sourceCenter);
                if (outward.sqrMagnitude <= 0.0001f)
                {
                    outward = Random.insideUnitCircle.normalized;
                }
                else
                {
                    outward.Normalize();
                }

                Vector2 force = outward * explosionForce + Random.insideUnitCircle * randomForce;
                rb.AddForce(force, ForceMode2D.Impulse);

                if (randomTorque > 0f)
                {
                    float torque = Random.Range(-randomTorque, randomTorque);
                    rb.AddTorque(torque, ForceMode2D.Impulse);
                }

                float pieceLifetime = RandomRange(pieceLifetimeMin, pieceLifetimeMax);
                if (pieceLifetime > 0f)
                {
                    Destroy(piece, pieceLifetime);
                }
            }
        }

        if (disableSourceRendererOnShatter)
        {
            spriteRenderer.enabled = false;
        }
    }

    private static float RandomRange(float min, float max)
    {
        if (max < min)
        {
            (min, max) = (max, min);
        }

        return Mathf.Approximately(min, max) ? min : Random.Range(min, max);
    }
}
