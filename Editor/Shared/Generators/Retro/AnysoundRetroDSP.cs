using System;
using UnityEngine;

namespace Anysound.Shared.Generators.Retro
{
    /// <summary>
    /// An 8-bit synth in the style of sfxr (https://sfxr.me): one oscillator with pitch slide, vibrato, arpeggio and repeat,
    /// followed by a resonant low pass, a high pass and a phaser. The length comes from the frontend instead of sfxr's
    /// envelope values, and the hardness mixes the waveform from sine/FM (soft) to the category's square/sawtooth (hard)
    /// </summary>
    public static class AnysoundRetroDSP
    {
        public const int SampleRate = 44100;

        // The peak the sounds are normalized to
        const float OutputPeak = 0.8f;

        // 3 ms
        const int DeclickSamples = 132;

        // The duration the sfxr values are made for. Longer sounds play the slides, sweeps, arpeggio and repeat slower,
        // so the whole sound is stretched instead of ending early
        const float ReferenceDuration = 0.3f;

        /// <param name="variation">0 is the sound itself, other values give slightly different versions of it (see AnysoundRetroObject.Vary)</param>
        public static AudioClip CreateAudioClip(AnysoundRetroObject retroObject, AnysoundRetroParameters parameters, int variation = 0)
        {
            if (!retroObject) return null;

            var settings = AnysoundRetroObject.Vary(retroObject.Resolve(parameters), parameters, variation);
            parameters = AnysoundRetroObject.Vary(parameters, variation);
            float[] samples = GenerateSamples(settings, parameters);
            var clip = AudioClip.Create($"Retro_{parameters.category}", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static float[] GenerateSamples(RetroSynthSettings settings, AnysoundRetroParameters parameters)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(parameters.duration * SampleRate));
            var samples = new float[sampleCount];

            // The same seed gives the same noise too
            var synth = new Synth(settings, new System.Random(parameters.seed), parameters.duration / ReferenceDuration, parameters.hardness);
            var envelope = new Envelope(settings, parameters, sampleCount);

            for (int i = 0; i < sampleCount; i++)
            {
                if (!synth.IsPlaying)
                {
                    // The pitch reached its limit before the end: fade out instead of cutting off with a click
                    FadeOut(samples, i);
                    break;
                }

                samples[i] = synth.NextSample() * envelope.Evaluate(i);
            }

            Normalize(samples);
            return samples;
        }

        static void FadeOut(float[] samples, int end)
        {
            int length = Mathf.Min(DeclickSamples, end);
            for (int i = 0; i < length; i++)
                samples[end - 1 - i] *= (float)i / length;
        }

        static void Normalize(float[] samples)
        {
            float peak = 0f;
            foreach (float sample in samples)
                peak = Mathf.Max(peak, Mathf.Abs(sample));

            if (peak < 1e-6f) return;

            float gain = OutputPeak / peak;
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= gain;
        }

        /// <summary>
        /// A short attack (so the sound doesn't start with a click), sustain (with punch) and decay over the whole duration
        /// </summary>
        readonly struct Envelope
        {
            readonly int _attackEnd, _sustainEnd, _end;
            readonly float _punch;

            public Envelope(RetroSynthSettings settings, AnysoundRetroParameters parameters, int sampleCount)
            {
                _end = sampleCount;
                _attackEnd = Mathf.Min(DeclickSamples, sampleCount / 4);
                // Always leave some decay, so the sound doesn't end with a click
                float sustain = Mathf.Min(settings[RetroParameter.Sustain], 0.9f);
                _sustainEnd = _attackEnd + Mathf.RoundToInt((sampleCount - _attackEnd) * sustain);
                _punch = settings[RetroParameter.Punch];
            }

            public float Evaluate(int i)
            {
                // Up to the start of the sustain, punch included, so there's no step between the two
                if (i < _attackEnd)
                    return (float)i / _attackEnd * (1f + 2f * _punch);
                if (i < _sustainEnd)
                    return 1f + (1f - (float)(i - _attackEnd) / (_sustainEnd - _attackEnd)) * 2f * _punch;
                return 1f - (float)(i - _sustainEnd) / Mathf.Max(1, _end - _sustainEnd);
            }
        }

        /// <summary>
        /// A port of the sfxr synth (SynthSample in the original source), with the frequency values of sfxr at 44100 Hz
        /// and 8x supersampling
        /// </summary>
        sealed class Synth
        {
            const int Supersampling = 8;
            const int PhaserBufferLength = 1024;
            const int NoiseBufferLength = 32;
            // The low pass on the soft noise, per supersample, and its make up gain
            const float SoftNoiseCoefficient = 0.05f;
            const float SoftNoiseGain = 1.5f;
            // The highest pitch, about 11 kHz. sfxr goes up to the sample rate, which aliases into harsh clicks
            const double MinPeriod = 32.0;

            // The FM index in the middle of the hardness, between the pure sine (0) and the hard waveform (1)
            const float MaxFmIndex = 1f;
            // The frequency of the FM modulator, relative to the tone
            const int FmRatio = 2;

