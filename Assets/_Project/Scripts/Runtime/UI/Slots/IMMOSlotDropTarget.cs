namespace RPGClone.UI
{
    public enum MMOSlotDropState
    {
        None,
        Valid,
        Invalid
    }

    /// <summary>
    /// Lightweight visual validation contract. The actual mutation is still performed
    /// by the destination gameplay system from its normal drop callback.
    /// </summary>
    public interface IMMOSlotDropTarget
    {
        MMOSlotDropState EvaluateDrop(MMOSlotDragPayload payload);
    }
}
