using UnityEngine;

namespace Anysound.Shared.Generators.Whoosh
{
    public class AnysoundWhooshDSP : MonoBehaviour
    {
        public static AudioClip CreateMorphedAudioClip(AnysoundWhooshObject settings, float duration, float movement, float size, float fluctuation)
        {
            int sampleRate = 44100; // Standard sample rate
            int channels = 1; // Mono audio
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);

            // Create the AudioClip
            AudioClip clip = AudioClip.Create("MorphedWhoosh", sampleCount, channels, sampleRate, false);

            // Generate white noise
            float[] samples = new float[sampleCount];
            float[] noiseSamples1 = new float[sampleCount];
            float[] noiseSamples2 = new float[sampleCount];
            float[] toneSamples = new float[sampleCount];
            System.Random random = new System.Random();

            var currentSettings = settings.GetWhooshMovementSettings(movement);


            // Generate noise samples for NoiseGenerator1 based on its settings
            GenerateNoiseSamples(noiseSamples1, currentSettings.NoiseGenerator1, random);

            // Generate noise samples for NoiseGenerator2 based on its settings
            GenerateNoiseSamples(noiseSamples2, currentSettings.NoiseGenerator2, random);

            // Process samples with filter sweep that matches the exact duration
            CreateFilterSweep(noiseSamples1, currentSettings.BandPassFilterSettings1, size, currentSettings.FluctuationLFOGenerator, fluctuation,
                currentSettings.FilterEnvelope1, sampleRate,
                duration);
            CreateFilterSweep(noiseSamples2, currentSettings.BandPassFilterSettings2, size, currentSettings.FluctuationLFOGenerator, fluctuation,
                currentSettings.FilterEnvelope2, sampleRate,
                duration);

            CreateToneSweep(toneSamples, currentSettings.ToneGenerator, currentSettings.SineFrequencyEnvelope, duration);

            // Mix the two channels
            for (int i = 0; i < sampleCount; i++)
            {
                noiseSamples1[i] = (noiseSamples1[i] + noiseSamples2[i]) * 0.5f;
            }

            // mix in the tone signal
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = (noiseSamples1[i] + (toneSamples[i] * 0.1f));
            }

            // add amplitude envelope
            ApplyAmplitudeEnvelope(samples, currentSettings.AmplitudeEnvelope, duration);

            // Set the processed data to the AudioClip
            clip.SetData(samples, 0);

