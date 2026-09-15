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
        private int _quantity = 1;

        public string ItemId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "Food";
        public string Icon { get; set; } = "🍎";
        public string Description { get; set; } = string.Empty;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                {
                    OnPropertyChanged(nameof(QuantityDisplay));
                }
            }
        }
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

        public int Coins => _save.Coins;

        public ICommand UseItemCommand { get; }
        public ICommand SelectCategoryCommand { get; }

        public InventoryViewModel(PetViewModel petVM)
        {
            _petVM = petVM;
            _save = petVM.GameSave;

            UseItemCommand = new RelayCommand<InventoryItemDisplay>(UseItem);
            SelectCategoryCommand = new RelayCommand<string>(cat => SelectedCategory = cat ?? "Tất Cả");

            _petVM.AppearanceChanged += RefreshInventory;
            _petVM.InventoryChanged += RefreshInventory;

            RefreshInventory();
        }

        public void RefreshInventory()
        {
            Items.Clear();
            OnPropertyChanged(nameof(Coins));

            // Chỉ nạp các vật phẩm người chơi thực sự sở hữu và có số lượng > 0
            foreach (var invItem in _save.Inventory)
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
                "Thuốc" => itemCategory == "Medicine",
                _ => true
            };
        }

        private void UseItem(InventoryItemDisplay? display)
        {
            if (display == null) return;

            var invItem = _save.Inventory.FirstOrDefault(i => i.ItemId == display.ItemId && i.Quantity > 0);
            if (invItem == null) return;

            var def = DataManager.Instance.GetItem(display.ItemId);
            if (def == null) return;

            // Kiểm tra dùng thuốc khi không ốm -> Không lãng phí thuốc (Rule 19)
            if (def.Category == "Medicine" && !_petVM.Pet.IsSick && _petVM.Pet.State != PetState.Sick)
            {
                _petVM.ShowEmote("Mimi đang hoàn toàn khỏe mạnh, không cần uống thuốc đâu nhé! ✨", 2.5);
                return;
            }

            // Khi đang ốm, chỉ dùng được Thức ăn và Thuốc, không hao tổn đồ chơi
            if ((_petVM.Pet.IsSick || _petVM.Pet.State == PetState.Sick) && def.Category != "Medicine")
            {
                bool isDrink = def.Id == "milk" || def.Name.Contains("Sữa", StringComparison.OrdinalIgnoreCase);
                if (def.Category != "Food" || isDrink)
                {
                    _petVM.ShowEmote("Tớ đang bị ốm, chưa muốn làm gì.", 2.5);
                    return;
                }
            }

            // Giảm số lượng 1 khi sử dụng
            invItem.Quantity--;
            if (invItem.Quantity <= 0)
            {
                _save.Inventory.Remove(invItem);
            }

            // Áp dụng hiệu ứng và hoạt ảnh cho pet
            _petVM.PetViewModel_ApplyItem(def);

            SaveService.Instance.SaveGame(_save);
            RefreshInventory();
            _petVM.NotifyStatProperties();
        }
    }

}
