using System.Collections.Generic;
using UnityEngine;

namespace Anysound.Shared.Generators.UI
{
    public static class AnysoundUIDSP
    {
        // Presets read their preview clip back as 44.1kHz stereo, so everything is rendered in that format
        public const int OutputSampleRate = 44100;
        public const int OutputChannels = 2;

        // Samples below this level at the end of the sound are trimmed away
        const float SilenceThreshold = 0.0005f;

        // Fade out used when a step is cut short by its duration, to avoid clicks
        const float StepFadeOutSeconds = 0.005f;

        struct RenderJob
        {
            public AudioClip clip;
            public int startFrame;
            public float pitchRatio;
            public float gain;

            // Output frames this job may play. 0 = the whole (resampled) clip
            public int maxFrames;
        }

        /// <summary>
        /// Renders a UI sound: the action sequencer triggers the material (+ extra material and extra sample) 1-4 times
        /// with individual delays and pitches. Size then sets the band pass filter and the amplitude envelope.
        /// The extra sample is mixed in after filtering and normalization, so size never affects it
        /// </summary>
        public static AudioClip CreateAudioClip(AnysoundUIObject uiObject, AnysoundUIParameters parameters)
        {
            if (!uiObject || uiObject.materials == null || uiObject.materials.Count == 0)
            {
                Debug.LogWarning("UI generator has no materials");
                return null;
            }

            var material = uiObject.materials[Mathf.Clamp(parameters.material, 0, uiObject.materials.Count - 1)];
            AnysoundUIObject.UIMaterialSettings extraMaterial = null;
            if (parameters.HasExtraMaterial && parameters.ExtraMaterialIndex < uiObject.materials.Count)
                extraMaterial = uiObject.materials[parameters.ExtraMaterialIndex];

            List<AnysoundUIObject.UIActionStep> steps = new();
            string actionName = "";
            if (uiObject.actions != null && uiObject.actions.Count > 0)
            {
                var action = uiObject.actions[Mathf.Clamp(parameters.action, 0, uiObject.actions.Count - 1)];
                actionName = action.name;
                if (action.steps != null)
                {
                    for (int i = 0; i < action.steps.Count && i < AnysoundUIObject.MaxActionSteps; i++)
                        steps.Add(action.steps[i]);
                }
            }

            if (steps.Count == 0)
                steps.Add(new AnysoundUIObject.UIActionStep(0f, 0f, 1f));

            AnysoundUIObject.UIExtraSettings extra = null;
            if (parameters.HasExtraSample && uiObject.extras != null && parameters.ExtraSampleIndex < uiObject.extras.Count)
                extra = uiObject.extras[parameters.ExtraSampleIndex];

            // The exact samples are chosen in the parameters, so the same parameters always render the same sound
            AudioClip materialClip = GetClip(material.clipCollection, parameters.materialClip);
            AudioClip extraMaterialClip = extraMaterial != null ? GetClip(extraMaterial.clipCollection, parameters.extraMaterialClip) : null;
            AudioClip extraClip = extra != null ? GetClip(extra.GetClipCollection(actionName), parameters.extraSampleClip) : null;

            if (!materialClip)
            {
                Debug.LogWarning($"UI material '{material.name}' has no samples");
                return null;
            }

            var resolvedSteps = parameters.ResolveSteps(steps);

            List<RenderJob> jobs = new();
            List<RenderJob> extraJobs = new();
            float stepTime = 0f;
            for (int i = 0; i < steps.Count; i++)
            {
                var step = resolvedSteps[i];
                stepTime += step.delay;
                int startFrame = Mathf.RoundToInt(stepTime * OutputSampleRate);
                float pitchRatio = Mathf.Pow(2f, step.pitchSemitones / 12f);
                int maxFrames = Mathf.RoundToInt(Mathf.Max(0f, step.duration) * OutputSampleRate);

                jobs.Add(new RenderJob
                {
                    clip = materialClip, startFrame = startFrame, pitchRatio = pitchRatio, maxFrames = maxFrames,
                    gain = step.volume * material.volume
                });

                if (extraMaterialClip)
                {
                    jobs.Add(new RenderJob
                    {
                        clip = extraMaterialClip, startFrame = startFrame, pitchRatio = pitchRatio, maxFrames = maxFrames,
                        gain = step.volume * extraMaterial.volume * uiObject.extraMaterialVolume
                    });
                }

                if (extraClip && (extra.followActionSequence || i == 0))
                {
                    extraJobs.Add(new RenderJob
                    {
                        clip = extraClip,
                        startFrame = extra.followActionSequence ? startFrame : 0,
                        pitchRatio = extra.followActionSequence ? pitchRatio : 1f,
                        maxFrames = extra.followActionSequence ? maxFrames : 0,
                        gain = (extra.followActionSequence ? step.volume : 1f) * extra.volume
                    });
                }
            }

            float[] output = Render(jobs, uiObject.GetEnvelopeSettings(parameters.size));

            AudioClip filtered = AudioClip.Create("UISound", output.Length / OutputChannels, OutputChannels, OutputSampleRate, false);
            filtered.SetData(output, 0);

            // The filter is applied after mixing, so it affects the material and the extra material alike
            filtered = AnysoundAudioDSP.ApplyBandpassFilter(filtered, uiObject.GetFilterSettings(parameters.size));
            output = new float[filtered.samples * filtered.channels];
            filtered.GetData(output, 0);

            if (uiObject.normalizeOutput)
                Normalize(output, uiObject.normalizePeak);

            // The extra sample skips envelope, filter and normalization, so it sounds the same at every size
            if (extraJobs.Count > 0)
                output = Mix(output, Render(extraJobs, null));

            return Finalize(output);
        }

