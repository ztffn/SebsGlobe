using UnityEngine;
using System.Collections.Generic;

namespace SebsGlobe.Clouds
{
    public class CloudSystem : MonoBehaviour
    {
        [Header("Spawn Properties")]
        [SerializeField, Tooltip("Total number of cloud particles in the system. Higher numbers create fuller clouds but impact performance.")]
        private int particleCount = 10000;
        [SerializeField, Tooltip("How many random points to test for each particle. Higher values create more defined cloud shapes but increase spawn time.")]
        private int spawnIterations = 32;
        [SerializeField, Tooltip("Number of noise layers used to create cloud shapes. More layers add detail but increase computation.")]
        private int noiseLayerCount = 4;
        [SerializeField, Range(1f, 4f), Tooltip("How quickly noise detail increases with each layer. Higher values create more detailed, varied clouds.")]
        private float lacunarity = 2f;
        [SerializeField, Range(0f, 1f), Tooltip("How much each noise layer contributes to the final shape. Lower values create smoother, more uniform clouds.")]
        private float persistence = 0.5f;
        [SerializeField, Tooltip("Overall scale of the noise pattern. Higher values create more, smaller cloud clusters.")]
        private float noiseScale = 2f;
        [SerializeField, Tooltip("Minimum size of individual cloud particles.")]
        private float sizeMin = 5f;
        [SerializeField, Tooltip("Maximum size of individual cloud particles.")]
        private float sizeMax = 20f;
        [SerializeField, Tooltip("Base radius of the cloud layer around the planet.")]
        private float cloudHeight = 400f;
        [SerializeField, Tooltip("How much particles can deviate from the base cloud height. Creates depth in the cloud layer.")]
        private float heightVariance = 150f;
        [SerializeField, Range(0.5f, 1f), Tooltip("Inner boundary of the cloud layer as a percentage of cloud height. 0.8 means clouds start at 80% of the base height.")]
        private float minHeightPercent = 0.8f;
        [SerializeField, Range(.9f, 1.5f), Tooltip("Outer boundary of the cloud layer as a percentage of cloud height. 1.2 means clouds extend to 120% of the base height.")]
        private float maxHeightPercent = 1.2f;

        [Header("Update Properties")]
        [SerializeField, Tooltip("Maximum distance from player at which clouds are rendered. Lower values improve performance.")]
        private float renderThresholdDst = 1000f;
        [SerializeField, Tooltip("How close the player needs to get to interact with clouds. Smaller values require flying closer to affect clouds.")]
        private float collisionRadius = 5f;
        [SerializeField, Tooltip("How much clouds pulse in size. Higher values create more dynamic movement.")]
        private float scaleAmplitude = 0.2f;
        [SerializeField, Tooltip("How quickly displaced cloud particles shrink and disappear after being hit.")]
        private float shrinkSpeed = 1f;

        [Header("References")]
        [SerializeField, Tooltip("The compute shader that handles cloud particle simulation. Should be set to CloudCompute.compute.")]
        private ComputeShader cloudComputeShader;
        [SerializeField, Tooltip("Material used to render the cloud particles. Should use the CloudParticle shader.")]
        private Material cloudMaterial;
        [SerializeField, Tooltip("Reference to the player/camera transform. Used for cloud interaction and culling.")]
        private Transform player;

        // Compute shader kernel IDs
        private int spawnKernel;
        private int updateKernel;

        // Buffers
        private ComputeBuffer particleBuffer;
        private ComputeBuffer renderBuffer;
        private ComputeBuffer argsBuffer;

