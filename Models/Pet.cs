using System;
using System.Collections.Generic;

namespace DesktopPet.Models
{
    public class Pet
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Mimi";
        public string SpeciesId { get; set; } = "cat";

        // Chỉ số cơ bản (0 - 100)
        public double Health { get; set; } = 100.0;
        public double Hunger { get; set; } = 80.0;
        public double Happiness { get; set; } = 85.0;
        public double Energy { get; set; } = 80.0;
        public double Cleanliness { get; set; } = 90.0;
        public double Affection { get; set; } = 50.0;

        // Cấp độ và Kinh nghiệm
        public int Level { get; set; } = 0;
        public int Exp { get; set; } = 0;
        public int MaxExp => Math.Max(1, Level) * 100;

        // Tuổi và Ngày sinh
        public DateTime Birthday { get; set; } = DateTime.UtcNow;
        public int AgeDays => Math.Max(1, (int)(DateTime.UtcNow - Birthday).TotalDays);

        // Vị trí và Hướng
        public double X { get; set; } = 200;
        public double Y { get; set; } = 500;
        public bool IsFacingLeft { get; set; } = false;

        // Trạng thái hiện tại
        public PetState State { get; set; } = PetState.Idle;

        // Hệ thống nhu cầu (Needs)
        public Dictionary<string, PetNeedState> Needs { get; set; } = new()
        {
            { PetNeedTypes.Hunger, new PetNeedState() },
            { PetNeedTypes.Thirst, new PetNeedState() },
            { PetNeedTypes.Play, new PetNeedState() },
            { PetNeedTypes.Bath, new PetNeedState() },
            { PetNeedTypes.Sleep, new PetNeedState() },
            { PetNeedTypes.Affection, new PetNeedState() }
        };

        // Kiểm tra ngưỡng trạng thái
        public bool IsHungry => Hunger < 30;
        public bool IsThirsty => Energy < 45 && State != PetState.Sleep;
        public bool IsSleepy => Energy < 25;
        public bool IsDirty => Cleanliness < 35;
        public bool IsSad => Happiness < 30;
        public bool IsSick { get; set; } = false;
        public bool SickPenaltyApplied { get; set; } = false;
    }
}
