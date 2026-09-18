using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Anysound.Shared
{
    public class AnysoundAudioDSP : MonoBehaviour
    {
        [Serializable]
        public struct BandPassFilterSettings : IEquatable<BandPassFilterSettings>
        {
            [SerializeField, Range(20f, 20000f)] public float centerFrequency;
            [FormerlySerializedAs("bandwidthOctaves")] [SerializeField, Range(0.1f, 5f)] public float bandwidth;
            [SerializeField, Range(0.1f, 1f)] public float gain;

            public BandPassFilterSettings(float centerFrequency, float bandwidth, float gain)
            {
                this.centerFrequency = centerFrequency;
                this.bandwidth = bandwidth;
                this.gain = gain;
            }

            public static BandPassFilterSettings Lerp(BandPassFilterSettings a, BandPassFilterSettings b, float t)
            {
                t = Mathf.Clamp01(t);
                float centerFrequency = Mathf.Lerp(a.centerFrequency, b.centerFrequency, t);
                float bandwidthOctaves = Mathf.Lerp(a.bandwidth, b.bandwidth, t);
                float gain = Mathf.Lerp(a.gain, b.gain, t);
                return new BandPassFilterSettings(centerFrequency, bandwidthOctaves, gain);
            }

            public static BandPassFilterSettings WeightedMix(List<BandPassFilterSettings> settings,
                List<float> weights)
            {
                if (settings == null || weights == null || settings.Count != weights.Count || settings.Count == 0)
                    throw new ArgumentException("Settings and weights must be non-null and of the same length > 0");

                float totalWeight = 0f;
                float sumCenterFreq = 0f;
                float sumBandwidth = 0f;
                float sumGain = 0f;

                for (int i = 0; i < settings.Count; i++)
                {
                    float w = weights[i];
                    totalWeight += w;
                    sumCenterFreq += settings[i].centerFrequency * w;
                    sumBandwidth += settings[i].bandwidth * w;
                    sumGain += settings[i].gain * w;
                }

                if (totalWeight == 0f)
                    throw new InvalidOperationException("Total weight must be greater than zero.");

                return new BandPassFilterSettings(
                    sumCenterFreq / totalWeight,
                    sumBandwidth / totalWeight,
                    sumGain / totalWeight
                );
            }

            public bool Equals(BandPassFilterSettings other)
            {
                return centerFrequency.Equals(other.centerFrequency) && bandwidth.Equals(other.bandwidth) && gain.Equals(other.gain);
            }

            public override bool Equals(object obj)
            {
                return obj is BandPassFilterSettings other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(centerFrequency, bandwidth, gain);
            }
        }

        /// <summary>
        /// Applies a bandpass filter to an audio clip
        /// <param name="clip">The input audio clip</param>
        /// <param name="settings"></param>
        /// <returns>A new audio clip with the bandpass filter applied</returns>
        /// /// </summary>
        public static AudioClip ApplyBandpassFilter(AudioClip clip, BandPassFilterSettings settings)
        {
            if (!clip) return null;

            float centerFrequency = Mathf.Clamp(settings.centerFrequency, 20f, 20000f);
            float bandwidthOctaves = Mathf.Clamp(settings.bandwidth, 0.1f, 5f);
            float gain = Mathf.Clamp(settings.gain, 0.1f, 10f);

            int channels = clip.channels;
            int sampleCount = clip.samples;
            float sampleRate = clip.frequency;

            // Get the audio data
            float[] samples = new float[sampleCount * channels];
            clip.GetData(samples, 0);

            // Calculate bandwidth in Hz from octaves
            float bandwidthHz = centerFrequency * (Mathf.Pow(2f, bandwidthOctaves) - 1f);
            if (bandwidthHz <= 0f) bandwidthHz = 1f; // Prevent division by zero or instability

            // Calculate biquad bandpass filter coefficients
            float omega = 2f * Mathf.PI * centerFrequency / sampleRate;
            float Q = centerFrequency / bandwidthHz;
            float alpha = Mathf.Sin(omega) / (2f * Q);

            float cosOmega = Mathf.Cos(omega);

            float a0 = 1f + alpha;
            float a1 = -2f * cosOmega;
            float a2 = 1f - alpha;
            float b0 = alpha;
            float b1 = 0f;
            float b2 = -alpha;

            // Normalize filter coefficients
            b0 /= a0;
            b1 /= a0;
            b2 /= a0;
            a1 /= a0;
            a2 /= a0;

            float[] filteredSamples = new float[samples.Length];

            // Apply filter for each channel
            for (int channel = 0; channel < channels; channel++)
            {
                float x1 = 0f, x2 = 0f; // Input history
                float y1 = 0f, y2 = 0f; // Output history

                for (int i = 0; i < sampleCount; i++)
                {
                    int index = i * channels + channel;
                    float x0 = samples[index];

                    float y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                    y0 *= gain; // Optional gain after filtering

                    filteredSamples[index] = y0;

                    // Shift filter history
                    x2 = x1;
                    x1 = x0;
                    y2 = y1;
                    y1 = y0;
                }
            }

            // Create and return new filtered clip
            AudioClip filteredClip = AudioClip.Create(
                clip.name + "_Filtered",
                sampleCount,
                channels,
                clip.frequency,
                false
            );

            filteredClip.SetData(filteredSamples, 0);
            return filteredClip;
        }


        /// <summary>
        /// Mixes multiple audio clips together based on provided weights
        /// <param name="clips">List of audio clips to mix</param>
        /// <param name="weights">List of weights for each clip (must match the number of clips)</param>
        /// <param name="normalize"></param>
        /// <returns>A new audio clip that is a weighted mix of the input clips</returns>
        /// </summary>
        public static AudioClip Mix(List<AudioClip> clips, List<float> weights, bool normalize = true)
        {
            // Validate inputs
            if (clips == null || weights == null)
                throw new ArgumentNullException("Clips and weights cannot be null");

            if (clips.Count == 0)
                throw new ArgumentException("At least one clip is required for mixing");

            if (clips.Count != weights.Count)
                throw new ArgumentException("The number of clips must match the number of weights");

            // Check if any clips are null
            for (int i = 0; i < clips.Count; i++)
            {
                if (!clips[i])
                    throw new ArgumentNullException($"Clip at index {i} is null");
            }

            // Calculate the sum of weights
            float totalWeight = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                totalWeight += weights[i];
            }

            // If all weights are zero, return null
            if (totalWeight <= 0f)
                throw new ArgumentException("The sum of weights must be greater than zero");

            // Determine output parameters - use the highest value for each parameter
            int maxChannels = 0;
            int maxSamples = 0;
            int maxFrequency = 0;

            foreach (AudioClip clip in clips)
            {
                maxChannels = Mathf.Max(maxChannels, clip.channels);
                maxSamples = Mathf.Max(maxSamples, clip.samples);
                maxFrequency = Mathf.Max(maxFrequency, clip.frequency);
            }

            // Create output buffer
            float[] mixedData = new float[maxSamples * maxChannels];

            // Mix clips according to weights
            for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                AudioClip clip = clips[clipIndex];
                float weight = weights[clipIndex] / totalWeight; // Normalize weight

                // Skip clips with zero weight (optimization)
                if (weight <= 0.0001f) continue;

                // Get the clip's audio data
                float[] clipData = new float[clip.samples * clip.channels];
                clip.GetData(clipData, 0);

                // Mix this clip into the output buffer
                for (int i = 0; i < clip.samples; i++)
                {
                    // Skip samples that are beyond our clip's length
                    if (i >= maxSamples) break;

                    for (int channel = 0; channel < clip.channels; channel++)
                    {
                        // Skip channels that our clip doesn't have
                        if (channel >= maxChannels) continue;

                        int outputIndex = i * maxChannels + channel;
                        int inputIndex = i * clip.channels + channel;

                        // Add this clip's weighted sample to the mix
                        mixedData[outputIndex] += clipData[inputIndex] * weight;
                    }
                }
            }

            // Normalize the mixed data if requested to prevent clipping
            if (normalize)
            {
                // Find the maximum absolute value
                float maxAbs = 0f;
                for (int i = 0; i < mixedData.Length; i++)
                {
                    maxAbs = Mathf.Max(maxAbs, Mathf.Abs(mixedData[i]));
                }

                // Normalize only if the max value exceeds 1.0
                if (maxAbs > 1.0f)
                {
                    float normalizationFactor = 0.99f / maxAbs; // 0.99 to avoid potential clipping
                    for (int i = 0; i < mixedData.Length; i++)
                    {
                        mixedData[i] *= normalizationFactor;
                    }
                }
            }

            // Create and return the mixed audio clip
            AudioClip mixedClip = AudioClip.Create(
                "MixedClip",
                maxSamples,
                maxChannels,
                maxFrequency,
                false
            );

            mixedClip.SetData(mixedData, 0);
            return mixedClip;
        }


        [Serializable]
        public struct ADSREnvelopeSettings : IEquatable<ADSREnvelopeSettings>
        {
            [SerializeField, Range(0.001f, 2f)] public float attackTime;
            [SerializeField, Range(0.001f, 2f)] public float decayTime;
            [SerializeField, Range(0f, 1f)] public float sustainLevel;
            [SerializeField, Range(0.001f, 5f)] public float releaseTime;
            [SerializeField, Range(0.001f, 5f)] public float duration;

            /// <summary>
            /// Default ADSR envelope settings with typical values
            /// </summary>
            public static ADSREnvelopeSettings Default => new ADSREnvelopeSettings(
                attackTime: 0.01f, // 10ms attack
                decayTime: 0.1f, // 100ms decay
                sustainLevel: 0.7f, // 70% sustain level
                releaseTime: 0.3f, // 300ms release
                duration: 1.0f // 1 second total duration
            );

            public ADSREnvelopeSettings(float attackTime, float decayTime, float sustainLevel, float releaseTime, float duration)
            {
                this.attackTime = attackTime;
                this.decayTime = decayTime;
                this.sustainLevel = sustainLevel;
                this.releaseTime = releaseTime;
                this.duration = duration;
            }

            public float Evaluate(float normalizedTime, float totalDuration)
            {
                totalDuration = attackTime + decayTime + releaseTime;
                float time = normalizedTime * totalDuration;
                float envelopeValue = 0f;

                if (time < attackTime)
                {
                    // Attack phase
                    envelopeValue = Mathf.Lerp(0f, 1f, time / attackTime);
                }
                else if (time < attackTime + decayTime)
                {
                    // Decay phase
                    envelopeValue = Mathf.Lerp(1f, sustainLevel, (time - attackTime) / decayTime);
                }
                else if (time < totalDuration - releaseTime)
                {
                    // Sustain phase (until release starts)
                    envelopeValue = sustainLevel;
                }
                else // time >= totalDuration - releaseTime
                {
                    // Release phase
                    envelopeValue = Mathf.Lerp(sustainLevel, 0f, (time - (totalDuration - releaseTime)) / releaseTime);
                }

                return Mathf.Clamp01(envelopeValue);
            }

            public static ADSREnvelopeSettings Lerp(ADSREnvelopeSettings a, ADSREnvelopeSettings b, float t)
            {
                t = Mathf.Clamp01(t);
                float attackTime = Mathf.Lerp(a.attackTime, b.attackTime, t);
                float decayTime = Mathf.Lerp(a.decayTime, b.decayTime, t);
                float sustainLevel = Mathf.Lerp(a.sustainLevel, b.sustainLevel, t);
                float releaseTime = Mathf.Lerp(a.releaseTime, b.releaseTime, t);
                float duration = Mathf.Lerp(a.duration, b.duration, t);
                return new ADSREnvelopeSettings(attackTime, decayTime, sustainLevel, releaseTime, duration);
            }

            public static ADSREnvelopeSettings WeightedMix(IList<ADSREnvelopeSettings> settings, IList<float> weights)
            {
                if (settings == null || weights == null || settings.Count != weights.Count || settings.Count == 0)
                    throw new ArgumentException("Settings and weights must be non-null and of the same length > 0");

                float totalWeight = 0f;
                float sumAttack = 0f;
                float sumDecay = 0f;
                float sumSustain = 0f;
                float sumRelease = 0f;
                float sumDuration = 0f;

                for (int i = 0; i < settings.Count; i++)
                {
                    float w = weights[i];
                    totalWeight += w;

                    sumAttack += settings[i].attackTime * w;
                    sumDecay += settings[i].decayTime * w;
                    sumSustain += settings[i].sustainLevel * w;
                    sumRelease += settings[i].releaseTime * w;
                    sumDuration += settings[i].duration * w;
                }

                if (totalWeight == 0f)
                    throw new InvalidOperationException("Total weight must be greater than zero.");

                return new ADSREnvelopeSettings(
                    sumAttack / totalWeight,
                    sumDecay / totalWeight,
                    sumSustain / totalWeight,
                    sumRelease / totalWeight,
                    sumDuration / totalWeight
                );
            }

            public bool Equals(ADSREnvelopeSettings other)
            {
                return attackTime.Equals(other.attackTime) && decayTime.Equals(other.decayTime) && sustainLevel.Equals(other.sustainLevel) &&
                       releaseTime.Equals(other.releaseTime);
            }

            public override bool Equals(object obj)
            {
                return obj is ADSREnvelopeSettings other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(attackTime, decayTime, sustainLevel, releaseTime);
            }
        }

        /// <summary>
        /// Applies an ADSR (Attack, Decay, Sustain, Release) envelope to an audio clip
        /// <param name="clip">The input audio clip</param>
        /// <param name="settings"></param>
        /// <returns>A new audio clip with the ADSR envelope applied</returns>
        ///  </summary>
        public static AudioClip ApplyADSREnvelope(AudioClip clip, ADSREnvelopeSettings settings)
        {
            if (!clip) return null;
            float attackTime = settings.attackTime;
            float decayTime = settings.decayTime;
            float sustainLevel = settings.sustainLevel;
            float releaseTime = settings.releaseTime;
            float duration = settings.duration;

            // Get the clip data
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            // Calculate envelope parameters in samples
            int attackSamples = Mathf.CeilToInt(attackTime * clip.frequency);
            int decaySamples = Mathf.CeilToInt(decayTime * clip.frequency);
            int releaseSamples = Mathf.CeilToInt(releaseTime * clip.frequency);
            int durationSamples = Mathf.CeilToInt(duration * clip.frequency);

            // Ensure sustain level is in valid range
            sustainLevel = Mathf.Clamp01(sustainLevel);

            // Calculate the sample where release begins (total samples - release samples)
            //int releaseSampleStart = clip.samples - releaseSamples;

            // Ensure we have valid envelope segments (in case clip is very short)
            attackSamples = Mathf.Min(attackSamples, clip.samples / 4);
            decaySamples = Mathf.Min(decaySamples, clip.samples / 4);
            //releaseSamples = Mathf.Min(releaseSamples, clip.samples / 4);

            var releaseSampleStart = Mathf.Max(attackSamples + decaySamples, durationSamples);
            var releaseSampleEnd = releaseSampleStart + releaseSamples;

            // Apply the envelope to each sample
            for (int sampleIndex = 0; sampleIndex < clip.samples; sampleIndex++)
            {
                // Determine the envelope multiplier based on the position in the clip
                float envelopeMultiplier;

                if (sampleIndex < attackSamples)
                {
                    // Attack phase - linear ramp from 0 to 1
                    envelopeMultiplier = (float)sampleIndex / attackSamples;
                }
                else if (sampleIndex < attackSamples + decaySamples)
                {
                    // Decay phase - linear ramp from 1 to sustainLevel
                    float decayProgress = (float)(sampleIndex - attackSamples) / decaySamples;
                    envelopeMultiplier = Mathf.Lerp(1f, sustainLevel, decayProgress);
                }
                else if (sampleIndex < releaseSampleStart)
                {
                    // Sustain phase - constant at sustainLevel
                    envelopeMultiplier = sustainLevel;
                }
                else
                {
                    // Release phase - linear ramp from sustainLevel to 0
                    //float releaseProgress = (float)(sampleIndex) / (releaseSampleStart + releaseSamples);
                    float releaseProgress = Mathf.InverseLerp(releaseSampleStart, releaseSampleEnd, sampleIndex);
                    envelopeMultiplier = Mathf.Lerp(sustainLevel, 0f, releaseProgress);
                }

                // Apply the envelope to all channels for this sample
                for (int channel = 0; channel < clip.channels; channel++)
                {
                    int index = sampleIndex * clip.channels + channel;
                    if (index < samples.Length)
                    {
                        samples[index] *= envelopeMultiplier;
                    }
                }
            }

            // Create a new audio clip with the processed samples
            AudioClip envelopedClip = AudioClip.Create(
                clip.name + "_ADSR",
                clip.samples,
                clip.channels,
                clip.frequency,
                false
            );

            envelopedClip.SetData(samples, 0);
            return envelopedClip;
        }


        [Serializable]
        public struct CurveEnvelopeSettings
        {
            public AnimationCurve envelopeCurve;

            public CurveEnvelopeSettings(AnimationCurve envelopeCurve)
            {
                this.envelopeCurve = envelopeCurve;
            }

            public static CurveEnvelopeSettings Lerp(CurveEnvelopeSettings a, CurveEnvelopeSettings b, float t)
            {
                t = Mathf.Clamp01(t);

                // Create a new AnimationCurve
                AnimationCurve lerpedCurve = new AnimationCurve();

                // Gather all time points (keys) from both curves
                HashSet<float> timePoints = new HashSet<float>();

                // Add all keys from curve A
                if (a.envelopeCurve != null)
                {
                    foreach (Keyframe key in a.envelopeCurve.keys)
                    {
                        timePoints.Add(key.time);
                    }
                }

                // Add all keys from curve B
                if (b.envelopeCurve != null)
                {
                    foreach (Keyframe key in b.envelopeCurve.keys)
                    {
                        timePoints.Add(key.time);
                    }
                }

                // If either curve is null, return the non-null one or an empty curve
                if (a.envelopeCurve == null && b.envelopeCurve == null)
                    return new CurveEnvelopeSettings(new AnimationCurve());

                if (a.envelopeCurve == null)
                    return b;

                if (b.envelopeCurve == null)
                    return a;

                // Sort the time points
                List<float> sortedTimePoints = new List<float>(timePoints);
                sortedTimePoints.Sort();

                // Create lerped keyframes at each unique time point
                foreach (float time in sortedTimePoints)
                {
                    float valueA = a.envelopeCurve.Evaluate(time);
                    float valueB = b.envelopeCurve.Evaluate(time);
                    float lerpedValue = Mathf.Lerp(valueA, valueB, t);

                    // Get the tangent and mode information by finding the closest keys
                    Keyframe keyA = GetClosestKeyframe(a.envelopeCurve, time);
                    Keyframe keyB = GetClosestKeyframe(b.envelopeCurve, time);

                    // Lerp the tangents
                    float inTangent = Mathf.Lerp(keyA.inTangent, keyB.inTangent, t);
                    float outTangent = Mathf.Lerp(keyA.outTangent, keyB.outTangent, t);

                    // Create and add the lerped keyframe
                    Keyframe lerpedKey = new Keyframe(time, lerpedValue, inTangent, outTangent);
                    lerpedCurve.AddKey(lerpedKey);
                }

                return new CurveEnvelopeSettings(lerpedCurve);
            }

            private static Keyframe GetClosestKeyframe(AnimationCurve curve, float time)
            {
                // Default keyframe if we can't find a close one
                Keyframe closestKey = new Keyframe(time, 0, 0, 0);

                if (curve == null || curve.length == 0)
                    return closestKey;

                float minDistance = float.MaxValue;

                foreach (Keyframe key in curve.keys)
                {
                    float distance = Mathf.Abs(key.time - time);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestKey = key;
                    }
                }

                return closestKey;
            }

            public static CurveEnvelopeSettings WeightedMix(List<CurveEnvelopeSettings> settings, List<float> weights)
            {
                if (settings == null || weights == null || settings.Count != weights.Count || settings.Count == 0)
                    throw new ArgumentException("Settings and weights must be non-null and of the same length > 0");

                // If there's only one setting, return it regardless of weight
                if (settings.Count == 1)
                    return settings[0];

                // Get the total weight
                float totalWeight = 0f;
                for (int i = 0; i < weights.Count; i++)
                {
                    totalWeight += weights[i];
                }

                if (totalWeight <= 0f)
                    throw new InvalidOperationException("Total weight must be greater than zero.");

                // Normalize weights
                List<float> normalizedWeights = new List<float>(weights.Count);
                for (int i = 0; i < weights.Count; i++)
                {
                    normalizedWeights.Add(weights[i] / totalWeight);
                }

                // Gather all unique time points from all curves
                HashSet<float> timePoints = new HashSet<float>();
                foreach (var setting in settings)
                {
                    if (setting.envelopeCurve != null)
                    {
                        foreach (Keyframe key in setting.envelopeCurve.keys)
                        {
                            timePoints.Add(key.time);
                        }
                    }
                }

                // Sort the time points
                List<float> sortedTimePoints = new List<float>(timePoints);
                sortedTimePoints.Sort();

                // Create a new animation curve
                AnimationCurve mixedCurve = new AnimationCurve();

                // For each time point, calculate the weighted value
                foreach (float time in sortedTimePoints)
                {
                    float weightedValue = 0f;
                    float weightedInTangent = 0f;
                    float weightedOutTangent = 0f;

                    for (int i = 0; i < settings.Count; i++)
                    {
                        if (settings[i].envelopeCurve != null)
                        {
                            float value = settings[i].envelopeCurve.Evaluate(time);
                            weightedValue += value * normalizedWeights[i];

                            // Get tangent information
                            Keyframe closestKey = GetClosestKeyframe(settings[i].envelopeCurve, time);
                            weightedInTangent += closestKey.inTangent * normalizedWeights[i];
                            weightedOutTangent += closestKey.outTangent * normalizedWeights[i];
                        }
                    }

                    // Add the weighted keyframe to the mixed curve
                    Keyframe mixedKey = new Keyframe(time, weightedValue, weightedInTangent, weightedOutTangent);
                    mixedCurve.AddKey(mixedKey);
                }

                return new CurveEnvelopeSettings(mixedCurve);
            }
        }


        /// <summary>
        /// Applies an envelope filter to an audio clip based on an animation curve
        /// </summary>
        /// <param name="clip">The input audio clip</param>
        /// <param name="envelopeCurve">Animation curve that defines the amplitude envelope</param>
        /// <returns>A new audio clip with the envelope filter applied</returns>
        public static AudioClip ApplyCurveEnvelope(AudioClip clip, CurveEnvelopeSettings envelopeCurve)
        {
            var envelope = envelopeCurve.envelopeCurve;

            if (!clip || envelope == null) return null;

            // Get the clip data
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            float clipDuration = clip.length;

            // Apply the envelope to each sample
            for (int sampleIndex = 0; sampleIndex < clip.samples; sampleIndex++)
            {
                // Calculate the relative time position within the clip (0 to 1)
                float normalizedTime = (float)sampleIndex / clip.samples;

                // Convert to actual time in seconds for the animation curve
                float timeInSeconds = normalizedTime * clipDuration;

                // Evaluate the curve at this time point to get the amplitude multiplier
                float envelopeMultiplier = envelope.Evaluate(timeInSeconds);

                // Apply the envelope to all channels for this sample
                for (int channel = 0; channel < clip.channels; channel++)
                {
                    int index = sampleIndex * clip.channels + channel;
                    samples[index] *= envelopeMultiplier;
                }
            }

            // Create a new audio clip with the processed samples
            AudioClip filteredClip = AudioClip.Create(
                clip.name + "_EnvelopeFiltered",
                clip.samples,
                clip.channels,
                clip.frequency,
                false
            );

            filteredClip.SetData(samples, 0);
            return filteredClip;
        }


        [Serializable]
        public struct DistortionSettings : IEquatable<DistortionSettings>
        {
            [SerializeField, Range(0f, 1f)] public float mix; // Mix between dry and wet signal

            [SerializeField, Range(0f, 20)] public float amount; // Amount of distortion to apply
            [SerializeField, Range(0.1f, 5f)] public float gain; // Post-distortion gain

            public DistortionSettings(float amount, float mix, float gain)
            {
                this.amount = amount;
                this.mix = mix;
                this.gain = gain;
            }

            public static DistortionSettings Lerp(DistortionSettings a, DistortionSettings b, float t)
            {
                t = Mathf.Clamp01(t);
                float amount = Mathf.Lerp(a.amount, b.amount, t);
                float mix = Mathf.Lerp(a.mix, b.mix, t);
                float gain = Mathf.Lerp(a.gain, b.gain, t);
                return new DistortionSettings(amount, mix, gain);
            }

            public static DistortionSettings WeightedMix(List<DistortionSettings> settings,
                List<float> weights)
            {
                if (settings == null || weights == null || settings.Count != weights.Count || settings.Count == 0)
                    throw new ArgumentException("Settings and weights must be non-null and of the same length > 0");

                float totalWeight = 0f;
                float sumAmount = 0f;
                float sumMix = 0f;
                float sumGain = 0f;

                for (int i = 0; i < settings.Count; i++)
                {
                    float w = weights[i];
                    totalWeight += w;
                    sumAmount += settings[i].amount * w;
                    sumMix += settings[i].mix * w;
                    sumGain += settings[i].gain * w;
                }

                if (totalWeight == 0f)
                    throw new InvalidOperationException("Total weight must be greater than zero.");

                return new DistortionSettings(
                    sumAmount / totalWeight,
                    sumMix / totalWeight,
                    sumGain / totalWeight
                );
            }

            public bool Equals(DistortionSettings other)
            {
                return amount.Equals(other.amount) && mix.Equals(other.mix) && gain.Equals(other.gain);
            }
        }

        /// <summary>
        /// Applies a distortion effect to an audio clip
        /// </summary>
        /// <param name="clip">The input audio clip</param>
        /// <param name="settings">Distortion settings</param>
        /// <returns>A new audio clip with distortion applied</returns>
        public static AudioClip ApplyDistortion(AudioClip clip, DistortionSettings settings)
        {
            if (!clip) return null;

            float amount = Mathf.Clamp(settings.amount, 0f, 20f);
            float mix = Mathf.Clamp01(settings.mix);
            float gain = Mathf.Clamp(settings.gain, 0.1f, 10f);

            int channels = clip.channels;
            int sampleCount = clip.samples;

            // Get the audio data
            float[] samples = new float[sampleCount * channels];
            clip.GetData(samples, 0);

            // Create array for processed samples
            float[] processedSamples = new float[samples.Length];

            // Process each sample
            for (int i = 0; i < samples.Length; i++)
            {
                // Store the original (dry) signal
                float drySample = samples[i];

                // Apply distortion with a unified approach
                float wetSample;
                if (amount > 0)
                {
                    float input = samples[i];
                    float absInput = Mathf.Abs(input);

                    // Base distortion factor increases with amount
                    float distortionFactor = 1.0f + 5.0f * amount;

                    // Unified distortion function that gets more aggressive with higher amounts
                    // This combines soft clipping, hard clipping, and waveshaping in a continuous curve

                    // Soft clipping component (tanh-like)
                    float softClip = Mathf.Sign(input) * (1.0f - Mathf.Exp(-(absInput * distortionFactor)));

                    // Add more aggressive components as amount increases

                    // Cubic waveshaping (adds odd harmonics)
                    float cubic = input * input * input;

                    // Asymmetric distortion (adds even and odd harmonics)
                    float asymmetric = 0.5f * Mathf.Sign(input) * (input * input);

                    // Fold-back distortion for high amounts (creates more complex harmonics)
                    float foldback = 0;
                    float threshold = 0.6f;
                    if (absInput > threshold)
                    {
                        foldback = Mathf.Sign(input) * (threshold - (absInput - threshold) % (threshold * 2));
                    }
                    else
                    {
                        foldback = input;
                    }

                    // Blend between different distortion types based on amount
                    // This creates a smooth transition as amount increases
                    if (amount <= 5.0f)
                    {
                        // For lower amounts, primarily use soft clipping
                        float t = amount / 5.0f;
                        wetSample = Mathf.Lerp(input, softClip, t);
                    }
                    else if (amount <= 10.0f)
                    {
                        // For medium amounts, blend from soft clipping to cubic distortion
                        float t = (amount - 5.0f) / 5.0f;
                        wetSample = Mathf.Lerp(softClip, softClip * (1.0f - t) + (cubic * t), t);

                        // Add a touch of asymmetric distortion as we approach 10
                        wetSample += asymmetric * t * 0.3f;
                    }
                    else
                    {
                        // For high amounts, blend in more aggressive effects
                        float t = (amount - 10.0f) / 10.0f;

                        // Start with the medium distortion
                        wetSample = softClip * 0.3f + cubic * 0.7f + asymmetric * 0.3f;

                        // Blend in foldback distortion for very high amounts
                        wetSample = Mathf.Lerp(wetSample, foldback, t * 0.8f);

                        // Add harmonics enhancement for extreme settings
                        wetSample += 0.2f * t * input * input * Mathf.Sign(input);
                    }

                    // Scale by amount to make higher distortion values more impactful
                    float intensityScale = 1.0f + (amount * 0.1f);
                    wetSample *= intensityScale;

                    // Apply additional gain
                    wetSample *= gain;
                }
                else
                {
                    // No distortion
                    wetSample = drySample * gain;
                }

                // Mix dry and wet signals
                processedSamples[i] = (drySample * (1.0f - mix)) + (wetSample * mix);

                // Soft limiting to preserve some of the distortion character while preventing digital clipping
                if (processedSamples[i] > 0.95f)
                    processedSamples[i] = 0.95f + (processedSamples[i] - 0.95f) * 0.05f;
                else if (processedSamples[i] < -0.95f)
                    processedSamples[i] = -0.95f + (processedSamples[i] + 0.95f) * 0.05f;
            }

            // Create and return new distorted clip
            AudioClip distortedClip = AudioClip.Create(
                clip.name + "_Distorted",
                sampleCount,
                channels,
                clip.frequency,
                false
            );

            distortedClip.SetData(processedSamples, 0);
            return distortedClip;
        }
    }
}