        // Particle data structure
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Pack = 4)]
        private struct CloudParticle
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public Vector3 position;    // 16 bytes (12 + 4 padding)
            [System.Runtime.InteropServices.FieldOffset(16)]
            public float size;          // 4 bytes
            [System.Runtime.InteropServices.FieldOffset(20)]
            public float baseSize;      // 4 bytes
            [System.Runtime.InteropServices.FieldOffset(24)]
            public float scaleSpeed;    // 4 bytes
            [System.Runtime.InteropServices.FieldOffset(28)]
            public float timeOffset;    // 4 bytes
            [System.Runtime.InteropServices.FieldOffset(32)]
            public Vector3 velocity;    // 16 bytes (12 + 4 padding)
            [System.Runtime.InteropServices.FieldOffset(48)]
            public int inCloud;         // 4 bytes
            
            public static int GetStride()
            {
                return 52; // Total size with padding
            }
        }

        // For Graphics.DrawMeshInstancedIndirect
        private Mesh particleMesh;
        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        private Vector3 lastPlayerPos;
        private Vector3 playerVelocity;

        void Start()
        {
            if (player == null)
            {
                Debug.LogError("Player reference is not set in CloudSystem!");
                return;
            }
            InitializeSystem();
            SpawnCloudParticles();
        }

        void InitializeSystem()
        {
            // Find compute shader kernels
            spawnKernel = cloudComputeShader.FindKernel("SpawnCloudParticles");
            updateKernel = cloudComputeShader.FindKernel("UpdateClouds");

            // Create particle buffer with preserve contents flag
            if (particleBuffer != null) particleBuffer.Release();
            particleBuffer = new ComputeBuffer(particleCount, CloudParticle.GetStride(), ComputeBufferType.Default);
            
            // Create render buffer (append/consume buffer)
            if (renderBuffer != null) renderBuffer.Release();
            renderBuffer = new ComputeBuffer(particleCount, sizeof(float) * 4, ComputeBufferType.Append);
            
            // Create args buffer for indirect rendering
            if (argsBuffer != null) argsBuffer.Release();
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

            // Create a simple quad mesh for particle rendering
            CreateParticleMesh();

            // Set up material properties
            if (cloudMaterial != null)
            {
                cloudMaterial.SetBuffer("_ParticleBuffer", renderBuffer);
            }

            if (player != null)
            {
                lastPlayerPos = player.position;
            }

            // Initial spawn
            SpawnCloudParticles();
        }

        void OnValidate()
        {
            if (Application.isPlaying && particleBuffer != null)
            {
                SpawnCloudParticles();
            }
        }

        void CreateParticleMesh()
        {
            particleMesh = new Mesh();
            
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(0.5f, -0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0),
                new Vector3(0.5f, 0.5f, 0)
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };

            int[] triangles = new int[] { 0, 2, 1, 2, 3, 1 };

            particleMesh.vertices = vertices;
            particleMesh.uv = uvs;
            particleMesh.triangles = triangles;
            particleMesh.RecalculateNormals();

            args[0] = (uint)particleMesh.GetIndexCount(0);
            args[1] = 0; // Will be set by compute shader
            args[2] = (uint)particleMesh.GetIndexStart(0);
            args[3] = (uint)particleMesh.GetBaseVertex(0);
        }

        void Update()
        {
            if (cloudMaterial != null && renderBuffer != null)
            {
                cloudMaterial.SetBuffer("_ParticleBuffer", renderBuffer);
            }
            UpdateClouds();
            RenderClouds();
        }

        void UpdateClouds()
        {
            if (cloudComputeShader == null || particleBuffer == null || player == null) return;

            // Calculate player velocity
            playerVelocity = (player.position - lastPlayerPos) / Time.deltaTime;
            lastPlayerPos = player.position;

            // Clear render buffer
            renderBuffer.SetCounterValue(0);

            // Set update parameters
            cloudComputeShader.SetBuffer(updateKernel, "Particles", particleBuffer);
            cloudComputeShader.SetBuffer(updateKernel, "RenderBuffer", renderBuffer);
            cloudComputeShader.SetInt("particleCount", particleCount);
            cloudComputeShader.SetVector("playerPos", player.position);
            cloudComputeShader.SetVector("playerDir", playerVelocity.normalized);
            cloudComputeShader.SetFloat("playerSpeed", playerVelocity.magnitude);
            cloudComputeShader.SetFloat("renderThresholdDst", renderThresholdDst);
            cloudComputeShader.SetFloat("collisionRadius", collisionRadius);
            cloudComputeShader.SetFloat("scaleAmplitude", scaleAmplitude);
            cloudComputeShader.SetFloat("shrinkSpeed", shrinkSpeed);
            cloudComputeShader.SetFloat("time", Time.time);
            cloudComputeShader.SetFloat("deltaTime", Time.deltaTime);

            // Dispatch update kernel
            int threadGroups = Mathf.CeilToInt(particleCount / 64f);
            cloudComputeShader.Dispatch(updateKernel, threadGroups, 1, 1);

            // Get the number of particles to render
            ComputeBuffer.CopyCount(renderBuffer, argsBuffer, sizeof(uint));

            // Update args buffer for indirect rendering
            args[1] = 0; // Reset instance count
            argsBuffer.SetData(args);
            ComputeBuffer.CopyCount(renderBuffer, argsBuffer, sizeof(uint));
        }

        void RenderClouds()
        {
            if (cloudMaterial != null && particleMesh != null)
            {
                Graphics.DrawMeshInstancedIndirect(particleMesh, 0, cloudMaterial, 
                    new Bounds(Vector3.zero, Vector3.one * 1000f), argsBuffer);
            }
        }

        void OnDestroy()
        {
            if (particleBuffer != null) particleBuffer.Release();
            if (renderBuffer != null) renderBuffer.Release();
            if (argsBuffer != null) argsBuffer.Release();
        }

        void OnDrawGizmos()
        {
            if (player != null)
            {
                // Draw render threshold sphere
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(player.position, renderThresholdDst);
                
                // Draw collision radius sphere
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(player.position, collisionRadius);

                // Only try to draw particle gizmos if we have a valid buffer and are in play mode
                if (Application.isPlaying && particleBuffer != null)
                {
                    // Draw cross on each particle
                    Gizmos.color = Color.green;
                    CloudParticle[] particles = new CloudParticle[10];
                    particleBuffer.GetData(particles);
                    for (int i = 0; i < 10; i++)
                    {
                        Vector3 pos = particles[i].position;
                        float size = particles[i].size;
                        Gizmos.DrawWireSphere(pos, size * 0.5f);
                    }
                }
            }
        }

        [ContextMenu("Spawn Cloud Particles")]
        public void SpawnCloudParticles()
        {
            if (cloudComputeShader == null || particleBuffer == null) return;

            Debug.Log("Starting cloud respawn...");

            // Clear existing particles
            if (renderBuffer != null)
            {
                renderBuffer.SetCounterValue(0);
            }

            // Recreate particle buffer to ensure clean state
            if (particleBuffer != null) particleBuffer.Release();
            particleBuffer = new ComputeBuffer(particleCount, CloudParticle.GetStride(), ComputeBufferType.Default);

            // Set spawn parameters
            cloudComputeShader.SetBuffer(spawnKernel, "Particles", particleBuffer);
            cloudComputeShader.SetInt("particleCount", particleCount);
            cloudComputeShader.SetInt("spawnIterations", spawnIterations);
            cloudComputeShader.SetInt("noiseLayerCount", noiseLayerCount);
            cloudComputeShader.SetFloat("lacunarity", lacunarity);
            cloudComputeShader.SetFloat("persistence", persistence);
            cloudComputeShader.SetFloat("noiseScale", noiseScale);
            cloudComputeShader.SetFloat("sizeMin", sizeMin);
            cloudComputeShader.SetFloat("sizeMax", sizeMax);
            cloudComputeShader.SetFloat("cloudHeight", cloudHeight);
            cloudComputeShader.SetFloat("heightVariance", heightVariance);
            cloudComputeShader.SetFloat("minHeightPercent", minHeightPercent);
            cloudComputeShader.SetFloat("maxHeightPercent", maxHeightPercent);

            // Dispatch spawn kernel
            int threadGroups = Mathf.CeilToInt(particleCount / 64f);
            cloudComputeShader.Dispatch(spawnKernel, threadGroups, 1, 1);

            // Update material with new buffer
            if (cloudMaterial != null)
            {
                cloudMaterial.SetBuffer("_ParticleBuffer", renderBuffer);
            }

            Debug.Log("Cloud respawn completed.");

            // Debug output
            CloudParticle[] debugParticles = new CloudParticle[10];
            particleBuffer.GetData(debugParticles, 0, 0, 10);
            for (int i = 0; i < 10; i++)
            {
                Debug.Log($"Particle {i} spawned at height: {debugParticles[i].position.magnitude}");
            }
        }
    }
}