using UnityEngine;

namespace SebsGlobe.Clouds
{
    public class CloudTextureGenerator : MonoBehaviour
    {
        [SerializeField] private int textureSize = 128;
        [SerializeField] private float noiseScale = 10f;
        [SerializeField] private AnimationCurve falloffCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        
        [ContextMenu("Generate Cloud Texture")]
        public void GenerateCloudTexture()
        {
            Texture2D cloudTexture = CreateCloudTexture();
            
            // Save as asset
            #if UNITY_EDITOR
            string path = "Assets/Graphics/CloudTexture.png";
            System.IO.File.WriteAllBytes(path, cloudTexture.EncodeToPNG());
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log($"Cloud texture saved to: {path}");
            #endif
        }
        
        private Texture2D CreateCloudTexture()
        {
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            
            for (int x = 0; x < textureSize; x++)
            {
                for (int y = 0; y < textureSize; y++)
                {
                    // Calculate distance from center for circular falloff
                    float centerX = (x - textureSize * 0.5f) / (textureSize * 0.5f);
                    float centerY = (y - textureSize * 0.5f) / (textureSize * 0.5f);
                    float distanceFromCenter = Mathf.Sqrt(centerX * centerX + centerY * centerY);
                    
                    // Apply falloff curve
                    float falloff = falloffCurve.Evaluate(Mathf.Clamp01(distanceFromCenter));
                    
                    // Generate noise
                    float noise = Mathf.PerlinNoise(x * noiseScale / textureSize, y * noiseScale / textureSize);
                    
                    // Combine noise with falloff
                    float alpha = noise * falloff;
                    
                    // Create cloud-like color (white with varying alpha)
                    Color pixelColor = new Color(1f, 1f, 1f, alpha);
                    texture.SetPixel(x, y, pixelColor);
                }
            }
            
            texture.Apply();
            return texture;
        }
    }
} 