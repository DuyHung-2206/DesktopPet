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
        private string _customName = string.Empty;
        public string CustomName
        {
            get => _customName;
            set => SetProperty(ref _customName, value);
        }
        public int Level { get; set; } = 0;
        public string ActionText => IsActive ? "Đang Nuôi 🐾" : (IsUnlocked ? "Chọn Thú Cưng" : $"Mở Khóa ({UnlockPrice}🪙)");
    }

    public class PetCollectionViewModel : ViewModelBase
    {
        private readonly PetViewModel _petVM;
        private readonly GameSave _save;

        public ObservableCollection<PetCollectionItemDisplay> Collection { get; } = new();

        public int Coins => _save.Coins;

        private bool _isRenameDialogOpen;
        public bool IsRenameDialogOpen
        {
            get => _isRenameDialogOpen;
            set => SetProperty(ref _isRenameDialogOpen, value);
        }

        private string _newPetName = string.Empty;
        public string NewPetName
        {
            get => _newPetName;
            set => SetProperty(ref _newPetName, value);
        }

        private string _renameErrorMessage = string.Empty;
        public string RenameErrorMessage
        {
            get => _renameErrorMessage;
            set
            {
                if (SetProperty(ref _renameErrorMessage, value))
                {
                    OnPropertyChanged(nameof(HasRenameError));
                }
            }
        }
        public bool HasRenameError => !string.IsNullOrEmpty(_renameErrorMessage);

        public PetCollectionItemDisplay? RenameTargetItem { get; private set; }

        public ICommand SelectOrUnlockCommand { get; }
        public ICommand OpenRenameDialogCommand { get; }
        public ICommand ConfirmRenameCommand { get; }
        public ICommand CancelRenameCommand { get; }

        public PetCollectionViewModel(PetViewModel petVM)
        {
            _petVM = petVM;
            _save = petVM.GameSave;

            SelectOrUnlockCommand = new RelayCommand<PetCollectionItemDisplay>(OnSelectOrUnlock);
            OpenRenameDialogCommand = new RelayCommand<PetCollectionItemDisplay>(OnOpenRenameDialog);
            ConfirmRenameCommand = new RelayCommand(OnConfirmRename);
            CancelRenameCommand = new RelayCommand(OnCancelRename);

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
                    Level = existingPet?.Level ?? 0
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

        public void OnOpenRenameDialog(PetCollectionItemDisplay? item)
        {
            if (item == null || !item.IsUnlocked) return;

            RenameTargetItem = item;
            NewPetName = item.CustomName;
            RenameErrorMessage = string.Empty;
            IsRenameDialogOpen = true;
        }

        public void OnCancelRename()
        {
            IsRenameDialogOpen = false;
            NewPetName = string.Empty;
            RenameErrorMessage = string.Empty;
            RenameTargetItem = null;
        }

        public void OnConfirmRename()
        {
            if (RenameTargetItem == null)
            {
                IsRenameDialogOpen = false;
                return;
            }

            if (!RenamePet(RenameTargetItem.SpeciesId, NewPetName, out string errorMessage))
            {
                RenameErrorMessage = errorMessage;
                return;
            }

            AudioService.Instance.PlayClick();
            IsRenameDialogOpen = false;
            NewPetName = string.Empty;
            RenameErrorMessage = string.Empty;
            RenameTargetItem = null;
        }

        public bool RenamePet(string speciesId, string newName, out string errorMessage)
        {
            var trimmed = newName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                errorMessage = "Tên thú cưng không được để trống.";
                return false;
            }

            if (trimmed.Length > 30)
            {
                errorMessage = "Tên thú cưng không được vượt quá 30 ký tự.";
                return false;
            }

            // Tìm thú cưng trong danh sách save
            var existingPet = _save.Pets.FirstOrDefault(p => p.SpeciesId == speciesId);
            if (existingPet == null)
            {
                existingPet = new Pet
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = trimmed,
                    SpeciesId = speciesId,
                    X = _petVM.X,
                    Y = _petVM.Y
                };
                _save.Pets.Add(existingPet);
            }
            else
            {
                existingPet.Name = trimmed;
            }

            // Nếu thú cưng được đổi tên là pet đang nuôi (Active Pet)
            if (existingPet.Id == _save.ActivePetId)
            {
                _petVM.Pet.Name = trimmed;
                _petVM.NotifyAllProperties();
                _petVM.ShowEmote($"Tên mới của bé là {trimmed}! ✨🐾", 2.5);
            }

            SaveService.Instance.SaveGame(_save);
            RefreshCollection();

            errorMessage = string.Empty;
            return true;
        }
    }
}
