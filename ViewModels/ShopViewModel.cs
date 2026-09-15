using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DesktopPet.Models;
using DesktopPet.Services;

namespace DesktopPet.ViewModels
{
    public class ShopItemDisplay : ViewModelBase
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "Food";
        public string Icon { get; set; } = "🍎";
        public string Description { get; set; } = string.Empty;
        public int Price { get; set; }
        public string StatsPreview { get; set; } = string.Empty;
        public bool IsPetUnlock { get; set; } = false;
        public bool IsAlreadyUnlocked { get; set; } = false;
        public string BuyButtonText => $"🛒 Mua ({Price} Xu)";
    }

    public class ShopViewModel : ViewModelBase
    {
        private readonly PetViewModel _petVM;
        private readonly GameSave _save;
        private string _selectedCategory = "Tất Cả";

        public ObservableCollection<ShopItemDisplay> DisplayItems { get; } = new();

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    FilterItems();
                }
            }
        }

        public int Coins => _save.Coins;

        public ICommand BuyCommand { get; }
        public ICommand SelectCategoryCommand { get; }

        public ShopViewModel(PetViewModel petVM)
        {
            _petVM = petVM;
            _save = petVM.GameSave;

            BuyCommand = new RelayCommand<ShopItemDisplay>(BuyItem);
            SelectCategoryCommand = new RelayCommand<string>(cat => SelectedCategory = cat ?? "Tất Cả");

            FilterItems();
        }

        public void RefreshCoins()
        {
            OnPropertyChanged(nameof(Coins));
            FilterItems();
        }

        private void FilterItems()
        {
            DisplayItems.Clear();

            // Nạp các vật phẩm từ items.json với giá bán chuẩn
            foreach (var item in DataManager.Instance.ItemsList)
            {
                if (_selectedCategory != "Tất Cả" && !MatchesCategory(item.Category, _selectedCategory))
                    continue;

                var stats = "";
                if (item.Category == "Medicine") stats += "💊 Trị Ốm ";
                if (item.HungerRestore > 0) stats += $"🍖 +{item.HungerRestore} ";
                if (item.HappinessBonus > 0) stats += $"😊 +{item.HappinessBonus} ";
                if (item.EnergyBonus > 0) stats += $"⚡ +{item.EnergyBonus} ";

                DisplayItems.Add(new ShopItemDisplay
                {
                    Id = item.Id,
                    Name = item.Name,
                    Category = item.Category,
                    Icon = item.Icon,
                    Description = item.Description,
                    Price = item.Price,
                    StatsPreview = stats.Trim(),
                    IsPetUnlock = false
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

        private void BuyItem(ShopItemDisplay? item)
        {
            if (item == null) return;

            var def = DataManager.Instance.GetItem(item.Id);
            if (def == null) return;

            if (_save.Coins < item.Price)
            {
                _petVM.ShowEmote($"❌ Bạn cần {item.Price} Xu để mua món này! (Hiện có: {_save.Coins} Xu)", 2.5);
                AudioService.Instance.PlayHurt();
                return;
            }

            // Trừ xu người chơi
            _save.Coins -= item.Price;

            // Thêm vật phẩm vào túi đồ (không giới hạn tối đa 50)
            var existing = _save.Inventory.FirstOrDefault(i => i.ItemId == item.Id);
            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                _save.Inventory.Add(new InventoryItem { ItemId = item.Id, Quantity = 1 });
            }

            AudioService.Instance.PlayCoin();
            _petVM.ShowEmote($"🛒 Đã mua 1 {item.Name}! (-{item.Price} Xu) ✨", 2.5);

            SaveService.Instance.SaveGame(_save);
            RefreshCoins();
            _petVM.TriggerInventoryChanged();
            _petVM.NotifyStatProperties();
        }
    }
}