        static AudioClip GetClip(AnysoundSoundCollectionObject collection, int index)
        {
            return collection ? collection.GetClip(index) : null;
        }

        static float[] Render(List<RenderJob> jobs, AnysoundAudioDSP.ADSREnvelopeSettings? envelope)
        {
            int totalFrames = 1;
            List<float[]> sourceData = new();
            foreach (var job in jobs)
            {
                float[] data = new float[job.clip.samples * job.clip.channels];
                job.clip.GetData(data, 0);
                sourceData.Add(data);
                totalFrames = Mathf.Max(totalFrames, job.startFrame + GetResampledFrameCount(job));
            }

            float[] output = new float[totalFrames * OutputChannels];
            for (int i = 0; i < jobs.Count; i++)
            {
                AddResampled(output, sourceData[i], jobs[i], envelope);
            }

            return output;
        }

        static float GetStep(RenderJob job) => job.pitchRatio * job.clip.frequency / OutputSampleRate;

        static int GetResampledFrameCount(RenderJob job)
        {
            int frames = job.clip.samples < 2 ? job.clip.samples : Mathf.FloorToInt((job.clip.samples - 1) / GetStep(job)) + 1;
            return job.maxFrames > 0 ? Mathf.Min(frames, job.maxFrames) : frames;
        }

