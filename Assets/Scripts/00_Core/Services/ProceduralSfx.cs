using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Các tín hiệu âm thanh gameplay. Gán clip riêng trên AudioManager để thay bản sinh bằng code.</summary>
public enum SfxCue
{
    /// <summary>Đạn người chơi trúng quái (được giới hạn tần suất).</summary>
    Hit,
    /// <summary>Tàu người chơi bị trúng đạn.</summary>
    PlayerHurt,
    /// <summary>Nhặt vật phẩm nâng cấp đạn.</summary>
    PowerUp,
    /// <summary>Dùng Skill hoặc Booster.</summary>
    Skill,
    /// <summary>Boss xuất hiện.</summary>
    BossWarning,
    Victory,
    Defeat,
    /// <summary>Nhận thưởng (nhiệm vụ, quà).</summary>
    Reward,
    Revive
}

/// <summary>
/// Sinh hiệu ứng âm thanh kiểu chiptune bằng code (sóng vuông/tam giác/sin/nhiễu) để game
/// có đủ âm thanh khi chưa có file audio riêng. Clip được tạo một lần rồi cache.
/// </summary>
public static class ProceduralSfx
{
    private const int SampleRate = 22050;

    private enum Wave { Square, Triangle, Sine, Noise }

    private readonly struct Note
    {
        public readonly float start;
        public readonly float duration;
        public readonly float startFrequency;
        public readonly float endFrequency;
        public readonly float volume;
        public readonly Wave wave;

        public Note(float start, float duration, float startFrequency, float endFrequency, float volume, Wave wave)
        {
            this.start = start;
            this.duration = duration;
            this.startFrequency = startFrequency;
            this.endFrequency = endFrequency;
            this.volume = volume;
            this.wave = wave;
        }
    }

    private static readonly Dictionary<SfxCue, AudioClip> Cache = new Dictionary<SfxCue, AudioClip>();

