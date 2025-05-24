
 Sebastian forgot to include a shader / system that renders clouds that we can fly through. I've pieced together some of his transcripts from the video along snippets of code. Analyze this and rebuild the shader / system in a new folder "clouds" 


 
 I've come up with this little compute shader, which runs for every cloud particle we want to spawn. All it does is pick a bunch of random candidate points on the surface of a sphere, and evaluate this fractal noise function at each one of them. The point that has the highest noise value becomes the spawn point for that particle.And this should result in the particles spawning in distinct clumps, hopefully at least vaguely resembling clouds.


[numthreads (64,1,1)]
void SpawnCloudParticles (uint id : SV_DispatchThreadID) E
uint prngState = id.x;
float maxNoiseVal = -1. #INF;
float3 spawnPoint = float3(0, 0, 0);
// Generate n random points around sphere, and pick the one with the greatest noise value
for (int i = 0; i < spawnIterations; i ++) {
float3 candidatePoint = randomPointOnSphere(prngState);
float noise = fractalNoise noiseLayerCount, lacunarity, persistence, noiseScale, candidatePoint);
if (noise › maxNoiseVal) {
maxNoiseVal = noise;
spawnPoint = candidatePoint;
｝
// Calculate size of particle based on noise value at spawn pos
float t = smoothstep(-0.5, 0.5, maxNoiseVal);
float size = lerp(sizeMin, sizeMax, t);
// Assign particle properties
float height = cloudHeight + heightVariance * randomGauss(prngState);
Particles[id.x].position = spawnPoint * height;
Particles[id.xl.size = size;
Particles[id.x].baseSize = size;
Particles[id.x].scaleSpeed = lerp(0.15, 0.75, randomValue(prngState));
Particles[id.x].timeOffset = randomValue(prngState) * 2 * PI;
Particles[id.x].inCloud = 1;
}

Here is the supporting code for that, stuff like the fractal noise function, generating the random points, and so on.


float fractalNoise(int numLayers, float lacunarity, float persistence, float scale, floats pos) E
float noise = 0;
float frequency = scale;
float amplitude = 1;
// Simplex noise function from github.com/keijiro/NoiseShader
for (int i = 0; i ‹ numLayers; i ++) { noise += (snoise(pos * frequency) * 2 - 1) * amplitude; amplitude *= persistence; frequency *= lacunarity;
return noise;
｝
void updatePrngState inout uint state) {
// Hash function from www.cs.ubc.ca/~rbridson/docs/schechter-sca08-turbulence.pdf
state ^= 2747636419u; state *= 2654435769u; state ^= state >> 16; state *= 2654435769u; state ^= state >> 16; state *= 2654435769u;
}
float randomValue(inout uint prngState) {
updatePrngState(prngState);
return prngstate / 4294967295.0; // Scale random number to range [0, 1]
｝
// Random number (gaussian distribution with mean=0; sd=1) Thanks to stackoverflow.com/a/6178290
float randomGauss(inout uint prngState) {
return sqrt(-2 * log(randomValue(prngState))) * cos(2 * PI * randomValue(prngState));
}
// Thanks to math.stackexchange.com/a/1585996
float3 randomPointOnSphere(inout uint prngState) {
return normalize(float3(randomGauss(prngState), randomGauss(prngState), randomGauss(prngState)));
}


Then there's also this part of the compute shader, for updating the clouds. What's going on in here is, first of all, if the particle is within some threshold distance from the player, it gets appended to this render buffer, which is a new trick I learned so that we don't have to submit all the particles we have for rendering.I thought it would be fun if it actually reacts to the collision and gets knocked out of the cloud.


[numthreads (64,1,1)]
void UpdateClouds (uint3 id : SV_DispatchThreadID) E
Particle particle = Particles[id.x];
float3 offsetToPlayer = playerPos - particle.position;
float dstFromPlayer = length(offsetToPlayer);
// Particle inside (potential) render radius, so update it!
if (dstFromPlayer < renderThresholdDst) {
// Add to buffer for rendering
RenderBuffer Append(float4(particle.position, particle.size));
if (particle.incloud) {
/I If the player comes near to a particle, then knock the particle out of the cloud if (dstFromPlayer ‹ collisionRadius) {
float3 collisionDir = -offsetToPlayer / dstFromPlayer;
float3 particleVelocity = collisionDir * lerp(0.3, 1, dot(collisionDir, playerDir)) * playerSpeed;
Particles[id.x].velocity = particleVelocity;
Particles[id.x]. incloud = 0;
}
// Scale particle with sin wave to make clouds look a bit more alive
Particles[id.x].size = particle.baseSize + sin(time * particle.scaleSpeed + particle.timeOffset) * scaleAmplitude;
else {
// Particle has been knocked out of the cloud, so move it by its velocity and shrink it over time
Particles[id.x].size = max(0, particle.size - deltaTime * shrinkSpeed);
Particles[id.x]•position += Particles[id.x].velocity * deltaTime;
}
}

There should be an inspector where we can tweak these values: 
Spawn Properties
Spawn Iterations
Noise Layer Count
Lacunarity Persistence
Noise Scale
Size Min
Size Max
Cloud Height
Height Variance

And a button to respawn particles. 