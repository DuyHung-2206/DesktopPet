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

            // Thêm các vật phẩm từ items.json (Tất cả đều miễn phí vô hạn)
            foreach (var item in DataManager.Instance.ItemsList)
            {
                if (_selectedCategory != "Tất Cả" && !MatchesCategory(item.Category, _selectedCategory))
                    continue;

                var stats = "";
                if (item.HungerRestore > 0) stats += $"🍖 +{item.HungerRestore} ";
                if (item.HappinessBonus > 0) stats += $"😊 +{item.HappinessBonus} ";
                if (item.EnergyBonus > 0) stats += $"⚡ +{item.EnergyBonus} ";
                if (item.Slot != null) stats += $"[Trang bị {item.Slot}]";

                DisplayItems.Add(new ShopItemDisplay
                {
                    Id = item.Id,
                    Name = item.Name,
                    Category = item.Category,
                    Icon = item.Icon,
                    Description = item.Description,
                    Price = 0,
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
                "Trang Phục" => itemCategory == "Accessory",
                _ => true
            };
        }

        private void BuyItem(ShopItemDisplay? item)
        {
            if (item == null) return;

            var def = DataManager.Instance.GetItem(item.Id);
            if (def == null) return;

            // Chế độ chill: Tự do dùng đồ / trang bị cho bé ngay lập tức hoàn toàn miễn phí
            if (def.Category == "Accessory" && def.Slot != null)
            {
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
                _petVM.PetViewModel_ApplyItem(def);
            }

            SaveService.Instance.SaveGame(_save);
            RefreshCoins();
            _petVM.NotifyStatProperties();
        }
    }
}
