namespace ForjaGame.Inventory
{
    public interface IInventoryReceiver
    {
        bool AddItem(string itemId, int amount);
    }
}
