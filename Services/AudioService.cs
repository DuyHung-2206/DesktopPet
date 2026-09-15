using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace DesktopPet.Services
{
    public class AudioService
    {
        private static AudioService? _instance;
        public static AudioService Instance => _instance ??= new AudioService();

        private double _volume = 0.8;
        public double Volume
        {
            get => _volume;
            set => _volume = Math.Clamp(value, 0.0, 1.0);
        }

        public bool IsMuted { get; set; } = false;

        public double EffectiveVolume => IsMuted ? 0.0 : _volume;

        public readonly struct ToneNote
        {
            public int Frequency { get; }
            public int DurationMs { get; }

            public ToneNote(int frequency, int durationMs)
            {
                Frequency = frequency;
                DurationMs = durationMs;
            }
        }

        private AudioService() { }

        public void PlayClick()
        {
            PlayMelody(new[] { new ToneNote(850, 35) });
        }

        public void PlayFeed()
        {
            PlayMelody(new[]
            {
                new ToneNote(600, 60),
                new ToneNote(850, 80)
            }, pauseBetweenNotesMs: 25);
        }

        public void PlayDrink()
        {
            PlayMelody(new[]
            {
                new ToneNote(480, 70),
                new ToneNote(620, 80),
                new ToneNote(750, 100)
            }, pauseBetweenNotesMs: 20);
        }

        public void PlayHappy()
        {
            PlayMelody(new[]
            {
                new ToneNote(523, 70), // C5
                new ToneNote(659, 70), // E5
                new ToneNote(784, 120) // G5
            }, pauseBetweenNotesMs: 20);
        }

        public void PlayLevelUp()
        {
            PlayMelody(new[]
            {
                new ToneNote(440, 75),  // A4
                new ToneNote(554, 75),  // C#5
                new ToneNote(659, 75),  // E5
                new ToneNote(880, 180)  // A5
            }, pauseBetweenNotesMs: 15);
        }

        public void PlaySleep()
        {
            PlayMelody(new[]
            {
                new ToneNote(350, 140),
                new ToneNote(280, 220)
            }, pauseBetweenNotesMs: 40);
        }

        public void PlayWakeUp()
        {
            PlayMelody(new[]
            {
                new ToneNote(440, 65), // A4
                new ToneNote(554, 65), // C#5
                new ToneNote(659, 120) // E5
            }, pauseBetweenNotesMs: 20);
        }

        public void PlayCoin()
        {
            PlayMelody(new[]
            {
                new ToneNote(987, 60),  // B5
                new ToneNote(1318, 140) // E6
            }, pauseBetweenNotesMs: 15);
        }

        public void PlayBath()
        {
            PlayMelody(new[]
            {
                new ToneNote(700, 50),
                new ToneNote(750, 50),
                new ToneNote(800, 60)
            }, pauseBetweenNotesMs: 15);
        }

        public void PlayError()
        {
            PlayMelody(new[] { new ToneNote(250, 140) });
        }

        public void PlayHurt()
        {
            PlaySound("hurt", new[]
            {
                new ToneNote(220, 90),
                new ToneNote(180, 110)
            });
        }

        public void PlaySound(string soundName, int fallbackFreq, int fallbackDuration)
        {
            PlaySound(soundName, new[] { new ToneNote(fallbackFreq, fallbackDuration) });
        }

        public void PlaySound(string soundName, ToneNote[] fallbackNotes)
        {
            if (IsMuted || EffectiveVolume <= 0.001) return;

            Task.Run(() =>
            {
                try
                {
                    var soundFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Audio", $"{soundName}.wav");
                    if (File.Exists(soundFile))
                    {
                        var rawBytes = File.ReadAllBytes(soundFile);
                        var scaledBytes = ScaleWavVolume(rawBytes, EffectiveVolume);
                        using var ms = new MemoryStream(scaledBytes);
                        using var player = new SoundPlayer(ms);
                        player.Play();
                        return;
                    }

                    PlayMelody(fallbackNotes);
                }
                catch
                {
                    // Fallback an toàn nếu môi trường không có SoundPlayer audio driver
                }
            });
        }

        public void PlayMelody(ToneNote[] notes, int pauseBetweenNotesMs = 20)
        {
            if (IsMuted || EffectiveVolume <= 0.001 || notes == null || notes.Length == 0) return;

            var wavBytes = GenerateMelodyWav(notes, EffectiveVolume, pauseBetweenNotesMs);
            if (wavBytes.Length == 0) return;

            Task.Run(() =>
            {
                try
                {
                    using var ms = new MemoryStream(wavBytes);
                    using var player = new SoundPlayer(ms);
                    player.Play();
                }
                catch
                {
                    // Fallback an toàn
                }
            });
        }

        /// <summary>
        /// Tạo byte array chuẩn WAV PCM 16-bit Mono 44.1kHz với âm lượng được nhân trực tiếp vào biên độ sóng.
        /// </summary>
        public static byte[] GenerateMelodyWav(IEnumerable<ToneNote> notes, double volume, int pauseBetweenNotesMs = 15)
        {
            double vol = Math.Clamp(volume, 0.0, 1.0);
            if (vol <= 0.0001) return Array.Empty<byte>();

            const int sampleRate = 44100;
            var pcmSamples = new List<short>();

            foreach (var note in notes)
            {
                int noteSamplesCount = (int)(sampleRate * (note.DurationMs / 1000.0));
                if (noteSamplesCount <= 0) continue;

                double freq = Math.Clamp(note.Frequency, 20, 20000);
                double maxAmplitude = 32000.0 * vol;

                // Attack 5ms, Release 10ms để tránh tiếng click do gián đoạn biên độ
                int attackSamples = Math.Min(noteSamplesCount / 4, (int)(sampleRate * 0.005));
                int releaseSamples = Math.Min(noteSamplesCount / 4, (int)(sampleRate * 0.010));
                int sustainSamples = noteSamplesCount - attackSamples - releaseSamples;

                for (int i = 0; i < noteSamplesCount; i++)
                {
                    double t = (double)i / sampleRate;
                    double env = 1.0;

                    if (i < attackSamples && attackSamples > 0)
                    {
                        env = (double)i / attackSamples;
                    }
                    else if (i >= attackSamples + sustainSamples && releaseSamples > 0)
                    {
                        int relIndex = i - (attackSamples + sustainSamples);
                        env = 1.0 - ((double)relIndex / releaseSamples);
                    }

                    double rawSample = Math.Sin(2.0 * Math.PI * freq * t) * maxAmplitude * env;
                    pcmSamples.Add((short)Math.Clamp(rawSample, short.MinValue, short.MaxValue));
                }

                // Khoảng lặng giữa các nốt
                int pauseSamplesCount = (int)(sampleRate * (pauseBetweenNotesMs / 1000.0));
                for (int p = 0; p < pauseSamplesCount; p++)
                {
                    pcmSamples.Add(0);
                }
            }

            if (pcmSamples.Count == 0) return Array.Empty<byte>();

            int subchunk2Size = pcmSamples.Count * sizeof(short);
            int chunkSize = 36 + subchunk2Size;

            using var stream = new MemoryStream(44 + subchunk2Size);
            using var writer = new BinaryWriter(stream);

            // RIFF header
            writer.Write("RIFF"u8.ToArray());
            writer.Write(chunkSize);
            writer.Write("WAVE"u8.ToArray());

            // fmt subchunk
            writer.Write("fmt "u8.ToArray());
            writer.Write(16); // Subchunk1Size (16 for PCM)
            writer.Write((short)1); // AudioFormat (1 = PCM)
            writer.Write((short)1); // NumChannels (1 = Mono)
            writer.Write(sampleRate); // SampleRate
            writer.Write(sampleRate * sizeof(short)); // ByteRate
            writer.Write((short)sizeof(short)); // BlockAlign
            writer.Write((short)16); // BitsPerSample

            // data subchunk
            writer.Write("data"u8.ToArray());
            writer.Write(subchunk2Size);

            foreach (var sample in pcmSamples)
            {
                writer.Write(sample);
            }

            return stream.ToArray();
        }

        /// <summary>
        /// Nhân hệ số âm lượng trực tiếp vào các mẫu 16-bit PCM của tệp WAV sẵn có.
        /// </summary>
        public static byte[] ScaleWavVolume(byte[] wavBytes, double volume)
        {
            double vol = Math.Clamp(volume, 0.0, 1.0);
            if (wavBytes == null || wavBytes.Length < 44) return wavBytes ?? Array.Empty<byte>();

            // Sao chép sang mảng mới
            byte[] result = (byte[])wavBytes.Clone();

            // Tìm vị trí chunk 'data'
            int dataPos = -1;
            for (int i = 12; i < result.Length - 8; i++)
            {
                if (result[i] == (byte)'d' && result[i + 1] == (byte)'a' && result[i + 2] == (byte)'t' && result[i + 3] == (byte)'a')
                {
                    dataPos = i + 8;
                    break;
                }
            }

            if (dataPos < 0) dataPos = 44;

            for (int i = dataPos; i <= result.Length - 2; i += 2)
            {
                short originalSample = BitConverter.ToInt16(result, i);
                short scaledSample = (short)Math.Clamp(originalSample * vol, short.MinValue, short.MaxValue);
                result[i] = (byte)(scaledSample & 0xFF);
                result[i + 1] = (byte)((scaledSample >> 8) & 0xFF);
            }

            return result;
        }
    }
}