            readonly RetroSynthSettings _s;
            readonly System.Random _random;
            readonly float _noiseMix;
            readonly double _timeScale;
            readonly float _hardness;
            float _softNoise;

            double _fperiod, _fmaxperiod, _fslide, _fdslide;
            // The period in supersamples, and the position within it (0-1). The position is kept as a fraction instead of a
            // sample count like in sfxr, so pitch changes (slide, vibrato, arpeggio, repeat) don't make the waveform jump
            double _period, _phase;
            float _squareDuty, _squareSlide;
            double _arpMod;
            int _arpTime, _arpLimit;

            float _fltp, _fltdp, _fltw, _fltwD, _fltdmp, _fltphp, _flthp, _flthpD;
            bool _lowPassEnabled;
            float _vibPhase, _vibSpeed, _vibAmp;
            float _fphase, _fdphase;
            int _iphase, _ipp;
            readonly float[] _phaserBuffer = new float[PhaserBufferLength];
            // One value more than the noise steps: the first value is the last of the previous buffer, so the
            // interpolated (soft) noise continues smoothly when the buffer is refilled
            readonly float[] _noiseBuffer = new float[NoiseBufferLength + 1];
            int _repTime, _repLimit;

            public bool IsPlaying { get; private set; } = true;

            public Synth(RetroSynthSettings settings, System.Random random, float timeScale, float hardness)
            {
                _hardness = Mathf.Clamp01(hardness);
                _s = settings;
                _random = random;
                _timeScale = Math.Max(timeScale, 0.01f);
                _noiseMix = settings.waveform == RetroWaveform.Noise ? 1f : settings[RetroParameter.NoiseMix];
                Reset(false);
            }

            // A per sample factor that gives the same change over the stretched length
            double Stretch(double factor) => Math.Pow(factor, 1.0 / _timeScale);

            float Random11() => (float)_random.NextDouble() * 2f - 1f;

            void FillNoise()
            {
                _noiseBuffer[0] = _noiseBuffer[NoiseBufferLength];
                for (int i = 1; i <= NoiseBufferLength; i++)
                    _noiseBuffer[i] = Random11();
            }

            // A restart (repeat) only resets the pitch, duty and arpeggio
            void Reset(bool restart)
            {
                float baseFrequency = _s[RetroParameter.BaseFrequency];
                _fperiod = 100.0 / (baseFrequency * baseFrequency + 0.001);
                _period = _fperiod;
                float limit = _s[RetroParameter.FrequencyLimit];
                _fmaxperiod = 100.0 / (limit * limit + 0.001);
                float slide = _s[RetroParameter.FrequencySlide];
                _fslide = Stretch(1.0 - slide * slide * slide * 0.01);
                float deltaSlide = _s[RetroParameter.FrequencyDeltaSlide];
                _fdslide = -deltaSlide * deltaSlide * deltaSlide * 0.000001 / (_timeScale * _timeScale);
                _squareDuty = 0.5f - _s[RetroParameter.DutyCycle] * 0.5f;
                _squareSlide = (float)(-_s[RetroParameter.DutySweep] * 0.00005f / _timeScale);

                float arpeggio = _s[RetroParameter.ArpeggioAmount];
                _arpMod = arpeggio >= 0f ? 1.0 - arpeggio * arpeggio * 0.9 : 1.0 + arpeggio * arpeggio * 10.0;
                _arpTime = 0;
                float arpeggioSpeed = _s[RetroParameter.ArpeggioSpeed];
                _arpLimit = arpeggioSpeed >= 1f ? 0 : (int)(((1f - arpeggioSpeed) * (1f - arpeggioSpeed) * 20000f + 32f) * _timeScale);

                if (restart) return;

                float lowPass = _s[RetroParameter.LowPassCutoff];
                _lowPassEnabled = lowPass < 0.999f;
                _fltp = 0f;
                _fltdp = 0f;
                _fltw = lowPass * lowPass * lowPass * 0.1f;
                _fltwD = (float)Stretch(1f + _s[RetroParameter.LowPassSweep] * 0.0001f);
                float resonance = _s[RetroParameter.LowPassResonance];
                _fltdmp = Mathf.Min(5f / (1f + resonance * resonance * 20f) * (0.01f + _fltw), 0.8f);
                _fltphp = 0f;
                float highPass = _s[RetroParameter.HighPassCutoff];
                _flthp = highPass * highPass * 0.1f;
                _flthpD = (float)Stretch(1f + _s[RetroParameter.HighPassSweep] * 0.0003f);

                _vibPhase = 0f;
                float vibratoSpeed = _s[RetroParameter.VibratoSpeed];
                _vibSpeed = vibratoSpeed * vibratoSpeed * 0.01f;
                _vibAmp = _s[RetroParameter.VibratoDepth] * 0.5f;

                float phaserOffset = _s[RetroParameter.PhaserOffset];
                _fphase = Mathf.Sign(phaserOffset) * phaserOffset * phaserOffset * 1020f;
                float phaserSweep = _s[RetroParameter.PhaserSweep];
                _fdphase = (float)(Mathf.Sign(phaserSweep) * phaserSweep * phaserSweep / _timeScale);
                _iphase = Math.Abs((int)_fphase);
                _ipp = 0;
                Array.Clear(_phaserBuffer, 0, _phaserBuffer.Length);

                FillNoise();

                _repTime = 0;
                float repeatSpeed = _s[RetroParameter.RepeatSpeed];
                _repLimit = repeatSpeed <= 0f ? 0 : (int)(((1f - repeatSpeed) * (1f - repeatSpeed) * 20000f + 32f) * _timeScale);
            }

