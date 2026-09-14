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
        public string Icon { get; set; } = "🍎";
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public string QuantityDisplay => $"x{Quantity}";
        public string ActionText => "Sử Dụng";
        public string ButtonBackground => "#7CB342";
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

            // Hiển thị các vật phẩm thực tế có trong kho đồ người chơi
            foreach (var invItem in _save.Inventory.ToList())
            {
                if (invItem.Quantity <= 0) continue;

                var def = DataManager.Instance.GetItem(invItem.ItemId);
                if (def == null) continue;

                if (_selectedCategory != "Tất Cả" && !MatchesCategory(def.Category, _selectedCategory))
                    continue;

                Items.Add(new InventoryItemDisplay
                {
                    ItemId = def.Id,
                    Name = def.Name,
                    Category = def.Category,
                    Icon = def.Icon,
                    Description = def.Description,
                    Quantity = invItem.Quantity
                });
            }
        }

        private bool MatchesCategory(string itemCategory, string filter)
        {
            return filter switch
            {
                "Thức Ăn" => itemCategory == "Food",
                "Đồ Chơi" => itemCategory == "Toy",
                _ => true
            };
        }

        private void UseItem(InventoryItemDisplay? display)
        {
            if (display == null) return;

            var def = DataManager.Instance.GetItem(display.ItemId);
            if (def == null) return;

            var invItem = _save.Inventory.FirstOrDefault(i => i.ItemId == display.ItemId);

            // Tiêu thụ thức ăn / đồ chơi: trừ 1 đơn vị
            if (invItem != null && invItem.Quantity > 0)
            {
                invItem.Quantity--;
                if (invItem.Quantity <= 0)
                {
                    _save.Inventory.Remove(invItem);
                }
            }
            _petVM.PetViewModel_ApplyItem(def);

            SaveService.Instance.SaveGame(_save);
            RefreshInventory();
            _petVM.NotifyStatProperties();
        }
    }

}
