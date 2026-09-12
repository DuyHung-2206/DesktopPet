using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DesktopPet.Models;
using DesktopPet.Services;

namespace DesktopPet.ViewModels
{
    public class PetCollectionItemDisplay : ViewModelBase
    {
        public string SpeciesId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string BaseColor { get; set; } = "#FFA726";
        public string SecondaryColor { get; set; } = "#FFE082";
        public int UnlockPrice { get; set; }
        public bool IsUnlocked { get; set; }
        public bool IsActive { get; set; }
        public string CustomName { get; set; } = string.Empty;
        public int Level { get; set; } = 1;
        public string ActionText => IsActive ? "Đang Nuôi 🐾" : (IsUnlocked ? "Chọn Thú Cưng" : $"Mở Khóa ({UnlockPrice}🪙)");
    }

    public class PetCollectionViewModel : ViewModelBase
    {
        private readonly PetViewModel _petVM;
        private readonly GameSave _save;

        public ObservableCollection<PetCollectionItemDisplay> Collection { get; } = new();

        public int Coins => _save.Coins;

        public ICommand SelectOrUnlockCommand { get; }

        public PetCollectionViewModel(PetViewModel petVM)
        {
            _petVM = petVM;
            _save = petVM.GameSave;

            SelectOrUnlockCommand = new RelayCommand<PetCollectionItemDisplay>(OnSelectOrUnlock);

            RefreshCollection();
        }

        public void RefreshCollection()
        {
            Collection.Clear();

            foreach (var sp in DataManager.Instance.SpeciesList)
            {
                var isUnlocked = _save.UnlockedSpeciesIds.Contains(sp.Id);
                var existingPet = _save.Pets.FirstOrDefault(p => p.SpeciesId == sp.Id);
                var isActive = existingPet != null && existingPet.Id == _save.ActivePetId;

                Collection.Add(new PetCollectionItemDisplay
                {
                    SpeciesId = sp.Id,
                    Name = sp.Name,
                    DisplayName = sp.DisplayName,
                    Description = sp.Description,
                    BaseColor = sp.BaseColor,
                    SecondaryColor = sp.SecondaryColor,
                    UnlockPrice = sp.UnlockPrice,
                    IsUnlocked = isUnlocked,
                    IsActive = isActive,
                    CustomName = existingPet?.Name ?? sp.Name,
                    Level = existingPet?.Level ?? 1
                });
            }

            OnPropertyChanged(nameof(Coins));
        }

        private void OnSelectOrUnlock(PetCollectionItemDisplay? item)
        {
            if (item == null) return;

            if (item.IsActive)
            {
                // Đang chọn rồi
                return;
            }

            if (item.IsUnlocked)
            {
                // Đã mở khóa -> Chuyển thú cưng
                var existingPet = _save.Pets.FirstOrDefault(p => p.SpeciesId == item.SpeciesId);
                if (existingPet == null)
                {
                    existingPet = new Pet
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = item.Name,
                        SpeciesId = item.SpeciesId,
                        X = _petVM.X,
                        Y = _petVM.Y
                    };
                    _save.Pets.Add(existingPet);
                }

                _petVM.SwitchActivePet(existingPet.Id);
                AudioService.Instance.PlayClick();
                RefreshCollection();
            }
            else
            {
                // Mở khóa bằng xu
                if (_save.Coins < item.UnlockPrice)
                {
                    AudioService.Instance.PlayError();
                    _petVM.ShowEmote("Chưa đủ Xu để mở khóa người bạn này! 🪙", 2.0);
                    return;
                }

                _save.Coins -= item.UnlockPrice;
                _save.UnlockedSpeciesIds.Add(item.SpeciesId);

                var newPet = new Pet
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = item.Name,
                    SpeciesId = item.SpeciesId,
                    X = _petVM.X,
                    Y = _petVM.Y
                };
                _save.Pets.Add(newPet);
                _petVM.SwitchActivePet(newPet.Id);

                AudioService.Instance.PlayLevelUp();
                _petVM.ShowEmote($"Mở khóa thành công {item.DisplayName}! 💖", 3.0);
                _petVM.CheckAchievementProgress("own_3_pets", _save.Pets.Count);
                _petVM.CheckAchievementProgress("own_5_pets", _save.Pets.Count);

                SaveService.Instance.SaveGame(_save);
                RefreshCollection();
            }
        }
    }
}