        /// <summary>
        /// Mixes a clip into the output with linear interpolation resampling. The resampling both converts the sample rate
        /// and pitches the sound (like a sampler, higher pitch = shorter sound).
        /// The envelope is applied per trigger, so a short envelope shortens every step instead of cutting off the sequence
        /// </summary>
        static void AddResampled(float[] output, float[] source, RenderJob job, AnysoundAudioDSP.ADSREnvelopeSettings? envelope)
        {
            int sourceChannels = job.clip.channels;
            int sourceFrames = job.clip.samples;
            float step = GetStep(job);
            int frames = GetResampledFrameCount(job);

            for (int i = 0; i < frames; i++)
            {
                int outFrame = job.startFrame + i;
                if (outFrame * OutputChannels >= output.Length) break;

                float position = i * step;
                int index = (int)position;
                float fraction = position - index;
                int nextIndex = Mathf.Min(index + 1, sourceFrames - 1);
                float envelopeGain = envelope.HasValue ? EvaluateEnvelope(envelope.Value, (float)i / OutputSampleRate) : 1f;
                float gain = job.gain * envelopeGain * EvaluateStepFadeOut(job, i);
                if (gain <= 0f && i > 0) continue;

                for (int channel = 0; channel < OutputChannels; channel++)
                {
                    // Mono sources are copied to both channels
                    int sourceChannel = Mathf.Min(channel, sourceChannels - 1);
                    float a = source[index * sourceChannels + sourceChannel];
                    float b = source[nextIndex * sourceChannels + sourceChannel];
                    output[outFrame * OutputChannels + channel] += Mathf.Lerp(a, b, fraction) * gain;
                }
            }
        }

        /// <summary>
        /// Short fade out at the end of a step that is cut by its duration
        /// </summary>
        static float EvaluateStepFadeOut(RenderJob job, int frame)
        {
            if (job.maxFrames <= 0) return 1f;
            int fadeFrames = Mathf.Min(Mathf.RoundToInt(StepFadeOutSeconds * OutputSampleRate), job.maxFrames);
            int framesLeft = job.maxFrames - frame;
            return framesLeft >= fadeFrames ? 1f : Mathf.Clamp01((float)framesLeft / fadeFrames);
        }

        /// <summary>
        /// Same ADSR shape as AnysoundAudioDSP.ApplyADSREnvelope: the release starts at "duration" (or after attack + decay)
        /// </summary>
        static float EvaluateEnvelope(AnysoundAudioDSP.ADSREnvelopeSettings envelope, float time)
        {
            float attack = Mathf.Max(envelope.attackTime, 0.0001f);
            float decay = Mathf.Max(envelope.decayTime, 0.0001f);
            float release = Mathf.Max(envelope.releaseTime, 0.0001f);
            float sustain = Mathf.Clamp01(envelope.sustainLevel);
            float releaseStart = Mathf.Max(attack + decay, envelope.duration);

            if (time < attack) return time / attack;
            if (time < attack + decay) return Mathf.Lerp(1f, sustain, (time - attack) / decay);
            if (time < releaseStart) return sustain;
            return Mathf.Lerp(sustain, 0f, (time - releaseStart) / release);
        }

        static void Normalize(float[] samples, float normalizePeak)
        {
            float peak = 0f;
            foreach (float sample in samples)
                peak = Mathf.Max(peak, Mathf.Abs(sample));

            if (normalizePeak <= 0f || peak <= 0f) return;

            float gain = normalizePeak / peak;
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= gain;
        }

        static float[] Mix(float[] a, float[] b)
        {
            float[] result = new float[Mathf.Max(a.Length, b.Length)];
            for (int i = 0; i < a.Length; i++) result[i] += a[i];
            for (int i = 0; i < b.Length; i++) result[i] += b[i];
            return result;
        }

        /// <summary>
        /// Trims trailing silence and protects against clipping
        /// </summary>
        static AudioClip Finalize(float[] samples)
        {
            float peak = 0f;
            int lastAudibleFrame = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = Mathf.Abs(samples[i]);
                peak = Mathf.Max(peak, abs);
                if (abs > SilenceThreshold)
                    lastAudibleFrame = i / OutputChannels;
            }

            float gain = peak > 0.99f ? 0.99f / peak : 1f;

            int frames = Mathf.Max(1, lastAudibleFrame + 1);
            float[] trimmed = new float[frames * OutputChannels];
            for (int i = 0; i < trimmed.Length; i++)
                trimmed[i] = samples[i] * gain;

            AudioClip result = AudioClip.Create("UISound", frames, OutputChannels, OutputSampleRate, false);
            result.SetData(trimmed, 0);
            return result;
        }
    }
}
