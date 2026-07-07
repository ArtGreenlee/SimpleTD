using System.Collections.Generic;
using UnityEngine;

public class BackgroundRenderer : MonoBehaviour
{
    private struct MeshInstance
    {
        public Mesh Mesh;
        public Matrix4x4 Matrix;
    }

    private readonly List<MeshInstance> _meshes = new List<MeshInstance>();

    private Material material;

    public Texture2D texture;

    [Tooltip("Optional material used when drawing the overlay meshes into the texture. If null, a default unlit color material is used.")]
    [SerializeField] private Material overlayMaterial;

    private string materialTextureString = "_Texture2D";

    private RenderTexture _rt;
    private Texture2D _combinedTexture;
    private Camera _renderCamera;
    private bool _dirty;
    public Mesh testMesh;
    public Transform testMeshTransform;

    private void Awake()
    {
        var mr = GetComponent<MeshRenderer>();
        if (mr != null) material = mr.material;
        EnsureResources();
        RenderOverlayIntoTexture();
    }

    public void AddMesh(Transform position, Mesh mesh)
    {
        if (mesh == null || position == null) return;

        _meshes.Add(new MeshInstance
        {
            Mesh = mesh,
            Matrix = position.localToWorldMatrix
        });

        _dirty = true;
    }

    private void LateUpdate()
    {
        if (!_dirty) return;
        _dirty = false;
        RenderOverlayIntoTexture();
    }

    private void OnDestroy()
    {
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
        }

        if (_renderCamera != null)
        {
            Destroy(_renderCamera.gameObject);
        }

        if (_combinedTexture != null)
        {
            Destroy(_combinedTexture);
        }

        if (overlayMaterial == null && _overlayMaterialRuntime != null)
        {
            Destroy(_overlayMaterialRuntime);
        }
    }

    private Material _overlayMaterialRuntime;

    private Material GetOverlayMaterial()
    {
        if (overlayMaterial != null) return overlayMaterial;
        if (_overlayMaterialRuntime != null) return _overlayMaterialRuntime;

        var shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        _overlayMaterialRuntime = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
        _overlayMaterialRuntime.color = Color.white;
        return _overlayMaterialRuntime;
    }

    private void EnsureResources()
    {
        if (texture == null)
        {
            texture = new Texture2D(1024, 1024, TextureFormat.RGBA32, mipChain: false);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

        int w = Mathf.Max(1, texture.width);
        int h = Mathf.Max(1, texture.height);

        if (_rt == null || _rt.width != w || _rt.height != h)
        {
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }
            _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _rt.Create();
        }

        if (_combinedTexture == null || _combinedTexture.width != w || _combinedTexture.height != h)
        {
            if (_combinedTexture != null) Destroy(_combinedTexture);
            _combinedTexture = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
        }

        if (_renderCamera == null)
        {
            var go = new GameObject("BackgroundRendererCamera");
            go.hideFlags = HideFlags.HideAndDontSave;

            _renderCamera = go.AddComponent<Camera>();
            _renderCamera.enabled = false;
            _renderCamera.orthographic = true;
            _renderCamera.clearFlags = CameraClearFlags.Nothing;
            _renderCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _renderCamera.cullingMask = 0;
            _renderCamera.nearClipPlane = -100f;
            _renderCamera.farClipPlane = 100f;
        }
    }

    private void ApplyTextureToMaterial()
    {
        if (material == null) return;
        if (_combinedTexture == null) return;

        material.SetTexture(materialTextureString, _combinedTexture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", _combinedTexture);
    }

    private void SetupCameraForMeshes()
    {
        if (_renderCamera == null) return;
        if (_rt == null) return;

        Bounds b = default;
        bool has = false;

        for (int i = 0; i < _meshes.Count; i++)
        {
            var mi = _meshes[i];
            if (mi.Mesh == null) continue;

            if (!has)
            {
                var p = mi.Matrix.MultiplyPoint3x4(mi.Mesh.bounds.center);
                b = new Bounds(p, Vector3.zero);
                has = true;
            }

            var mb = mi.Mesh.bounds;
            Vector3 c = mb.center;
            Vector3 e = mb.extents;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 p = mi.Matrix.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3(sx, sy, sz)));
                b.Encapsulate(p);
            }
        }

        if (!has)
        {
            _renderCamera.transform.position = new Vector3(0f, 0f, -10f);
            _renderCamera.orthographicSize = 5f;
            _renderCamera.aspect = _rt.width / (float)_rt.height;
            return;
        }

        _renderCamera.aspect = _rt.width / (float)_rt.height;
        _renderCamera.transform.rotation = Quaternion.identity;
        _renderCamera.transform.position = new Vector3(b.center.x, b.center.y, -10f);

        float pad = 0.5f;
        float halfH = b.extents.y + pad;
        float halfW = (b.extents.x + pad) / Mathf.Max(0.0001f, _renderCamera.aspect);
        _renderCamera.orthographicSize = Mathf.Max(halfH, halfW);
    }

    private void RenderOverlayIntoTexture()
    {
        EnsureResources();
        if (_rt == null || _combinedTexture == null) return;

        // Start with the base texture.
        Graphics.Blit(texture, _rt);

        // Render all overlay meshes into the render texture.
        SetupCameraForMeshes();
        _renderCamera.targetTexture = _rt;

        Material mat = GetOverlayMaterial();
        for (int i = 0; i < _meshes.Count; i++)
        {
            var mi = _meshes[i];
            if (mi.Mesh == null) continue;
            Graphics.DrawMesh(mi.Mesh, mi.Matrix, mat, layer: 0, camera: _renderCamera);
        }
        _renderCamera.Render();

        // Read back into a Texture2D and apply to the background material.
        var prev = RenderTexture.active;
        RenderTexture.active = _rt;
        _combinedTexture.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
        _combinedTexture.Apply(updateMipmaps: false);
        RenderTexture.active = prev;

        ApplyTextureToMaterial();
    }
}
