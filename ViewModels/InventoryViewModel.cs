using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DesktopPet.Models;
using DesktopPet.Services;

namespace DesktopPet.ViewModels
{
    public class InventoryItemDisplay : ViewModelBase
    {
        public string ItemId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "Food";
        public string? Slot { get; set; }
        public string Icon { get; set; } = "🍎";
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; } = 999;
        public string QuantityDisplay => "∞";
        public bool IsEquipped { get; set; }
        public string ActionText => Category == "Accessory" ? (IsEquipped ? "Tháo Ra" : "Mặc") : "Sử Dụng";
        public bool ShowEquippedBadge => Category == "Accessory" && IsEquipped;
        public string ButtonBackground => Category == "Accessory" && IsEquipped ? "#E64A19" : "#7CB342";
    }

    public class InventoryViewModel : ViewModelBase
    {
        private readonly PetViewModel _petVM;
        private readonly GameSave _save;
        private string _selectedCategory = "Tất Cả";

        public ObservableCollection<InventoryItemDisplay> Items { get; } = new();

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    RefreshInventory();
                }
            }
        }

        public ICommand UseItemCommand { get; }
        public ICommand SelectCategoryCommand { get; }

        public InventoryViewModel(PetViewModel petVM)
        {
            _petVM = petVM;
            _save = petVM.GameSave;

            UseItemCommand = new RelayCommand<InventoryItemDisplay>(UseItem);
            SelectCategoryCommand = new RelayCommand<string>(cat => SelectedCategory = cat ?? "Tất Cả");

            _petVM.AppearanceChanged += RefreshInventory;

            RefreshInventory();
        }

        public void RefreshInventory()
        {
            Items.Clear();

            // Chế độ chill: Luôn hiển thị toàn bộ vật phẩm trong game với số lượng vô hạn (∞)
            foreach (var def in DataManager.Instance.ItemsList)
            {
                if (_selectedCategory != "Tất Cả" && !MatchesCategory(def.Category, _selectedCategory))
                    continue;

                var isEquipped = def.Category == "Accessory" && _petVM.IsItemEquipped(def.Id);

                Items.Add(new InventoryItemDisplay
                {
                    ItemId = def.Id,
                    Name = def.Name,
                    Category = def.Category,
                    Slot = def.Slot,
                    Icon = def.Icon,
                    Description = def.Description,
                    Quantity = 999,
                    IsEquipped = isEquipped
                });
            }
        }

        private bool MatchesCategory(string itemCategory, string filter)
        {
            return filter switch
            {
                "Thức Ăn" => itemCategory == "Food",
                "Đồ Chơi" => itemCategory == "Toy",
                "Trang Phục" => itemCategory == "Accessory",
                _ => true
            };
        }

        private void UseItem(InventoryItemDisplay? display)
        {
            if (display == null) return;

            var def = DataManager.Instance.GetItem(display.ItemId);
            if (def == null) return;

            if (def.Category == "Accessory" && def.Slot != null)
            {
                // Mặc / Tháo phụ kiện
                if (_petVM.IsItemEquipped(def.Id))
                {
                    _petVM.UnequipItem(def);
                }
                else
                {
                    _petVM.EquipItem(def);
                }
            }
            else
            {
                // Chế độ chill: Thức ăn / đồ chơi vô hạn, không trừ số lượng
                _petVM.PetViewModel_ApplyItem(def);
            }

            SaveService.Instance.SaveGame(_save);
            RefreshInventory();
        }
    }

}