            return clip;
        }

        private static void CreateToneSweep(float[] samples, AnysoundWhooshObject.ToneGeneratorSettings toneGeneratorSettings,
            AnysoundAudioDSP.ADSREnvelopeSettings pitchEnvelope, float duration)
        {
            int sampleRate = 44100; // Standard sample rate
            float currentPhase = 0f;
            float minFrequency = 50f; // Starting frequency for the sweep (Hz)
            float maxFrequency = 5000f; // Ending frequency for the sweep (Hz)

            for (int i = 0; i < samples.Length; i++)
            {
                float normalizedTime = (float)i / samples.Length;

                // Evaluate the pitch envelope to get a normalized frequency value (0.0 to 1.0)
                float pitchEnvelopeValue = pitchEnvelope.Evaluate(normalizedTime, duration);

                // Map the normalized pitch envelope value to an actual frequency range
                // Ensure frequency is always positive
                float currentFrequency = Mathf.Lerp(minFrequency, maxFrequency, pitchEnvelopeValue);
                currentFrequency = Mathf.Max(0f, currentFrequency); // Frequency cannot be negative

                float sampleValue = 0f;
                switch (toneGeneratorSettings.toneType)
                {
                    case AnysoundWhooshObject.ToneGeneratorSettings.ToneTypes.Sine:
                        sampleValue = Mathf.Sin(currentPhase);
                        break;
                    case AnysoundWhooshObject.ToneGeneratorSettings.ToneTypes.Square:
                        // Square wave: 1 for first half of cycle, -1 for second half
                        sampleValue = currentPhase < Mathf.PI ? 1f : -1f;
                        break;
                    case AnysoundWhooshObject.ToneGeneratorSettings.ToneTypes.Saw:
                        // Sawtooth wave: ramps linearly from -1 to 1 over one cycle
                        sampleValue = 2f * (currentPhase / (2f * Mathf.PI)) - 1f;
                        break;
                    case AnysoundWhooshObject.ToneGeneratorSettings.ToneTypes.Triangle:
                        // Triangle wave: ramps from -1 to 1, then from 1 to -1 over one cycle
                        if (currentPhase < Mathf.PI)
                        {
                            sampleValue = currentPhase / Mathf.PI * 2f - 1f; // Ramps from -1 to 1 in the first half-cycle
                        }
                        else
                        {
                            sampleValue = 1f - (currentPhase - Mathf.PI) / Mathf.PI * 2f; // Ramps from 1 to -1 in the second half-cycle
                        }

                        break;
                }

                // Apply the amplitude from the tone generator settings to the sample
                samples[i] = sampleValue * toneGeneratorSettings.amplitude;

                // Update phase for the next sample
                // Add the angular frequency (2 * PI * frequency) scaled by the inverse of sample rate (time per sample)
                currentPhase += 2f * Mathf.PI * currentFrequency / sampleRate;

                // Keep phase within the range [0, 2*PI) to prevent floating point issues over long durations
                while (currentPhase >= 2f * Mathf.PI)
                {
                    currentPhase -= 2f * Mathf.PI;
                }
            }
        }

        private static void ApplyAmplitudeEnvelope(float[] samples, AnysoundAudioDSP.ADSREnvelopeSettings envelopeSettings,
            float duration)
        {
            int sampleCount = samples.Length;

            for (int i = 0; i < sampleCount; i++)
            {
                // Calculate exact normalized time for this sample (0.0 to 1.0 over the full duration)
                float normalizedTime = (float)i / sampleCount;

                // Evaluate the envelope at the current normalized time
                // ASSUMPTION: AnysoundAudioDSP.ADSREnvelopeSettings has an Evaluate method that takes normalizedTime and totalDuration.
                float envelopeValue = envelopeSettings.Evaluate(normalizedTime, duration);

                // Apply the envelope to the sample
                samples[i] *= envelopeValue;
            }
        }

        private static void CreateFilterSweep(float[] samples, AnysoundAudioDSP.BandPassFilterSettings filterSettings, float size,
            AnysoundWhooshObject.LFOGeneratorSettings fluctuation, float fluctuationAmount,
            AnysoundAudioDSP.ADSREnvelopeSettings envelopeSettings, int sampleRate, float duration)
        {
            int sampleCount = samples.Length;

            // Filter state variables
            float x1 = 0f, x2 = 0f; // Input history
            float y1 = 0f, y2 = 0f; // Output history

            // LFO state variables
            float lfoPhase = 0f;
            float lfoFrequency = fluctuation.speed;

            // Calculate optimal block size based on duration to ensure smooth transitions
            // For very short durations, we need smaller blocks to capture curve detail
            int blockSize = Mathf.Max(16, Mathf.Min(1024, Mathf.CeilToInt(sampleRate * 0.005f)));
            // Process the audio in blocks
            for (int blockStart = 0; blockStart < sampleCount; blockStart += blockSize)
            {
                int blockEnd = Mathf.Min(blockStart + blockSize, sampleCount);

                // Process each sample in the block
                for (int i = blockStart; i < blockEnd; i++)
                {
                    // Calculate exact normalized time for this sample (0.0 to 1.0 over the full duration)
                    float normalizedTime = (float)i / sampleCount;

                    // Evaluate the envelope at the current normalized time
                    float envelopeValue = envelopeSettings.Evaluate(normalizedTime, duration);

                    // Calculate center frequency based on filter settings and modulated by the envelope
                    float baseCenterFreqNormalized = filterSettings.centerFrequency;
                    // Modulate the base normalized frequency by the envelope value (e.g., sweep from 0 to the target)
                    float modulatedCenterFreqNormalized = baseCenterFreqNormalized * envelopeValue;

                    // Apply LFO modulation to the center frequency
                    float lfoValue = 0f;
                    if (fluctuationAmount > 0f && fluctuation.amplitude > 0f)
                    {
                        switch (fluctuation.lfoType)
                        {
                            case AnysoundWhooshObject.LFOGeneratorSettings.LFOTypes.Sine:
                                lfoValue = Mathf.Sin(lfoPhase);
                                break;
                            case AnysoundWhooshObject.LFOGeneratorSettings.LFOTypes.Square:
                                lfoValue = lfoPhase < Mathf.PI ? 1f : -1f;
                                break;
                            case AnysoundWhooshObject.LFOGeneratorSettings.LFOTypes.Triangle:
                                if (lfoPhase < Mathf.PI)
                                    lfoValue = lfoPhase / Mathf.PI * 2f - 1f;
                                else
                                    lfoValue = 1f - (lfoPhase - Mathf.PI) / Mathf.PI * 2f;
                                break;
                        }

                        // Update LFO phase for next sample
                        lfoPhase += 2f * Mathf.PI * lfoFrequency / sampleRate;
                        while (lfoPhase >= 2f * Mathf.PI)
                        {
                            lfoPhase -= 2f * Mathf.PI;
                        }

                        // Scale the LFO by the fluctuation amplitude and amount
                        lfoValue *= fluctuation.amplitude * fluctuationAmount;
                    }

                    // Apply LFO to the center frequency
                    float currentCenterFrequency =
                        modulatedCenterFreqNormalized * (1f + lfoValue) * Mathf.Lerp(2, 0.5f, Mathf.InverseLerp(0.5f, 2, size));
                    currentCenterFrequency = Mathf.Clamp(currentCenterFrequency, 20f, 20000f);

                    // Convert bandwidthOctaves to Q factor (narrower bandwidth = higher Q)
                    // Low values of bandwidthOctaves now mean narrow filter (high Q)
                    // High values of bandwidthOctaves mean wide filter (low Q)
                    float qFromSettings = 5f / (filterSettings.bandwidth * size); // Inverse relationship: lower bandwidth = higher Q

                    // Shorter durations might benefit from a more resonant (higher Q) filter sweep
                    float durationEffectOnQ = Mathf.Lerp(1.0f, 2.0f, Mathf.Clamp01(duration)); // Multiplier 1.0 to 2.0 for shorter durations

                    float q = qFromSettings * durationEffectOnQ;
                    q = Mathf.Max(0.5f, q); // Ensure Q does not go below a reasonable minimum

                    // Calculate filter coefficients for this sample
                    float omega = 2f * Mathf.PI * currentCenterFrequency / sampleRate;

                    // Adjust bandwidth based on Q
                    float alpha = Mathf.Sin(omega) / (2f * q);

                    // Compute bandpass filter coefficients
                    float a0 = 1f + alpha;
                    float a1 = -2f * Mathf.Cos(omega);
                    float a2 = 1f - alpha;
                    float b0 = alpha;
                    float b1 = 0f;
                    float b2 = -alpha;

                    b2 = -alpha; // b2 was mistakenly overwriting the parameter name. Changed to b2.

                    // Normalize coefficients
                    b0 /= a0;
                    b1 /= a0;
                    b2 /= a0;
                    a1 /= a0;
                    a2 /= a0;

                    // Get current sample
                    float x0 = samples[i];

                    // Apply filter
                    float y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;

                    // Update sample with filtered value
                    samples[i] = y0 * filterSettings.gain;

                    // Update filter state
                    x2 = x1;
                    x1 = x0;
                    y2 = y1;
                    y1 = y0;
                }
            }
        }

        /// <summary>
        /// Generates an array of noise samples based on the specified noise type and amplitude.
        /// </summary>
        /// <param name="samples">The float array to fill with generated noise samples.</param>
        /// <param name="settings">The NoiseGeneratorSettings containing noise type and amplitude.</param>
        /// <param name="random">The System.Random instance to use for noise generation.</param>
        private static void GenerateNoiseSamples(float[] samples, AnysoundWhooshObject.NoiseGeneratorSettings settings, System.Random random)
        {
            int sampleCount = samples.Length;
            float amplitude = settings.amplitude;

            switch (settings.noiseType)
            {
                case AnysoundWhooshObject.NoiseGeneratorSettings.NoiseTypes.WhiteNoise:
                    for (int i = 0; i < sampleCount; i++)
                    {
                        // White noise: uniform distribution between -1 and 1
                        samples[i] = ((float)random.NextDouble() * 2f - 1f) * amplitude;
                    }

                    break;

                case AnysoundWhooshObject.NoiseGeneratorSettings.NoiseTypes.PinkNoise:
                    // Pink Noise (1/f noise) approximation using a 3-stage filter (often used in game audio).
                    // These coefficients provide a general approximation of pink noise's spectral characteristics.
                    float b0 = 0f, b1 = 0f, b2 = 0f; // State variables for the filter
                    for (int i = 0; i < sampleCount; i++)
                    {
                        float white = ((float)random.NextDouble() * 2f - 1f); // Generate a white noise sample

                        // Apply filter stages
                        b0 = 0.99886f * b0 + white * 0.0555179f;
                        b1 = 0.99332f * b1 + white * 0.1408169f;
                        b2 = 0.96900f * b2 + white * 0.0579933f;

                        // Sum filtered components and a direct white noise component
                        float pink = b0 + b1 + b2 + white * 0.185f;

                        // Scale and clamp the output to ensure it stays within a reasonable range
                        samples[i] = Mathf.Clamp(pink, -1f, 1f) * amplitude;
                    }

                    break;

                case AnysoundWhooshObject.NoiseGeneratorSettings.NoiseTypes.BrownNoise:
                    // Brown Noise (1/f^2 noise), generated by integrating white noise.
                    // The integration factor controls the "smoothness" (browness), and brownMaxAccumulation
                    // prevents the value from drifting indefinitely and is used for normalization.
                    float lastBrownValue = 0f;
                    float integrationFactor = 0.05f; // Smaller values result in "browner" (smoother) noise
                    float brownMaxAccumulation = 0.5f; // Maximum absolute value before normalization

                    for (int i = 0; i < sampleCount; i++)
                    {
                        float white = ((float)random.NextDouble() * 2f - 1f); // Generate a white noise sample

                        // Integrate the white noise
                        lastBrownValue += white * integrationFactor;

                        // Clamp the accumulated value to prevent excessive drift
                        lastBrownValue = Mathf.Clamp(lastBrownValue, -brownMaxAccumulation, brownMaxAccumulation);

                        // Normalize the value and apply the desired amplitude
                        samples[i] = (lastBrownValue / brownMaxAccumulation) * amplitude;
                    }

                    break;
            }
        }
    }
}