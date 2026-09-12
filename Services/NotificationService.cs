using System;
using System.Collections.Generic;

namespace DesktopPet.Services
{
    public static class NotificationService
    {
        private static readonly Dictionary<string, DateTime> _cooldowns = new();
        private static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(10);

        public static Action<string, string>? OnShowNotification;

        public static bool RequestNotification(string key, string title, string message, TimeSpan? customCooldown = null)
        {
            var cooldown = customCooldown ?? DefaultCooldown;
            var now = DateTime.UtcNow;

            if (_cooldowns.TryGetValue(key, out var lastSent))
            {
                if (now - lastSent < cooldown)
                {
                    return false; // Còn trong thời gian chờ
                }
            }

            _cooldowns[key] = now;
            LoggerService.Info($"Thông báo [{title}]: {message}");
            OnShowNotification?.Invoke(title, message);
            return true;
        }
    }
}
