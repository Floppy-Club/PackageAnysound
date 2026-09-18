using System.Linq;
using UnityEngine;

namespace Anysound.Shared
{
    public static class WaveformMaker
    {
        /// <summary>
        /// Generates a texture representing the waveform of an audio clip.
        /// </summary>
        /// <param name="audioClip">The audio clip to visualize.</param>
        /// <param name="gain">Apply gain to the generated waveform texture.</param>
        /// <param name="width">Width of the output texture in pixels.</param>
        /// <param name="height">Height of the output texture in pixels.</param>
        /// <param name="foregroundColor">Color of the waveform.</param>
        /// <param name="backgroundColor">Background color of the texture.</param>
        /// <returns>A texture representing the audio waveform.</returns>
        public static Texture2D GenerateWaveformTexture(
            AudioClip audioClip,
            float gain = 1,
            int width = 400,
            int height = 100,
            Color? foregroundColor = null,
            Color? backgroundColor = null)
        {
            if (audioClip == null) return null;

            Color fgColor = foregroundColor ?? new Color(0.9568627451f, 0.2509803922f, 0.2509803922f);
            Color bgColor = backgroundColor ?? Color.black;

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = Enumerable.Repeat(bgColor, width * height).ToArray();
            texture.SetPixels(pixels);

            float[] samples = new float[audioClip.samples * audioClip.channels];
            audioClip.GetData(samples, 0);

            // Define bar properties
            int barWidth = 2;
            int barSpacing = 2;
            int totalBars = width / (barWidth + barSpacing);
            float samplesPerBar = (float)samples.Length / totalBars;

            for (int i = 0; i < totalBars; i++)
            {
                int startSample = Mathf.FloorToInt(i * samplesPerBar);
                int endSample = Mathf.Min(startSample + Mathf.FloorToInt(samplesPerBar), samples.Length);

                float peak = 0f;
                for (int j = startSample; j < endSample; j++)
                {
                    peak = Mathf.Max(peak, Mathf.Abs(samples[j]));
                }

                // Apply the gain parameter to increase the peak height
                peak = Mathf.Clamp01(peak * gain);
                
                int barHeight = Mathf.CeilToInt(peak * height);
                int yStart = (height - barHeight) / 2;

                for (int x = 0; x < barWidth; x++)
                {
                    for (int y = 0; y < barHeight; y++)
                    {
                        int px = i * (barWidth + barSpacing) + x;
                        int py = yStart + y;

                        if (px < width && py < height)
                        {
                            texture.SetPixel(px, py, fgColor);
                        }
                    }
                }
            }

            texture.Apply();
            return texture;
        }
    }
}