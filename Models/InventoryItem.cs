namespace DesktopPet.Models
{
    public class InventoryItem
    {
        public string ItemId { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public bool IsEquipped { get; set; } = false;
    }
}