    /// <summary>Xoá cache khi vào Play Mode (Domain Reload có thể bị tắt).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        Cache.Clear();
    }

    public static AudioClip Get(SfxCue cue)
    {
        if (Cache.TryGetValue(cue, out AudioClip clip) && clip != null)
            return clip;

        clip = Build(cue);
        Cache[cue] = clip;
        return clip;
    }

    private static AudioClip Build(SfxCue cue)
    {
        switch (cue)
        {
            case SfxCue.Hit:
                return Render("sfx_hit",
                    new Note(0f, 0.05f, 900f, 320f, 0.35f, Wave.Square),
                    new Note(0f, 0.04f, 0f, 0f, 0.25f, Wave.Noise));

            case SfxCue.PlayerHurt:
                return Render("sfx_player_hurt",
                    new Note(0f, 0.26f, 240f, 70f, 0.55f, Wave.Square),
                    new Note(0f, 0.14f, 0f, 0f, 0.45f, Wave.Noise));

            case SfxCue.PowerUp:
                return Render("sfx_power_up",
                    new Note(0.00f, 0.07f, 523f, 523f, 0.35f, Wave.Square),
                    new Note(0.06f, 0.07f, 659f, 659f, 0.35f, Wave.Square),
                    new Note(0.12f, 0.07f, 784f, 784f, 0.35f, Wave.Square),
                    new Note(0.18f, 0.14f, 1047f, 1047f, 0.35f, Wave.Square));

            case SfxCue.Skill:
                return Render("sfx_skill",
                    new Note(0f, 0.32f, 280f, 1300f, 0.45f, Wave.Triangle),
                    new Note(0f, 0.20f, 0f, 0f, 0.12f, Wave.Noise));

            case SfxCue.BossWarning:
                return Render("sfx_boss_warning",
                    new Note(0.00f, 0.24f, 660f, 660f, 0.30f, Wave.Square),
                    new Note(0.25f, 0.24f, 440f, 440f, 0.30f, Wave.Square),
                    new Note(0.50f, 0.24f, 660f, 660f, 0.30f, Wave.Square),
                    new Note(0.75f, 0.40f, 440f, 330f, 0.30f, Wave.Square));

            case SfxCue.Victory:
                return Render("sfx_victory",
                    new Note(0.00f, 0.13f, 523f, 523f, 0.40f, Wave.Triangle),
                    new Note(0.12f, 0.13f, 659f, 659f, 0.40f, Wave.Triangle),
                    new Note(0.24f, 0.13f, 784f, 784f, 0.40f, Wave.Triangle),
                    new Note(0.36f, 0.60f, 1047f, 1047f, 0.40f, Wave.Triangle),
                    new Note(0.36f, 0.60f, 659f, 659f, 0.20f, Wave.Square));

            case SfxCue.Defeat:
                return Render("sfx_defeat",
                    new Note(0.00f, 0.20f, 392f, 392f, 0.35f, Wave.Square),
                    new Note(0.19f, 0.20f, 330f, 330f, 0.35f, Wave.Square),
                    new Note(0.38f, 0.20f, 262f, 262f, 0.35f, Wave.Square),
                    new Note(0.57f, 0.55f, 196f, 150f, 0.35f, Wave.Square));

            case SfxCue.Reward:
                return Render("sfx_reward",
                    new Note(0.00f, 0.10f, 1319f, 1319f, 0.35f, Wave.Sine),
                    new Note(0.08f, 0.25f, 1976f, 1976f, 0.35f, Wave.Sine));

            case SfxCue.Revive:
                return Render("sfx_revive",
                    new Note(0f, 0.45f, 200f, 900f, 0.45f, Wave.Triangle),
                    new Note(0.30f, 0.30f, 900f, 1200f, 0.25f, Wave.Sine));

            default:
                return Render("sfx_blip", new Note(0f, 0.05f, 880f, 880f, 0.3f, Wave.Square));
        }
    }

    private static AudioClip Render(string name, params Note[] notes)
    {
        float length = 0f;
        foreach (Note note in notes)
            length = Mathf.Max(length, note.start + note.duration);

        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(length * SampleRate));
        var samples = new float[sampleCount];
        var noise = new System.Random(name.GetHashCode());

        foreach (Note note in notes)
        {
            int first = Mathf.FloorToInt(note.start * SampleRate);
            int count = Mathf.Min(Mathf.CeilToInt(note.duration * SampleRate), sampleCount - first);
            double phase = 0d;
            float held = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                // Quét tần số theo hàm mũ nghe tự nhiên hơn quét tuyến tính.
                float frequency = note.startFrequency > 0f && note.endFrequency > 0f
                    ? note.startFrequency * Mathf.Pow(note.endFrequency / note.startFrequency, t)
                    : 0f;
                phase += frequency / SampleRate;

                float value;
                switch (note.wave)
                {
                    case Wave.Square:
                        value = phase % 1d < 0.5d ? 1f : -1f;
                        break;
                    case Wave.Triangle:
                        value = 4f * Mathf.Abs((float)(phase % 1d) - 0.5f) - 1f;
                        break;
                    case Wave.Sine:
                        value = Mathf.Sin((float)(phase * Math.PI * 2d));
                        break;
                    default:
                        // Nhiễu giữ mẫu 2 sample một lần -> bớt chói.
                        if ((i & 1) == 0)
                            held = (float)(noise.NextDouble() * 2d - 1d);
                        value = held;
                        break;
                }

                // Envelope: lên nhanh 4ms, tắt dần theo hàm mũ -> không bị "lụp bụp" ở đầu/cuối.
                float attack = Mathf.Clamp01(i / (0.004f * SampleRate));
                float release = Mathf.Pow(1f - t, 2f);
                samples[first + i] += value * note.volume * attack * release;
            }
        }

        // Nén mềm để chồng nhiều nốt không bị vỡ tiếng.
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (float)Math.Tanh(samples[i] * 1.2f);

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