            public float NextSample()
            {
                if (_repLimit != 0 && ++_repTime >= _repLimit)
                {
                    _repTime = 0;
                    Reset(true);
                }

                if (_arpLimit != 0 && ++_arpTime >= _arpLimit)
                {
                    _arpLimit = 0;
                    _fperiod *= _arpMod;
                }

                _fslide += _fdslide;
                _fperiod *= _fslide;
                if (_fperiod > _fmaxperiod)
                {
                    _fperiod = _fmaxperiod;
                    // The pitch fell to the limit: the sound ends, like in sfxr
                    if (_s[RetroParameter.FrequencyLimit] > 0f)
                        IsPlaying = false;
                }

                double rfperiod = _fperiod;
                if (_vibAmp > 0f)
                {
                    _vibPhase += _vibSpeed;
                    rfperiod = _fperiod * (1.0 + Math.Sin(_vibPhase) * _vibAmp);
                }

                _period = Math.Max(rfperiod, MinPeriod);
                _squareDuty = Mathf.Clamp(_squareDuty + _squareSlide, 0f, 0.5f);

                _fphase += _fdphase;
                _iphase = Math.Min(Math.Abs((int)_fphase), PhaserBufferLength - 1);

                if (_flthpD != 1f)
                    _flthp = Mathf.Clamp(_flthp * _flthpD, 0.00001f, 0.1f);

                float sum = 0f;
                for (int i = 0; i < Supersampling; i++)
                {
                    _phase += 1.0 / _period;
                    if (_phase >= 1.0)
                    {
                        _phase -= Math.Floor(_phase);
                        if (_noiseMix > 0f)
                            FillNoise();
                    }

                    float fp = (float)_phase;
                    float sample = Tone(fp);
                    if (_noiseMix > 0f)
                        sample = Mathf.Lerp(sample, Noise(fp), _noiseMix);

                    // Low pass
                    float previous = _fltp;
                    _fltw = Mathf.Clamp(_fltw * _fltwD, 0f, 0.1f);
                    if (_lowPassEnabled)
                    {
                        _fltdp += (sample - _fltp) * _fltw;
                        _fltdp -= _fltdp * _fltdmp;
                    }
                    else
                    {
                        _fltp = sample;
                        _fltdp = 0f;
                    }

                    _fltp += _fltdp;

                    // High pass
                    _fltphp += _fltp - previous;
                    _fltphp -= _fltphp * _flthp;
                    sample = _fltphp;

                    // Phaser
                    _phaserBuffer[_ipp & (PhaserBufferLength - 1)] = sample;
                    sample += _phaserBuffer[(_ipp - _iphase + PhaserBufferLength) & (PhaserBufferLength - 1)];
                    _ipp = (_ipp + 1) & (PhaserBufferLength - 1);

                    sum += sample;
                }

                return sum / Supersampling;
            }

            // Soft to hard: sine, then more and more FM up to the middle, then crossfaded into the hard waveform
            float Tone(float fp)
            {
                if (_s.waveform == RetroWaveform.Noise) return 0f;

                float fmIndex = MaxFmIndex * Mathf.Clamp01(_hardness * 2f);
                float fm = Mathf.Sin(2f * Mathf.PI * fp + fmIndex * Mathf.Sin(2f * Mathf.PI * FmRatio * fp));
                if (_hardness <= 0.5f) return fm;

                return Mathf.Lerp(fm, HardOscillator(fp), _hardness * 2f - 1f);
            }

            // Hard noise holds every value like sfxr, soft noise glides between them and is low passed, so it doesn't crackle
            float Noise(float fp)
            {
                float position = fp * NoiseBufferLength;
                int index = Math.Min((int)position, NoiseBufferLength - 1);
                float held = _noiseBuffer[index + 1];
                float glided = Mathf.Lerp(_noiseBuffer[index], _noiseBuffer[index + 1], position - index);
                _softNoise += (glided - _softNoise) * SoftNoiseCoefficient;
                return Mathf.Lerp(_softNoise * SoftNoiseGain, held, _hardness);
            }

            // The category's waveform, as in sfxr
            float HardOscillator(float fp)
            {
                switch (_s.waveform)
                {
                    case RetroWaveform.Square:
                        return fp < _squareDuty ? 0.5f : -0.5f;
                    case RetroWaveform.Sawtooth:
                        return 1f - fp * 2f;
                    case RetroWaveform.Sine:
                        return Mathf.Sin(fp * 2f * Mathf.PI);
                    case RetroWaveform.Triangle:
                        return Mathf.Abs(fp - 0.5f) * 4f - 1f;
                    default:
                        return 0f;
                }
            }
        }
    }
}
