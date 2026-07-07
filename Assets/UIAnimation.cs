using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class UIAnimation : MonoBehaviour
{
    public List<UnityEngine.UI.RawImage> shopImages;
    public List<UnityEngine.UI.RawImage> inventoryImages;
    public List<UnityEngine.UI.RawImage> craftingImages;
    public List<UnityEngine.UI.RawImage> relicShopImages;
    private List<Material> shopMaterials = new List<Material>();
    private List<Material> inventoryMaterials = new List<Material>();
    private List<Material> craftingMaterials = new List<Material>();
    private string shineEffectMaterialString = "_ShineLocation";
    private bool shopIndicated = false;
    private bool inventoryIndicated = false;
    private bool craftingIndicated = false;
    private string shineOnMaterialString = "SHINE_ON";
    private string shineGlowMaterialString = "_ShineGlow";
    private string shineWidthMaterialString = "_ShineWidth";
    private float shineWidth = 0.128f;
    private string shineRadialMaterialString = "_ShineRotate";
    private readonly Dictionary<Material, Coroutine> activeShineRoutines = new Dictionary<Material, Coroutine>();

    public float shineDuration = 1f; // Duration of the shine effect in seconds
    public float glowIntensity = 5f; // Intensity of the glow effect
    public static UIAnimation instance;

    private void Awake()
    {
        instance = this;
        //build and copy the materials to the lists
        foreach (var image in shopImages)
        {
            if (image == null || !image.gameObject.activeInHierarchy) continue;
            var mat = new Material(image.material);
            shopMaterials.Add(mat);
            image.material = mat;
        }
        foreach (var image in inventoryImages)
        {
            var mat = new Material(image.material);
            inventoryMaterials.Add(mat);
            image.material = mat;
        }
        foreach (var image in craftingImages)
        {
            var mat = new Material(image.material);
            craftingMaterials.Add(mat);
            image.material = mat;
        }

    }

    private void Update()
    {
        // if (Input.GetMouseButtonDown(1))
        // {
        //     IndicateShop();
        // }
        // if (Input.GetMouseButtonDown(0))
        // {
        //     IndicateInventory();
        // }
    }
    public void IndicateShop()
    {
        if (shopIndicated) return;
        Shine(shopMaterials);
        List<Transform> transforms = new List<Transform>();
        foreach (var image in shopImages)
        {
            if (image == null || !image.gameObject.activeInHierarchy) continue;
            transforms.Add(image.transform);
        }
        AOEIndicate(transforms);
    }

    public void AOEIndicate(List<Transform> targets)
    {
        foreach (var target in targets)
        {
            AOEObjectPool.instance.Indicate(target.position, 0.5f, Color.white);
        }
    }

    public void IndicateInventory()
    {
        if (inventoryIndicated) return;
        Shine(inventoryMaterials);
    }

    public void IndicateCrafting()
    {
        if (craftingIndicated) return;
        Shine(craftingMaterials);
    }

    public void AnimateMaterial(Material mat)
    {
        Shine(new List<Material> { mat });
    }

    public void AnimateMaterials(List<Material> mats)
    {
        Shine(mats);
    }

    public void Shine(List<Material> materials)
    {
        if (materials == null || materials.Count == 0) return;

        for (int i = 0; i < materials.Count; i++)
        {
            var mat = materials[i];
            if (mat == null) continue;
            RestartShine(mat);
        }
    }

    private void RestartShine(Material mat)
    {
        if (activeShineRoutines.TryGetValue(mat, out var running) && running != null)
        {
            StopCoroutine(running);
        }

        Coroutine routine = StartCoroutine(ShineRoutine(mat));
        activeShineRoutines[mat] = routine;
    }

    private IEnumerator ShineRoutine(Material mat)
    {
        if (mat == null) yield break;

        mat.SetFloat(shineOnMaterialString, 1); // Enable the shine effect
        mat.SetFloat(shineGlowMaterialString, glowIntensity); // Enable the shine glow
        mat.SetFloat(shineEffectMaterialString, 0f); // Always restart from beginning when retriggered
        // mat.SetFloat(shineWidthMaterialString, shineWidth); // Set shine width

        float elapsedTime = 0f;
        float duration = shineDuration; // Duration of the animation in seconds
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float shinePosition = Mathf.Lerp(0, 1, elapsedTime / duration); // Move from left to right
            mat.SetFloat(shineEffectMaterialString, shinePosition);
            yield return null; // Wait for the next frame
        }
        // go back the other way
        elapsedTime = 0;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float shinePosition = Mathf.Lerp(1, 0, elapsedTime / duration); // Move from right to left
            mat.SetFloat(shineEffectMaterialString, shinePosition);
            yield return null; // Wait for the next frame
        }
        // Ensure the final position is set
        mat.SetFloat(shineOnMaterialString, 0); // Disable the shine effect
        mat.SetFloat(shineGlowMaterialString, 0); // Disable the shine glow
        // mat.SetFloat(shineWidthMaterialString, 0); // Reset shine width

        if (activeShineRoutines.TryGetValue(mat, out var current) && current == null)
        {
            activeShineRoutines.Remove(mat);
        }
        else
        {
            activeShineRoutines.Remove(mat);
        }
    }

}
