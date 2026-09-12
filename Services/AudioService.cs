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

        public bool IsMuted { get; set; } = false;
        public double Volume { get; set; } = 0.8;

        private AudioService() { }

        public void PlayClick()
        {
            PlaySound("click", 800, 40);
        }

        public void PlayFeed()
        {
            Task.Run(() =>
            {
                PlayProceduralBeep(600, 60);
                Task.Delay(80).Wait();
                PlayProceduralBeep(850, 80);
            });
        }

        public void PlayHappy()
        {
            Task.Run(() =>
            {
                PlayProceduralBeep(523, 70); // C5
                Task.Delay(60).Wait();
                PlayProceduralBeep(659, 70); // E5
                Task.Delay(60).Wait();
                PlayProceduralBeep(784, 120); // G5
            });
        }

        public void PlayLevelUp()
        {
            Task.Run(() =>
            {
                PlayProceduralBeep(440, 80); // A4
                Task.Delay(50).Wait();
                PlayProceduralBeep(554, 80); // C#5
                Task.Delay(50).Wait();
                PlayProceduralBeep(659, 80); // E5
                Task.Delay(50).Wait();
                PlayProceduralBeep(880, 200); // A5
            });
        }

        public void PlaySleep()
        {
            Task.Run(() =>
            {
                PlayProceduralBeep(350, 150);
                Task.Delay(100).Wait();
                PlayProceduralBeep(280, 250);
            });
        }

        public void PlayCoin()
        {
            Task.Run(() =>
            {
                PlayProceduralBeep(987, 60); // B5
                Task.Delay(40).Wait();
                PlayProceduralBeep(1318, 140); // E6
            });
        }

        public void PlayBath()
        {
            Task.Run(() =>
            {
                PlayProceduralBeep(700, 50);
                Task.Delay(50).Wait();
                PlayProceduralBeep(750, 50);
                Task.Delay(50).Wait();
                PlayProceduralBeep(800, 60);
            });
        }

        public void PlayError()
        {
            Task.Run(() =>
            {
                PlayProceduralBeep(250, 150);
            });
        }

        private void PlaySound(string soundName, int fallbackFreq, int fallbackDuration)
        {
            if (IsMuted) return;

            Task.Run(() =>
            {
                try
                {
                    var soundFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Audio", $"{soundName}.wav");
                    if (File.Exists(soundFile))
                    {
                        using var player = new SoundPlayer(soundFile);
                        player.Play();
                        return;
                    }

                    PlayProceduralBeep(fallbackFreq, fallbackDuration);
                }
                catch
                {
                    // Fallback an toàn
                }
            });
        }

        private void PlayProceduralBeep(int frequency, int durationMs)
        {
            if (IsMuted) return;

            try
            {
                Console.Beep(Math.Clamp(frequency, 37, 32767), Math.Clamp(durationMs, 20, 2000));
            }
            catch
            {
                // Bỏ qua nếu môi trường không có còi beep
            }
        }
    }
}
